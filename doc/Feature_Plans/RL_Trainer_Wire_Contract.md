# RL Trainer Wire Contract

> STATUS: living — the frozen C#↔Python boundary record the trainer-runtime
> takeover (`RL_Trainer_Runtime_Takeover.md`) builds against; amended only by
> stage rulings, never by implementation riders.

*Slice 1 of the takeover arc. Sources: the arc-opening session's
wire-contract lane report (audited at main `cd7c9898`, 2026-08-05 — cited
line numbers are as of that SHA), the ml-agents usage inventory, and the
operational-substrate sweep. "Frozen" means: a surface listed here changes
only through an explicit amendment attached to a stage ruling; a stage that
cannot meet a frozen surface stops and amends rather than drifting.*

Each surface carries a **disposition** for the committed stages:

- **RETAINED** — provided by pinned upstream code (`mlagents_envs` or
  imported `mlagents.trainers` classes); the owned runtime keeps calling it.
- **OWNED @1a / @1b / @2** — the owned runtime becomes the producer at that
  stage and must reproduce the surface exactly (or re-map it and repoint
  every consumer in the same slice).
- **C# / OURS** — project-owned on the Unity or driver side; the trainer
  swap must leave it byte-identical.
- **STAGE-4/5 AMENDMENT** — untouched by the committed scope; the named
  stage must amend this doc before design.

## 1. Version pins

| Item | Value | Authority |
|---|---|---|
| Unity ML-Agents package | 4.0.3 (`com.unity.ml-agents`) | `Packages/manifest.json` |
| Its inference dep | `com.unity.ai.inference` 2.6.1 | package.json |
| Communicator API version | **"1.5.0"** on BOTH sides | `Academy.cs:104` ⟷ `mlagents_envs/environment.py:68` |
| Python `mlagents` / `mlagents_envs` | 1.1.0 / 1.1.0 | `training/rl/requirements.txt` + lock |
| Python | 3.10.12 (hard ceiling: mlagents requires ≤3.10.12) | `.python-version`, dist-info |
| torch / numpy / protobuf / grpcio / onnx | 2.2.2 / 1.23.5 / 3.20.3 / 1.48.2 / 1.15.0 | requirements.lock.txt |
| torch pin rationale | ≥2.9 routes `torch.onnx.export` through dynamo and breaks mlagents export | requirements.txt:5 |
| Unity fixed timestep | 0.02 s (50 Hz) | `ProjectSettings/TimeManager.asset` |

Disposition: RETAINED. Stage 3 (CUDA torch) and stage 5 relax pins only via
amendment.

## 2. gRPC transport + handshake — RETAINED (`mlagents_envs`)

The Python side of this entire section is provided by pinned
`mlagents_envs`; stages 1a–3 keep it verbatim. Stage 4 (local actors) is the
only stage that may touch it — **stage-4 amendment required**. Recorded so
the retained behavior is inspectable:

- Service `UnityToExternalProto`, single RPC
  `Exchange(UnityMessageProto) → UnityMessageProto`; Unity is the gRPC
  **client**, dialing `localhost:{port}`, insecure channel
  (`RpcCommunicator.cs:232`). Python is the server.
- **Two-call init handshake** (`RpcCommunicator.cs:229-247`): Unity sends
  `Exchange(UnityOutput{RlInitializationOutput}, status 200)`, then a second
  `Exchange(null, 200)`. Reply 1 carries the init input; reply 2 carries the
  first real input. Both replies must carry `Header.Status == 200`.
- Init Unity→Python: `{Name: "AcademySingleton", PackageVersion: "4.0.3",
  CommunicationVersion: "1.5.0", Capabilities}`. Init Python→Unity:
  `{seed, num_areas, package_version, communication_version, capabilities}`
  — **the seed drives `UnityEngine.Random` AND `Academy.InferenceSeed`**
  (`Academy.cs:467-474`); the owned runtime keeps supplying it through the
  retained layer.
- Semantic-version compatibility check on `communication_version`
  (`RpcCommunicator.cs:160-190`) — mismatch refuses the connection.
- Commands: `CommandProto.Quit` → Unity exits (drivers rely on this for
  clean shutdown + exit code); `Reset` → `Academy.ForcedFullReset()`.
  Graceful close = `Exchange(null, status 400)`.

## 3. Port grammar + worker identity

| Fact | Value | Disposition |
|---|---|---|
| Port flag Unity parses | `--mlagents-port <p>` — **case-sensitive exact match** (`Academy.cs:114,366-393`) | C#, byte-identical |
| No-flag fallback | editor 5004 (`MLAgentsSettings` defaults — no settings asset exists in-project); standalone player −1 ⇒ never connects | C# |
| Worker port law | **`port(worker k) = base_port + k`, contiguous from k=0** | producer OWNED @1b (today `SubprocessEnvManager`); must hold exactly |
| Repo base port | 5006 via `--base-port` (`run_parallel.py:89-90`); **single-occupancy across sessions** | OURS |
| Worker-index derivation | C# derives `k = --mlagents-port − --harness-base-port`; missing/negative → **throw** (`TrainingHost.cs:162-178`); k seeds `DeriveWorkerSeed` (worker 0 = identity replay) | C#, byte-identical |
| Explicit index (new) | stage 1b adds `--harness-worker-index k` to per-worker argv; `TrainingHost` keeps the port-arithmetic **as a cross-check throw**, never a silent fallback. AMENDED 2026-08-06 (slice-3 ruling): split — the owned runtime **emits** the flag @1b (Unity ignores unknown argv; port arithmetic stays the sole C# authority); the C# read + cross-check lands as a named follow-up micro-PR outside the arc's quiet-lane cadence. V9 completes when that micro-PR merges. | OWNED @1b (emit); C# half deferred |

⚠ Asymmetry worth keeping frozen: `Academy.ReadPortFromArgs` matches the
flag case-sensitively while `TrainingHost.CommandLineArg` matches
ignore-case. Emit the flag in exactly the canonical casing.

## 4. Behavior / observation / action schema — C#-owned

The arc never changes this schema; it freezes the record the export and any
future identity tests are written against. **Reference = production main,
which since #250's atomic merge (`659861da`, 2026-08-05) carries the K1-3
schema**: the base record below with the K1-3 delta applied. The pre-K1
values stay recorded — they describe every archived checkpoint
(`ShipCombat-699941`, `ship_combat_500k`, …).

### Base record (audited at `cd7c9898`, pre-#250 values)

- Behavior name **`"ShipCombat"`** (`ShipCombatPolicy.cs:6`); YAML
  `behaviors:` key and every curriculum `completion_criteria.behavior` must
  match. Teams: 0 (agent) / 1 (self-play ghost) — ghost brain key on the
  wire is `ShipCombat?team=1`.
- **Two sensors; wire order is ALPHABETICAL BY SENSOR NAME**
  (`SensorUtils.SortSensors`, invariant culture — `ISensor.cs:133-137`):
  - `obs_0` = BufferSensor `"AsteroidBuffer"`: `[batch, 64, 7]`
    (`MaxNumObservables=64`, `ObservableSize=7`, `AgentObservations.cs`).
    Token = `[relPos.x/r, relPos.y/r, dist/r, relVel.x/maxSpeed,
    relVel.y/maxSpeed, radius/4.17, healthPct]`; nearest-N, **no zero-pad,
    no ordering guarantee — absence is the mask**.
  - `obs_1` = auto-named `"VectorSensor_size26"`: `[batch, 26]`. Channels:
    0-1 self ego-vel /maxSpeed · 2-3 speedPct,yawRatePct · 4-5
    health,shield · 6-7 boostAvail,boostCooldownPct · 8 hasTarget · 9-10
    target relPos /arenaRadius · 11 target dist /arenaRadius · 12-13 target
    relVel /maxSpeed · 14-15 target facing (unit) · 16-17 target
    health,shield · 18-19 inMyEnvelope,inEnemyEnvelope · 20-21 arena center
    ego /arenaRadius · 22 primary ready · 23 primary heatPct · 24-25 unit
    dir to intercept point. Channels 9-17 zero-filled when no target.
- Actions: `ActionSpec.MakeContinuous(6)` — `[vx, vy, fire, boost, fx, fy]`
  ∈ [−1,1]; fire/boost trigger = `value > 0`. Two comment-synced decode
  sites (`ShipAgent.OnActionReceived`, `LivePilotAgent.OnActionReceived`).
- Decision pacing: **no `DecisionRequester` anywhere** — explicit
  `RequestDecision()` every `DecisionIntervalSteps = 10` fixed steps;
  Academy auto-steps (`AcademyFixedUpdateStepper`, exec order 0);
  `AICommander` at order +10 gives zero decision latency; `MaxStep = 0`,
  the hosting loop is the single reset owner.
- Episode ends: reward → `EndEpisode()` (terminal, Φ forced 0) vs reward →
  `EpisodeInterrupted()` (truncation, Φ kept so V bootstraps). **The
  runtime must honor done vs max_step_reached distinctly** — PPO
  bootstrapping through truncation is the reason the distinction exists.

### K1-3 delta — PRODUCTION since #250 merged (`659861da`, 2026-08-05)

Obs 26 → **28** (enemy `{ready, heatPct}` at 26-27, target-conditional);
actions → **5 continuous `[ox, oy, vr, vt, vw]` + 2 discrete branches × 2
`[fire, boost]`**; episode JSONL `rl-episode-v5` → `v6`; ONNX gains the
discrete output triplet and live `action_masks` input; sensor
`VectorSensor_size28` still sorts after `"AsteroidBuffer"` — obs index order
is stable across the break.

## 5. Side channels

Framing (RETAINED, `SideChannelManager`): repeated
`[16-byte channel GUID][int32 length][payload]` blobs riding
`UnityRLOutput/InputProto.SideChannel`.

| Channel | GUID owner | Repo usage | Disposition |
|---|---|---|---|
| `EngineConfigurationChannel` | upstream | **load-bearing**: YAML `engine_settings` → `time_scale 1`, `capture_frame_rate 50` (+ 320×240, quality 1, target −1). `PacingContract.AssertHolds` throws at runtime and a per-frame watchdog enforces it — upstream defaults (20/60) VIOLATE it | RETAINED; the owned runtime must keep sending these exact values from stage 1b on |
| `EnvironmentParametersChannel` | upstream | **load-bearing**: 8 keys read via `GetWithDefault`, re-applied every episode (`TrainingHost.cs:82-84`); keys = `use_asteroid_field, field_density_scale, collision_lethality, opponent_weight_{aggressor,evader,orbiter,kiter,dummy}` (`EnvParamOverlay.ParamNames`, pinned vs YAML by `RLTrainerConfigEditModeTests`); weights summing ≤0 throws | RETAINED classes; **invocation timing OWNED @1b** (curriculum canary covers) |
| Sampler subtlety | — | lessons must be sampler-valued where the curriculum is sampler-valued: a constant lesson mixed into a sampler curriculum **breaks the env handshake at boot** (bisect-proven 2026-07-24, documented in `ppo_ship_combat.yaml:72-74`) | frozen behavior |
| `StatsSideChannel` | upstream | registered by C#, **zero repo usage** — runtime must tolerate its presence, nothing more | RETAINED |
| `TrainingAnalyticsSideChannel` | upstream | telemetry only; must not fault on absence | RETAINED |
| Custom side channels | — | **none exist in the repo** (verified) | — |

## 6. Lifecycle grammar (drivers ⟷ runtime ⟷ Unity)

### CLI surface the owned entry must accept — OWNED @1a

`<config.yaml>` positional first, then `--run-id --env --num-envs
--base-port --no-graphics [--resume | --force] [--initialize-from <RUN_ID>]
--env-args …` with `--env-args` **trailing**, forwarded verbatim to every
worker's argv alongside `--mlagents-port`. `--run-id` overrides the YAML's
`checkpoint_settings.run_id`; omitted run-id falls back to the YAML
(`run_smoke.py` relies on this). `--resume` + `--initialize-from` **fails
loud** @1a (stock mlagents silently drops `--resume`; the launcher guard
stops being load-bearing). `--initialize-from` resolves
`<results_dir>/<RUN_ID>/<behavior>/checkpoint.pt` and fails before any
worker boots.

### Environment pass-through — OWNED @1a (inherited spawn), @1b (owned spawn)

`RL_SMOKE`, `RL_SELFPLAY`, `RL_HYBRID_SCRIPTED_WORKERS` reach every worker
process unmodified; the launcher explicitly pops unset ones so inherited
values can't bypass its flag/YAML cross-checks (which stay: `--self-play` ⟷
`self_play:` block mismatch is a hard refusal in either direction).

### Unity-side inputs — C#, byte-identical

`--harness-base-port` (required iff `--mlagents-port` present, else throw),
`--harness-jsonl-dir` (launcher-owned absolute results dir),
`--harness-num-arenas` (invalid/<1 throws), plus the `RL_*` vars above.

### Editor arm-and-wait lane — OURS, byte-identical

boot editor (`-executeMethod
TrainingBootstrap.EnterTrainingPlayModeWhenSignaled`, no `-quit`) → C# logs
**`[TrainingBootstrap] armed`** → driver starts the runtime and waits for
**`Listening on port`** on its stdout (OWNED @1a: stock emits it while the
loop is delegated; @1b the owned scheduler must emit the same substring or
repoint `driver_common.py`/`run_training.py` in the same slice) → driver
touches `results/rl-training/start-play.flag` → editor deletes it and enters
play. Handshake window: `env_settings.timeout_wait: 300` (editor boot +
Burst reload outlast the 60 s default); the runtime survives ~5 min severed
connection and reconnects with zero step loss.

### Process behavior — OWNED @1a/@1b

Exit 0 on success (any non-zero = hard FAIL to every driver); workers are
**children of the runtime process** — Windows `taskkill /F /T /PID <pid>`
must reap the fleet (the gate's `--auto-stop-pid` imports the same killer);
48 h default run timeout, 10 s poll; stdout is a growable text file at
`results/rl-training/<run-id>-parallel-trainer.log` (launcher-owned
redirect; editor lanes use `<run-id>-trainer.log`).

## 7. Results layout, checkpoints, ONNX

### Filesystem contract — RETAINED @1a, publish path OWNED @1b

```
results/rl-training/<run-id>/
  ShipCombat.onnx                 # final export, RUN ROOT (driver success assert)
  configuration.yaml              # resolved-config dump
  run_logs/…                      # training_status.json, timers, player logs
  ShipCombat/                     # behavior dir — literal "ShipCombat"
    ShipCombat-<step>.onnx        # per-interval; stem grammar ^ShipCombat-(\d+)\.onnx$
    ShipCombat-<step>.pt
    checkpoint.pt                 # --resume / --initialize-from target
    events.out.tfevents.*         # behavior dir, not run root
```

`checkpoint_settings.results_dir` resolves **relative to the runtime's CWD**
(launchers set `cwd=training/rl`); the run dir exists from launch (dashboard
attribution window keys on it). Steps are decimal, **monotonic across
`--resume`** (the eval gate's `--from-step` convention depends on it), not
interval-round. @1b additions (per the takeover plan): write-then-rename
publish + a checkpoint-manifest line after fsync; `keep_checkpoints` must
still leave post-run `eval_gate.py --once` backfill viable (the default eval
policy). Archived seed runs (`training/archive/ship_combat_500k/`) stay
initializable or are migrated explicitly. AMENDED 2026-08-06 (slice-3
ruling): the publish sequence is saver-staged interval artifacts
(`<stem>.pt` → `<stem>.onnx`, tmp+fsync+rename each) followed by a
loop-owned **commit tail** ordered manifest line → atomic
`training_status.json` save → `checkpoint.pt` rename (resume pointer commits
LAST); manifest = `<run-dir>/checkpoint_manifest.jsonl`, duplicate-step
lines read last-wins; consumers repointed = `checkpoint_watch.py` (manifest
preferred, glob fallback, yields only existing artifacts) + `eval_gate.py`
(behavior from run manifest, hardcoded fallback). Full rationale:
takeover plan §Slice-3 decision brief.

### ONNX export — RETAINED (ModelSerializer) until any stage touches it

opset **9**; outputs `version_number` (== **3**), `memory_size` (== 0),
`continuous_actions`, `continuous_action_output_shape`,
`deterministic_continuous_actions` (+ discrete triplet and live
`action_masks` input at K1-3); inputs `obs_0…obs_{n-1}` per §4's alphabetical
order; dynamic axis 0 = batch. Ground truth: the committed fixture
`Assets/Tests/Fixtures/ShipCombat-smoke.onnx` (ir 4, producer pytorch
2.2.2). C# validation (`SentisModelParamLoader`): version range [2,3],
constants present, per-sensor input presence + shape (rank-2 check pins the
64×7 buffer), output presence honoring `DeterministicInference` (eval pins
deterministic + Burst + `EvalProtocol.InferenceSeed`), output shape vs
ActionSpec. Checkpoint **stem is the identity key** on both sides of the
eval boundary ("Python keys on the stem" — pinned by
`RLSessionSpecEditModeTests`). Any stage that changes who writes the ONNX
arms the export-identity test (takeover plan §Equivalence gates).

## 8. Stdout + stats consumers

| Surface | Exact shape | Consumer | Disposition |
|---|---|---|---|
| `Listening on port` | substring on runtime stdout | `run_training.py`, `run_smoke.py` (300 s timeout) | OWNED @1b (stock emits through 1a) |
| Summary line | `… Step: N. Time Elapsed: T s. Mean Reward: R. Std of Reward: S. Training.[ ELO: E.]` | `dev/rl-status/server.py` + `bench_throughput.py` (two different regexes; bench hard-FAILs on drift; ELO presence is the dashboard's self-play discriminator) | OWNED @1a re-map: `summaries.jsonl` via an additive `StatsWriter` the owned entry registers in-process around the delegated loop (amended 2026-08-05, stage-1a ruling: wrapper-scoped registration replaces the venv-global `[mlagents.stats_writer]` entry point, which would load the owned writer into `--trainer-runtime ml-agents` reference runs), BOTH consumers repointed in the same slice; stdout line keeps flowing until then |
| `max_steps:` echo | any line, `split("max_steps:")[1]` parses as int | dashboard progress/ETA | folded into the summary stream @1a |
| Lesson line | `Parameter 'X' is in lesson 'Y' and has value 'Z'.` | dashboard curriculum chips | RETAINED through 1a; @1b emit-or-repoint |
| tfevents | behavior dir; tags: the six `plot_progress.py` panels + `Environment/Lesson Number/*` (+ `Self-play/ELO`) | `plot_progress.py` only | RETAINED (stats writers untouched; the 1a plugin is additive) |
| Run manifest (new) | `{runId, behavior, resultsDir, startedAt, maxSteps, mode, configHash}` at launch | gate/dashboard/bench progressively repoint | OWNED @1a — the producer-emitted replacement for V3/V4/V6/V7 |

## 9. Project-owned contracts riding the boundary (trainer swap must not touch)

- **Episode JSONL `rl-episode-v5`** (→ v6 at K1-3): C# writes
  (`EpisodeResult`/`EpisodeJsonl`), path grammar
  `{yyyyMMdd-HHmmss}-{tag}{suffix}.jsonl`; Python checks existence +
  non-emptiness per expected suffix only.
- **`-w{k}-a{j}` suffix pair**: C# `TrainingHost.ComposeSuffix` owns the
  format; `run_parallel.py` constants are pinned by
  `RLDriverContractEditModeTests` reading the Python source — the pin stays
  and extends to the owned launcher. Coupled to §3's contiguous-port law.
- **Eval summary `rl-eval-summary-v2`**, gate artifact tree
  (`step-<N>/rep-<k>/`), calibration bundle, banking artifacts — consume
  checkpoints, not the runtime; unchanged throughout.
- **`RLTrainerConfigEditModeTests`**: globs every `training/rl/*.yaml` and
  pins cross-config hyperparameter identity, γ ↔ `RewardSpec.Default.gamma`,
  pacing engine settings, env-param key set, lesson-0 values, terminal band
  ⊇ eval density, self-play/hybrid shape. The owned runtime keeps the YAML
  schema, so these keep gating; schema changes migrate the invariants.

## 10. Known loose ends (named, not fixed here)

1. Two comment-synced compose sites spell the schema independently
   (`ShipAgentFactory.Compose` ⟷ `InferenceChooser.Compose`); K1-3's brief
   consolidates them — until then, schema edits touch both.
2. `TrainingHost.numArenas` is not scene-serialized (field initializer 1);
   only the parallel lane's `--harness-num-arenas` makes fan-out visible.
3. The §3 case-sensitivity asymmetry.
4. `dev/rl-status/server.py` hardcodes a 3.5M max_steps fallback and
   `plot_progress.py` carries run-1 reward thresholds — stale cosmetics that
   the @1a repoint should clean up in passing, not preserve.

## 11. Amendment protocol

A stage ruling may amend this doc in the same docs-only landing that records
the ruling; the amendment names the surface, old → new, and every consumer
repointed. Stage 4 (local actors — removes the per-decision exchange) and
stage 5 (PPO re-ownership — replaces the export path) **must** amend before
their pr-prep freezes. Implementation PRs never amend; if a build can't meet
a frozen surface, it stops and escalates.
