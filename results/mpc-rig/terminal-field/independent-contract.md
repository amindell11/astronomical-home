# Independent terminal-field PR-1 acceptance contract

Approved 2026-09-09 in the independent reimplementation session. This supersedes the supplied brief only on weight tuning and the visual scenario. Implementation starts from main without inspecting PR #548, its branch, or its artifacts. The supplied scope and required shared ledger contained prior-build observations; those are not independent evidence.

Amendment explicitly approved by the user on 2026-09-09, before revised final validation: for **dummy-closeout only**, movement acceptance uses accumulated absolute ring error after the existing two-second warmup, `∫|range − 6 m| dt`, with median regression at most `max(10% of baseline, 1 m·s)`. Keep the original episode-average error in reports. Dummy success, closeout time and collisions, all other rows' metrics, and every other threshold remain unchanged. The original average penalized shorter successful episodes in development; the paired evidence and proposed change are preserved in `independent/revision-boundary-weight3/dummy-metric-diagnostic.md`. Historical failures under the original metric remain failures under that original policy.

## What is fixed?

Implement the supplied PR-1 field, scanner extension, solver integration, diagnostics, native gizmo and paired capture. Horizon 1.7 s. No RL training. One terminal-field weight may be tuned using development experiments, then frozen for validation and the committed asset. Sentences, scenario durations, and acceptance thresholds do not change to rescue a result.

## What must pass?

- Minefield transit: final range <20 m in the existing 25 s scenario on >=18/20 seeds in EACH of three fresh-process runs.
- Paired benefit: >=4 more successful seeds out of 20 than terminal-field-off in EACH run (turn-away enabled in both).
- Collision safety: no increase in collision-bearing episodes or total collision steps versus field off. Threat steps reported separately.
- Existing tactics: all six session rows at density 2.0, 15 seeds and three fresh-process repeats. Per row and repeat: at most one fewer successful episode out of 15; movement-error and closeout-time medians worsen by at most 10%, with 1 m / 1 s absolute tolerance. Define each row's metric before running.
- Dense fleeing chase: report target-gap reduction, stalls, collisions, and distance travelled per metre of closing progress. Catches are not an acceptance criterion. Non-closing episodes remain in the report.
- Correctness: independent shortest-path oracle, declared float tolerance for empty-grid excess, occupied/disconnected sampling, diagonal corners, rock-centred goals, edges, cadence/reset, FIELD weighting, terminal-vs-breakdown agreement. No-POS/disabled-cost controls preserve baseline behavior; scanner's existing nearest-first trace stays unchanged.
- Performance: 32/48/64 resolution x density 2.0/2.5; warmed bake median/p95, gathering, allocations and total solver-loop overhead measured separately. No steady-state allocations after buffers stabilize. Timing is measurement-only.
- Visual proof: same-seed dense fleeing-chase clips with field on/off, native field gizmos, inspected middle and late stills, and closing/stall/collision/route metrics. This replaces the stationary-Dummy capture and its 10 m / 30 s arrival requirement.

## How are runs selected and reported?

Transit validation seeds: 1234, 7, 99, 2001, 2002, and 3101 through 3115. Session seeds: 3201 through 3215. Reserved evaluation seeds 1001-1020 are not used.

Run four cloned-settings arms: both shaping costs on, terminal field off, turn-away off, both off. Collision penalty stays enabled. Development uses existing five transit seeds plus separate development terrain seeds. Freeze settings before final validation. Publish every attempt, including failed runs and infrastructure interruptions; no retry-until-green. Minimize and diagnose observed failures with controlled experiments. Persist artifacts outside the pool slot.

Approved addition 2026-09-09: fast collision-free traversal alongside pursuit, with30s maximum-speed request, zero swept collisions and at least10% median goalward-speed improvement. Full frozen scenario and seed split: independent/fast-traversal/contract.md and issue461 comment5614047294. Historical criteria above remain unchanged.
