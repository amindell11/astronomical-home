# Terminal field PR-1 — design scope (frozen brief + prep rulings + round-2 addendum)

Assembled 2026-09-09 from GitHub issue #461 (arc brief and its 2026-09-09 prep-rulings comment) for an independent reimplementation. Namespaces per #506's 2026-09-09 coordination comment. Nothing here is a result; results live in PR #548's body.

# Part 1 — The frozen brief (issue #461 body, verbatim)

Design records this builds on: #485 (intent grammar), #486 (MPC retune rulings), #513 (the deleted nav-field lineage). Brief frozen 2026-09-08 after a two-round cross-model design consult (independent GPT design vs an adaptation of the deleted #206 code; merged by consensus). Code is the source of truth — this body carries only the why, the rulings, the open forks, and the acceptance targets.

## Why the field

Obstacle avoidance is a property of the terrain, not of any referent, so it is unsayable in the referent grammar (#485). The MPC's rollout sees 1.2–1.7 s ahead; a Dijkstra cost-to-go grid over the rock occupancy gives it global route knowledge beyond the horizon as a terminal cost. Two payoffs:

1. The dense-field long-chase gap: the Bench-1 policy times out against a fleeing opponent (Evader 5/15, 16 timeouts, ~96 s episodes); timeout-draws are sentence-indistinguishable from wins, so the fix is terrain, not designation.
2. A shorter horizon (1.2 s) gained +5 roster and +3 Evader on the frozen policy but regressed `Bingo_MinefieldTransit` 3/5 → 0/5 (stalls ~90 m short of a goal through 8 rocks) and yaw-station drift ~2×. The field carries the long-range route knowledge the short rollout lacks, so field + 1.2 may keep the gain without the regression.

## Rulings (user)

- **Solver-side, always on, not sayable.** The policy's only handle is the existing sentence `FieldSlot` weight (today it scales the reactive turn-away cost; it will scale the terminal field the same way; unarmed = ×1). Terrain routing stays out of the referent grammar by design.
- **Goal = the POS term's resolved centre** (referent position + frame offset, excluding the ring radius), the same resolution the POS ring uses, so field and ring agree on the point. No POS referent → no field, zero cost. POS weight does not enable or disable it.
- **Every MPC-driven ship bakes its own field** from its arena's obstacle source. No arena static, no service-interface member.
- **Collision penalty and turn-away stay.** The field adds beyond-horizon route knowledge; it does not replace admissibility. Whether turn-away becomes redundant is a rig ablation, not an assumption.
- **No occlusion / line-of-sight information in this field.** Cover is out of scope here (see Open forks).
- **Landing: two PRs.** PR-1 = the field at the current horizon 1.7, proven on the solver rig first, then the hand-sentence session rows. PR-2 = the one-line 1.2 flip only if PR-1's transit target holds at 1.2 with combat rows preserved.
- **RL is benched** (ruling recorded on #485), so the first consumers are hand-authored sentences. The former obligation to re-bench the frozen `ShipCombat-3500017` under the field before landing is lifted: the field becomes part of the environment baseline, and whatever trains next trains under it. Re-bench the incumbent only if a retrain comparison against it is ever wanted.
- **Prior art is reference only.** `fable/chase-b3-terminal-field` and the code deleted at `6df1f325` predate the `Cost/Terms/` split and the sentence carrier; do not cherry-pick.

## Data model

`Mpc` owns one persistent `TerminalField`: a cell-centred XY square, row-major `x + y·N`, initially 48×48, minimum spacing 4 m. Cells hold traversable route distance to the goal in metres; occupied or disconnected cells hold +∞. A read-only `TerminalFieldView` (distances, validity, origin, spacing, resolution, baked goal, seed index, max finite distance) rides `CostInput` into the Burst rollout job. The owner keeps occupancy, obstacle storage and indexed-heap scratch; allocated once, disposed with `Mpc`, recreated on episode reset. An invalid view keeps its storage, so no dummy array is needed. Bakes complete before rollouts are scheduled: no double buffer, no pump.

## Bake

`Mpc.Plan` advances the bake clock on simulation time. Bake on the first valid POS, then every 0.4 s, and early only when the ship or the resolved goal leaves the grid. No goal-displacement or obstacle-count triggers. A solve without a valid POS invalidates the view first.

Grid centred between ship and goal; spacing ≥ 4 m chosen so the grid contains both plus max-speed travel over rollout duration + bake interval, hull clearance, and two cells of padding. No maximum spacing in this experiment (with a fixed cell count, a cap eventually cannot contain both goal and rollout reach, and nothing in the model supplies a terrain-resolution criterion); record the actual spacing per bake and measure far-goal degradation.

Obstacles come through `ObstacleScanner`, generalized with a caller-centred extent and a caller-owned reusable buffer that grows and re-queries on full (rule 6: the scanner is the coordinator; no second path to `IObstacleField`). The production 64-entry nearest-first scan keeps its buffer, count, extent and ordering, pinned by a same-seed rig-trace diff. Query the grid AABB expanded by hull clearance. Do not bake the merged ship/rock scan.

One `[BurstCompile] IJob`: rasterize discs inflated by unbanked ship radius + the existing safety margin, then 8-neighbour Dijkstra with costs `h` / `√2·h`, no diagonal corner-cutting. Seed = the nearest free cell to the goal (ties by index), initialised with its distance to the goal — this makes a POS bound to a rock centre work. No free cell = entirely unreachable.

## Terminal cost

Charge **detour excess**, the route distance added by occupied cells. With `(a, b)` the cell-index displacement from the seed:

`D_empty(i) = h·[max(a,b) + (√2−1)·min(a,b)] + |c_seed − goal|`, `E_i = max(0, D_i − D_empty(i))`.

An empty grid gives E ≡ 0 by construction (tests use a declared float tolerance), so the field cannot double-count the POS ring's attraction and has no grid-direction bias. Unreachable corners are replaced by `B = max finite D + grid diagonal` (diagonal alone if nothing is finite) before bilinear interpolation; outside the sample domain, sample the clamped point and add the Euclidean distance back into the domain. Every value the sampler sees is finite.

After the last `Model.Step`: `J_F = wTerminalField · (FIELD armed ? weight : 1) · E(x_H)`. Sampled once at the rollout's end state via a shared `Cost.EvaluateTerminal` used by both candidate evaluation and the trajectory breakdown. Not ramped (the ramp already weights the accumulated per-step state cost), not saturated. `wTerminalField` starts at 1 cost/metre, tuned on the rig. `wObstacle` stays independent. POS keeps attraction and ring settling; collision stays unchanged and un-ramped.

## Seams & wiring

One path: arena obstacle field → existing spawn / `SetSensing` → `Scout`'s `ObstacleScanner` → `Navigator` passes the scanner at `Mpc` construction → `Mpc`-owned `TerminalField` → `CostInput`. Navigator only composes; geometry and scheduling live in the field module. No new world reference, service-interface member, setter or static. Ownership by the consumer removes buffer-lifetime overlap structurally (fix-ladder rung 1).

**Namespaces (coordinated with #506).** New types take folder-mirroring names: `TerminalField`, `TerminalFieldView`, `TerminalFieldBakeJob` under `AI/Navigation/MPC/TerminalField/` as `AI.Navigation.MPC.TerminalField` (a new leaf, so no existing namespace is split). The cost term lands as `Cost/Terms/TerminalField.cs`, a `partial class Cost` in the folder's current `Movement.MPC` like its six siblings, so the terms folder stays one namespace until the #506 sweep migrates `Movement.MPC` → `AI.Navigation.MPC`. `RigObstacleField` takes `Game.RLHarness` like the rest of `RLHarness/SolverRig/`. PR-1 renames nothing.

## Rig & test plan

`RigObstacleField : IObstacleField` adapts `RigScenario.obstacles`; behavioural rows exercise the production scanner + baker path (no injected grids; direct arrays only in baker/sampling unit tests). No `RigScenario` change.

Four cloned-settings arms: both shaping costs on · terminal field off (`wTerminalField = 0`) · turn-away off (`wObstacle = 0`) · both off. Collision always on. `FieldZeroed` explicitly arms FIELD at weight 0 and now disables both shaping terms.

Rows: minefield-transit (primary), field-authority, rock-referent rows, ring/offset rows, dummy-closeout, kite (sign check: the field must not drag a kiter toward the enemy), and no-POS controls. Trace the emitted trajectory's endpoint field cost separately from the step-zero breakdown; count collisions separately from threat steps.

**Transit target:** ≥ 18/20 predeclared seeds (including the original five) with final range < 20 m. The existing no-field ≥ 3/5 pin stays as the regression witness — it is known to flap (2/5 once on an unmodified tree), so the with-field pin uses the wider seed set. Predeclare clean-process repeats and publish every outcome; no retry-until-green. Targets, not results.

Unit tests: octile distances, empty-grid excess ≡ 0, blocked/disconnected sampling, corner-cutting, rock-centre seeds, domain edges, cadence/reset, terminal-vs-breakdown agreement. **Microbench:** the warmed Burst job with preallocated arrays at 32/48/64 × density 2.0/2.5, gathering and allocation timed separately. No throughput budget is assumed; the number is the deliverable (the old 64×64 / 0.15 s baker was estimated at 10–25% of RL throughput and never isolated).

PR-1 keeps 1.7 s, proves the rig, then runs all six `SentenceRows` session rows at density 2.0 plus a dense fleeing-opponent chase row (hand sentences cannot catch the Evader — pure tail-chase, 0/15 even in open space — so read route efficiency and closeout time, not catches). PR-2 requires the transit target at 1.2 s with combat rows preserved.

## Extensibility (within the experiment)

Cadence, resolution and minimum spacing are `MpcSettings` fields. The goal caller supplies a point; swapping the goal source changes the caller, not the Dijkstra. A second layer (multi-source "distance to nearest cell satisfying P", e.g. a cover field seeded from every cell occluded from the enemy — the old flee mode's seeding shape) adds another owner/view and an explicit cost; Dijkstra initialisation generalizes to many seeds only when that layer lands, predicates supply seeds outside Burst. Deliberately not pre-built: layer registries, goal-provider interfaces, per-arena caches, predicates inside Burst, asynchronous scheduling.

## Open forks (not in PR-1)

- **Occlusion-aware LANE.** LANE today is lateral distance from the enemy's facing ray and knows nothing about rocks; the weapon sight already gates fire on an asteroid-layer raycast and projectiles damage asteroids, so cover is physically real. Making LANE zero once the ray is blocked before reaching the ship would let a negative LANE weight be satisfied by ducking behind a rock within the horizon (the sampler finds the rock; no cover point is authored). Its own rig experiment against the existing `cover-take` row; independent of the field.
- **Cover field** (beyond-horizon "distance to nearest occluded cell"): the second layer above. A fourth class term under the #485 cap → design event.
- **Replace the POS ring's Euclidean error with the field sample** (one term instead of two): changes every POS row's behaviour and the rig pins; a follow-up experiment only if detour excess proves insufficient.
- **`Movement.MPC` → `AI.Navigation.MPC` migration**: #506's sweep, not this arc.

## Risks

- Observed: `UpdatingAsteroidField` reports only loaded rocks; a grid extending past the update radius sees empty space (optimistic routes). No radius clamp can make this complete through today's interface; documented, not fixed.
- Observed: the POS ring saturates, so detour-only shaping adds no open-space pull. If the 1.2 transit stall is insufficient attraction rather than route choice, the field alone may not rescue it — the rig decides, and the follow-up fork above is the fallback.
- Hypothetical: coarse spacing, conservative disc inflation, projected rock goals and 0.4 s staleness on rotating frame offsets may distort routes or erase usable gaps. Throughput and behavioural measurements decide whether refinement earns its cost.

## History

- **Tier-0 (hand-sentence pursuit probe, 2026-08-30) — abandoned.** A POS-ring pursuit is a pure tail-chase: 0/15 catches in both the dense field and near-empty space, so it cannot test the horizon hypothesis. Needed a row-scoped `agentBoundsRelaxed` knob (never landed).
- **Tier-1 (horizon 1.7→1.2 on frozen `ShipCombat-3500017`, 2026-08-30) — measured, parked.** Roster 54 → 59, Evader 3.5 → 6.5/15, no archetype regressed; the full Both gate caught `Bingo_MinefieldTransit` 3/5 → 0/5 and `MpcYawOnly` drift 1.0 → 1.82. Artifacts `results/rl-eval/tier1-h17-3500017/`, `.../tier1-h12-3500017/`. User ruled field first, then retry 1.2 with it.
- **Standing gotcha:** `horizonSeconds` lives on the shared `MpcSettings` asset, read per-solve by train and eval alike; hold the policy cadence at 5 Hz in any horizon experiment (plan fast-forward couples horizon to tick rate).



# Part 2 — PR-1 prep rulings (issue #461 comment, verbatim)

## PR-1 prep rulings (2026-09-09, pr-prep against the frozen brief)

Build session for PR-1 (`terminal-field`, slot agent-3). The brief above stands; this comment records only what the prep pass decided beneath it.

**Sequencing fork (user-ruled): the `ObstacleScanner` generalization rides PR-1.** The caller-centred query with a caller-owned growable buffer is additive (`IObstacleField.QueryObstacles` already takes centre, extent and buffer), so `Scan()` and the production 64-entry buffer are not in the diff at all. The scanner hunk lands in its own commit so the PR reads as two concerns; a separate PR would have bought a standalone gate run for a ~100-line additive seam, which the user judged not worth the round-trip.

**Assumptions (code-grounded, none vetoed):**
- `Mpc` takes the scanner as an optional fourth constructor argument; null = no obstacle source (precedent: a null `IObstacleField` senses zero obstacles), so existing solver tests and the rig's no-field arms build unchanged. `Navigator.Initialize` passes `scout.obstacleScanner`; `AICommander.TryInitializeSystems` already initialises Scout before Navigator.
- The rig's rollout obstacle path stays as it is (`BuildScan` → `ConvertObstacles`); only the baker goes through `ObstacleScanner` + `RigObstacleField`. The rig's scanner has no `Transform` (null origin; only the caller-centred query is used).
- The four arms are in-memory `MpcSettings` clones (the `posWidthOverride` precedent); the asset file is never written by the rig. Per #486, no mode enum.
- New `MpcSettings` fields: `wTerminalField` (1), `terminalFieldBakeInterval` (0.4 s), `terminalFieldResolution` (48), `terminalFieldMinSpacing` (4 m); written explicitly into `MpcSettings_AgentPilot.asset` so the asset stays the source of truth.
- `TerminalFieldView` rides `CostInput`; `SolverBuffers` keeps the last view the way it keeps `obstacles`, so `BuildCostInput` (breakdowns) sees what the solve saw. `CostBreakdown` gains `terminalField`; `EvaluateTrajectoryBreakdown` adds it at the endpoint; `RigTraceRow` gains `costTerminalField` sampled at the emitted trajectory's endpoint.
- Goal resolution reuses `EvalContext.Create` at step 0 with a new `posResolved` flag (a resolved weight-0 POS is otherwise indistinguishable from unresolved, and weight does not gate the field).
- Disc inflation = `dynamics.shipRadius + collisionSafetyMargin`, unbanked; lobes rasterised individually when `multiSphereObstacles` is on, mirroring `ConvertObstacles`.
- The 20-seed transit pin runs in the gate (the 5-seed pin measures ~1 s); the four-arm sweeps and the microbench are `MPC_RIG_EMIT=1`-gated like the bingo emitters.
- Names as ruled with #506. Known hazard, accepted: the type `TerminalField` shares its enclosing namespace's leaf name; nothing declares `using AI.Navigation.MPC` today, so no ambiguity until #506's sweep, which qualifies at the use site.

**Non-goals confirmed:** horizon 1.2 (PR-2), RL training or re-bench, occlusion-aware LANE, cover field, POS-ring replacement, editor gizmo for the field.



# Part 3 — Round-2 addendum: field gizmo + verification capture (user request 2026-09-09, after PR review)

Scope: PR-1 additionally ships (a) an editor gizmo that lets a human verify the field's behaviour in a live editor and on film, and (b) a captured clip proving both the gizmo and the field work on a real ship. Same slot/branch as PR-1; no new namespaces, no renames.

## Gizmo (a native `[DrawGizmo]` drawer, per the capture skill: no capture-only overlays)

- One drawer on `Navigator` (the component that owns the `Mpc` that owns the field), registered with `GizmoView.Register(typeof(Navigator), "field", …, "Steering")` so it gates like the existing `candidates` / `predicted` / `obstacles` / `controls` subviews and rides the `Steering` capture profile automatically. Draw only in play mode, only when the view is valid.
- Must show, in plane space: the grid domain outline; occupied cells (distinct colour); disconnected free cells (distinct from occupied); a detour-excess heat over the remaining free cells normalised to the grid's own maximum that bake (an absolute scale saturates in dense fields); the goal point; the seed cell; the emitted plan's endpoint with the excess it pays; and a label with spacing, bake count and the excess at the ship. Skip cells below a small excess so open space stays uncluttered.
- Reads only what the field already exposes (`TerminalFieldView` + the owner's occupancy/bake count); adds no state to the field beyond an accessor.

## Verification scenario (promoted, committed `CaptureScenario` under `Editor/Tests/PlayMode/Scenarios/`)

- A real ship transits a density-2.0 harness rock field (`HarnessField.Spawn` … `Rebuild(seed, clearingA, clearingB)`) toward a stationary Dummy 140 m away, spawned through `UnitService.SpawnShip` with the field as its obstacle source; Steering profile, gizmo scope `Selected` on the transiting ship; ~30 s or until within 10 m.
- The transiting ship's sentence MUST carry a closing VEL slot (the rig's dummy-closeout shape: AIM 1, POS ring 6 m weight 1, VEL radial ≈8 m/s weight 0.5, FIELD 1). The session `dummy-closeout` row (POS only) does not move from rest at this range — see traps.
- Proof = two clips, same tree, same seed: field on (asset as committed) and field off (`wTerminalField = 0`, toggled locally, never committed). Read a mid-clip and a late PNG yourself before claiming anything. What the frames must show: occupied cells coinciding with the rock rings; heat legible (not uniformly saturated); the goal ring at the Dummy; the endpoint label changing as the plan changes; the ship reaching the Dummy. Report both arms' time-to-10 m; one pair is a proof the machinery works, not a measurement of the field's value.
- Deliver: `assemble.py --web` mp4s copied out of the slot into the primary tree's `results/mpc-rig/terminal-field/`, plus the stills eyeballed.

## Known traps (observed during the first build; guidance, not rulings)

1. **Capture frame names are fixed-step indices** (`f_00500.png` = step 500 = 10 s at 50 Hz), not frame counts. Reading `f_00290` and calling it "29 s" produced a false stall diagnosis once.
2. **Aligned-start trap.** From rest with the nose already on a goal ~140 m away, a POS ring alone yields no candidate that strictly beats the zero-control incumbent (`IncumbentElite` requires strict improvement; POS gain over 1.7 s is hundredths, effort is tenths), with the field on or off. Reproduced on the rig by an aligned-start arm of a dense closeout probe. Pre-existing solver property, not the field's; the VEL slot is the remedy for the scenario. (Consult: codex gpt-6-astra, Mode B, 2026-09-09.)
3. **Windowed editor log noise.** A windowed test editor's Package Manager panel logs a token-refresh 400 on most boots; the test framework fails the capture test on it before a frame is filmed. `LogAssert.ignoreFailingMessages = true` inside the test *body* (SetUp has its own disposed log scope) covers the body; a message landing before the body still fails the run — retry.
4. **Dense-field scale.** On a dense random band the field's excess is tens of metres, so at 1 cost/m it dominates every normalised term (rig sweep: ships end far from the goal at w=1, tightest at 0.03–0.1). The asset stays at 1 per the brief; the weight is a user tuning decision, recorded with the sweep in the PR body.
