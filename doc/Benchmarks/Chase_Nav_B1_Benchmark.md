# Chase-Nav B1 — Eval Harness

The measuring stick both chase-nav tracks (see
`doc/Feature_Plans/Chase_Nav_Track_A_Solver.md` and
`doc/Feature_Plans/Chase_Nav_Track_B_Field_And_Eval.md`) report against. Every
behaviour-changing PR cites before/after numbers from this harness in its PR body.

## What it measures

A **two-AI scenario** in a deterministic `BigFieldSettings` field: a pursuer
(`MaintainRange`) chasing an evader (`Flee`), both weaving through the dense field.
The utility `Brain` is disabled and the pursuit/flee intents are pushed directly, so
the pairing is fixed and the measurement isolates the **nav / MPC / sensing** stack
that the tracks actually change (not the utility selector).

Because both ships run the MPC-under-test, the **headline metrics are per-ship**
(robust to that shared-MPC confound), aggregated over a small seed sweep as
**mean ± spread**:

| Metric | Meaning |
|---|---|
| `collisions` | asteroid contacts entered during the run |
| `impactImpulse` | Σ collision impulse magnitude (impact-energy proxy) |
| `meanSpeed` | mean plane speed (u/s) — did it keep moving |
| `chatterPerSec` | mean Σ\|Δu\| across thrust/strafe/yaw per second — the thrash detector |
| `meanSolveMs` | mean MPC solve time per tick (editor timing) |

Secondary **relational context** (confounded — the evader changes build-to-build too):
`minDistance`, `meanDistanceBehind`, `timeToInterceptSec` (-1 = never intercepted).

A "seed" in the sweep is a **start-offset × seed-bias** pair: the field is
deterministic and effectively infinite, so flying at a different absolute offset
samples a different obstacle neighbourhood; `SolverBuffers.SeedBias` then varies the
MPC sampler noise deterministically. Evaluation is **statistical** — runs are not
bit-reproducible (the sampler seed also folds in per-tick ship position), so a phase
"wins" when its per-ship distribution beats baseline, not by exact replay.

## Running it

Unity must be **closed** (batch mode refuses if the editor holds the project).

```bash
rm -rf src/Asteroids3D/Library/BurstCache/          # always, before a run
pwsh scripts/unity_test_agent.ps1 -Mode PlayMode -TestCategory ChaseBenchmark
```

Run rows are written as JSONL to `results/chase-benchmark/` (`runs_<stamp>.jsonl`
plus `latest_runs.jsonl`). That dir is git-ignored; the committed reference lives at
`doc/Benchmarks/chase_nav_b1_baseline.jsonl`. Diff a candidate against it:

```bash
pwsh scripts/benchmark_diff.ps1 \
  -Baseline  doc/Benchmarks/chase_nav_b1_baseline.jsonl \
  -Candidate results/chase-benchmark/latest_runs.jsonl
```

### Watch a live chase

`Tools ▸ Chase Benchmark ▸ Live View` builds a throwaway scene and enters Play with
one pursuer/evader pair (same scenario the benchmark drives). No committed scene
asset — authoring one needs the editor open; this needs nothing.

## Baseline (current `main`)

Default sweep — 3 start-offsets, 300 ticks (6 s sim) each — on `main` at the B1 branch
point. Rows in `doc/Benchmarks/chase_nav_b1_baseline.jsonl`; mean ± spread over the 3 runs:

| Metric | Pursuer | Evader |
|---|---|---|
| collisions | 0.00 ± 0.00 | 0.33 ± 0.58 |
| impactImpulse | 0.0 ± 0.0 | 194.5 ± 336.9 |
| meanSpeed (u/s) | 6.23 ± 2.94 | 4.22 ± 1.79 |
| chatterPerSec | 20.70 ± 2.66 | 22.33 ± 2.22 |
| meanSolveMs | 0.61 ± 0.08 | 0.53 ± 0.09 |

Relational (secondary): `minDistance` 10.9 ± 7.1, `meanDistanceBehind` 17.2 ± 5.6,
`interceptRate` 0.00 (no intercept within 6 m in 6 s — the short window is a reference,
not a target).

**Reading the baseline:** chatter ~21–22/s is high because obstacle avoidance is
deliberately neutered on `main` (trade study §2) — this is the thrash level the solver
track (A1–A3) should drive *down*. Collisions are near-zero only because the ships mostly
stay in open space over the short window; longer/denser runs will exercise them more.

Regenerate: run the command above, then
`cp results/chase-benchmark/latest_runs.jsonl doc/Benchmarks/chase_nav_b1_baseline.jsonl`.
