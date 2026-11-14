# Harness lane launcher

The Python side of the harness lanes. The trainer, its configs and the eval gate
were removed with RL (#853); the `rl-seed` tag marks the last commit that has them.
What stays launches the matrix lane through the unity-access coordinator:

- `eval_lane.py` composes the lane's environment, runs the batch child and reads
  the summary back.
- `harness_child.ps1` is that batch child: it carries the environment into
  `-executeMethod RL.Hosts.TrainingBootstrap.RunHarness`.
- `unity_access.py` is the coordinator client; `driver_common.py` finds the Unity exe.

## The `RL_HARNESS_*` grammar

The `RL_HARNESS_*` family is the harness-run grammar, parsed once by `HarnessSpec` (C#)
at the batch boundary. The matrix lane is the only lane left. A retired `RL_EVAL_*`
name throws there, naming its replacement, and a variable of a removed lane
(`RL_HARNESS_ONNX`, `_OPPONENT`, `_DENSITY`, `_BUNDLE`, `_SENTENCE`, `_DUEL`) throws as removed:

```powershell
# One matrix mode is required:
$env:RL_HARNESS_MATRIX = "Lasers,Missiles"   # weapon-pair mode: "all" or catalog weapon names; every unordered pair, a weapon against itself included, is one block
$env:RL_HARNESS_MATRIX_REFERENCE = "Crimson:KiterPilot:Lasers"   # roster mode: the fixed side, Chassis:Pilot:Weapon[:Shield]
$env:RL_HARNESS_MATRIX_ROSTER = "Vanguard:AggressorPilot:Lasers,Nightshade:OrbiterPilot:TurboLasers"   # roster mode: one block per side against the reference
$env:RL_HARNESS_MATRIX_KITS = "off"   # roster mode only: both commanders hold the keystone off

$env:RL_HARNESS_LANE = "matrix"   # optional; any removed lane name (eval, capture, sentence, duel) throws
$env:RL_HARNESS_SEEDS = "3001,3002"   # omit (or "held-out") for 1001-1020, "train" for seed 1, or a comma list
$env:RL_HARNESS_EPISODES_PER_SEED = "4"   # omit for 5; odd episodes replay the previous pose with sides swapped
$env:RL_HARNESS_PROBES = "matrix"   # comma-separated probe tokens writing per-probe sidecars; "" for none; omit for "matrix"
$env:RL_HARNESS_RECORD = "all"   # omit/"" = off; "all" or comma indices (0-based, < episodes/seed) select which episodes film. Recording forces a graphics device — the batch child drops -nographics
$env:RL_HARNESS_RECORD_SIZE = "960x540"   # clip WxH, positive + even (yuv420p); omit for 960x540
$env:RL_HARNESS_RECORD_EVERY = "5"   # capture cadence in fixed steps; omit for 5
$env:RL_HARNESS_GIZMOS = "combat"   # native gizmo capture profile filmed through the Game View: steering / combat / everything. Needs RL_HARNESS_RECORD; films collider silhouettes without presentation meshes. Omit to film plain gameplay with presentation on
$env:RL_HARNESS_OUT_DIR = "..."   # caller-owned artifact dir; omit for results/rl-eval/
```

Each run writes the episode JSONL, the matrix probe's `-matrix.jsonl` rows and
its `-matrix-probe.json` sidecar with a markdown table beside it, and the lane's
`-summary.json`. With `RL_HARNESS_RECORD` set, clips land beside the JSONL:
`frames/<jsonl-stem>-s<seed>-<block>-ep<NN>/`.

The shared `harness_child.ps1` launcher stamps `RL_HARNESS_CAPTURE_COMMIT` from
the selected project's Git HEAD before starting a recording run. Direct callers
of `HarnessSpec.Parse` must supply that provenance when recording is enabled.
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
python eval_lane.py --matrix Lasers,Missiles --seeds 3001,3002 --episodes-per-seed 4
python eval_lane.py --matrix-reference Crimson:KiterPilot:Lasers --matrix-roster Vanguard:AggressorPilot:Lasers --seeds 3001
```

Point `--project` at a free pool slot; values pass through as strings —
`HarnessSpec` is the single grammar authority. The launcher does not pass
`RL_HARNESS_RECORD`; a filmed run sets the environment by hand and runs
`harness_child.ps1` through the coordinator.

The seed selection tags the run's artifacts (`matrix-train` / `matrix-held-out` /
`matrix-custom`, with `matrix-kitsoff-` for a kits-off roster).
