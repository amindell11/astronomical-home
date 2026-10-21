# Bulwark cockpit glass

The original glass material used DrawnSurface, so its highlights stayed painted onto the canopy. The corrected material uses the same DrawnCanopy painted-glass mode as Nightshade and Valis. The glass color, Shadow/Ink paint and original layer strengths remain fixed; the recovered Light-channel strokes move with the shared reflection mapping.

## What do the captures show?

The Unity-rendered comparison holds geometry, pose, lighting and camera constant. Only 3,324 pixels differ by more than one channel level in the full-ship still, all inside the cockpit rectangle [288, 120, 351, 259]. The motion comparison applies the same 55-degree roll and 20-degree pitch sweep to both versions. These are controlled Unity asset renders with no bloom, not gameplay flight approval.

![Whole ship](comparison.png)
![Banked cockpit](bank-comparison.png)
![Matched motion](motion.gif)

## What was checked?

The baseline run failed the Bulwark shader-binding and moving-reflection checks. The corrected graphics run passed 60 of 60 checks, including the temporary nonblank-capture assertion and existing canopy and ship-anatomy coverage. The six permanent Bulwark cases cover intact/debris projection, fixed glass paint, light-driven reflection movement, surface-normal response, and damage/debris overrides. See the two XML reports.

`recover_reflections.py` reproduces the exact committed RGBA pixels from the imported mesh/UV dump and original Shadow/Light/Ink images. Its resampling functions come from `project_art.py`, preserved from the four-ship canopy work at f85576ff418013ac25e3501ed6d3909785646741. The original glass has zero emission coverage. The intact and debris glass triangles have identical positions and UVs, so the existing shared material binding is retained.

The production change is commit 46f38c4a. Neither source geometry nor UVs, paint masks, shaders or prefab wiring changed. The independent quality review found no issues. The static-analysis result is reported on the PR.
