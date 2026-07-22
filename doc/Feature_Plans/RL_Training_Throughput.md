# RL Training Throughput — headless player + `--num-envs`

> STATUS: living — RL training throughput design reference. PR-1 player-buildable SHIPPED
> #185; PR-2 `--num-envs` decorrelation + `run_parallel.py` implemented (this PR; e2e
> `--num-envs 2` liveness gate is a coordinated follow-up run). In-process M arenas = Path A
> (deferred). Supersedes the *in-process* multi-arena framing as the near-term throughput play.

**Driver:** the curriculum retrain (`handoff_2026-07-20_reward_fix_retrain.md`) is
frame-rate-bound in a single batch-mode editor at one arena. The user chose a multi-arena
throughput PR before committing to the 2M run. Grounded via pr-prep with the user.

Parent context: `Multi_Arena_Substrate.md` (the shipped in-process substrate — now Path A,
deferred), `RL_Env_Step_Package.md` (the single-arena env-step arc this parallelizes).
Driving memory: `project_multi_arena_rethink`, `project_tactical_ai_direction`.

## The delivery-mechanism decision (Fork 1 — resolved)

Two ways to get throughput:

- **Path A — in-process M arenas (one editor).** Uses the shipped `ArenaContext`-offset
  substrate. But it requires refactoring the manual-stepping loop: `EpisodeLoopDriver`
  today *owns* `Academy.EnvironmentStep()` in a sequential per-arena coroutine, and
  `EnvironmentStep()` is process-global — M arenas under one Academy is the delicate part.
  Ceiling ~3–6× before main-thread physics saturates; stays under editor overhead.
- **Path B — headless standalone player + ML-Agents `--num-envs`.** Each process runs
  today's single-arena scene **unchanged** — zero stepping-loop change. Ceiling =
  editor→player (~2–4×) × cores. Cost is a *bounded* player-build rework (the harness is
  editor-only only because the composition loads prefabs via `AssetDatabase`; the
  composition asm already targets `WindowsStandalone64`).

**Decision: Path B now, Path A later (phased).** Path B has the higher throughput ceiling
*and* avoids the manual reset↔step-ordering refactor entirely. The initial "editor-only is a
wall" read was wrong: the block is ~5 files of `AssetDatabase` prefab loads over a fixed
asset set, and `Game.RLHarness.Editor` already lists `WindowsStandalone64`. Path A stays a
documented follow-up (below); the substrate is correct and unused-for-now, not wasted.

They compose: `--num-envs` of in-process-M-arena players later, if memory/core-efficiency
demands it.

---

# PR-1 — Player-buildable harness — Decision brief (frozen 2026-07-21, pr-prep)

## Scope

**In:** make the RL training harness compile and run in a headless `StandaloneWindows64`
player, proven by a single-env `mlagents-learn --env` run at behavioral parity with the
batch-editor run. Concretely: a serialized `HarnessAssets` SO catalog (ship + two pilot
prefabs + field prefab) threaded through the composition, replacing the editor-only
`AssetDatabase` loads in `EpisodePair`/`HarnessField`; drop the `UNITY_INCLUDE_TESTS`
defineConstraint from `Game.RLHarness.Editor`; wire `RLTraining.unity`'s `TrainingHost` to
the SO; a net-new editor build method producing the standalone exe; a player-appropriate
episode-JSONL output path.

**Out (non-goals):** `--num-envs` / parallelism (PR-2); per-worker seed decorrelation (PR-2);
per-worker JSONL suffixing (PR-2); any in-process multi-arena / `ArenaContext` offset work
(Path A follow-up); eval in a player (`CheckpointEvaluator`/`EvalHost` stay editor-only,
their ONNX `AssetDatabase` load untouched); the training run itself (a run, not a PR);
renaming `Game.RLHarness.Editor` (cosmetic; defer).

## Fork resolutions (with why)

1. **Asset loading = one serialized `HarnessAssets` SO, threaded through the composition**
   (not `Resources.Load`, not overloaded entry points). The SO holds `ship`, `agentPilot`,
   `baselinePilot`, `fieldPrefab` refs; the scene's `TrainingHost` references it
   (`[SerializeField]`, baked into the build) and tests/eval load the *same* asset via
   `AssetDatabase`. `EpisodePair.Spawn`/`SpawnWithAgentChooser`/`SpawnLasersOnlyShip` and
   `HarnessField.Spawn` take it as a parameter. *Why:* single source of truth — `EpisodePair`
   exists so "the scenario cannot drift between hosts"; asset refs must be single-sourced the
   same way, which a player-only side-path (overloads) or path-string loading breaks.
   `Resources.Load` would relocate game-shared prefabs (`Ship_2`, `UtilityPilot`) into
   `Resources/`, bloating every build with the discouraged pattern. Cost: ~15 mostly-mechanical
   call-site edits (most are editor tests). Serialized-prefab-ref precedent: `GameDriver`,
   `SectorEntry`.
2. **asmdef = drop the `UNITY_INCLUDE_TESTS` constraint from `Game.RLHarness.Editor`**
   (narrow), not extract a dedicated `Game.RLHarness.Training` asm (structural). *Why:* the asm
   is `autoReferenced: false` and referenced only by the two test asms — Unity includes an
   asmdef in a build only when a built scene reaches its types, so it lands in the `RLTraining`
   player build but **never in the shipped game build**; reachability already does what the
   test-gate did. `com.unity.test-framework` is in the manifest, so the editor always defines
   the symbol regardless. Verified: all six editor-API-touching files in the asm
   (`EvalHost`, `ShipAgentFactory`, `TrainingBootstrap`, `EpisodePair`, `HarnessField`,
   `TraversalDrivers`) already wrap their `UnityEditor` usage in `#if UNITY_EDITOR`, so the asm
   compiles clean for `StandaloneWindows64` once the gate drops — no file-wrapping needed. The
   asm-split is a speculative restructure (touches PR-4 in-flight files) that reachability makes
   unnecessary.

## Assumptions (user-reviewed)

- `RLTraining.unity` is the build scene; `TrainingHost` runs on `Start` in the player
  (`TrainingBootstrap` stays editor-only executeMethod scaffolding, unused in-player).
- Training uses `RemotePolicy` (trainer over gRPC) — **no model asset in the player build**;
  only the *prefab* loads block it, so `ShipAgentFactory`'s ONNX load (eval/inference-only)
  needs no change.
- The `HarnessAssets` asset lives at a known editor path (e.g. `Assets/Settings/RL/…`); the
  scene references it directly, tests/eval load it once via `AssetDatabase`.
- `TrainingHost` fails loud if its catalog ref is null (composition-boundary throw, not a
  silent default — fix-ladder rung 2/4).
- Build method is a net-new `#if UNITY_EDITOR` `-executeMethod` entry
  (`BuildPipeline.BuildPlayer`, scenes passed explicitly, `StandaloneWindows64`).
- Editor-attach single-arena path (`run_training.py` / batch editor) stays for dev iteration —
  the player path is additive.
- Eval, tests, traversal probe stay editor-only (unchanged).
- Tests headless EditMode/PlayMode; `-ScopeType Auto`; worktree loop.

## Blindsider resolutions

- **Whole-asm standalone compilation** (the drop-the-gate wrinkle): resolved to a no-op —
  every editor-API usage in the asm is already `#if UNITY_EDITOR`-guarded (verified file by
  file); the `#else throw` branches in `EpisodePair`/`HarnessField` vanish once the SO replaces
  `AssetDatabase`. No `#if` sprinkling, no asm split.
- **Player episode-JSONL path:** `EpisodeJsonl.NewRunPath` climbs `Application.dataPath/../../..`
  (editor layout) — wrong in a player. PR-1 writes the training JSONL to a player-appropriate
  dir (beside the exe / `persistentDataPath`); the trainer's checkpoint + episode-log markers
  are the parity signal, so JSONL location is diagnostic, not load-bearing. (Worker-suffixing
  for collisions is PR-2.)
- **Parity verification without PR-2's launcher:** PR-1 ships a minimal single-env `--env`
  invocation (or a `run_smoke` `--env` flag); the `--num-envs` orchestration is PR-2. Gate =
  the built exe trains one env to a checkpoint at parity with the editor run.

## Interaction with in-flight PR-4 (self-play, unpushed on agent-1)

PR-4's `ScriptedRosterComposition`/`SelfPlayComposition` call `EpisodePair.SpawnWithAgentChooser`
and `HarnessField.Spawn`, whose signatures gain the `HarnessAssets` param here. **PR-1 lands
first** (foundation); PR-4 rebases and threads the catalog through its compositions. Path B is
otherwise orthogonal to PR-4 — self-play stays single-arena-per-process and `--num-envs`
parallelizes it for free.

---

# PR-2 — `--num-envs` throughput (scope sketch, prep when PR-1 lands)

**In:** `run_training.py` gains an `--env <exe> --num-envs N` path launching N player copies
under one trainer; per-worker seed decorrelation (parse `--mlagents-port` → worker offset
`k` → `runSeed = derive(baseSeed, k)`, so worker seeds are `N`-independent and reproducible;
editor-attach = worker 0 = today's seed); per-worker JSONL filename suffixing; a `--num-envs 2`
player smoke proving two *decorrelated* envs both train (the merge gate).

**Why decorrelation is required, not optional:** poses / field layout / opponent-archetype
selection all derive from `runSeed`; N copies on one seed share identical initial conditions
per episode index, so the throughput buys near-duplicate experience.

---

# Path A — in-process M arenas (deferred follow-up)

The shipped `ArenaContext`-offset substrate (`Multi_Arena_Substrate.md`) remains the tool for
running M arenas **per process** (one editor, or layered under each `--num-envs` player). Its
open cost is the stepping refactor: demote `Academy.EnvironmentStep()` ownership out of the
sequential `EpisodeLoopDriver` coroutine into a central per-tick stepper driving M async arena
state machines (the substrate doc's "Academy is the clock" model), plus per-arena seeding
(same derivation as PR-2). Revisit only if Path B's throughput proves insufficient or memory
per process becomes the bottleneck.

> **Superseded 2026-07-22:** the stepping cost above was paid by the stepping-migration arc
> (#188/#197 — the Academy auto-clock is now the sole stepper), unblocking Path A. The frozen
> decision brief below is the shape actually built.

---

# Path A — M-arena harness fan-out — Decision brief (frozen 2026-07-22, pr-prep)

> Grounds against post-#197 `main`. The Academy auto-clock is the sole stepper; the harness
> is already arena-local below `HarnessArena.Compose` (per-arena `UnitService`/
> `NavFieldService`/`ProjectileService`/`ArenaContext`; poses + field placement parameterized
> on `arena.Offset`). **Plan-vs-code correction:** the substrate doc's session-root /
> `UnloadSector`-reset `RLDriver` model targets *game sectors* and predates the harness's
> current shape — post-#197 `EpisodeLoopDriver` already *is* the reset-only driver. The whole
> single-arena hardwiring is: `HarnessArena.cs:14` `Offset = Vector2.zero`, one composition +
> driver per process, `TrainingHost`'s single sequential episode loop, one JSONL path, no
> per-arena seed stream.

## Scope

**In:** M in-process arenas in the training harness — `TrainingHost` spawns M child
GameObjects, each composed via `HarnessArena.Compose(host, offset_j)` (signature gains the
offset; both compositions thread it) with its own composition object + `EpisodeLoopDriver`;
M **async** per-arena episode-loop coroutines (curriculum overlay → `RunEpisode` → JSONL →
repeat; no cross-arena barrier); per-arena seed derivation (`ArenaSeedStream`, arena-0
identity); per-arena JSONL suffix `-a{j}` composing with `-w{k}`; `numArenas` serialized +
`--harness-num-arenas` CLI contract; `run_parallel.py --num-arenas` plumbing + smoke-gate
glob update; EditMode seed pin + PlayMode M=2 isolation/decorrelation test; self-play trainer
smoke at 2 arenas as the heavy merge gate.

**Out (non-goals):** eval parallelism (`CheckpointEvaluator`/`EvalHost` stay M=1);
editor-attach `run_training.py` changes (M=1 default reaches it untouched); any game-side /
substrate code (`SessionHost`, sectors, `UnitService.arenaBaseSeed` TODO — game path only);
`PhysicsScene`-per-arena (substrate PR-C, escalation only on an observed leak); the
throughput benchmark itself (a run, not a PR: post-merge sweep M ∈ {1,2,4,8} on the rebuilt
exe decides real-run M).

## Fork resolutions (with why)

1. **Arena unit = harness-native fan-out** (M × `HarnessArena.Compose` at offsets), NOT
   migration onto the SessionHost/sector substrate. *Why:* everything below the compose call
   is already arena-local and offset-aware, so fan-out is "call the existing composition M
   times" — near-zero new wiring, zero game-side diff; substrate adoption would drag
   sector/rig machinery (the deferred SessionRig cleave comes due) into a deliberately
   minimal training composition for no throughput gain. `ArenaContext` is the shared seam and
   both paths already go through it. User blessed the deviation from the substrate doc's
   sketch explicitly.
2. **Loop model = M independent async per-arena coroutines**; `episodes` budget is
   per-arena. *Why:* lockstep wastes exactly the throughput this buys (fast arenas idle at a
   barrier) and correlates boundaries; the driver/runner are instance-scoped already; dense
   per-arena episodeIndex is required anyway (poses/field/archetype streams derive from it).

## Assumptions (user-reviewed 2026-07-22)

- `ArenaSeedStream = 707` sibling of `WorkerSeedStream = 606`:
  `arenaSeed_j = j==0 ? workerSeed : SeedScope(workerSeed).Derive(707).Derive((uint)j)`,
  applied base → worker → arena. Arena-0 identity ⇒ **M=1 byte-identical to today** (all
  pins/fixtures/evals untouched, no re-mints; existing full suite is the parity proof).
  Decision seeds (101/202), field (303), poses, archetype (505) fan from `spec.runSeed` →
  decorrelate downstream for free (the PR-2 argument).
- M knob: serialized `numArenas` default 1 + `--harness-num-arenas` override, parsed once at
  Start; present-but-garbage throws loud, never a silent 1 (PR-2 boundary rule).
- `run_parallel.py` owns the launcher contract (`--num-arenas` via trailing `--env-args`);
  its smoke-gate file assertions widen to the `-a{j}` glob. Rides in this PR (CLAUDE.md #6
  corollary).
- Fan-out: M child GameObjects under the host; compositions thread the offset; `EvalHost`
  passes zero. Scripted-vs-selfPlay stays a process-wide choice; `PacingContract` stays
  host-owned, applied once.
- Spacing: arenas on a line, spacing ≥ 2·`arenaRadius` + laser `maxDistance` + drift margin;
  exact constant computed from the shipped prefab during build. OOB episode termination
  (`EpisodeTypes.cs:45-48`) already bounds ship excursion.
- Self-play at M>1 = M parameter-shared team-0/1 pairs under one behavior (standard
  ML-Agents multi-area topology); proven behaviorally by the smoke gate.
- Tests: EditMode arena-seed pin (j=0 identity; j0 ≠ j1; deterministic) mirroring
  `RLWorkerSeedEditModeTests`; PlayMode M=2 with deterministic leak-specific asserts
  (per-arena field-handle isolation, episode-0 poses differ across arenas, ships confined to
  their own half, both arenas complete episodes) — never mirror-determinism (substrate
  postmortem). Headless, `-ScopeType Auto`, worktree loop, unity-access coordination.

## Blindsider resolutions

- **JSONL = per-arena files `-w{k}-a{j}`** (mirrors the worker-suffix precedent; zero schema
  change; downstream gates widen their glob). Single-file-plus-arenaIndex-field rejected —
  touches the schema and every consumer for file-count aesthetics.
- **Ghost-rock hazard accepted with margin + documentation:** a rock drifting from arena A
  past the gap is invisible to B's handle-scoped obstacle scan but physically collidable.
  Drift is bounded per episode (field rebuilds every reset); the spacing margin covers
  worst-case drift. `PhysicsScene`-per-arena stays the escalation if a leak is ever
  observed. Field-culls-beyond-radius rejected — new field API surface for an unobserved
  hazard.
