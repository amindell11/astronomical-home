# Circular mine blast with growing spike

The user requested restoring the circular blast and merging. The spike still
grows upright, with complete pointed ends and the original 400 ms core lifetime.
The disc and boundary ring are restored to their previous circular appearance.
The missile stays unchanged. The standalone art comparison retains its authored
800 ms sweep. The integrated game preserves main's mine balance from PR #1105:
radius 10, expansion speed 15 and damage falloff 0.7. Its disc follows that wave,
about 667 ms.

The main preview now shows the circular game view; the small oblique view remains
a labelled comparison. The preview circles fit within their panels at full size.
The source extraction and atlas checks passed.

The Unity capture ran on `b4da449a74a2554aadc214b7ea1ec5d543ab210e`, after resolving
the prefab conflict by retaining main's mine balance and this PR's drawn effect.
All 24 selected graphics-enabled PlayMode tests passed, with no failures or skips.
The capture contains 179 frames at 50 fps, lasting 3.58 seconds. Both effects replay
twice through the game camera. The pointed upright core and later circular disc
were visually checked. The recording verifies appearance and pooled replay; it
does not exercise missile flight or audio. Main's FMOD debug overlay is visible.

The hosted full suite passed all 1,115 tests on the same commit, with 850 EditMode
and 265 PlayMode tests, zero failures and zero skips. ReSharper reported zero
blocking findings; five findings elsewhere in touched files remain outside the
changed lines. The merge workflow accepted both checks for the exact landing tree.
The source NUnit XML and ReSharper summary are included here.

Run: https://github.com/amindell11/astronomical-home/actions/runs/39753159964

PR #1091 merged on 2026-10-22 as `4e5f136a7b7f17e6f18d405ab27e2b09250635ee`.
Main was synced after the merge.
