# Chase Nav B1 Benchmark

This benchmark lives in `ChaseBenchmarkPlayModeTests`.

Run the short deterministic smoke benchmark:

```powershell
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter "ChaseBenchmarkPlayModeTests.ChaseVsEvade_ShortBenchmark_IsRepeatable" -OutDir "results/unity-tests-agent"
```

Run the longer variant set manually:

```powershell
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter "ChaseBenchmarkPlayModeTests.ChaseVsEvade_LongBenchmark_LogsVariants" -OutDir "results/unity-tests-agent"
```

The long method is tagged `Slow`. JSONL rows are written to:

```text
results/unity-tests-agent/chase-benchmark/chase-benchmark-baseline.jsonl
```

Compare two result sets:

```powershell
.\scripts\compare_chase_benchmark.ps1 -Baseline results\unity-tests-agent\chase-benchmark\baseline.jsonl -Candidate results\unity-tests-agent\chase-benchmark\candidate.jsonl
```

Metrics captured per run:

- intercept time, final/mean/min separation
- pursuer and evader mean speed
- collision counts and total collision impulse
- pursuer MPC solve time
- pursuer control chatter
- `gapsThreaded`, currently `-1` until Track A exposes gap telemetry

## Baseline: `origin/main` on 2026-07-05

Command:

```powershell
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter "ChaseBenchmarkPlayModeTests.ChaseVsEvade_LongBenchmark_LogsVariants" -OutDir "results/unity-tests-agent"
```

| scenario | seed | intercept s | final sep | mean sep | min sep | pursuer speed | evader speed | pursuer collisions | evader collisions | pursuer solve ms | chatter/sec |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| offset-cross | 12345 | 3.44 | 6.13 | 9.05 | 1.84 | 7.03 | 4.22 | 3 | 5 | 0.635 | 18.30 |
| wide-lateral | 22345 | -1.00 | 17.99 | 19.78 | 16.85 | 4.22 | 2.14 | 4 | 1 | 0.523 | 23.76 |
| near-cluster | 32345 | -1.00 | 13.56 | 16.88 | 13.46 | 4.55 | 2.85 | 0 | 0 | 0.513 | 25.92 |

The short smoke test runs the same seed twice and asserts broad repeatability
for mean separation, pursuer speed, and pursuer collision count. It is not
bit-identical: current MPC/physics still produce small run-to-run variance, so
candidate comparisons should use row means/spread rather than exact equality.
