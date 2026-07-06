# Chase Benchmark (Track B1) — Usage & Baseline

**Parent plan:** `Chase_Nav_Track_B_Field_And_Eval.md` (PR B1).
**Scenario:** two-AI chase in the dense deterministic field (`BigFieldSettings`) — a
pursuer (MaintainRange, tight enemy-anchored band, `ChasePursue` profile) chasing an
evader (`Flee`, `ChaseFlee` profile). One scenario, two entry points.

## Watchable entry (in-editor)

Set `ChaseBenchmarkSector` (Prefabs/Sectors) as the `MainGameManager` sector entry in
InitScene and press play. Serialized defaults on `ChaseBenchmarkModule` apply: endless
episode, authored field seed, chunk streaming anchored to the evader.

## Headless entry (benchmark)

```powershell
# from the worktree root; always clear Burst cache first
Remove-Item -Recurse -Force src/Asteroids3D/Library/BurstCache -ErrorAction SilentlyContinue
$env:CHASE_BENCH = "1"
$env:CHASE_BENCH_TAG = "my-candidate"        # row tag + output file name
$env:CHASE_BENCH_SEEDS = "5"                 # field seeds (101 + i*17)
$env:CHASE_BENCH_OFFSETS = "2"               # field start offsets per seed
$env:CHASE_BENCH_DURATION = "40"             # sim seconds per episode
./scripts/unity_test_agent.ps1 -Mode PlayMode -TestCategory ChaseBenchmark
```

Output: `results/chase-benchmark/<timestamp>-<tag>.jsonl`, one row per episode
(schema `chase-bench-v1`). Without `CHASE_BENCH` set, only the short smoke episode runs
(it is part of the default Workspace suite and keeps the harness verified).

Aggregate / compare:

```
python scripts/chase-benchmark/compare_chase_benchmark.py <baseline.jsonl> [candidate.jsonl]
```

## Metric model

Both ships run the MPC-under-test, so **headline metrics are per-ship** (robust to the
shared-solver confound): collision count, total impact impulse, mean sustained speed,
control chatter (mean |du|/axis/s), solver ms. **Chase-geometry metrics (mean/min/final
distance, contact time) are secondary context** — confounded because the evader changes
too. Runs are statistical (mean ± std over N seeds), not replay-deterministic: the
sampler is frameCount-seeded in shipped play. `SolverBuffers.SamplerSeedOverride` (the
B1 seed hook) pins the sampler for reproducible single debug runs only.

Episodes never end early: a `ChaseMetricsProbe` on each ship resets damage every tick
(impacts still recorded via `OnCollisionEnter` impulse), so lethal collisions do not
truncate the sample.

## Baseline — current main (pre Track A/B changes)

Two independent repeats at identical config (5 field seeds x 2 field offsets,
40 s sim per episode, 10 episodes each; sampler unpinned — statistical model).
Committed JSONL: `doc/Feature_Plans/chase-benchmark-baselines/` (runtime output
directory `results/chase-benchmark/` is gitignored; promote result sets worth
keeping into the baselines folder). All headline means agree within ~1 std
between repeats — the distribution is stable run-to-run.

| metric (mean ± std over 10 eps)   | baseline A          | baseline B          |
|-----------------------------------|---------------------|---------------------|
| pursuer collisions/ep             | 8.60 ± 4.62         | 9.20 ± 4.26         |
| pursuer impact impulse            | 17256 ± 5824        | 15925 ± 8969        |
| pursuer mean speed                | 7.71 ± 2.01         | 8.28 ± 1.50         |
| pursuer chatter thrust /s         | 4.39 ± 0.86         | 4.00 ± 0.43         |
| pursuer chatter strafe /s         | 6.87 ± 0.43         | 6.81 ± 0.27         |
| pursuer chatter yaw /s            | 9.21 ± 0.63         | 8.97 ± 0.33         |
| pursuer solve ms (mean)           | 0.456 ± 0.026       | 0.457 ± 0.029       |
| evader collisions/ep              | 13.80 ± 9.40        | 15.00 ± 7.27        |
| evader impact impulse             | 28124 ± 19883       | 35754 ± 7951        |
| evader mean speed                 | 7.00 ± 1.75         | 7.38 ± 1.51         |
| evader chatter thrust /s          | 4.89 ± 0.80         | 4.54 ± 0.30         |
| evader chatter strafe /s          | 6.83 ± 0.32         | 6.75 ± 0.17         |
| evader chatter yaw /s             | 9.68 ± 0.62         | 9.53 ± 0.37         |
| evader solve ms (mean)            | 0.466 ± 0.027       | 0.462 ± 0.027       |
| chase mean distance (secondary)   | 10.59 ± 7.89        | 8.17 ± 1.62         |
| chase min distance (secondary)    | 2.65 ± 2.33         | 1.42 ± 0.26         |
| chase final distance (secondary)  | 9.59 ± 3.91         | 8.47 ± 4.35         |
| chase contact time s (secondary)  | 35.02 ± 9.88        | 37.48 ± 3.21        |

Reading: ~9 pursuer / ~14 evader rock collisions per 40 s episode and high strafe/yaw
chatter are exactly the trade-study failure modes (topology-blind reactive avoidance,
effectively-disabled obstacle cost). These rows are the reference for every later
Track A/B PR.
