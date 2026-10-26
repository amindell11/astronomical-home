# Recap adaptation verification

PR #1146 adapts the earlier camera/input correction into the recap modules now on main, including #1180. The saved designer profile from evidence/jump-preview at 1e511fd70db91c502836096b5eb2f1b1a5472f89 is preserved. A one-second death reveal delay is added.

## Focused tests

- 20261025-190557: red sequence run, 4 passed and 2 failed. The new assertions exposed the particle lifetime hold and frozen defeat phases before the adaptation.
- 20261025-191322: 13 passed, comprising six sequence tests and seven screen tests.
- 20261025-191727: graphics run, 11 passed and 1 failed. All eight screen tests passed, including UI-only extraction and cooperative cancellation. Three production flow tests passed. The death test failed its camera zoom comparison while startup smoothing was still active.
- 20261025-192030: graphics run, 3 passed and 1 failed. Setting the rendered camera size alone did not stabilize the test's zoom target.
- 20261025-192226: graphics run, death test passed. The setup now gives the observer a manual zoom target and waits for it to settle before killing the player. The assertion tolerance remains unchanged.

Across these runs all 18 distinct tests passed. No claim of a full-suite run is made. Production source was unchanged between the final two runs; the last correction only stabilized the test setup and added a camera-restoration assertion.

## Visual evidence limits

The existing recap-response.gif and recap-response.mp4 on this evidence branch show the earlier PR revision. They preserve the layout evidence but do not demonstrate the newer designer fade timings. This adaptation produced no new visual capture. Combined interactive Playtest coverage belongs to the follow-up under #1181.

## Quality review

The combined review found no correctness, scoped simplification or Unity-convention issues. It removed one redundant GameHost comment. ReSharper passed on 302284d86 with no blocking Unity findings on changed lines and 97 report-only findings elsewhere in touched files. The subsequent source change deletes only that comment.

## Input lifetime follow-up

The combined resident-editor run in agent-5, 20261025-194952, failed the death test with InputManager.AddStateChangeMonitor IndexOutOfRangeException. InputTestFixture.Setup reset the input system after the host had created real controls, and its local finally restored the system before those controls were destroyed. The fixture now surrounds the host and world lifetime through the existing per-test SetUp/TearDown. Keyboard creation also precedes game boot.

All four production-flow tests passed together with graphics in agent-4 run 20261025-195252. The source fix is test-only commit f9b5b4f17d27ae08075a5a29f266594c2a03699a. The narrow quality review found no issues. After the Playtest work integrated the fix, the original resident-editor scenario passed all four tests in agent-5 run 20261025-195723.

The ReSharper ratchet passed again on f9b5b4f17 with no blocking findings and 97 report-only findings outside changed lines.
