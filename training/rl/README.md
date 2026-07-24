# RL training home (Tactical-AI PR-3)

Python side of the ML-Agents loop. Unity side: `Assets/Scenes/RLTraining.unity`
hosting `TrainingHost` (`Assets/Scripts/RLHarness/Agent/`), which composes the
PR-2b episode pair and drives runner-owned manual Academy stepping.

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

## Training run

1. Start the trainer (it waits for the editor):

   ```powershell
   cd training/rl
   .venv\Scripts\mlagents-learn ppo_ship_combat.yaml --run-id <run-id>
   ```

2. Open `Assets/Scenes/RLTraining.unity` in the Unity editor and enter play
   mode within 30 s. `TrainingHost` (BehaviorType `Default`) connects, and
   training proceeds at frame-rate-bound speed (the pacing contract trades
   timescale for per-fixed-step Update semantics).

3. Checkpoints + TensorBoard summaries land under `results/rl-training/<run-id>/`
   (untracked). `TrainingHost` also appends per-episode JSONL rows
   (`rl-episode-v4` schema) under `results/rl-episodes/`.

Resume with `--resume`, force a fresh run with `--force`.

### Pilot → full run (asserting runner)

`run_training.py` is `run_smoke.py`'s long-run sibling: same armed-batch-editor +
start-flag structure, but it drives a real config, passes `--resume`/`--force`
through, and asserts trainer exit 0 + the pacing marker + the exported ONNX
before printing where checkpoints and TensorBoard summaries landed.

```powershell
cd training/rl
.venv\Scripts\python run_training.py --config ppo_ship_combat_pilot.yaml   # ~200k-step pilot
.venv\Scripts\python run_training.py                                       # full 2M-step run
.venv\Scripts\python run_training.py --resume                              # continue an interrupted run
```

Run the pilot first: it measures real steps/sec (training is frame-rate-bound
under the pacing contract) and confirms a learning signal at real arena scale
before committing to the 2M wall-clock. `--force` overwrites a run id's
results; `--run-timeout` (seconds) caps the wait, default 48 h.
`ppo_ship_combat.yaml` keeps `keep_checkpoints: 21` (2M steps / 100k interval
= 20 checkpoints + final) so checkpoint selection covers the whole run, not
the tail.

### Parallel workers (`--num-envs`, asserting runner)

`run_parallel.py` is the throughput driver: instead of one editor at one arena
it runs `mlagents-learn --env <player exe> --num-envs N`, launching N headless
copies of the `RLTraining` standalone player under one trainer (build the exe
first — `RLTrainingPlayerBuild`, below). Each worker derives an independent run
seed from its ML-Agents port offset against `--harness-base-port`, so the N
copies produce decorrelated experience rather than N identical rollouts. It does
**not** boot an editor or route through unity-access (headless player exes touch
neither the shared editor nor MCP), and asserts trainer exit 0 + an exported
checkpoint + every worker's `-w{k}` episode JSONL present and non-empty.

```powershell
cd training/rl
# build the player once (headless StandaloneWindows64):
Unity.exe -projectPath ../../src/Asteroids3D -batchmode -nographics `
  -executeMethod Game.RLHarness.RLTrainingPlayerBuild.Build -logFile build.log
.venv\Scripts\python run_parallel.py --num-envs 8               # full config, 8 workers
.venv\Scripts\python run_parallel.py --smoke --num-envs 2 --force   # 2-env liveness gate
```

`--base-port` (default 5006) is passed to both `mlagents-learn --base-port` and
the workers' `--harness-base-port`; the two must match. Worker 0 (`k=0`) keeps
today's `runSeed`, so a `--num-envs 1` run reproduces the single-env editor run.
The JSONL dir is launcher-owned (`results/rl-episodes/`, `-w{k}`-suffixed).

### Trainer smoke (asserting runner)

```powershell
cd training/rl
.venv\Scripts\python run_smoke.py
```

`run_smoke.py` boots a batch-mode editor (armed via
`TrainingBootstrap.EnterTrainingPlayModeWhenSignaled` — an editor boot outlasts
the trainer's 60 s handshake window, so it enters play on
`results/rl-training/start-play.flag`), runs
`mlagents-learn ppo_ship_combat_smoke.yaml --force` with `RL_SMOKE=1`
(tight-arena/short-clock spec so both end kinds occur), then **fails unless**
the trainer exited 0, `ShipCombat.onnx` was exported, the editor log carries
the `[PacingContract] holds` marker, and at least one terminal AND one
truncation episode ran. It boots its own editor — coordinate access first
(`skills/unity-access`).

The eval fixture is that exported checkpoint committed (LFS) at
`Assets/Tests/Fixtures/ShipCombat-smoke.onnx`, pinned by
`RLAgentPlayModeTests.InferenceOnly_PinnedCheckpoint_DrivesAFullEpisode`.

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
protocol. Batch entry:

```powershell
$env:RL_EVAL_ONNX = "results/rl-training/<run-id>/ShipCombat.onnx"   # default: the smoke fixture
$env:RL_EVAL_EPISODES_PER_SEED = "5"
$env:RL_EVAL_SEEDS = "train"   # checkpoint selection; omit (or "held-out") for the sealed set, or pass "7,42,99"
$env:RL_EVAL_DENSITY = "3.0"   # stretch/diagnostic only; omit for the canonical eval env (training's terminal lesson)
Unity.exe -projectPath src/Asteroids3D -batchmode -nographics `
  -executeMethod Game.RLHarness.TrainingBootstrap.RunEval -logFile <log>
```

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

## Characterization floor

`RLEpisodePlayModeTests.Characterization_WritesJsonl` (env `RL_EPISODES=1`,
optional `RL_EPISODE_COUNT`) re-measures the scripted ranger-vs-baseline
floor under the current harness (boost sampling zeroed on the agent's MPC
clone, contract pacing). PR-3's merge floor: win-rate > 5% with fewer
timeouts than the ranger.
