# RL Env-Step Package — arc plan & PR briefs

> STATUS: living — env-step arc (asteroid episodes → obstacle tokens → opponent archetypes → curriculum), then PR-4 self-play on top.

**Date:** 2026-07-17 (package settled with the user; PR-A scoped via pr-prep).
**Parent:** `Tactical_AI_Audit_And_Roadmap.md` §3′/§4′; builds on PR-3 + training arc
(`RL_MLAgents_Agent.md`). Driving memory: `project_tactical_ai_direction.md`
§"Env-step package".

> **One-line intent.** Fix the pilot's single-opponent overfit (kites, can't pursue) at
> the environment, not the reward: asteroid episodes, obstacle observations, an opponent
> mixture, and a parameter curriculum — with human gates before every training-hour spend.

## Package shape (settled 2026-07-17)

Trigger: the pilot checkpoint exploits the baseline's 1-D aggression and cannot close on
an evading human. Diagnosis: opponent-distribution gap, NOT an obs/interface/reward gap —
no dense closing-speed term (the MaintainRange trap). Six commitments:

1. Asteroid episodes + k-nearest obstacle tokens (ego-frame rel pos+vel+radius,
   distance-sorted, zero-padded, same query the MPC consumes).
2. Scripted traversal probe as a gate before training spend; calibrates density-ramp
   endpoints. MPC owns local dodging (obstacle regularizers are always-on in velocity
   mode); the policy's asteroid skill is mid-horizon routing / LOS / cover.
3. Parameter curriculum (density, collision lethality, mixture weights via ML-Agents
   environment parameters; lessons keyed on reward). Task curriculum (fly-far pretrain)
   REJECTED — trains a skill that lives in the MPC layer + transfer risk.
4. Opponent mixture sampled per episode: aggressor (existing baseline) · evader (the
   pursuit teacher) · orbiter · kiter (anti-exploit before self-play; frozen
   `ShipCombat-pilot.onnx` = second flavor) · passive dummy (curriculum floor, decays).
   Per-episode parameter jitter within archetype instead of more archetypes; no authored
   ambusher (terrain breaks LOS for free).
5. Per-archetype degeneracy gate: each archetype individually exercised (watch lane +
   JSONL, human go/no-go) BEFORE any training hours.
6. Eval reports per-archetype win rates, never a blended number.

Weapons: fully deferred, including opponent-side (missiles without threat tokens are
unlearnable damage-noise). Re-entry: learned firing discipline; trigger: post-asteroid
combat still shallow attrition.

## PR slicing

- **PR-A** — asteroid episodes in the harness + traversal probe gate (brief below).
- **PR-B** — k-token obs extension (24 → 24+6k) sourced from the agent's
  `ObstacleScanner`; re-mint smoke fixture (checkpoints break — accepted).
- **PR-C** — archetype roster + jitter + per-archetype degeneracy watch lane.
  Independent of A/B (empty-arena fights) — parallelizable in its own slot.
- **PR-D** — curriculum wiring (env params → density/lethality/mixture) + per-archetype
  eval reporting; then the training run (a run, not a PR).

Gates: probe (after A) and archetype degeneracy (after C), both before D's spend.

---

# PR-A — Decision brief (frozen 2026-07-17, pr-prep)

## Scope

**In:** deterministic asteroid field composed into harness episodes (spec-gated, default
OFF — every existing test/floor/characterization unchanged); `densityScale` runtime
modifier + host-passed exclusion volumes on the field API; per-episode layout rebuild;
trajectory-equivalence extension to field state; scripted traversal probe (single-ship,
sweep → JSONL → human go/no-go + ramp-endpoint calibration), built as a durable
MPC-tuning instrument.

**Out (non-goals):** obs changes of any kind (24 floats stays — pilot checkpoint + smoke
fixture pin it; k-tokens are PR-B); opponent archetypes (PR-C); curriculum / env-param
wiring and the collision-lethality knob (PR-D); any training; NavField/terminal-field
work (unreached in velocity mode — Navigator.cs:128, and deliberately so: the policy
occupies the long-horizon slot the learned value function was originally aimed at).

## Fork resolutions (with why)

1. **Field = production `UpdatingAsteroidField`, streaming neutralized; fresh layout per
   episode.** Static anchor (own transform), `loadRadius` covers the arena, `fieldRadius`
   bounds generation; reset = `SetLayoutSeed(derive(runSeed, episodeIndex))` +
   `RebuildField()` (atomic despawn + deterministic synchronous refill, wipes the
   destruction overlay). *Why:* one source of truth — `QueryObstacles` is exactly what
   the MPC scans and what PR-B's tokens will read; per-episode layouts are what the
   training goal wants and make spawn exclusions natural. Reset cost measured in-PR.
2. **Spawn safety = pose-derived exclusion volumes.** Poses derive first; both spawn
   positions become generation-time `ExclusionVolume`s (the `startClearRadius`
   mechanism, extended to N host-passed points at rebuild). *Why:* guaranteed clearance;
   layout stays a pure function of (runSeed, episodeIndex); the static-authored-only
   guard protects mid-session streaming determinism, which per-episode rebuild sidesteps.
3. **Density knob = first-class `densityScale` on the field API** (folded into
   `BuildGenerationParams` output, set via the same pre-rebuild API family as
   `SetLayoutSeed`; default 1). *Why:* smallest honest seam; continuous (PR-D's env
   param maps directly); one authored asset stays the tuning source; domain-honest —
   "this field, scaled" is a field capability, not RL vocabulary. (Raised and parked:
   the general runtime-params-API-with-SO-authoring pattern — parking lot 2026-07-17.)
4. **Probe = single-ship composition, diameter crossings, per-density curves.** Own
   small spec + runner beside `EpisodeRunner` (PR-2a pattern); edge spawn
   (exclusion-carved) → fixed velocity reference at the opposite edge; ends on
   arrival/death/timeout. Sweep: densityScale × commanded speed fraction × layout seeds
   × optional `MpcSettings` overrides (generalizing the PR-2a `wVelTrack` sweep — the
   probe doubles as an MPC-tuning instrument later; user directive, build clean+robust).
   Per-density aggregates: completion rate, effective traversal speed, collisions per
   episode, cumulative damage. Ramp endpoints: density where traversal is free (ramp
   start) and where MPC local dodging degrades (ramp ceiling). Go/no-go: usable band
   between them; no-go reopens the division-of-labor design before any training spend.
   *Why single-ship:* the probe measures MPC traversal only — a pair drags weapons,
   reward, and pair-reset semantics into a measurement that wants none of them.
   **Driver-agnostic (user amendment 2026-07-17):** the traversal driver is a seam
   (`IIntentChooser` config), rows are driver-tagged, and PR-A ships TWO drivers —
   the scripted velocity-reference chooser (the gate) and a legacy comparator running
   the same crossings through the old goal-mode path with the nav/terminal field
   active — so per-density curves compare velocity-mode obstacle competence against
   the old nav-field stack directly. The learned-policy driver slots into the same
   seam later (meaningful post-PR-B; today's policy has no obstacle obs) to verify
   the policy sacrifices no obstacle competence. Risk flag: confirm NavField baking
   runs in the harness composition (NavFieldService exists there, but the bake path
   is only exercised by sector scenes today); if it needs scene machinery, the
   comparator is the one probe piece with lift.

## Assumptions (user-reviewed)

- Wiring mirrors the sector bridge: `arena.ObstacleField = field`, cleared on teardown
  (`AsteroidFieldSpawner.cs:21`); no NavField baking.
- Minimal composition: `UpdatingAsteroidField` + `AsteroidSpawner` (+ required
  `Fragger`); no `AsteroidFieldSpawner`, no `WorldFollow`. Field API already public.
- New harness base settings asset (fieldRadius ≈ arena 120, loadRadius covering it) —
  `BigFieldSettings` is 10,000-radius streamed-world tuning.
- Fragments stay enabled; `RebuildField`'s overlay wipe IS the reset.
- Pool pre-size hint scales with `densityScale` (`WorstCaseLoadedCount` reads unscaled
  settings).
- Probe ship = `TestPilotMPC` host + `wVelTrack=50` clone; no target → free-yaw
  traversal (PR-1 semantics); production `AsteroidDamage` scale.
- Probe JSONL via `EpisodeJsonl` → `results/rl-probe/`, own schema; watch-flag lane;
  pacing contract (frame ≙ fixed step) on probe runs.
- Combat-row spec additions ride JSONL additively; schema stays `rl-episode-v2`.
- Episode-0 double field build (Start auto-init + first reset rebuild) accepted.
- Tests headless (no RequiresGraphics); `-ScopeType Auto` iteration; worktree loop.

## Blindsider resolutions

- **Equivalence test pins field reset via a per-reset field digest** — capture the full
  loaded-asteroid set (positions + radii via `QueryObstacles` over the arena) once per
  reset and compare across the reset cycle; existing per-step ship channels catch
  behavioral divergence. Never loosened.

---

# PR-C — Decision brief (frozen 2026-07-18, pr-prep)

## Scope

**In:** opponent archetype roster for harness episodes — aggressor (existing UtilityPilot
baseline, untouched) · evader (scripted flee + juke) · orbiter (scripted live-target
orbit, firing) · kiter (scripted hold-long-range + fire) · passive dummy (zero-velocity
chooser on the ship airframe); per-episode archetype selection + parameter jitter via an
`OpponentRoster` consulted by the episode loop; opponent-side install through
`EpisodePair`'s chooser seam via `Brain.InstallChooser` (no respawn); archetype tag +
jitter draw recorded per JSONL row (additive); per-archetype degeneracy watch-lane gate
(opt-in fixture: `RL_ARCHETYPES` env / `results/rl-archetypes/watch.flag`; JSONL +
per-archetype summaries; human go/no-go per archetype before any training hours).

**Out (non-goals):** mixture-weight curriculum + env-param wiring (PR-D); per-archetype
eval reporting (PR-D); frozen-checkpoint (`ShipCombat-pilot.onnx`) opponent — deferred to
PR-D/PR-4; any training; obs changes (PR-B); weapons; asteroid-field coupling (archetype
checks run in empty arenas — the PR-A field composition stays off).

## Fork resolutions (with why)

1. **Substrate = mixed, scripted-leaning.** Aggressor stays the production UtilityPilot
   (its chooser runs a single `AttackAggressive` profile — it already *is* the
   archetype); evader/orbiter/kiter are scripted `IIntentChooser`s over the velocity
   interface in the harness (`RangerChooser`'s hold-range law and `ManeuverChooser`'s
   orbit law are the parents); dummy is a pinned zero-velocity chooser. *Why:* each
   archetype provably does its one job (the degeneracy gate's premise); juke doesn't
   exist in the utility path (`FleeEnemyGoal` has no params — the SO route would force a
   Game.Core goal change); jitter = seeded `Configure` params, no SO-clone machinery;
   zero runtime change.
2. **Checkpoint kiter flavor deferred to PR-D/PR-4.** `InferenceChooser` owns Academy
   stepping (disables automatic stepping, calls `EnvironmentStep` itself); the training
   runner already owns that clock for the agent — two manual steppers break the pacing
   contract. Scripted kiter ships now; stepping ownership gets its own change alongside
   the curriculum/league work that actually needs the checkpoint opponent.
   *(Resolved by the stepping-migration arc: `RL_Stepping_Migration.md` PR-2 moved
   `InferenceChooser` onto the Academy auto-clock — no manual steppers remain.)*
3. **Per-episode selection lives in an `OpponentRoster` owned by the episode loop** —
   spec-configured (fixed weights in PR-C; PR-D turns the weights into ML-Agents env
   params), consulted each episode to pick + jitter an archetype, installing via
   `Brain.InstallChooser` on the opponent ship before `pair.Reset` (install-then-respawn
   ordering per the traversal-probe precedent — respawn re-inits the installed chooser).
   The degeneracy gate pins the roster to a single archetype per run.

## Assumptions (user-reviewed)

- Jitter + selection draws from `SeedScope(runSeed).Derive(episodeIndex).Derive(<new
  stream id>)` — replayable, pose/spawn streams untouched.
- Archetype tag + jitter params ride each JSONL row additively (schema stays
  `rl-episode-v2`).
- Dummy = the ship airframe + pinned zero-velocity `VelocityReference` intent (not
  `NavigationIntent.None`, for determinism; not `DummyTarget` — episodes need a killable
  `Ship` for reward/obs), no rotation, no fire: the curriculum floor.
- Jitter ranges are authored consts per archetype in the harness (desired range, orbit
  radius/direction, juke cadence, speed fraction), tuned during the degeneracy gate.
- New scripted choosers live beside `RangerChooser` in `Game.RLHarness` (editor asm);
  `ManeuverChooser` and the PR-2a oracle tests are untouched.
- Fire-capable archetypes: aggressor, kiter, orbiter. Never fire: evader, dummy.
- Tests headless (no RequiresGraphics); `-ScopeType Auto` iteration; worktree loop.

## Blindsider resolutions

- **Border handling:** shared scripted-archetype steering blend — inside an edge margin,
  rotate the commanded velocity toward the border tangent (deterministic, no randomness);
  the degeneracy gate verifies no border-pinning.
  *(Amended 2026-07-18, user directive — bloat/seam guard.)* The blend is ONE static pure
  function (a velocity-law post-step in the `RangerChooser.HoldRangeVelocity` style):
  `(planePos, commandedVel, arenaCenter, borderRadius, margin) → steered velocity`, called
  as the final step of each chooser's intent build. Arena bounds enter each chooser once
  through `Configure` as plain floats (episode-constant), sourced by the roster/host from
  constants already in the episode composition. Explicitly NOT: a base-class obligation, a
  new interface or service, `ArenaContext`/component refs inside choosers, or any runtime
  lookup. A shared base may *call* the function if one exists for other reasons; the
  function is the unit.
- **Orbiter fires** — a non-firing orbiter lets the policy park inside the orbit circle
  and farm free damage (a degenerate lesson); firing keeps pressure honest while the
  geometry stays distinct from the kiter.
- **Degeneracy-gate reference opponent = the `RangerChooser` stand-in on the agent side**
  (deterministic close-hold-fire pressure, the PR-2b/3 pattern) — one comparable
  reference across all archetypes; the production aggressor's utility-state stochasticity
  would muddy degeneracy attribution.

---

# PR-B — Decision brief (frozen 2026-07-19, pr-prep)

## Scope

**In:** obstacle-token observation extension 24 → 72: k=8 nearest-asteroid tokens × 6
floats appended to the shared `AgentObservations.Fill`; `DetectedObstacle` gains a
plane-projected velocity captured at the field query; `Scout.AsteroidScan` accessor
(asteroid-only raw scanner view); re-mint `ShipCombat-smoke.onnx` at the new size;
layout-pin tests moved to 72 + token semantics.

**Out (non-goals):** any MPC/avoidance behavior change (Burst converter ignores the new
velocity field — byte-identical solver inputs); BufferSensor/attention (flat vector per
plan); training, curriculum, mixture wiring (PR-D); re-minting `ShipCombat-pilot.onnx`
(checkpoint break accepted — stale, null-check tests only); weapons/threat tokens.

## Fork resolutions (with why)

1. **Token source = asteroids-only** (raw `ObstacleScanner` buffer via a new
   `Scout.AsteroidScan`, mirroring the merged `ObstacleScan` property). *Why:* the
   opponent already has a rich block (ch 9–17) — the merged buffer would represent it
   twice and burn a token slot in close combat; policy's asteroid skill is terrain
   routing, ship avoidance stays the MPC's job.
2. **Velocity rides the obstacle pipeline** — `DetectedObstacle.velocity` (plane-projected),
   captured in `UpdatingAsteroidField.BuildObstacle` from the asteroid's rigidbody; the
   Scout ship-append site populates it from ship kinematics; Burst `ObstacleData`
   unchanged. *Why (user directive):* the scan result carries its own data — consumers
   dereferencing collider→rigidbody at obs time is the reach-into-the-object pattern this
   codebase forbids; the extra field on the shared path is cheap and never half-initialized.
3. **k=8, token = `relPos.xy, distance, relVel.xy, radius`** (obs 72). *Why:* mirrors the
   target block (relPos + explicit distance + relVel) and internal `ObstacleToken`
   (relPos/distance/radius); k=8 covers routing-relevant blockers at training densities
   1.0–2.0. Bearing-scalar variant rejected (±π wraparound discontinuity; continuous
   encoding = sin/cos = the same two floats).

## Assumptions (user-reviewed)

- Flat `VectorSensor`; `AgentObservations.Size` 24→72 auto-propagates to both hosts
  (`ShipAgentFactory`, `InferenceChooser` set size from the const); no prefab/YAML edits
  (`normalize: false` — magnitudes matter).
- Token block appended after ch 23 inside the shared `Fill`; scan enters as a parameter
  (no lookups inside `Fill`); both hosts pass their ship's Scout, cached at composition —
  never per-decision `GetComponent`.
- Freshness: `Scout.Update` scans every frame in all modes; obs reads the latest buffer.
- Ego math reuses `EgoFrame`; relVel = (asteroid − self) ego-rotated (target-block
  convention); distance center-to-center (radius is its own channel).
- Normalization: relPos + distance / arenaRadius (120); relVel / ship MaxSpeed (1e-3
  floor); radius / const pinned from spawn settings' max asteroid radius at build time.
- PR sorts ascending by distance itself (`KeepNearest` only sorts on overflow); zero-pad
  tail; empty/no-field arena → all-zero block; radius 0 ⇔ empty slot (no presence flag).
- Smoke fixture re-mint via `training/rl/run_smoke.py` (unity-access coordination), LFS
  commit; `RLAgentEditModeTests` layout pins updated; focused EditMode token-math test
  (frame, sort, pad) + PlayMode case with a live PR-A field (`useAsteroidField` on).
- Tests headless; `-ScopeType Auto` iteration; worktree loop, slot agent-1
  (lease `rl-env-prb-ktokens`); user editor open on agent-1 (PID 38380) — coordinate
  batch runs via unity-access, never close it.

## Blindsider resolutions

(None survived the post-lock pass — all candidates resolved to code-grounded
assumptions above.)

---

# PR-D — Decision brief (frozen 2026-07-19, pr-prep)

## Scope

**In:** ML-Agents environment-parameter wiring for the curriculum — per-episode overlay
of env params onto `RewardSpec` in `TrainingHost` (pure overlay function, param names as
C# consts); mixture weights move from `OpponentRoster` consts to spec fields (defaults =
today's .4/.2/.15/.15/.1); collision-lethality knob `UpdatingAsteroidField.SetLethalityScale`
flowing through the spawn chain into `AsteroidDamage.Initialize(volume, lethality)`
(default 1, folds into `CalcDamage`); per-episode density applied in `HarnessField.Reset`;
roster wired into `TrainingHost` (mixture live in training from episode 0) and
`CheckpointEvaluator`; per-archetype stratified eval — pinned archetype blocks, per-archetype
W/L/D + Wilson lower bound, **no blended number anywhere in the summary** — plus spec-driven
optional field spawn in eval (density-3.0 stretch); `environment_parameters` + curriculum
lessons in `ppo_ship_combat.yaml` (`use_asteroid_field`=1 constant; density 1.0→1.5→2.0;
lethality 0.25→1.0; dummy-weight decay ≈0.4→0.1; reward-keyed, provisional thresholds);
YAML-keys ↔ C#-consts ↔ defaults pin test; orbiter centripetal feed-forward fix +
watch-lane verification.

**Out (non-goals):** the training run itself (a run, not a PR — launch is a separate
spend approval; thresholds finalized then from pilot TensorBoard); frozen-checkpoint
kiter opponent → PR-4 (`ShipCombat-pilot.onnx` is obs-24, incompatible since PR-B's
24→72); `StatsRecorder`/TensorBoard code (lessons key on mlagents' own reward measure);
smoke/pilot YAML changes; smoke-fixture re-mint (obs unchanged); weapons; renaming
`RewardSpec`.

## Fork resolutions (with why)

1. **Curriculum values flow through `RewardSpec`, overlaid per episode in TrainingHost.**
   *Why:* every downstream consumer already takes the spec (`HarnessField.Reset`,
   `OpponentRoster.Install`); the spec embeds verbatim in each JSONL row, so effective
   per-episode curriculum values are recorded for free (self-description preserved);
   eval/tests keep authoring specs directly, never touching Academy; the overlay is pure →
   EditMode-testable. Push-setters (values bypass the recorded spec) and consumers-read-
   Academy (spreads the ML-Agents dependency) rejected.
2. **Lethality = field-level knob through the spawn choke point.** `SetLethalityScale`
   beside `SetDensityScale` (same staged pre-rebuild API family); value rides
   `AsteroidSpawner.Spawn → AsteroidController.Initialize → AsteroidDamage.Initialize`.
   *Why:* the one choke point covers layout spawns, pool reuse, and mid-episode fragments;
   Initialize-injection philosophy verbatim; PR-A's domain-honest field-capability
   precedent. Static multiplier rejected (multi-arena), harness-side sweep rejected
   (misses fragments).
3. **Schedule: dummy-only mixture decay; density 1.0→1.5→2.0; lethality 0.25→1.0;
   independent reward-keyed lessons staged by ascending thresholds.** *Why:* `Pick`
   normalizes weights implicitly, so only weights that change need lessons; probe GO
   fixed the density endpoints (3.0 is eval stretch, not a lesson); thresholds ship
   provisional — the run launch owns final values.
4. **Eval = stratified pinned blocks.** *Why:* equal n per archetype (mixture sampling
   starves exactly the low-weight archetypes of episodes); the pinned
   `Install(archetype, …)` overload + degeneracy-gate precedent already exist; "never a
   blended number" taken literally — the summary carries no aggregate win rate.
5. **Orbiter fix = centripetal feed-forward**: one inward `Kff·v²/r` term in
   `OrbiterChooser`'s radial component. *Why (root cause):* the law commands a purely
   tangential rotating velocity — dynamically inconsistent with circular motion — and the
   P-only radial term needs a standing radius error (∝ v²/r) to supply the centripetal
   demand; the feed-forward matches the disturbance's shape, killing both bias and
   jitter compression across the draw range. `Kff` tuned via the orbiter watch lane
   (`meanOrbitRadiusError` already measured). Gain-raise (residual shape remains,
   oscillation risk), constant bias (draw-dependent scatter remains), and widened drawn
   range (compression untouched) rejected.

## Assumptions (user-reviewed)

- Weights are five flat `RewardSpec` fields; `Pick` reads them from the spec it already
  receives; the burn-the-selection-roll contract is preserved (pinned/mixture jitter
  draws stay aligned).
- Overlay reads via an injected getter; TrainingHost passes
  `Academy.Instance.EnvironmentParameters.GetWithDefault`; EditMode tests feed a
  dictionary.
- Pin test (RLTrainerConfigEditModeTests style): YAML env-param keys ↔ C# consts, and
  lesson-0 values ↔ `RewardSpec.Default` — closes `GetWithDefault`'s
  silent-fallback-on-typo trap.
- Weight sum ≤ 0 from YAML throws at the overlay boundary (operating error, checked once).
- `use_asteroid_field` rides as a 0/1 env param (>0.5 parse at the boundary) so the YAML
  fully describes the run; `RewardSpec.Default` stays `false` — tests and smoke
  byte-identical when no trainer is attached.
- TrainingHost constructs the roster at composition (opponent still on its prefab-default
  chooser — the ctor precondition) and passes it via the existing optional driver param.
  Every training row now records `OpponentDraw` — free offline per-archetype analysis.
- CheckpointEvaluator constructs/disposes a roster per seed block (fresh pair per seed
  already); eval cost grows ~5× (archetypes × seeds × episodesPerSeed) — accepted.
- JSONL schema stays `rl-episode-v2` (spec additions ride additively); eval summary JSON
  gains the per-archetype array (artifact, not a pinned stream).
- Orbiter jitter range 10–18 stays as drawn; fix verified by rerunning the opt-in
  orbiter watch lane until `meanOrbitRadiusError` flattens across draws.
- Tests headless, `-ScopeType Auto` iteration (known wart: RLHarness-only diffs fall back
  to the full suite); worktree loop, slot agent-3 (warm Library post-#175).

## Blindsider resolutions

- **Pool pre-size vs density ramp: accept mid-run growth.** The pool pre-sizes at field
  spawn (lesson-0 density); the first 2.0-density rebuild instantiates the shortfall at
  an episode boundary (rebuilds are synchronous between episodes — never mid-episode) and
  the pool stays grown. Ceiling pre-size rejected (max-density memory held all run for a
  one-off boundary burst).

---

# Eval-env mirror — Decision brief (frozen 2026-07-19, pr-prep)

Follow-up to PR-D (codex P1 on #176, board card "Eval env must mirror training
curriculum"). Root cause: `EvalHost` hands `CheckpointEvaluator.Run` a bare
`RewardSpec.Default` (field off, density 1.0) while training terminates at field-on
density 2.0 — the PR-D mechanism (spec-driven field spawn, per-episode
`HarnessField.Reset` density/lethality) is complete; only the eval entry's spec
authoring is wrong. Blocks trusting any checkpoint selection.

## Scope

**In:** canonical eval-env authoring at the eval entry — `EvalProtocol` pins the
canonical eval spec values (field ON, density = the YAML curriculum's final lesson);
`EvalHost` authors its spec from them via serialized fields; `RunEval` maps an optional
`RL_EVAL_DENSITY` env var onto them (stretch/diagnostic runs); pin test in
`RLTrainerConfigEditModeTests` asserting canonical density == the YAML `density_full`
final-lesson value; `Summary` gains additive eval-env fields; density-overridden runs
suffix the artifact tag; README batch-entry doc line.

**Out (non-goals):** any evaluator-mechanism change (density grid runs as separate
batch invocations — B1); field-OFF eval (the empty-arena eval IS the bug; no env var
resurrects it); seed-protocol changes (selection on "train", sealed held-out final gate,
unchanged); mixture-weight handling (dead in eval — pinned `Install` bypasses `Pick`);
threshold finalization and the training run itself.

## Fork resolutions (with why)

1. **A3 — hybrid pin + override.** `EvalProtocol` pins the canonical values with a
   YAML-final-lesson pin test (the exact mirror of the lesson-0 pin); `RL_EVAL_DENSITY`
   overrides for the 3.0 stretch. *Why:* the defect WAS silent training/eval env drift —
   env-var defaults alone (A2) recreate it one layer down; a pin alone (A1) can't express
   the stretch the probe GO bought. The pin makes drift fail a test; the override keeps
   the grid expressible without touching the evaluator.
2. **B1 — density grid outside the evaluator.** One density per batch invocation,
   separately tagged artifacts. *Why:* selection protocol runs train-seed eval per
   checkpoint with the stretch reserved for finalists, so in-run iteration (B2) reopens
   the frozen evaluator + summary schema to save boots that mostly don't happen.

## Assumptions (user-reviewed)

- Only spec authoring changes; `CheckpointEvaluator`/`EpisodeLoopDriver`/`HarnessField`
  untouched.
- Canonical env = field on + density 2.0; lethality 1.0 already equals the final lesson;
  weights stay Default (unused under pinned install).
- Spec authored directly at the entry, never via Academy (PR-D fork 1 stands).
- Pin test needs a `LessonFinalValue` parser (last `value:` in the param block; analog of
  `LessonZeroValue`).
- `EvalHost` serialized fields default to the canonical values (`onnxAssetPath`
  precedent); `RL_EVAL_DENSITY` parsed invariant-culture at the boundary, throw on
  garbage.
- README batch entry gains the `RL_EVAL_DENSITY` line.
- Tests headless EditMode; `-ScopeType Auto`; worktree loop.

## Blindsider resolutions

- **Artifact marking for overridden runs:** `Summary` gains `useAsteroidField` +
  `fieldDensityScale` (additive; summary JSON is an artifact, not a pinned stream), and
  the filename tag is suffixed only when density is overridden (e.g. `held-out-d3`) — a
  stretch run can never masquerade as the canonical eval in a folder listing or in its
  own summary.
