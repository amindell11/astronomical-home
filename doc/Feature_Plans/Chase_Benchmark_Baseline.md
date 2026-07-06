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

<!-- BASELINE TABLE — filled from the committed baseline JSONL; see results/chase-benchmark/ -->

_To be filled by the B1 baseline run (two repeats at identical config to show
distribution stability)._
