# Capture recorder B acceptance record

Tested source head: `4c198b2d41d37566030ba587c462521774e423d4`.
Stack base: `f61d9a81a5909659665d4e647f423167150f111a`, PR #1178.
The production recorder/caller implementation is `fd5ddec248ea5d6dceef96880c3e40f03d33b525`.
The later source commit only attributes native-gizmo and recap-banner pixel assertions to their actual subjects.

## Current main-path evidence

- RecapParityScenario passed 1/1 through the resident-editor lane at 4c198b2d, summary `20261024-212238`. Ten hold clips cover the five existing recap states at 1280x720 and 1920x1080. The additional live clip records the actual PreviewHost transport and banner.
- The live clip has 24 matching PNG/CSV pairs. Simulation time stays at 0.98727857142857134; unscaled time advances from 8.9507242984693871 to 9.7593271967120181; transport moves from span 1 to span 2. The test compares the centered banner region, excluding the animated FMOD overlay. Both MP4 and GIF assembly passed with raw frames retained. Encoded duration is 0.838648 seconds, including the documented inferred final hold.
- RecapFixtureSheetScenario passed 1/1 at 4c198b2d, summary `20261024-212400`. Its complete 34 output folders cover the existing fixture sheet and banner captures at 1280x720 and 1680x720. The Preview Host panel screenshot uses the recorder-returned output directory.
- PlainCaptureViewEditModeTests passed 1/1 through the resident editor at 4c198b2d, summary `20261024-212718`.
- Focused EditMode checks passed 52/52 at 4c198b2d, summary `20261024-211842`.
- The pool-selected script suite passed 11/11 at fd5ddec2. No script changed afterward. This includes strict frame identity/completion, timestamp assembly, launch provenance, and selected runner/access tests.

## Recorder lifecycle and native rendering

The resident-editor lifecycle test passed at fd5ddec2. `recording-20261025-040714-451` retains every output: prepared scopes wrote zero PNGs and rows; Finish returned 20/20 completed pairs; recording disposal retained 4/4 incomplete pairs; an actual output-path conflict after three successful writes retained 3/3 pairs, kept completed=false, and threw from Finish; reuse produced 4/4 completed pairs. Every decoded frame ID matched its CSV render_id, including first and last. The test checks frozen simulation, unscaled timing, stable completed output, focus, Game View configuration and rendered dimensions after repaint, and no late frames after cancellation.

The native silhouette capture at fd5ddec2 retains its complete 14-frame output and exact metadata. The representative image contains native circles, lines and ship outlines. Three of four selected warm RL tests passed at that source: plain episodes, native capture/restoration, and collider silhouettes. The on/off/on test failed because it mistook the grayscale FMOD overlay for native geometry. The 4c198b2d fixture correction measures the existing colored-ink discriminator; it has not been rerun. The user then explicitly deprioritized RL maintenance. No additional RL-only runs were scheduled. The launcher provenance check uses a fake Unity boundary; no fresh external RL harness launch is claimed.

## Negative results and boundaries

The test-record archive preserves the failed diagnostic runs. An Editor-only MonoBehaviour placement failed attachability and was moved to the runtime assembly. A Package Manager access-token HTTP 400 interrupted a later test before assertions. A live CSV inspection used incompatible file sharing; the fixture now waits for PNGs and reads metadata after disposal. An externally aborted lifecycle fixture leaked its high-depth camera; unchanged isolated RL tests passed 2/2, and NUnit teardown now owns that fixture's cleanup. No auth workaround or broad log suppression was added.

The warm editor briefly stopped answering during a forced domain reload between checks. Readiness was retained and checked before further dispatch. The final lane released Enter Play Mode Options, then the coordinator closed the owned editor. Generated analytics and quality-setting changes were restored after closure.

These are focused development and graphics results, not merge-grade headless proof. Named presentation preparations and their resident-editor acceptance belong to split C. The FMOD debug overlay remains visible in B footage. No merge is authorized, and all split PRs stay draft for the user's review.

## Archive contents

`capture-b-main-path-4c198b2d.tar.gz` preserves complete lifecycle, recap parity/live, and native silhouette outputs at their original relative paths. Each folder's provenance identifies its own capture source, either fd5ddec2 or 4c198b2d. The three `capture-b-fixture-sheet-*-4c198b2d.tar.gz` archives preserve all fixture-sheet output folders. `capture-b-test-records.tar.gz` contains summaries, diagnostic logs, the exact lifecycle result, and the selected script log. Individual PNGs and the live MP4/GIF are copied unchanged for review.

## Final review checks

ReSharper passed at 4c198b2d against origin/main with zero blocking findings across 40 changed C# files and two report-only HarnessHost performance findings. The independent combined review found no actionable runtime/assembler issue. Final review commit 8a9a025ce changes only the warm-lane documentation to resolve provenance against its target project and removes a closing-brace namespace comment. All source proof remains attributed to the capture commits stated above. This review does not authorize a merge.
