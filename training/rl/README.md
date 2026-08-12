# RL training home

Python side of the ML-Agents loop. Unity side: `Assets/Scenes/RLTraining.unity`
hosting `TrainingHost` (`Assets/Scripts/RLHarness/Hosts/`), which composes the
episode pair per arena and drives runner-owned Academy stepping.

Every driver here is an **asserting runner**: it launches what it needs, waits on
log markers, and fails loudly if the run did not produce what it promised. Shared
plumbing (Unity discovery, marker constants, log polling, trainer-config reads)
lives in `driver_common.py`; the unity-access coordinator protocol lives in
`unity_access.py` and nowhere else.

## Version pins (do not drift)

| Piece | Version | Why |
| --- | --- | --- |
| Unity package `com.unity.ml-agents` | 4.0.3 (release_23) | project manifest |
| Python | **3.10.12 exactly** | release-pair constraint `>=3.10.1,<=3.10.12` |
| `mlagents` (PyPI) | 1.1.0 | pairing named by the 4.0.3 package Installation.md |
| trainer γ | 0.99 | must equal `RewardSpec.Default.gamma`; pinned by `RLTrainerConfigEditModeTests` |
| `engine_settings` | `time_scale: 1`, `capture_frame_rate: 50` | pacing contract: one rendered frame ≙ one 50 Hz fixed step; `TrainingHost` fails loud otherwise |

## Environment setup (uv)

Installs come FROM the lock (`requirements.lock.txt`, a `pip freeze` of the
working env); `requirements.txt` is the human-readable intent behind it.

```powershell
cd training/rl
uv venv                      # picks up .python-version (3.10.12)
uv pip install -r requirements.lock.txt
.venv\Scripts\mlagents-learn --help     # sanity
```

Without uv: install CPython 3.10.12, `py -3.10 -m venv .venv`, activate, then
`pip install -r requirements.lock.txt`.

To (re)resolve intentionally: `uv pip install -r requirements.txt`, verify a
smoke run passes (below), then `uv pip freeze > requirements.lock.txt` and
commit both files together.

## Which driver

| Driver | Boots | Use it for |
| --- | --- | --- |
| `run_smoke.py` | one batch editor, via unity-access | liveness: does the whole loop still run end to end |
| `run_training.py` | one batch editor, via unity-access | a single-env run (pilot or full) against a real config |
| `run_parallel.py` | N headless player exes, no editor | every real training run — the throughput path |
| `bench_throughput.py` | wraps `run_parallel.py` | steps/s and CPU cost of a configuration |
| `eval_lane.py` | one batch child, via unity-access | a manual scripted eval of one checkpoint |
| `eval_gate.py` | one batch child per checkpoint, via unity-access | scoring checkpoints while a run is still going |

**Editor-booting drivers go through the coordinator.** `run_smoke.py`,
`run_training.py`, `eval_lane.py`, and `eval_gate.py` all launch Unity through
`scripts/unity_access.ps1` (`unity_access.py` is the client), so the editor is
owner-tracked from birth and its startup serializes through the machine-wide
boot lane. Never launch Unity beside the coordinator — see `skills/unity-access`.
`run_parallel.py` is the deliberate exception: headless player exes touch neither
the shared editor nor MCP, so it launches them directly.

## Trainer smoke (start here)

```powershell
cd training/rl
.venv\Scripts\python run_smoke.py
.venv\Scripts\python run_smoke.py --self-play --num-arenas 2   # mirror composition, fanned out
```

`run_smoke.py` boots a batch-mode editor armed by
`TrainingBootstrap.EnterTrainingPlayModeWhenSignaled` (an editor boot outlasts the
trainer's 60 s handshake window, so it enters play on
`results/rl-training/start-play.flag`), runs the smoke config with `RL_SMOKE=1`
(tight-arena/short-clock spec so both end kinds occur), then **fails unless** the
trainer exited 0, `ShipCombat.onnx` was exported, the editor log carries
`[PacingContract] holds`, at least one terminal AND one truncation episode ran,
and every requested arena produced episodes.

`--self-play` swaps in `RL_SELFPLAY=1` and the self-play smoke config: the
opponent becomes a second team-1 `ShipCombat` agent, which is the proof that
native mlagents `self_play` trains two team_ids under automatic Academy stepping.

The eval fixture is that exported checkpoint committed (LFS) at
`Assets/Tests/Fixtures/ShipCombat-smoke.onnx`, pinned by
`RLAgentPlayModeTests.InferenceOnly_PinnedCheckpoint_DrivesAFullEpisode`.

## Parallel workers — the real training path

`run_parallel.py` defaults to the project-owned trainer entry, which emits the
run contract and delegates the training loop to pinned ML-Agents. Pass
`--trainer-runtime ml-agents` for the direct reference entry. Both launch N
headless copies of the `RLTraining` standalone player under one trainer runtime.
Each worker derives an independent run seed from its ML-Agents port
offset against `--harness-base-port` (`TrainingHost.ResolveWorkerIndex`), so the
N copies produce decorrelated experience rather than N identical rollouts.
`--num-arenas M > 1` additionally fans each worker out to M in-process arenas.

```powershell
cd training/rl
.venv\Scripts\python run_parallel.py --num-envs 6                   # full config
.venv\Scripts\python run_parallel.py --self-play --num-envs 6 --hybrid-scripted-workers 2
.venv\Scripts\python run_parallel.py --smoke --num-envs 2 --force   # 2-env liveness gate
```

Build the `--env` exe first with `Game.RLHarness.RLTrainingPlayerBuild.Build`
(headless StandaloneWindows64, lands at `build/rl-training/RLTraining.exe`). That
is a Unity launch like any other, so it runs as a coordinator batch child —
`harness_child.ps1` is the shape to copy: a self-exiting `-batchmode -nographics
-executeMethod ... -logFile <log>` handed to `unity_access.ps1 -Action RunBatch`.

`run_parallel.py` asserts trainer exit 0, an exported checkpoint, and one
non-empty episode JSONL per expected worker/arena. `--base-port` (default 5006)
is passed to both `mlagents-learn --base-port` and the workers'
`--harness-base-port`; the two must match, and **base port 5006 is
single-occupancy across sessions** — a second concurrent run collides
(`UnityWorkerInUseException`) and corrupts both runs' numbers. Worker 0 keeps
today's `runSeed`, so `--num-envs 1 --num-arenas 1` reproduces the single-env
editor run.

The JSONL dir is launcher-owned (`results/rl-episodes/`, passed down as
`--harness-jsonl-dir`). The `-w{k}` / `-w{k}-a{j}` filename suffix is owned by
`TrainingHost.ComposeSuffix` on the C# side; `run_parallel.py` only reads it back,
and `RLDriverContractEditModeTests` pins the two together.

`--initialize-from RUN_ID` warm-starts fresh weights from another run's
checkpoint (self-play graduation seeds from the curriculum winner). mlagents
resolves it as `<results_dir>/RUN_ID/<behavior>/checkpoint.pt`, so an archived run
must be staged under `results/rl-training/` first.

## Single-env run (pilot or full)

`run_training.py` is `run_smoke.py`'s long-run sibling: same armed-batch-editor +
start-flag structure, but it drives a real config, passes `--resume`/`--force`
through, and asserts trainer exit 0 + the pacing marker + the exported ONNX.

```powershell
cd training/rl
.venv\Scripts\python run_training.py --config ppo_ship_combat_pilot.yaml   # ~200k-step pilot
.venv\Scripts\python run_training.py                                       # full run
.venv\Scripts\python run_training.py --resume                              # continue an interrupted run
```

One arena at frame-rate-bound pace makes this a diagnostic path, not the way to
spend a real training budget — use `run_parallel.py` for that.
`ppo_ship_combat.yaml` runs 3.5M steps and keeps `keep_checkpoints: 36` against
its 100k `checkpoint_interval` (35 + final), so checkpoint selection covers the
whole run rather than the tail. `--self-play` and the config's `self_play:` block
must agree — a mismatch is refused before boot, because it would train the wrong
composition while looking healthy.

Checkpoints, `run_manifest.json`, `summaries.jsonl`, and TensorBoard summaries
land under `results/rl-training/<run-id>/` (untracked). The manifest identifies
the run/config; the JSONL stream is the dashboard and throughput-bench source.
`TrainingHost` also appends per-episode JSONL rows (`rl-episode-v6` schema,
`EpisodeResult.SchemaId`) under `results/rl-episodes/`.

## Throughput bench

```powershell
.venv\Scripts\python bench_throughput.py --num-envs 6 --label baseline
.venv\Scripts\python bench_throughput.py --report
```

Runs a short job through `run_parallel.py` with `max_steps` cut to `--steps` and
reports steady-state steps/s plus worker cores and peak RSS. Comparisons are only
meaningful between rows sharing a config, `--steps`, and `--initialize-from`. Its
resolution is coarse and its documented repeatability is contested against
observation — read the docstring before trusting a small delta.

## Python-free loop check

`Tests.PlayMode/RLAgentPlayModeTests` runs the identical loop with the
Heuristic policy (inverse-mapped ranger) — no trainer needed. Set the scene's
`TrainingHost.behaviorType` to `HeuristicOnly` to watch the same thing live.

## Eval (training seeds, then held-out)

`CheckpointEvaluator.Run` executes the frozen protocol
(`doc/Feature_Plans/RL_MLAgents_Agent.md`): the 20 pinned held-out seeds
(`EvalProtocol.HeldOutSeeds`, disjoint from training), W/L/D aggregation with
the Wilson 95% lower bound on win-rate (draws are non-wins; gate: > 50%),
artifacts under `results/rl-eval/`. Checkpoints are selected on training-seed
eval BEFORE the held-out set is opened; any RewardSpec change resets the
protocol.

It runs as a coordinator batch child (`harness_child.ps1`, which carries the
environment into `-executeMethod Game.RLHarness.TrainingBootstrap.RunHarnessSession`).
The `RL_HARNESS_*` family is the session grammar, parsed once by `SessionSpec` (C#)
at the batch boundary — a retired `RL_EVAL_*` name present in the environment
throws there, naming its replacement:

```powershell
$env:RL_HARNESS_ONNX = "results/rl-training/<run-id>/ShipCombat.onnx"   # default: the smoke fixture
$env:RL_HARNESS_EPISODES_PER_SEED = "5"
$env:RL_HARNESS_SEEDS = "train"   # checkpoint selection; omit (or "held-out") for the sealed set, or pass "7,42,99"
$env:RL_HARNESS_DENSITY = "3.0"   # stretch/diagnostic only; omit for the canonical eval env (training's terminal lesson)
$env:RL_HARNESS_OPPONENT = "mirror"   # "roster" (default: stratified archetype blocks) / an archetype name / "mirror" (checkpoint vs itself) / a path ending .onnx (checkpoint vs checkpoint; blocks labeled by its stem)
$env:RL_HARNESS_PROBES = "gate,facing(wFacing=5)"   # comma-separated "name" or "name(key=value,...)" probe tokens writing per-probe sidecars; "" for none; omit for the default "gate,combat" ("velrebase" on the open-loop lane, which accepts only velrebase)
                                                    # registered: gate, combat, contact, facing (wFacing >= 0 scales the measured agent's facing authority; default 1), velrebase
$env:RL_HARNESS_LANE = "capture"   # "eval" (default: scripted/roster W/L/D + summary) / "capture" (film one seed against one opponent block, no summary)
$env:RL_HARNESS_OPENLOOP = "all"   # K1-2 velrebase lane instead of a checkpoint eval: "all" or one of Aggressor/Evader/Orbiter/Kiter, each measured as a paired legacy+anchored block pair; excludes RL_HARNESS_ONNX, RL_HARNESS_OPPONENT, and RL_HARNESS_LANE
$env:RL_HARNESS_RECORD = "all"   # omit/"" = off; "all" or comma indices (0-based, < episodes/seed) select which episodes film. Recording forces a graphics device — the batch child drops -nographics
$env:RL_HARNESS_RECORD_SIZE = "960x540"   # clip WxH, positive + even (yuv420p); omit for 960x540
$env:RL_HARNESS_RECORD_EVERY = "5"   # capture cadence in fixed steps; omit for 5
$env:RL_HARNESS_PAINTERS = "ship-diagnostics,movement-forces"   # comma-separated painter/preset names drawn onto filmed frames; presets expand + dedupe; omit for NONE (nothing drawn). Painters: ship-diagnostics, movement-forces; presets: everything, combat
$env:RL_HARNESS_OUT_DIR = "..."   # caller-owned artifact dir; omit for results/rl-eval/ (capture writes rl-capture/)
```

Recording is orthogonal to the lane: `RL_HARNESS_RECORD` on the eval lane films
a scored eval (the rules-change telemetry instrument); the capture lane runs the
"once" protocol — exactly one seed, one opponent block (`roster` is refused —
five archetype films are five sessions), JSONL rows kept as clip fingerprints,
no summary. Clips land beside their JSONL under `RL_HARNESS_OUT_DIR` (or
`results/rl-capture/`): `frames/<jsonl-stem>-s<seed>-<opponent>-ep<NN>/`.

Setting the environment by hand is the exception; `eval_lane.py` is the lane
launcher — it composes the child env from its arguments alone (stripping every
inherited `RL_HARNESS_*` and retired `RL_EVAL_*` variable first), runs the batch
child through the coordinator, and reads the summary back from the out dir it
named under `results/rl-eval/manual/` in the primary tree:

```powershell
cd training/rl
.venv\Scripts\python eval_lane.py --onnx <ckpt.onnx> --seeds 2001,2002,2003,2004,2005 --episodes-per-seed 3
```

Point `--project` at a free pool slot (like the eval gate); values pass through
as strings — `SessionSpec` is the single grammar authority.

The eval environment defaults to `EvalProtocol.EvalSpec` — asteroid field on at
the curriculum's terminal density, pinned against `ppo_ship_combat.yaml` by
`RLTrainerConfigEditModeTests` so eval cannot silently drift from where
training ends.

The seed selection tags the run's JSONL/summary artifacts (`train` /
`held-out` / `custom`), so training-seed selection runs can never be mistaken
for the sealed held-out eval; a density-overridden run suffixes the tag
(e.g. `train-d3`) and records its env in the summary JSON.

Under the hood `ShipAgentFactory.ComposeInferenceOnly` pins
`BehaviorType.InferenceOnly`, `DeterministicInference = true` (it defaults
false — InferenceOnly alone samples stochastically), `InferenceDevice.Burst`,
and the `EvalProtocol.InferenceSeed` Academy inference seed.

### Automated eval gate (during a run)

`eval_gate.py` watches a live run's checkpoint exports and evals each new one so
erosion shows up while the run is going, not after it. Verdicts follow the
**calibration bundle** (`eval_bundle_v1.json` — seed set, thresholds, replicate
counts, execution mode; every `verdict.json` records the bundle id): one watch
replicate per checkpoint; while armed, a degraded read (Evader ≤ 10/15 or total
< 55/75) triggers confirmation replicates of the same checkpoint and a pooled
re-verdict over the replicate cells with Wilson intervals. **ALERT** is a
CONFIRMED degraded checkpoint, **STOP** two consecutive. The gate is report-only
until a checkpoint first reads healthy (auto-arm), so a fresh run's untrained
checkpoints cannot false-STOP. It composes the two lane libraries: `eval_lane.py`
launches each replicate, `checkpoint_watch.py` owns discovery and replay of
finished `step-<N>/rep-<k>/` dirs (one run dir = one sample); the verdict rules
live in the gate itself.

```powershell
cd training/rl
.venv\Scripts\python eval_gate.py --run-id <run-id> --project ../../../agent-2/src/Asteroids3D
.venv\Scripts\python eval_gate.py --run-id <run-id> --once     # drain the backlog and exit
.venv\Scripts\python -m unittest                                # verdict/stats/bundle/bank unit tests
```

Point `--project` at a free pool slot; the eval boots an editor there. The gate names
each replicate's output dir and passes it as `RL_HARNESS_OUT_DIR`, so it reads back exactly
what it named (artifacts under `results/rl-eval/gate/<run-id>/step-<N>/rep-<k>/`,
`verdict.json` at the step root). It reports and exits 2 on STOP; it kills the trainer
only with the opt-in `--auto-stop-pid <trainer pid>`. Resume convention: after a trainer
`--resume`, restart the gate with `--from-step` set to the last step already judged —
pre-resume checkpoints keep their verdicts and are never re-judged.

### Banking a checkpoint

`eval_bank.py` produces promotion-grade evidence for one candidate: the bundle's
K_bank replicates on the canonical seeds, and with `--incumbent` the paired A/B
reading (exact per-episode McNemar over shared (seed, opponent, episodeIndex)
identities plus the mean difference over replicate totals — the instrument that
reads a 74-vs-67 single-draw gap as noise). `--draw-held-out` is OPT-IN, spends
one draw of the sealed held-out set (interval-reported only, never judged against
the canonical thresholds), and appends to the auditable usage registry
`heldout_draws.jsonl`.

```powershell
.venv\Scripts\python eval_bank.py --candidate <ckpt.onnx> --incumbent <banked.onnx> --project <pool-slot>
```

## Characterization floor

`RLEpisodePlayModeTests.Characterization_WritesJsonl` (env `RL_EPISODES=1`,
optional `RL_EPISODE_COUNT`) re-measures the scripted ranger-vs-baseline
floor under the current harness (boost sampling zeroed on the agent's MPC
clone, contract pacing).
