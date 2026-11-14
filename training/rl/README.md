# Harness lane launcher

The Python side of the harness lanes. The trainer, its configs and the eval gate
were removed with RL (#853); the `rl-seed` tag marks the last commit that has them.
What stays launches the scripted lanes through the unity-access coordinator:

- `eval_lane.py` composes a lane's environment, runs the batch child and reads
  the summary back.
- `harness_child.ps1` is that batch child: it carries the environment into
  `-executeMethod RL.Hosts.TrainingBootstrap.RunHarness`.
- `unity_access.py` is the coordinator client; `driver_common.py` finds the Unity exe.

## The `RL_HARNESS_*` grammar

The `RL_HARNESS_*` family is the harness-run grammar, parsed once by `HarnessSpec` (C#)
at the batch boundary — a retired `RL_EVAL_*` name present in the environment
throws there, naming its replacement:

```powershell
$env:RL_HARNESS_ONNX = "results/rl-training/<run-id>/ShipCombat.onnx"   # default: the smoke fixture
$env:RL_HARNESS_EPISODES_PER_SEED = "5"
$env:RL_HARNESS_SEEDS = "train"   # checkpoint selection; omit (or "held-out") for the sealed set, or pass "7,42,99"
$env:RL_HARNESS_DENSITY = "3.0"   # stretch/diagnostic only; omit for the canonical eval env
$env:RL_HARNESS_OPPONENT = "mirror"   # "roster" (default: stratified archetype blocks) / an archetype name / "mirror" (checkpoint vs itself) / a path ending .onnx (checkpoint vs checkpoint; blocks labeled by its stem)
$env:RL_HARNESS_PROBES = "gate,facing(wFacing=5)"   # comma-separated "name" or "name(key=value,...)" probe tokens writing per-probe sidecars; "" for none; omit for the default "gate,combat"
                                                    # registered: gate, combat, contact, facing (wFacing >= 0 scales the measured agent's facing authority; default 1)
$env:RL_HARNESS_LANE = "capture"   # "eval" (default: scripted/roster W/L/D + summary) / "capture" (film one seed against one opponent block, no summary)
$env:RL_HARNESS_RECORD = "all"   # omit/"" = off; "all" or comma indices (0-based, < episodes/seed) select which episodes film. Recording forces a graphics device — the batch child drops -nographics
$env:RL_HARNESS_RECORD_SIZE = "960x540"   # clip WxH, positive + even (yuv420p); omit for 960x540
$env:RL_HARNESS_RECORD_EVERY = "5"   # capture cadence in fixed steps; omit for 5
$env:RL_HARNESS_GIZMOS = "combat"   # native gizmo capture profile filmed through the Game View: steering / combat / everything. Capture lane + RL_HARNESS_RECORD only; films collider silhouettes without presentation meshes. Omit to film plain gameplay with presentation on
$env:RL_HARNESS_OUT_DIR = "..."   # caller-owned artifact dir; omit for results/rl-eval/ (capture writes rl-capture/)
```

Recording is orthogonal to the lane: `RL_HARNESS_RECORD` on the eval lane films
a scored eval (the rules-change telemetry instrument); the capture lane runs the
"once" protocol — exactly one seed, one opponent block (`roster` is refused —
five archetype films are five sessions), JSONL rows kept as clip fingerprints,
no summary. Clips land beside their JSONL under `RL_HARNESS_OUT_DIR` (or
`results/rl-capture/`): `frames/<jsonl-stem>-s<seed>-<opponent>-ep<NN>/`.

The shared `harness_child.ps1` launcher stamps `RL_HARNESS_CAPTURE_COMMIT` from
the selected project's Git HEAD before starting a recording run. Direct callers
of `HarnessSpec.ParseEval` must supply that provenance when recording is enabled.
Each completed clip has exact PNG identities and unscaled/simulation timestamps
in its manifest-named frame metadata. The caller prepares the diagnostic view
and pacing before episode reset; recording starts at the existing `onBegin`
boundary. A failed recording keeps its partial evidence and fails the episode
before a success row is appended.

## Running a lane

Setting the environment by hand is the exception; `eval_lane.py` is the lane
launcher — it composes the child env from its arguments alone (stripping every
inherited `RL_HARNESS_*` and retired `RL_EVAL_*` variable first), runs the batch
child through the coordinator, and reads the summary back from the out dir it
named under `results/rl-eval/manual/` in the primary tree:

```powershell
cd training/rl
python eval_lane.py --seeds 2001,2002,2003,2004,2005 --episodes-per-seed 3
```

Point `--project` at a free pool slot; values pass through as strings —
`HarnessSpec` is the single grammar authority.

The seed selection tags the run's JSONL/summary artifacts (`train` /
`held-out` / `custom`); a density-overridden run suffixes the tag
(e.g. `train-d3`) and records its env in the summary JSON.
