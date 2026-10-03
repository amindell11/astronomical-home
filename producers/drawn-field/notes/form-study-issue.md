## Why did thicker contours not deliver the intended look?

User feedback rejected the previous low-poly asteroid and found little visible change to the ship. The isolation confirms the gap: the shell changed 5,390 pixels around a 768×768 ship but added zero ink inside its eroded silhouette. Width cannot recover canopy or hull-overlap lines. The original asteroid mesh is restored in commit 2359bca3 on PR #691; the 282-triangle flat-normal substitution was removed.

## What does the new experiment show?

A temporary depth/normal screen pass produces visible overlap lines in close-ups. Matched outer-only/internal-ink recordings share all 900 motion rows exactly and each decode to 899 frames. Two orthographic/perspective overlap graphics tests and three outer-contour regressions pass. ReSharper reports zero blocking findings; combined quality review found no required changes.

This is not an art acceptance result. The asteroid remains scratchy; fine ship features fill with black or break up in gameplay. MSAA off reduced fringes, but did not solve the underlying line selection. The original-detail asteroid exposes existing magenta capture defect #619 on both sides, including with MSAA off; native close-ups do not show it. The low-poly mesh hid that defect rather than fixing it.

The comparison retains these shortcomings visibly. The remaining artistic problem is selective, continuous linework over the actual forms, not simply increasing the global contour width. No production material rollout, performance claim, or issue closure.
