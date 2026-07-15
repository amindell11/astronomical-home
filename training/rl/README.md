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

```powershell
cd training/rl
uv venv                      # picks up .python-version (3.10.12)
uv pip install -r requirements.txt
uv pip freeze > requirements.lock.txt   # capture the resolved lock at setup
.venv\Scripts\mlagents-learn --help     # sanity
```

Without uv: install CPython 3.10.12, `py -3.10 -m venv .venv`, activate, then
`pip install -r requirements.txt`.

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
   (`rl-episode-v2` schema) under `results/rl-episodes/`.

Resume with `--resume`, force a fresh run with `--force`.

### Headless / batch-mode attach (trainer smoke)

An editor boot outlasts the trainer's 60 s handshake window, so the batch flow
arms first and enters play on a flag file:

1. `Unity.exe -projectPath src/Asteroids3D -batchmode -nographics
   -executeMethod Game.RLHarness.TrainingBootstrap.EnterTrainingPlayModeWhenSignaled
   -logFile <log>` (no `-quit`), and wait for `[TrainingBootstrap] armed` in the log.
2. Start `mlagents-learn ppo_ship_combat_smoke.yaml --force` and wait for
   `Listening on port 5004`.
3. Create `results/rl-training/start-play.flag` — the editor enters play and
   connects. The smoke config completes by itself (max_steps 4000) and exports
   `results/rl-training/ship_combat_smoke/ShipCombat.onnx`; kill the editor after.

The eval fixture is that exported checkpoint committed (LFS) at
`Assets/Tests/Fixtures/ShipCombat-smoke.onnx`, pinned by
`RLAgentPlayModeTests.InferenceOnly_PinnedCheckpoint_DrivesAFullEpisode`.

## Python-free loop check

`Tests.PlayMode/RLAgentPlayModeTests` runs the identical loop with the
Heuristic policy (inverse-mapped ranger) — no trainer needed. Set the scene's
`TrainingHost.behaviorType` to `HeuristicOnly` to watch the same thing live.

## Eval (held-out seeds)

`ShipAgentFactory.ComposeInferenceOnly(pair, chooser, spec, center, onnxPath)`
pins `BehaviorType.InferenceOnly` + `DeterministicInference = true` (it
defaults false — InferenceOnly alone samples stochastically). Arc-gate
protocol (frozen in `doc/Feature_Plans/RL_MLAgents_Agent.md`): 20 pinned
held-out seeds disjoint from training, Wilson 95% lower bound on win-rate
> 50%, checkpoint selected on training-seed eval BEFORE the held-out set is
opened; any RewardSpec change resets the protocol.

## Characterization floor

`RLEpisodePlayModeTests.Characterization_WritesJsonl` (env `RL_EPISODES=1`,
optional `RL_EPISODE_COUNT`) re-measures the scripted ranger-vs-baseline
floor under the current harness (boost sampling zeroed on the agent's MPC
clone, contract pacing). PR-3's merge floor: win-rate > 5% with fewer
timeouts than the ranger.
