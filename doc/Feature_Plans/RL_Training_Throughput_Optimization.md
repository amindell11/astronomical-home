# RL training throughput optimization

> STATUS: living — arc opened 2026-07-22 after the PR-4 launch prep measured the real
> per-step cost. Driver: many training runs are planned, so per-step cost compounds.

**Parent:** `RL_Training_Throughput.md` (Path A/B delivery mechanisms — this arc is about
the cost of a step, not how many step in parallel).
**Driving memory:** `project_rl_training_throughput`, `project_tactical_ai_direction`.

## Why this arc exists

The PR-4 self-play launch prep measured throughput for the first time. The plan doc's
assumed "editor -> player 2-4x speedup" does not exist, scaling across processes is
sublinear, and a 2M-step run costs ~6.4 h. With many runs planned, per-step cost is the
compounding term.

## Measured baseline

Main `0925ecda`, Mono **development** player build, scripted-roster composition,
Intel Ultra 7 155H (6 P + 8 E + 2 LP-E = 16 physical / 22 logical), 31.5 GB.

| config | steps/s | cores (total) | cores/worker |
| --- | --- | --- | --- |
| N=1, M=1 | 37.8 | 5.0 | 5.0 |
| N=4, M=1 | **87.3** | 14.0 / 16 | 3.5 |
| N=2, M=2 | 58.4 | 9.9 | 5.0 |

- Trainer (PPO, CPU torch): **0.00 cores** — not the bottleneck; gRPC/torch-thread levers are dead.
- Worker RSS ~250 MB (drift +2.3 MB/min). **RAM is not a constraint.**
- `--num-arenas M > 1` is a throughput **loss** (M arenas share one Unity main thread, so the
  process cannot expand onto more cores). M stays a memory / CTDE-teams tool. **Use M=1.**
- 4x processes buys 2.31x, and N=4 already consumes 14/16 cores. Repacking is near its ceiling.

**Derived load.** 1 decision = `DecisionIntervalSteps` 10 fixed steps. N=1 => 378 fixed
steps/s (7.6x real-time). Each fixed step runs the MPC for both ships at
`samples 512` x horizon 17 (`MpcSettings_AgentPilot.asset`) =>
**~6.6M rollout-steps/s**. That is where the 5 cores go — Burst `IJobParallelFor`
(`MPC/BurstSolver.cs:249,278`), real work, not job-thread spin.

**Consequence: the lever is per-step work, not scheduling.**

## Phase 0 — a reproducible bench (foundation, do first)

Throughput was measured four times during prep and the methodology moved each time
(boot time included/excluded, first-summary vs steady-state delta). Every later phase is
an A/B, so the measuring instrument comes first.

`training/rl/bench_throughput.py`:
- Wraps `run_parallel.py` (does NOT re-implement the trainer invocation — imports its
  constants so the trainer-log location stays producer-owned, CLAUDE.md #6 corollary).
- Args: `--num-envs`, `--num-arenas`, `--config`, `--steps`, `--label`.
- Emits steady-state steps/s from **consecutive summary deltas** (first interval discarded —
  it carries env boot), plus cores/worker and peak RSS, appended as a JSONL row.
- Acceptance: two runs of the same config agree within ~5%.

## Phase 1 — semantics-preserving wins

Each lands separately and is A/B'd on the Phase 0 bench, so a regression is attributable.
None of these change the transition function, so any checkpoint stays valid.

- **1a — non-development build.** `RLTrainingPlayerBuild.cs:26` uses `BuildOptions.Development`
  (profiler instrumentation + full `Debug.Log` stack traces). One line; cheapest test first.
- **1b — strip headless presentation.** The ship prefab carries 6 ParticleSystem, 5
  ParticleSystemRenderer, 3 AudioSource, a Light, Canvas + CanvasRenderer, 2 SortingGroup,
  driven every frame by `ThrusterVisuals.cs:15`, `EngineAudio.cs:53`, `HullVisuals.cs:134`,
  `ShieldUI.cs:68`. Particles simulate under `-nographics`. **No `EnableVisuals` toggle exists.**
  Because the pacing contract puts one full player frame on every fixed step, all of this sits
  on the critical path. Design fork (resolve before building): harness-side strip at compose
  vs. a prefab variant vs. a first-class visuals toggle.
- **1c — envelope math off non-boundary steps.** `EpisodeRunner.cs:74` builds a full
  `CombatSnapshot` every fixed step including `AnySlotInEnvelope` x2
  (`CombatSnapshot.cs:62-79`), but only `myAlive`/`enemyAlive`/`distFromCenter` are read
  off-boundary (`EpisodeTypes.cs:37-50`). Doubled in self-play. Hoisting is observationally
  identical.

## Phase 2 — diagnosis — RUN 2026-07-22, GATE PASSED

Throwaway exe with `samples: 512 -> 128` on **both** MPC assets (agent and opponent host), so
the cut covers both ships. Assets reverted immediately after; nothing shipped.

| bench row | steps/s | cores | wall for 24k steps |
| --- | --- | --- | --- |
| `no-dev-build` (baseline) | 83.5 | 13.32 | 300 s |
| `mpc-samples-128` | **158.6** | 10.65 | 160 s |

**1.90x.** With `speedup = 1 / (1 - 0.75f)`, the MPC solve is **~63% of per-step cost** —
comfortably past the 40% gate. Cores also *fell* to 10.65/16, so the configuration stops being
core-saturated and N has room above 4 again: the two levers compound.

Early fidelity signal (indicative only, NOT the gate): mean reward tracked the baseline
(both ~0.84 at 24k after a mid-run dip) and the outcome mix stayed comparable across ~120
episodes. No collapse, but far too few episodes to call tracking preserved.

## Phase 2b — the real fidelity question (do before Phase 3 ships)

The 1.90x is only bankable if a cheaper solver still *tracks the commanded velocity*.

The oracle is `MpcVelocityReferenceEditModeTests` (the PR-1 tracking-fidelity suite; its plan
doc was purged in the doc-lifecycle cleanup, the tests are the surviving artifact). It closes
the loop on `Mpc.Plan`/`Model.Step` against the real settings asset and asserts on-axis
convergence, off-axis strafe authority, and braking to a zero reference.

Those are **pass/fail at fixed thresholds**, which answers "did it break" but not "how much
margin is left" — so the sweep wants the same scenarios reporting the underlying ratios as
continuous metrics, across 512 / 256 / 128 / 64, to find the knee rather than the cliff.
Cheap: EditMode only, no player build and no training run.

Sample count is also not the only axis: `horizonSeconds`/`rolloutDt` (17 steps) and
`noiseKnots 5` trade against tracking differently, and one may buy the same time at lower
fidelity cost. Sweep them rather than assuming `samples` is the right knob.

## Phase 3 — MPC CEM budget for velocity mode (semantics-changing)

`MpcSettings_AgentPilot` (`samples 512`, `horizonSeconds 1.7` / `rolloutDt 0.1` = 17 steps,
`noiseKnots 5`) was sized for the **full tactical planner**. In velocity-reference mode
`tacticalEnabled=false` and the cost collapses to feasibility + aim + velocity-track
(`Cost.cs:104-111`). Tracking a velocity reference is near-convex; 512 samples is plausibly
several times more than needed.

- **Gate = the existing tracking-fidelity oracle** from the tactical arc
  (`MPC_Velocity_Reference_Mode.md` PR-1): reduce the budget until tracking fidelity degrades,
  and stop before that. If tracking is preserved, policy-visible dynamics are preserved — that
  is what makes this defensible rather than a blind knob turn.
- **Home:** rides the MPC rip-out arc (`MPC_GoalMode_Ripout.md`), which is already deleting the
  tactical costs that justified the budget. Sequence after that arc's PR-2.
- **Risk:** any surviving checkpoint warm-starts into a shifted env. Needs explicit sign-off.

## Phase 4 — structural candidates (not scoped; evidence first)

- **IL2CPP for Standalone.** `ProjectSettings.asset` sets `scriptingBackend` only for Android,
  so Standalone falls through to Mono. Would not touch Burst-compiled MPC. Determinism pins
  must be re-verified before this is credible.
- **Physics config** (`DynamicsManager.asset`): layer collision matrix entirely unpruned
  (`all-ffff`), `m_SleepThreshold 0.005` (bodies effectively never sleep), `m_WorldBounds` +-250
  while arenas fan out at 400 spacing (`TrainingHost.cs:29`). All semantics-adjacent.
- **Asteroid field:** global `Physics.SyncTransforms()` once per asteroid spawned
  (`AsteroidController.cs:119`, hundreds per episode rebuild); anchorless harness fields keep
  every detailed MeshCollider enabled (`AsteroidController.cs:190`).

## Non-goals

The PR-4 self-play run itself (parked); the `RL_SELFPLAY` launcher blocker (a correctness fix,
tracked separately); anything altering obs/action shape.

## Coordination

- **agent-1, MPC rip-out PR-2** deletes the utility/tactical layer and touches
  `HarnessAssets`/`EpisodePair`. Phase 3 sequences after it; Phase 1b may contend over ship
  prefabs.
- Re-measure under **self-play** composition once the `RL_SELFPLAY` blocker is fixed — the
  baseline above is the scripted roster. Per-step levers are composition-independent, so
  Phase 1 conclusions carry, but absolute steps/s will move.
