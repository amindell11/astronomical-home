# Unity Profiler Frame-Drop Investigation

## Board item

`Add Automated Unity Profiling`

## Scope

Capture a repeatable Unity Profiler baseline under representative live combat load, identify the dominant source of frame-time spikes, apply evidence-backed low-risk fixes, and capture the same workload again for a before/after comparison.

## Measurement contract

- Use Unity's native Profiler `.raw` capture as the source of truth.
- Prefer a Development Player over Editor Play Mode for rendering and frame-time conclusions.
- Reuse an existing gameplay/test scenario only to make the workload repeatable.
- Preserve baseline and candidate metadata plus derived frame-time summaries under `results/profiling/`.
- Compare identical scenario, quality, resolution, frame count, and profiler settings.

## Guardrails

- Do not infer hotspots from test duration or `Stopwatch` values alone.
- Do not overlap the active MPC/navigation editor-assembly split unless profiling proves a runtime fix is necessary.
- Keep gameplay behavior, visual quality, and balance unchanged.
- Defer architectural rewrites such as DOTS or broad jobification unless the capture demonstrates they are required.

## Unknowns

- The exact ship count and encounter composition where the user's machine first crosses its frame budget.
- Whether content streaming or spawn-time drops exist independently of the steady-state MPC spikes measured here.

## Stages

1. Select and pin the representative scenario.
2. Capture the baseline in Unity Profiler.
3. Analyze spike frames and establish the root cause.
4. Implement the smallest structural fix that removes the measured cause.
5. Capture the candidate and report before/after results.

## Results

All captures used the Windows Development Player at 1920×1080 with native Unity CPU, GPU, Rendering, Memory, Physics, and UI Profiler modules. Each measured run followed a 300-frame warmup.

The normal CombatSector workload (player plus one active enemy) did not reproduce a drop across 1,200 frames: CPU median 6.860 ms, p95 7.459 ms, p99 8.006 ms, and max 8.759 ms. GPU median was 5.928 ms.

The controlled stress workload added six fully wired enemy ships, for eight active ships total. This reproduced CPU-only drops while GPU time stayed flat:

| Run | CPU median | CPU p95 | CPU p99 | CPU max | CPU mean | GPU median |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline, batch 1 | 7.580 ms | 10.866 ms | 14.297 ms | 57.811 ms | 8.227 ms | 6.042 ms |
| Candidate, batch 8 | 7.614 ms | 10.344 ms | 14.109 ms | 28.339 ms | 8.148 ms | 6.016 ms |
| Experiment, batch 16 | 7.591 ms | 12.177 ms | 13.562 ms | 28.096 ms | 8.687 ms | 5.861 ms |

Spike frames were dominated by `AICommander.FixedUpdate`, `WaitForJobGroupID`, `JobHandle.Complete`, and `EvaluateCandidatesJob`. Every commander synchronously scheduled candidate generation and evaluation with an inner-loop batch count of one. At the configured 512 samples, each eight-ship fixed update created excessive scheduler and fence work before the main thread could continue.

Batch size 8 is the selected fix. It retains 64 independent ranges for worker utilization while reducing scheduling granularity eightfold. Batch size 16 reduced the single worst frame by another 0.243 ms but regressed p95 and mean CPU time, so it was rejected. The selected candidate cut the worst measured CPU frame by 50.98% and p95 by 4.81% without changing gameplay inputs, quality, GPU load, candidate indexing, seeds, or solver results.

Raw captures and generated summaries are retained in the ignored `results/profiling*` directories of the profiling worktree.
