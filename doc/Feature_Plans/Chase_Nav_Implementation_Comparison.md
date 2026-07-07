# Chase Nav — Three-Implementation Comparison & Synthesis Plan

**Parent plans:** `Chase_Nav_Track_A_Solver.md`, `Chase_Nav_Track_B_Field_And_Eval.md`
(diagnosis in `Chase_Navigation_Trade_Study.md`; Opus lineage history in
`Chase_Nav_Track_A_Implementation_Log.md`).
**Scope:** static review (code, design, plan fidelity, committed evidence) of the three
independent implementations of the chase-nav plan; no benchmarks were re-run for this
report. Evaluation priorities agreed 2026-07-06: robustness/correctness first, then
test & benchmark quality, then cleanliness/simplicity.

---

## 1. The three implementations

| | **Fable** (agent-5) | **Opus team** (agent-4) | **Codex** (agent-1) |
|---|---|---|---|
| Branch / PR | `fable/chase-combined` (#73; stack #66–#72) | `agent-4` (stack: #65, #75, #67, #74) | `agent-1` = `task/chase-nav-track-b-b1-eval-harness` (#64) |
| Stages | A1 A2 A3 B1 B2 B3 — **complete** | A1 A2 B1 B2 B3 — **no A3** (shelved on `task/chase-nav-a3-shelved`) | A1 A2 A3 B1 B2 B3 — complete but shallow in places |
| Size (excl. meta/results) | 38 files, +3911 / −412 | 46 files, +2564 / −419 | 28 files, +1647 / −349 |
| New tests | ~24 methods across 7 new suites | ~16 methods across 6 new suites (+3 suites on the shelved A3) | ~9 methods, mostly folded into existing suites |
| Benchmark evidence committed | 5 JSONL result sets (2×10-ep baseline, B2, B3, combined A3+B3) + stats doc + python compare script | 3-run baseline JSONL + doc + PS diff script + determinism test | 3-scenario single-run baseline table in doc + PS compare script |

### Lineage is not fully independent

- **B3 core is shared.** Opus's `NavField.cs` / `TerminalField.cs` are byte-identical to
  Fable's apart from two comment tweaks (Fable committed 2026-07-05 18:19, Opus
  2026-07-06 01:54). Opus adopted Fable's Burst Dijkstra core and re-plumbed only the
  service layer onto their own B2 seam. There are effectively **two** B3 service designs,
  not three independent B3s (Codex's B3 is independent — and the weakest).
- **The Opus Track-A agent benchmarked on Codex's harness.** Commit `c9639d70` (Codex's
  B1 harness) is the base of the Opus A-track stack (`agent-2`, PRs #67/#74); the
  scenario names in the Opus implementation log (offset-cross / wide-lateral /
  near-cluster) are Codex's. Opus's *own* B1 (#65) is a different harness used by their
  B-track. All the A1/A2/A3 ablation numbers in the log are therefore Codex-harness numbers.
- Mechanical work (deleting `adaptiveDt*`/`controlSmoothing`/`relax*`, un-baking
  `shipRadius` from `ConvertObstacles`, the one-line terminal cost hook) is essentially
  identical in all three — the divergence is concentrated in A2 semantics, A3
  architecture, B1 harness form, and B2 wiring.

---

## 2. Stage-by-stage

### A1 — Sampler hygiene + knot-correlated noise

*Why (plan):* i.i.d. per-step Gaussian noise averages itself away — a single draw can
never express "hold hard strafe for 0.5 s". Knot-based correlated noise fixes that;
`adaptiveDt`/`controlSmoothing`/`relax*` were dead at asset values and complicate A3.

| | Fable | Opus | Codex |
|---|---|---|---|
| Knot draw | Streaming walk: draw `prev/next` knot as the segment advances; one `float3` Gaussian per knot | Pre-draw up to 16 knots per channel into `float4x4` registers, then lerp | Stateless hash: `KnotSeed(candidate, channel, knot)` → fresh `Random` + Box-Muller **re-computed per step** |
| Interpolation | Linear | Linear | Smoothstep |
| Default / asset | 5 / 5, `[Range(2,16)]` | 4 / 4, clamp ≤16 | 5 / 5, `[Range(2,8)]` |
| dt-noise rescale removed | Yes | Yes (with comment) | Kept as a no-op (`rolloutDt/config.dt` ≡ 1) |

All three are behaviorally correct. Fable and Opus are equivalent in cost (one Gaussian
per knot); Codex's per-step hash pays ~2 RNG constructions + Box-Muller per channel per
step (~6× redundant math in the hottest job) for statelessness nobody needs, though its
counter-based determinism is a nice property. **Verdict: Fable or Opus, interchangeable;
Fable's is already integrated with the injection slots.**

### A2 — Obstacle cost redesign (the load-bearing divergence)

*Why (plan):* the threshold potential thrashed at its cost boundary and was neutered in
the live asset. Replace with (a) a hard, near-binary collision term against the
bank-narrowed hull, and (b) a continuous admissibility term with no range boundary.

| | Fable | Opus | Codex |
|---|---|---|---|
| Collision | Any-overlap → single `collisionPenalty` **outside the terminal ramp** (early hit = late hit) | Per-overlapping-obstacle sum, inside the ramped positional bucket | Hard-coded const 100 × obstacle *mass weight*, × `wObstacle`, ramped |
| Admissibility | **Stopping-distance ratio** along closing direction; decel = `reverseAcc/mass + drag·v` (matches `Model.Step` physics exactly); worst obstacle (max) | **Collision-course-gated turn-away**: only obstacles the velocity leads into (perp < corridor) cost anything; measures lateral-sidestep feasibility (`½·a_lat·t²` vs deficit); C¹ at the boundary; max-aggregated | Stopping-distance ratio, **but `maxDecel = max(reverseAcc, maxStrafeAcc)` — raw force, never divided by mass, drag ignored** |
| Tunables | `collisionPenalty 1000`, `collisionSafetyMargin 0.25`, `wObstacle 1` (asset) | `collisionPenalty 10000`, `obstacleSafetyMargin 0.3`, `wObstacle 5` (asset) | penalty/margin are code constants; only `wObstacle 1` tunable |
| Tests | 5 (banked-gap clears, speed-invariant margin, monotonicity, receding-free, boundary continuity) | 4 (banked-gap, monotone, **off-path-free gate test**, colliding-out-costs-all) | 2 |

**Codex has a real correctness bug:** with the default dynamics (mass 200, reverseAcc 4,
drag 0.1) the true braking decel is `0.02 + 0.1·v` m/s²; Codex uses `4.0` — a ~4–200×
overestimate of braking, so its stopping distance is tiny and the admissibility term
almost never fires. Its A2 is effectively collision-term-only. Its penalty is also
weight-scaled (obstacle mass / ship mass), so dominance over stage costs is
unpredictable, and it left orphaned comments from the deleted harmonic-sum code.

**Fable vs Opus is the substantive design fork.** Fable implemented the grill-decision-7
spec (stopping-distance, physically faithful to the sim model including drag). Opus
implemented the same thing first, then **ablated it** (`wObstacle=0`) and found the
timidity it caused was real and the term wasn't the cause of dense-path failure — and
replaced it with the collision-course-gated turn-away term, which lets a weaving pursuer
pass off-course rocks for free. The plan doc's FINAL OUTCOME records the turn-away
version as the keeper ("0 collisions where measurable… KEEP"). Fable's combined-stack
benchmark also shows excellent collision numbers, but Fable never isolated A2, so the
turn-away variant is the only one with per-stage ablation evidence.

**Verdict: Opus's turn-away semantics + tuning, with Fable's outside-the-ramp collision
placement grafted on.** (Do not stack both admissibility terms; the ablation says
braking-based cost = timidity.)

### A3 — Gap layer + seeded primitives

*Why (plan):* Gaussian sampling essentially never draws the narrow control sequence that
threads a tight gap; detect gaps analytically and inject scripted bank-through
primitives as CEM candidates, with hysteresis at gap selection.

| | Fable | Opus (shelved) | Codex |
|---|---|---|---|
| Detector | Exact arc geometry: hull-inflated blocked arcs, circular merge, two passes (upright vs banked hull) → bank-only classification; occlusion handled by arc merge | 180-bin egocircle raster (2°); open/bank-only classes; occlusion via per-bin nearest blocker | **Pairwise obstacle midpoints, O(N²), no occlusion test** — a "gap" can have rocks in front of it |
| Hysteresis | In `ChooseGap`: axis re-association + switch margin (plan-required) | Separate `GapSelector` with margin | **None** |
| Primitives | Open-loop scripted: align → thrust → strafe pulse timed to mouth → mirrored unwind; sign/length variants | Closed-loop forward-simulated (yaw PD against actual model), bank pulse timed at simulated mouth crossing | Constant yaw torque from *initial* heading error, trapezoid strafe over whole horizon, no unwind; bank-only gaps get a *bonus* (+0.25) instead of a penalty |
| Injection | Reserved candidate slots 1..K in the generate job; winning injected sequence cached & re-seeded next tick (`LastBestIndex`) | Into CEM iteration 0; required the **iCEM + softmax-elite rearchitecture** to survive elite averaging | Overwrites candidates 1..K after generation; plain elite mean (same 1/eliteCount dilution Opus proved inert — a lone cheap primitive moves the applied control by ~4%) |
| Solver change | None (kept single-shot elite averaging per grill decision 6) | cemIterations/meanMomentum/softmax — regressed, salvaged, still no benchmark win | None |
| Evidence | Unit test: bank-only wall slot where Gaussian-only fails and the injected primitive wins the elite (seed-pinned) | Benchmarked **inert**: `gapsThreaded=0` in every config; dropped by user decision | Unit tests for detector only; no injection-wins test; `gapsThreaded` telemetry stubbed at −1 |

The Opus experiment is the decisive evidence here: at current field densities bank-only
gaps essentially never occur, and the chase deficit is pursue/evade *dynamics*, not
navigation — so no A3 variant has demonstrated in-benchmark value. Codex's A3 is
additionally the weakest technically (no hysteresis, no occlusion, dilution-blind).
Fable's is the best-engineered and the cheapest to carry (no solver rearchitecture,
fully unit-proven, disabled by `gapTopK 0`), but its benchmark contribution is unproven —
Fable's good combined numbers were never ablated against `gapTopK 0`.

**Verdict: carry Fable's gap layer (it is part of the only end-to-end-measured config),
but run a `gapTopK 0` vs `3` ablation on the synthesized branch and default it off if
inert, matching the user's A3 decision. Opus's shelved iCEM/softmax stays archived.**

### B1 — Eval harness

*Why (plan):* the measuring stick for both tracks: two-AI chase (MaintainRange vs Flee)
in the dense field, watchable in-editor **and** headless, per-ship headline metrics
(shared-MPC confound), N-seed statistical model, JSONL + diff script, committed baseline,
additive sampler-seed seam.

| | Fable | Opus | Codex |
|---|---|---|---|
| Form | **ChaseBenchmarkSector prefab** (watchable through normal game flow) + `ChaseBenchmarkModule`/`ChaseMetricsProbe` + PlayMode driver — matches the plan's "one scenario, two entry points" | Test-code-only scenario factory (Brain disabled, intents pushed directly) + `Tools ▸ Chase Benchmark ▸ Live View` menu | Test-code-only, extends `AIIntegrationFixture`; 3 fixed named scenarios |
| Sweep | field-seed × field-offset, env-configurable (`CHASE_BENCH_*`), 40 s episodes, keep-alive damage reset so episodes never truncate | 3 start-offsets × seed-bias, 300–800 ticks | 3 fixed (scenario, seed) pairs, single run each |
| Metrics | Per-ship (collisions, impulse, speed, per-axis chatter, solve ms) + secondary geometry + contact time | Same headline set (chatter summed across axes) + timeToIntercept | Intercept/separation-centric + per-ship speed/collisions/chatter |
| Seed seam | `SolverBuffers.SamplerSeedOverride` (nullable static; per-instance solve counter) — **pinned mode drops the per-ship position hash, so both ships share one noise stream** | `SolverBuffers.SeedBias` — replaces only the frame term, **keeps the position hash** (better) | None (relies on fixed scenario seeds) |
| Extras | Baseline stability shown across two independent 10-ep repeats; python aggregate/diff | Explicit determinism test (same seeds → stable distributions); PS diff | Repeatability smoke asserting loose tolerances; PS diff |

**Verdict: Fable's harness is the plan's harness** (sector + module + probes + sweep +
committed baselines) and its two-repeat baseline is the strongest statistical evidence.
Graft Opus's seed semantics (keep the position hash when pinned) and its
determinism-test idea.

### B2 — Deterministic-field obstacle sensing

*Why (plan):* the speed-adaptive `OverlapSphere` made the obstacle set breathe with
speed and cost a physics round-trip; query the live registry in a fixed AABB instead,
same `ObstacleScan` contract, destroyed asteroids must vanish immediately.

| | Fable | Opus | Codex |
|---|---|---|---|
| Producer | Static `AsteroidSpawner.ActiveSpawners` → iterate **every live asteroid** in every spawner, AABB filter | `IObstacleField` implemented by `UpdatingAsteroidField`: **chunk-cell lookup** (`spawnedByChunk`) then per-asteroid box test | Static `AsteroidFieldRegistry` → chunk-cell lookup, radius-inflated bounds |
| Wiring | None needed (static access); **legacy physics scan kept as fallback** → primitive-obstacle tests/scenes untouched | Full DI: EnvironmentService → MainGameManager → UnitService → AICommander → Scout; **physics path deleted**, `SetObstacleExclusion` now a no-op; scanner tests rewritten + env stubs | Static registry **plus** a vestigial empty `IObstacleField` marker interface on the environment service; physics fallback kept for non-asteroid obstacles |
| Radius source | `ast.Radius` (authoritative controller value) | `ast.Radius` | `CurrentPlaneRadius()` — projects **every mesh vertex** to the plane per asteroid **per query** (hot path: per ship, every Scout update), plus per-collider `GetComponentInParent<AsteroidController>` in the fallback |
| Fixed extent | `maxSpeed·t + ½·a·t²` (constant per ship) | Same, computed once in Initialize | `maxSpeed + maxSpeed²/(2a)` — adds a *speed* to a *distance* (dimensional slip; works as an arbitrary buffer) |

Opus's is the best architecture (proper service seam, aligned with the sector-refactor
direction, cheapest query) but deleting the physics fallback cost churn in unrelated
tests and silently no-ops the exclusion API. Fable's is the most conservative (plan
said "minimal game-code changes; prefer existing seams") and kept every existing test
green untouched, but its linear sweep over all live asteroids is O(field population) per
ship per scan. Codex's has two real hot-path perf hazards (vertex-projection radius per
query; GetComponentInParent per scan hit — the cached-refs rule exists for exactly this)
and a dead marker interface.

**Verdict: Opus's chunk-cell `UpdatingAsteroidField.QueryObstacles` + DI seam, with
Fable's physics fallback retained inside the scanner for scenes with no live field.**

### B3 — Terminal cost-to-go field

*Why (plan):* the MPC horizon (~1.7 s) cannot see around clusters; a Dijkstra
cost-to-go field from the chase target, sampled once per rollout at the terminal state,
fixes the topology blindness without waypoints/goal substitution.

| | Fable | Opus | Codex |
|---|---|---|---|
| Core | Burst `IJob` (stamp + 8-connected Dijkstra, binary heap, NativeArrays) | **Identical file** (adopted from Fable) | Managed C#, plain arrays, reusable MinHeap — not Burst, not a job |
| Service | `NavFieldService` MonoBehaviour singleton; per-target double buffer; job runs **off the main thread**, buffers swap on completion; rebuild on moved>cell / registry delta ≥3 / staleness; obstacles from spawner registry | Same service re-plumbed to take B2's `IObstacleField` (one obstacle producer for both B2 and B3 — the nicest layering); +8192 obstacle guard; gathers obstacles slightly eagerly | Static `TerminalNavFieldService`; **synchronous Dijkstra on the main thread inside the solve path**; rebuilds whenever obstacle count changes by 1 or target crosses a cell — the rebuild-spike the plan explicitly warned about |
| Hook & sampling | `wTerminal·Sample(x_terminal)`; bilinear; blocked/off-grid/∞ → **per-corner** large-but-finite distance fallback (×3 pessimism); seconds via cellSize/nominalSpeed | Identical | Bilinear in `Cost` with raw arrays in `CostInput`; **any** non-finite corner → whole-sample distance fallback (discontinuous); grid is **copied (≤16k floats) into a NativeArray every solve, per ship** |
| Config | Service-owned grid (64×3 u), `wTerminal 1` in asset | Same (64, 3 u) | Grid/cell/fallback-speed live in **MpcSettings** (solver asset owns field geometry); default `wTerminal 0` in code, 1 in asset |
| Tests | 10 EditMode (incl. wall-routing, unit scaling, solver-hook reordering) + 2 PlayMode (non-blocking bake, rebuild-follows-target) | 7 EditMode + 2 PlayMode (obstacle-removal-on-rebuild) | 2 |

**Verdict: the shared Fable/Opus core is the keeper; use Opus's service variant if B2
lands as the `IObstacleField` seam (single obstacle producer), otherwise Fable's.**

---

## 3. Cross-cutting observations

- **Benchmark reality check (Fable combined, 10 eps vs 10-ep baseline):** pursuer
  collisions 8.6→3.9, evader 13.8→3.2, strafe chatter ~halved (6.9→3.6), speed flat —
  but mean chase distance ~doubled (10.6→20.6) and contact time fell (35→24.5 s), the
  same "both ships got better, the evader benefits more" confound the Opus log hit as
  `minSep rises`. Solve cost rose 0.46→0.74 ms mean. The nav stack is measurably safer
  and smoother; **the chase itself does not close better** — that deficit is pursue/evade
  dynamics, out of scope for all six stages, and should be tracked as its own follow-up.
- **Everyone respected the frozen `ObstacleScan`/`DetectedObstacle` contract**, and all
  three deleted the same dead solver features nearly identically (that part of the
  synthesis is conflict-free).
- **Asset tuning diverges** (`noiseKnots` 5/4/5; `wObstacle` 1/5/1; penalty 1000/10000/
  const-100). The synthesized branch must pick ONE set and re-baseline; recommend Opus's
  A2 values (ablation-tuned) + Fable's everything-else.
- **Commit/PR hygiene:** Fable = clean stacked PRs + a merge commit documenting every
  conflict resolution + benchmark-result commits. Opus = disciplined stacked PRs + an
  exemplary implementation log (its ablation-first debugging is the reason we know which
  A2/A3 variants actually matter). Codex = one linear branch, terse messages, no log.

## 4. Recommended synthesis

**Base: `fable/chase-combined` (PR #73)** — the only branch that is complete, integrated,
and end-to-end measured. Apply five grafts, each small and independently testable:

1. **A2 semantics (from Opus, `agent-4`):** replace Fable's `AdmissibilityCost` with the
   collision-course-gated turn-away term (`Config.maxLatAccel = maxStrafeAcc/mass`),
   port Opus's four obstacle-cost tests, adopt Opus tuning (`wObstacle 5`,
   `collisionPenalty 10000`, margin 0.3) — but keep Fable's collision-outside-the-ramp
   placement and any-overlap single penalty.
2. **B2 producer (from Opus):** swap Fable's linear `ScanRegistry` sweep for Opus's
   `IObstacleField` + chunk-cell `UpdatingAsteroidField.QueryObstacles`, wired through
   EnvironmentService/UnitService as on `agent-4`; **retain Fable's `ScanPhysics`
   fallback** when no field is registered (keeps legacy scanner tests and primitive-
   obstacle scenes working, and keeps `SetObstacleExclusion` honest). Route
   `NavFieldService` obstacle gathering through the same seam (Opus's service variant).
3. **Seed seam fix (one line, from Opus's design):** in pinned mode keep the per-ship
   position hash (`SamplerSeedOverride + solveCount·7919 + pos.GetHashCode()`), so the
   two ships never share a noise stream.
4. **Harness merge:** keep Fable's sector/module/probe harness, baselines and python
   compare; add Opus's same-seeds→stable-distributions determinism test; optionally port
   the Live View menu item.
5. **A3 decision gate:** keep Fable's gap layer code; on the synthesized branch run the
   combined benchmark at `gapTopK 3` vs `0`. If ON≈OFF (expected, per Opus's finding),
   ship with `gapTopK 0` (code stays, one asset toggle re-enables it) and note the
   revival conditions from the Track-A doc (denser benchmark field + chase-dynamics fix
   first).

**Discard:** Codex's branch in full (its unique ideas — stateless hash noise, smoothstep
knots, plane-radius accuracy — are not worth the maxDecel bug, the hot-path vertex/
GetComponent scans, the main-thread Dijkstra, and the missing hysteresis). Keep it for
reference until the synthesis PR merges, then close #64. The shelved Opus A3 stays
archived on `task/chase-nav-a3-shelved`.

**Validation before merge:** full EditMode+PlayMode suites (BurstCache cleared, Unity
closed), then a fresh 2×10-episode baseline-vs-synthesized benchmark run; attach both to
the PR body per the B1 gate. Supersede/close PRs #64–#75 in favor of the synthesized
stack (or land it as one squash PR given all constituent parts were already reviewed on
their own branches).

**Effort estimate:** grafts 1–4 are each ≤1 day; the A3 ablation is one benchmark run.
The dominant risk is A2 retuning interacting with B3 (`wTerminal 1` was tuned against
Fable's A2); the benchmark gate covers it.
