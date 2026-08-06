# RL training throughput optimization

> STATUS: living — **the throughput measurement record**: measured baselines, step
> decompositions, and per-lever A/B verdicts that future passes must not re-derive.
> That evidence, not the arc plan, is what outlives the arc.
> Pass 1 closed 2026-07-23 at the core-saturation ceiling. **Pass 2 (§Pass 2, opened
> 2026-07-31): stages 0–2 COMPLETE** (Stage 0 baseline 2026-08-03, Stage 1 decompose,
> Stage 2 levers); its "custom trainer runtime" deferral became the trainer-runtime
> arc (`RL_Trainer_Runtime_Takeover.md`, issue #284), which now owns the open work.

**Parent arc:** Path A/B delivery mechanisms — shipped, brief deleted 2026-08-06;
record in memory `project_rl_training_throughput.md`. This arc is about the cost
of a step, not how many step in parallel.
**Driving memory:** `project_rl_training_throughput`, `project_tactical_ai_direction`.

## Why this arc exists

The PR-4 self-play launch prep measured throughput for the first time. The plan doc's
assumed "editor -> player 2-4x speedup" does not exist, scaling across processes is
sublinear, and a 2M-step run costs ~6.4 h. With many runs planned, per-step cost is the
compounding term.

## Measured baseline

Main `0925ecda`, Mono **development** player build, scripted-roster composition,
Intel Ultra 7 155H (6 P + 8 E + 2 LP-E = 16 physical / 22 logical), 31.5 GB.

> **EXPIRED as a comparison base.** `10b3849a` (#204, utility brain deleted) and `6df1f325`
> (#206, velocity-only MPC — goal modes, tactical costs, and the terminal field gone) both
> landed after these rows were taken. #206 in particular reshapes the solve that Phase 2
> measured at ~63% of per-step cost. Every row records its own `git_sha`, so the data stands;
> re-run the bench on current main before A/B-ing anything against these numbers.

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

### Phase 2b — RUN 2026-07-22, verdict: samples 512→128 is fidelity-free; don't touch rolloutDt

Method: the three oracle scenarios (on-axis converge, off-axis strafe, brake-to-zero) run
closed-loop (100 re-plans, `Mpc.Plan` + `Model.Step`, sim always stepped at the baseline
dt 0.1 config so rolloutDt sweeps stay comparable) as continuous ratios, via `execute_code`
in an editor on main `dfda3f15` against the real `MpcSettings_AgentPilot` asset. 21 configs
× 3 scenarios × 5 seeds, plus a 15-seed confirmation on the headline pair. Metrics normalized
by the commanded speed (0.5 × maxSpeed): `fwd` = achieved forward / commanded, `strafe` =
achieved strafe / commanded, `brake` = residual speed / initial. `planMs` = mean main-thread
`Plan` wall time (editor, warm Burst).

**There is no knee — every config down to samples 32 passes every oracle threshold on every
seed.** The grid's spread on `fwd` is 0.91–1.02, `strafe` 0.70–0.81, `brake` 0.03–0.11
(threshold 0.5). What does move:

| axis | effect |
| --- | --- |
| `samples` 512→128 | **fidelity-flat.** 15 seeds: fwd 0.956±0.029 vs 0.959±0.027, strafe 0.796±0.029 vs 0.780±0.015, brake 0.069±0.034 vs 0.078±0.019. Solve 0.288→0.116 ms = **2.5× on the solve**. |
| `samples` below 128 | still passes, but seed-variance grows (fwd ±0.073 at 64) and `planMs` hits a fixed-overhead floor (~0.096 ms at 32, only 1.2× below 128) — **no throughput reason to go under 128**. |
| `rolloutDt` 0.1→0.17/0.2 | the one real degradation: strafe authority drops ~8–10% (0.78→0.70–0.72). **Keep dt 0.1.** |
| `horizonSeconds` 1.7→1.2/0.8 | roughly neutral (fwd −2–3%, braking actually improves); buys little on top of the samples cut (0.133→0.104 ms at s128). Optional, not needed. |
| `noiseKnots` 5→3/2 | fidelity-neutral-to-slightly-better on fwd, more drift; no cost win (knots don't change the budget). Leave at 5. |

**Recommendation for Phase 3: cut `samples` 512→128 on both assets, change nothing else.**
Raw rows + sweep harness archived at `training/archive/mpc_fidelity_sweep_2026-07-22/`
(`rows_A/B/C/D.jsonl`, `sweep_body.cs`); rerun is ~2 min in any editor via `execute_code`.

### Obstacle addendum (user pull: "was the oracle run with asteroids?") — verdict unchanged

The open-space sweep matched the oracle's `enableObstacleAvoidance=false`; avoidance is the
multimodal regime where budget could actually bite, so three asteroid scenarios were added
(same closed loop, avoidance on, hand-built `ObstacleScan`, 150 steps, 10 seeds): **head-on**
(one r=3 rock on the commanded path), **gap** (two r=3 rocks, 3-unit gap centered on the
path), **slalom** (three staggered rocks). Metrics: min hull clearance over the trajectory,
collision steps, progress = displacement along the command / (speed × time). Configs: samples
512/256/128/64 at baseline horizon, plus 512/128 at horizonSeconds 1.2
(`rows_O1/O2.jsonl`, `sweep_obst.cs`).

**Zero collisions in all 180 runs; min clearance never below 0.33** (the 0.3 safety margin
held everywhere). s128 vs s512 at baseline: progress 0.835/0.751 vs 0.845/0.766
(head-on/slalom) — no avoidance degradation attributable to the samples cut, down to 64.

**Separate finding (budget-independent, pre-existing):** at the stock horizon 1.7 the tight
gap is bimodal across CEM seeds — the ship either threads (~0.77 progress) or balks and
stalls in front of it (~0.12). More samples do NOT fix it (s512 threads 3/10, s128 5/10);
horizon 1.2 threads 10/10 (~0.83, near-zero variance). Long-horizon collision-cost mass makes
the gap look expensive and the solver stalls at the wall. Not a regression and not this arc's
scope — noted for the tactical-AI backlog; it is additional evidence the budget is not the
binding term in avoidance behavior. Ship decision still needs the Phase 3 rebuild +
bench re-measure on current main (post-#206 baseline 100.5 steps/s) and explicit user
sign-off (env shift for any warm-started checkpoint).

## Phase 3 — MPC CEM budget for velocity mode (semantics-changing)

> **SHIPPED #209 2026-07-22 (main `e0318cd1`): `samples 512→128`, nothing else.** User-approved
> on the Phase 2b evidence. Bench A/B vs `post-206-baseline` (100.53 steps/s, N=4 M=1 24k
> warm-started, exe the only variable): **181.4 / 178.3 (repeat) = ~1.79×**. Worker CPU fell
> from 14.66-core saturation to well below — the loop went latency-bound at N=4. N-probe
> (same exe/config): **N=6 = 212.4, N=8 = 213.0 steps/s — plateau at N=6** (+18% over N=4,
> ~13–14 cores again; N=8 buys nothing for +400 MB). **Use `--num-envs 6`.** Arc total
> ~2.5× (83–87 → 213 steps/s); a 2M run ≈ 2.6 h. Rows in `results/rl-bench/bench.jsonl`.

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

## Profiling pass 2026-07-23 — the fixed step decomposed (post-samples-128)

Dev player (`RL_BUILD_DEVELOPMENT=1`, main `e0318cd1`), N=1 scripted-roster training load,
profiler attached from a tracked editor, 300 main-thread frames aggregated by self-time
(raw table: `training/archive/mpc_fidelity_sweep_2026-07-22/profile_fixedstep_2026-07-23.txt`).
Main thread ≈ 1.9 ms/frame (dev instrumentation inflates managed markers somewhat):

| bucket | ms/300f | share | notes |
| --- | --- | --- | --- |
| ML-Agents decision path (`root.DecideAction`) | 164 | **~29%** | fires every 10th step ⇒ ~5.5 ms/decision: obs collection + gRPC round-trip + action apply — the latency-bound term |
| MPC solve (128 samples: jobs + waits) | 97 | ~17% | `WaitForJobGroupID` 45 + `EvaluateCandidatesJob` 42 + gen/complete 10 |
| PhysX simulate + pipeline | ~86 | ~15% | Phase 4 territory (unpruned matrix, no sleeping) |
| AI perception/command (`AICommander.FixedUpdate` 32, `Scout.Update` 23) | 55 | ~10% | Scout scans every frame — 1c-adjacent candidate |
| Presentation + audio + UI (particles, canvas, audio, audibility) | ~25 | **~5%** | **Phase 1b's assumed big win is small — below bench resolution; DEPRIORITIZED** |
| Asteroid field maintenance | 16 | ~3% | |

Implications: (1) **Phase 1b is not worth its design fork** at ~5%. (2) The top term is the
per-decision round-trip — per-fixed-step Unity cuts don't shrink it. (3) MPC at 17% would
yield only ~9% more from a further 128→64 cut — not worth re-opening.
Bonus rows: dev-build N=1 = 62.0 steps/s (vs 37.8 at samples-512 dev = 1.64× per-worker).

**Trainer-CPU probe (same day, user pull):** instrumented N=6 bench with a 5 s sampler on the
mlagents process (`trainer_cpu_n6_2026-07-23.csv` in the archive dir). Trainer = **0.71 cores
steady** — real (the old "0.00 cores" datum is obsolete at 2.4× the decision rate) but NOT
saturated, so the single-trainer-cap theory is unconfirmed; the N=6/8 plateau reads as the
machine's practical ~14-core aggregate budget (workers 12.86 + trainer 0.71 + system).
Trainer pins ~1.0 core around ~260 steps/s, so env-side wins >~25% will hit it — check
trainer CPU again after any such win. Run-to-run: this run 184.9 steps/s vs the probes'
212–213 (spread ~60 at these rates ⇒ single runs swing ±15%; the N=6≈N=8 plateau stands).
**Arc closed 2026-07-23** — remaining candidates (PhysX ~15%, trainer-side) are sub-15%
items; nothing left above the bench's noise floor without new evidence.

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

> Pass 2 note: the obs/action non-goal was a Pass-1 constraint (protecting warm-started
> checkpoints), not a property of the problem. It is lifted for Pass 2 — see below.

## Pass 2 — profile before the K1 retrain (opened 2026-07-31)

Pass 1 closed with "nothing left above the noise floor" — but that verdict was taken
under a hard constraint: no obs/action changes, because surviving checkpoints
warm-start into the env. That constraint walled off the three largest terms in the
decomposition — the ML-Agents decision path (~29%), AI perception/Scout (~10%), and the
decision interval itself. The K1-3 schema break (obs 28, 5-cont + 2-disc actions;
memory `project_anchored_k1_arc.md`) invalidates every existing checkpoint anyway, so the
next run is from-scratch and the walled-off levers ride along at zero marginal retrain
cost. That — not re-running the exhausted safe levers — is what Pass 2 is for.

Also genuinely new since the closure: the entire Pass-1 profile was scripted-roster
(self-play "re-measure" was flagged and never run), and the code has moved (#210
headless hosts, #231/#238/#239/#240/#246 harness-lane arc, #247 velrebase).

**Metric correction.** The bench reports fixed-steps/s, but PPO consumes *decisions*
(= steps / `DecisionIntervalSteps` 10), and wall-clock-to-trained-policy =
decisions/s × samples-needed. The two only move together while the decision interval
is fixed — the moment it becomes a lever, optimize decisions/s, never steps/s. All
Pass-2 stages report both.

### Stage 0 — scratch-path correctness + re-baseline (mandatory first)

**2026-08-03 ruling — retire the legacy bridge.** The original warm-start arm is
invalid. `ship_combat_500k` exports one 72-float observation and 4 continuous actions;
current main expects entity observations `[64,7]` plus 26 core observations and 6
continuous actions. ML-Agents rejects the seed's Policy, value optimizer, and critic,
then silently initializes them from scratch. It therefore cannot bridge current main to
the historical ~213 steps/s result.

The historical absolute rate is also not a current acceptance threshold. That bench began
at density 1.0 with Dummy weight 0.4; the current full curriculum begins at density
0.1–0.3 with Dummy weight 8.0 (roughly 90% Dummy after normalization). Code, policy work
mix, and environment load all moved. Keep 213 as archaeology only — never a pass/fail line.

#### Stage 0C — full-config scratch correctness gate

Correctness precedes timing. Build a non-development player from finalized pre-K1 main,
then run the production `ppo_ship_combat.yaml` from scratch at N=6, M=1, scripted roster,
base port 5006. Start with a 4k-step canary. Require trainer exit 0, a checkpoint/ONNX,
six fresh worker JSONLs, and no surviving trainer/player process. A throughput number from
an incomplete fleet is not a degraded result; it is no result.

The first clean attempt on main `b2b8a8d1` exposed the current blocker: workers 0 and 3
each wrote a 600-decision random-vs-Dummy timeout, then stopped answering during/after the
episode boundary. ML-Agents aborted at step 1968 with `Workers {0, 3} stuck in waiting
state`; no bench row was appended.

**Resolved 2026-08-03 — invalid LFS-pointer player build, no runtime fix.** The worktree's
Git LFS assets were unresolved pointer files when that player was built. All six player
logs showed repeated `ArgumentNullException` from
`AsteroidController.MeanVertexRadius(meshInfo.mesh)`: the ten asteroid FBXs had imported
as null mesh references, four workers exited, and ML-Agents' failure-drain path reported
the two survivors as stuck. Hydrating those ten FBXs and rebuilding the same tree produced
a successful non-development player. A full-config scratch canary then reached step 4000
at N=6, M=1 with six fresh field-enabled JSONLs, an exported ONNX, no matching exception
in any player log, and no surviving process. The experimental cached-radius patch was
reverted; hiding unresolved render/collider assets would have preserved an invalid build.

Player-build preflight now includes verifying every training-required LFS asset is a real
payload, not a pointer. A Unity `Succeeded` build receipt alone does not prove this. Stage
0C first went green on the hydrated `b2b8a8d1` tree/build; the fully hydrated measured
trees and Stage 0M result follow.

#### Stage 0M — scratch-to-scratch matrix

Only after Stage 0C is green:

| Arm | Tree | Init | Purpose |
|---|---|---|---|
| **M** | finalized pre-K1 main | scratch | current production-schema/full-curriculum floor |
| **K** | K1 branch containing that exact main | scratch | prices the K1 obs/action and anchored-policy path |

Both arms use the same frozen `ppo_ship_combat.yaml`, N=6, M=1, 24k steps, and scripted
roster; neither passes `--initialize-from`, `--self-play`, or hybrid-worker flags. Rebuild
the non-development player per tree and record tree SHA, config SHA-256, and successful
player-build receipt with every row. Any later source/config merge invalidates both players.

Run ≥2 quiet-machine replicates per arm. If `abs(r1-r2) / mean(r1,r2) > 5%`, add a third
run and do not interpret the unstable pair. Report both fixed-steps/s and decisions/s
(`DecisionIntervalSteps` is 10); the bench's existing ~10–15% resolution remains the
attribution floor. Deliverable: the replicated M floor and K-vs-M delta — not whether an
obsolete 213 holds.

**2026-08-03 result — Stage 0 complete.** Both worktrees had all 309 LFS payloads
hydrated before their accepted builds. M is current main `3635cc65`; K is local-only
merge `49687e74` (K1 `48a0cbee` plus that exact main). Both used
`ppo_ship_combat.yaml` SHA-256
`DC13C81D887AFA1B65F4CEB192931164639A9178BC2F9B64915A3F5AB18E6855`.
Their non-development build receipts reported `Succeeded`, zero errors, and near-identical
payload size (~225.64 MB).

| Arm | r1 steps/s | r2 steps/s | mean steps/s | mean decisions/s | replicate gap |
|---|---:|---:|---:|---:|---:|
| **M** `3635cc65` | 160.92 | 161.45 | **161.185** | **16.119** | 0.329% |
| **K** `49687e74` | 139.33 | 139.06 | **139.195** | **13.920** | 0.194% |

Neither arm triggered a third replicate. K is **21.99 steps/s / 2.199 decisions/s slower
than M (-13.643%)**. The repeats are exceptionally stable, but the magnitude sits inside
the bench's historical 10–15% attribution band. Treat it as a likely material regression
at the resolution boundary, not a causal bucket assignment; Stage 1 decides which K path
owns it.

A future schema-compatible warm arm may diagnose policy-dependent episode mix, but it is
not part of Stage 0 and must never serve as the scratch K1 comparator. The self-play arm
remains DROPPED: K1-4 is locked as stock PPO against the scripted gate roster.

> **Closed setup finding — the player build was broken on main for 8 days.**
> `Game.Capture.Editor.asmdef` (Slice-B #246)
> carried `defineConstraints: [UNITY_INCLUDE_TESTS]`, so player builds silently dropped
> the whole Capture assembly while `Game.RLHarness.Editor` still referenced
> `CaptureDraw`/`CaptureConfig` — CS0246, Build Failure. Editor gates never see it; the
> merge gate never builds a player. Same failure mode #185 fixed on the RLHarness
> asmdef. **Fixed + shipped #251 (`a1319f04`).** It blocked the K1-4 run itself
> (`run_parallel` launches that exe) and PR-5 player-eval, not just this bench.
> Follow-up carded: the gate needs a player-build tripwire, or the next 8-day
> invisible break is only a matter of time.

### Stage 1 — re-decompose on the K1 scripted-roster path

Profiler-attach on an N=1 K-tree training load using K1-4's locked scripted roster; do not
substitute self-play. Refresh the Pass-1 bucket table under the composition that will
actually retrain. The critical split Pass 1 never made: inside the ~29% decision path, how
much is core-consuming obs-build/action-apply vs non-core gRPC wall-time? That split
routes Stage 2 — core-bound obs cost says "trim obs", wall-time latency says "decision
interval / more workers".

**2026-08-03 result — Stage 1 complete; do not hold K1-4 for obs trim.** Profiled the
unmodified measured K tree `49687e74` with a development player, N=1/M=1, the production
`ppo_ship_combat.yaml`, and the locked scripted roster. The profiling-only run reached
6,026 steps and exported normally. A bounded raw capture retained the last 300 main-thread
frames; ML-Agents' own hierarchical timers covered 60,226 fixed frames / 6,027 substantive
decisions. Do not treat this development/profile run as a throughput row.

| K main-thread bucket (300 frames) | self ms | share | Pass-1 comparison |
|---|---:|---:|---|
| synchronous ML-Agents exchange (`root.DecideAction`) | **152.2** | **47.2%** | 164 ms; absolute latency is essentially unchanged |
| obs-build + serialization + action apply | **~2.1** | **~0.6%** | previously hidden inside the decision parent |
| MPC jobs + waits | **~57.5** | **~17.8%** | 97 ms / 17%; same share, much less absolute work |
| PhysX + physics queries/pipeline | **~31.8** | **~9.9%** | ~86 ms / 15% |
| AI command + Scout scan | **~27.4** | **~8.5%** | 55 ms / 10% |
| presentation/audio/UI | **~9.1** | **~2.8%** | ~25 ms / 5%; still not a lever |
| asteroid maintenance | **~3.3** | **~1.0%** | 16 ms / 3% |
| other/profiler overhead | **~39.5** | **~12.2%** | residual |
| **main thread** | **322.8** | **100%** | 573.6 ms in Pass 1 |

The causal split is decisive. Across 6,027 decisions, `AgentSendState` (collection,
serialization, and masks) used 0.354 s and `AgentAct` used 0.028 s: **~0.063 ms per
decision interval combined**. The synchronous exchange used 29.130 s: **~4.833 ms per
decision, 98.7% of the complete ML-Agents decision path**. Even deleting all core
obs/action work would recover under 1% of this main-thread capture. The K1 observation
shape is therefore not the Stage-0 regression's actionable throughput cause, and trimming
it cannot justify another schema edit before K1-4.

**Route:** close the pre-K1-4 fork and proceed without obs trim. Preserve observation
changes for a later schema break only if they have a learning/semantic reason. A future
throughput experiment must attack the latency term directly (decision interval, batching,
or trainer/communicator scheduling), with the MDP-shift warning on decision interval and
decisions/s as the primary measure. PhysX remains a separate semantics-adjacent A/B, not
an explanation for the decision-path result.

### Stage 2 — one lever per bucket, A/B'd separately

**2026-08-03 result — existing batching and Torch thread-count levers closed; keep
N=6/M=1 and four Torch threads.** User-authorized matrix reused exact K `49687e74`, the
same config SHA-256 and scratch scripted roster, a fresh non-development player build,
24k steps, and two quiet replicates per interpreted arm. A temporary repository-side
entry wrapper changed only `torch.set_num_threads`; every trainer log recorded requested
and actual thread count, and the wrapper/venv junction were removed after capture.

| Arm | r1 steps/s | r2 steps/s | mean steps/s | decisions/s | gap | vs N6/M1/T4 |
|---|---:|---:|---:|---:|---:|---:|
| **N=6/M=1, Torch 4** (accepted Stage-0 K floor) | 139.33 | 139.06 | **139.195** | **13.920** | 0.194% | — |
| **N=3/M=2, Torch 4** | 137.86 | 137.16 | **137.510** | **13.751** | 0.509% | **-1.211%** |
| **N=6/M=1, Torch 1** | 104.23 | 103.92 | **104.075** | **10.408** | 0.298% | **-25.231%** |
| **N=6/M=1, Torch 2** | 127.56 | 129.69 | **128.625** | **12.862** | 1.656% | **-7.594%** |

No arm triggered a third run. N=3/M=2 is a stable tie inside resolution, not a throughput
win. It halves player RSS (~590 vs ~1,170 MB) and improves worker steps/core ~9%, but
two arenas share a Unity main thread and their episode boundaries desynchronize: policy
evaluation still fired ~21–23k times instead of collapsing toward ~12k. Keep M>1 as the
memory/CTDE topology, not the K1-4 throughput topology.

The thread result is causal, not noise. Four-thread Stage-0 PPO updates averaged 64.5 s;
two threads raised them to 94.8 s and one thread to 160.2 s. Policy evaluation did not
compensate: ~3.89 ms/call at four threads, ~3.82 at two, and ~4.31 at one. The Stage-1
N=1 profile stopped at 6k, below the 10,240-step PPO buffer, so it correctly decomposed
the base exchange but could not expose these update stalls; the full 24k matrix does.

**Ruling:** ship no topology or thread override. Existing ML-Agents defaults already win
on wall throughput. A future trainer-side lever must either batch ready *processes* before
`TorchPolicy.evaluate` or overlap PPO updates (`threaded: true`, explicitly relaxing strict
on-policy collection). Neither is required before K1-4 and neither starts without a new
learning-semantics decision.

- **Safe (no semantics):** PhysX config — prune the all-ffff collision matrix, raise
  `m_SleepThreshold` off 0.005, fix `m_WorldBounds` ±250 vs 400 arena spacing.
  Semantics-adjacent: re-verify determinism pins.
- **Schema-riders (free only because the K1 retrain is from-scratch):**
  - *Decision interval* — direction non-obvious: lowering trades sim-ticks-per-sample
    against round-trip count; measure in decisions/s, expect the MDP to shift.
  - *Scout trim* — Scout scans remain ~8.5% with AI command, but this is per-step semantics
    and below the bench attribution floor. Observation trim is CLOSED by Stage 1.

**Sequencing / coordination.** heat-gate #248 (`53368b6a`) cleared the pre-launch
tripwire, and Stages 0–2 completed on the exact K tree above. The pre-K1-4 fork is closed:
Stage 1 rules out obs trim as a throughput lever, so it does not block the locked scripted-
roster K1-4 retrain. Baseline note: pre-#248 numbers were taken against
overheating teachers; the fix raises sustained teacher fire rate (~0.7 → ~1.2 shots/s),
so projectile/physics load shifts. Stage 0 now measures only the frozen scratch-vs-scratch
M/K pair above; legacy absolute rates are context, not gates.

## Coordination

- **MPC rip-out PR-2 (#204) and PR-3 (#206) have both merged** — Phase 3's precondition is
  clear, and its first act is re-measuring, not retuning: #206 deleted the tactical/goal-mode
  costs that Phase 2's `samples 512→128` result was taken against. Phase 1b may still contend
  over ship prefabs.
- Self-play is not part of the K1-4 throughput gate. Re-measure it only when a future arc
  actually selects a ghost-league composition; the accepted Stage-0 baseline and Stage-1
  profile deliberately use K1-4's scripted roster and `ppo_ship_combat.yaml`.
