# Rollback quality review

No actionable findings for the requested exact restoration.

Reviewed commit `36931e9f352a94efa59c9991a99e6b7418389387` against main `520cb1576ef1de96c9a4524d4a785c95befa0973` and target `c51bfd8f05b726911e9e019f1329713a3d322320`.

- Independently compared all 13 paths from merged PR #1029 with the target using Git tree entries. Every entry matches, including the four files absent from the target.
- The diff against current main contains no paths outside those 13. Unrelated main changes remain intact, and the tracked worktree is clean.
- The separate brush exporter, atlas, material bindings and related tests are removed together. Tracked art and Unity files contain no remaining references to the removed brush property, texture GUID or exporter trailer.
- The restored shader, two Valis materials and graphics tests match the original prototype. Touched comments and Unity conventions require no changes. Simplifying this code would alter the requested restoration.
- Recorded verification passes all four original graphics cases and all 18 Python tests. The diff passes whitespace validation. This review did not run Unity. ReSharper was still running when the review began.

The original residual diagonals and near-zero coarse color-ratio behavior are deliberately restored by the owner's choice. They remain acknowledged limitations, not new restoration findings. No tracked files were edited by this review.
