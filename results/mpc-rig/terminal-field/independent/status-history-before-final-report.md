# Independent implementation status, 2026-09-09

Worktree: D:/amind/git/agent-2, lease terminal-field-independent, base main 27c356bf. Posted PR branch/code/artifacts were not inspected. Shared ledger and supplied scope contained earlier observations; they are not evidence for this implementation.

## Prepared

Independent field owner/view/Burst raster+Dijkstra, scanner regional query (local commit 43da5e87), Mpc/rollout/diagnostic integration, production settings, native Navigator field gizmo, independent shortest-path oracle tests, cadence/geometry/weighting/resolution/scanner tests, original five-seed field-off witness, seven-weight development sweep, frozen twenty-seed validation runner, four-arm session runner, six-case bake microbenchmark, dense Evader capture scenario with raw range/path/contact/field metrics.

Standalone Unity Roslyn compile succeeded for Game.Core, Game.Core.Editor, Game.RLHarness.Editor, Tests.EditMode and Tests.PlayMode. Two existing unreachable-code warnings in ShipChildComponentStatePlayModeTests. Temporary compiler outputs are under agent-2/results/terminal-field-compile; they do not alter Unity's loaded assemblies and are not Unity test evidence.

## Not yet done

Focused Unity correctness passed 10/10 (20260909-015238; artifacts copied to independent/correctness-20260909-015238). The first development sweep completed (independent/development-20260909-090536-945): weights 0/.01/.03/.1 achieved 3/5, .3/1/3 achieved 4/5. All measured collision and threat steps were zero. The emitter test passing is not acceptance proof. Latest diagnostics and absent-source tests remain untested.

Refined sweep (independent/development-20260909-093325-878) found 0.6 and 2.0 pass all five development seeds. Candidate asset weight is now 0.6, not frozen. A missing Burst test assembly reference caused one compile interruption (023156), then four existing direct-SolverBuffers tests exposed an uninitialized native array for absent fields (023307). Fixed with an owned empty array representation. Full MPC EditMode regression then passed 123/130 with seven opt-in skips (023452), copied to independent/mpc-regression-20260909-023452.

No final validation, performance measurements, captures, quality sub-agent, ReSharper pass, or new PR. Six-row development matrices at 0.6 and 2.0 completed; both regress several movement/closeout metrics. The weight-2 orbit-only comparison passed and reproduced in its full matrix. Session emitters finishing is not acceptance proof. Full raw results are sessions-development-20260909-093624-434.jsonl (0.6), sessions-development-20260909-094448-336.jsonl (2 orbit), sessions-development-20260909-095209-486.jsonl (2 all).

Lower sweep (development-20260909-100238-483) found 0.55 and 0.75 also pass 5/5 transit. Weights 0.25/0.3/0.35 pass 4/5, all missing seed 2001. Stronger controls regression now uses minefield terrain and passed. Far-goal probe ran: spacing 4.595 m at goal 90 m, 6.688 m at goal 180 m; a physically clear 11.4 m gap is represented occupied in the coarser case. Probe/sweep/control runner passed three tests (030219); artifacts copied to independent/lower-weights-20260909-030219.

The 0.3 full session development run completed (030859; sessions-development-20260909-100916-393.jsonl). Orbit improved, but kite/cover tracking and dummy closeout still regressed. All runner artifacts copied out of the slot.

ACTIVE DIAGNOSTIC: worktree TerminalFieldBakeJob currently uses cell-center disc membership instead of disc/cell-square intersection. Original file preserved at independent/center-raster-experiment/TerminalFieldBakeJob-square-before.cs. This is a controlled rasterization diagnostic, not a settings freeze or an adopted final change. The old square-intersection unit assertion is intentionally not selected. Original seven-weight sweep + far-goal probe + no-POS/FIELD-zeroed controls passed (031654), with 5/5 transit at 1.0 and 4/5 at .01/.03/.1/.3/3. The gap stays open at 180 m but the two rocks disappear at 360 m, demonstrating aliasing instead of conservative overblocking. Output root independent/center-raster-experiment.

Center-raster weight-1 session development completed (031812; sessions-development-20260909-101830-262.jsonl): orbit/kite pass, cover/closeout regress. All raw artifacts saved under the experiment root. CURRENT RUN: center-raster weight .03 full session development, exec session 40055 (launched after 031812). After the diagnostic, decide whether evidence supports a correction or restore the original file before full correctness testing. Asset remains 0.6; overrides are clone-only, and final validation seeds remain untouched.

UPDATE: candidate frozen at **0.03 with cell-center occupancy**; see independent/validation-freeze.md. The occupancy unit now pins exact center membership and an explicit 1 m clearance case; original square rasterizer remains backed up. This supersedes the diagnostic-only state above. Additional development terrain (3601–3620, predeclared before running) produced 20/20 baseline and 20/20 at .03; artifacts are center-raster-experiment/development-20260909-103435-623.

Formal validation run 1 (033657; independent/validation-20260909-103717-413) returned 18/20 on vs 16/20 off, zero collisions both. Arrival target passes, required +4 seed benefit fails (+2 observed). Other MPC tests: 123 passed, 9 opt-in skipped. Candidate must not be retuned from validation outcomes. Repeats 2 and 3 are running sequentially in exec session 78734, each through the coordinator with a fresh Unity process and >=9 GB memory check. Final acceptance has failed unless an authorized contract change occurs. Still finish performance, ablation and paired dense-chase visual evidence; full 15-seed session validation remains unrun. Do not call this implementation accepted or shipping-ready.

The solo-machine throughput/whole-solver baseline measurement, far-goal degradation experiment, full rig ablation rows and permanent >=18/20 test still need completion after the first correctness/development runs. Capture middle/late images and paired chase measurements remain required.

## Next action and blockers

Run focused Tests.EditMode.TerminalFieldTests through agent_worktree_pool.sh run-tests agent-2. Then MPC_RIG_EMIT=1 with Tests.EditMode.TerminalFieldRigTests.DevelopmentWeightSweep, persisting MPC_FIELD_OUT under the primary results tree. Diagnose failures before tuning. Final validation remains sealed until one weight is fixed; use MPC_FIELD_VALIDATE=1 in three separate processes, preserving every attempt.

User approved >=9 GB free RAM for remaining agreed debugging, benchmark and capture launches, one Unity editor at a time. The user closed the primary editor and says Alastor is down. Do not attempt Alastor again. No permission to close other workloads. Coordinated quiet window for the delivery-timing task lasts through 09:26 UTC; no heavy launch until then or that task confirms completion.

The approved benchmark contract is independent-contract.md. User changed visual proof to dense fleeing chase and allowed weight tuning; all other terms frozen. Publishing that exact file as a public comment on issue #461 was separately rejected by automatic approval review; explicit publication approval is pending. Do not read PR #548 while resolving that.

## Session metric definitions (before any run)

Density 2.0, EvalProtocol.EvalSpec episode duration/pacing, existing sentences unchanged. Post-first-2-seconds mean absolute range error around 16 m orbit / 18 m kite / 30 m cover / 6 m dummy; fire-lane-dodge mean distance to the existing 12 m facing-frame offset; drift-hold mean displacement from spawn. Dummy closeout time is first range <10 m, with timeout assigned to non-arrivals and a separate reached flag. Dummy success = Win; other rows success = no Loss; movement-error and collision comparisons also apply. Empty measurement windows are failures, with raw outcomes saved. Compare each row/replicate on vs terminal-off; median allowance max(10% of baseline, 1 m/1 s). Session validation seeds 3201-3215; development 3401-3403.

Capture: seed 3301, density 2.0, 40 s maximum, production Evader at speed fraction .85 and juke period 1.2, border 120 m, pursuer closing sentence AIM 1 / POS 6 m weight 1 / VEL radial 8 weight .5 / FIELD 1. MPC_FIELD_CAPTURE_OFF=1 disables cost on a per-pursuer clone, never the asset. Contact counter measures physical collision-bearing fixed steps. All raw paths/ranges are retained; no catch requirement.

Capture report definitions fixed before filming: net closing progress = initial 50 m gap minus final gap; path per closing metre = pursuer path / positive net progress, otherwise explicitly non-closing (no finite ratio). A stalled closing window is a non-overlapping 2 s interval with gap reduction <=0.5 m; report its duration and count, with pursuer speed separately so retreat and stationary stalls remain distinguishable. These are descriptive metrics, not added acceptance thresholds.
