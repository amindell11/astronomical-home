# Independent terminal field: revised candidate

Final validation is running on frozen commit `676e02f6`. Weight 3 was selected from development experiments. The initial candidate remains a failed result in `REPORT.md`; its published videos do not represent this revision.

Implementation was developed independently without inspecting the posted terminal-field PR, its branch, or artifacts. Required shared-ledger reads and the supplied scope exposed prior observations, which are excluded from independent evidence.

## Contract and implementation

The user approved one acceptance amendment before revised validation: dummy-closeout movement uses integrated absolute ring error after the two-second warmup, with median regression limited to max(10%, 1 m·s). Original mean error remains reported. Success, closeout, collisions, other rows, and transit thresholds remain unchanged. See `../independent-contract.md`.

The revised implementation retains the field's grid placement while rollout reach and goal padding fit; seeds a goal region when POS specifies a radius; and extends reachable excess into occupied boundary cells instead of interpolating the global disconnected penalty into clear space. Disconnected free cells retain their penalty. Collision costs remain enabled. The independent oracles and controlled red/green experiments are recorded in `revision-plan.md`.

Weight 3, horizon 1.7 s, resolution 48, minimum spacing 4 m, bake interval 0.4 s. Settings asset SHA256 after rebase line-ending normalization: `A3411E06D3BFBEC484503DFB0C91E0F3B6F40FD9B01E89175C899AFAF8058984`. See `revised-validation-freeze.md` for the verified byte-level equivalence to the development asset.

## Development evidence

All six development rows pass the amended policy at weight 3. These use three development seeds per row and are not final acceptance evidence. Dummy-closeout median integrated error is 316.45 versus 532.68 m·s, closeout 10.84 versus 16.58 s, successes 3/3 in both arms, and collision steps 0 versus 17. Its original mean error is 19.23 versus 15.60 m and fails the old policy.

The repeated instrumented dummy run in `revised-performance/sessions-development-20260910-004013-986.jsonl` reproduces those outcomes. Recorded integrals agree with mean × sample count × 0.02 s to within 0.00287 m·s over all 12 records.

Pre-integration performance covers 12 combinations: resolution 32/48/64, density 2/2.5, and point/18 m region goals. All measured steady-state managed allocations are zero. At resolution 48, median scheduled bake cost is 0.894–0.996 ms for point goals and 1.247–1.453 ms for regions. Whole-solver mean overhead is approximately 33–48%. Timing is descriptive, not an acceptance threshold. Raw CSVs are in `revised-performance`.

## Integration and validation provenance

The candidate was rebased onto main `260877fb`, incorporating unrelated PRs #550 and #552. Integrated commit before quality review: `060475923682ebf7387169cc0447c7da04831f34`. After clearing ScriptAssemblies, Bee and BurstCache, run `20260909-174828` passed 136 MPC tests with 16 opt-in skips and no failures. ReSharper passed with no blocking changed-line findings; six existing touched-file findings remain report-only. Artifacts are preserved in `integration-main-260877fb`.

The combined quality review corrected five test namespaces, indentation, and assertion wording; it made no runtime changes. Post-review run `20260909-175341` again passed 136 tests, with 16 opt-in skips. Final freeze is commit `676e02f6efb5b8f1ebd6ad1f1aee053c74fde518`. Final runs use three fresh processes for each contractual matrix. Transit uses the transparently reused known seeds; session seeds 3201–3215 remain unused at this checkpoint. Fresh solver seeds 3701–3715 are an additional diagnostic, not replacement acceptance seeds. No revised validation-driven tuning is permitted.

## Remote footage

Revised warm-capture clips are published and verified: [field on](https://github.com/amindell11/astronomical-home/releases/download/codex/terminal-field-evidence-20260909/20260909-182124-terminal-field-dense-chase-on.mp4), [field off](https://github.com/amindell11/astronomical-home/releases/download/codex/terminal-field-evidence-20260909/20260909-181842-terminal-field-dense-chase-off.mp4). Both use seed 3301, density 2, 40 simulated seconds, 400 frames at 10 fps, and native Steering gizmos scoped to the pursuer. Middle and late frames were inspected in both clips; field cells, rock outlines, goal ring, selected endpoint and changing costs are visible. Some native labels overlap.

| Metric | Field on | Field off |
|---|---:|---:|
| Final gap | 9.94 m | 25.87 m |
| Closing progress from 50 m | 40.06 m | 24.13 m |
| Pursuer path | 431.19 m | 464.71 m |
| Path per metre of closing | 10.76 | 19.26 |
| Collision-bearing steps | 7 | 13 |
| Stalled 2-second windows | 9/20 | 8/20 |

These are descriptive one-seed results, not broad chase acceptance. Stalls mean <=0.5 m gap reduction per nonoverlapping 2 s window and include retreat. Both traces have 2,000 samples and preserve the 0.02 s capture interval throughout.

Final matching warm pair: `20260909-182124-terminal-field-dense-chase-on.mp4` and `20260909-181842-terminal-field-dense-chase-off.mp4`, under `revised-capture/capture/frames`. The earlier cold on clip is retained but excluded from this pair; it ended at 15.52 m gap with four contact steps. The first cold off attempt failed before recording on an unrelated Package Manager access-token error. The routed wrapper initially rejected a fully qualified regex filter; the supported literal class filter selected exactly one test. Neither recovery changed source or test error policy. Both final warm capture tests passed. The task-owned editor was closed, capture state restored, and coordinator verified empty.

## Frozen transit results

All three fresh-process repeats pass: 20/20 arrivals with the field versus 16/20 off, and zero collision-bearing episodes or steps in both arms. The required gain is four arrivals and the observed gain is four in each repeat. Baseline failures are 1234, 2001, 3104, and 3115; the field has no failed seeds.

Runs: `20260909-175602`, `20260909-175654`, `20260909-175747`. Raw traces, settings, and machine-readable comparisons are preserved in `revised-validation`. These are three process repeats over the same known seed set, not 60 independently sampled terrains. Session validation remains pending.

## Session infrastructure interruption

The first session attempt, run `20260909-180440`, was intentionally stopped after 42 records because its default 1,800-second wrapper timeout was too short at the measured full-matrix rate. All records and logs remain in `revised-validation/interrupted-timeout-budget`; this is not a completed validation verdict. Orbit arms 0/1 each completed all 15 seeds and passed, while arm 2 was partial. The replacement complete run uses a 7,200-second wrapper allowance. Source, settings, seeds, simulation pacing, metrics and thresholds are unchanged; no outcome-based tuning occurred.

## Interim session acceptance failure

In the replacement first matrix (`sessions-validation-20260910-012459-215.jsonl`), completed kite arms 0/1 each contain all 15 seeds. Kite collision steps are 164 with the field versus 156 off, failing the strict no-increase requirement. Collision-bearing episodes are 10 versus 13; success and movement comparisons pass their allowed tolerances. Completed orbit and cover comparisons pass. The full matrix and remaining repeats are still in progress; no retuning is allowed against these results. The candidate is not accepted.

First complete session matrix finished all360 episodes in2251.754 seconds (run20260909-182429). Independent count verification confirms six rows, four arms, fifteen seeds each. Collision-step acceptance fails for kite164vs156, dummy-closeout17vs11, and drift-hold517vs238; dummy also fails collision-bearing episode non-increase. All movement/success tolerances pass, including the approved dummy integrated-error policy. Orbit, cover, and fire-lane pass all comparisons. Session repeats2 and3 are now queued sequentially on the unchanged frozen candidate; it remains not accepted.

Second complete session repeat (run20260909-210124, JSONL040142-550) contains360 verified episodes and also fails acceptance. Kite collision steps272vs246 and drift526vs520 regress. Dummy and fire-lane fail collision-bearing episode non-increase despite fewer total contact steps. Cover fails movement median tolerance; orbit passes. These fresh-process outcomes differ from repeat1; all are retained with no retuning. Third repeat JSONL044142-374 is still running, over300/360 episodes at22:10 local.

Third matrix (run20260909-214124) completed360 episodes and failed before the user challenged continuing full repeats after two acceptance failures. No Unity remains running. Continuing the broad matrix was an overly rigid reading of the predeclared repeat plan; acceptance was already disproven. Further work should target diagnosis, not additional broad validation.
