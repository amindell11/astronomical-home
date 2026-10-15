# Vanguard detail model — 2026-10-15

Review candidate for [#1016](https://github.com/amindell11/astronomical-home/issues/1016), following the owner's confirmed hybrid scope. Owner approval is pending. These are Blender geometry previews; they do not establish the final painted look or in-game approval.

## What needs review

The only new shapes are three mirrored assemblies: pod cooling hatches, cockpit vents, and wing service hatches. Their casings have bevels, inset floors and side walls, with raised slats or a latch. The original outline is retained. Flat review colors expose the additions; they are not an approved palette.

![Before, proposed detail model, changed pixels](compare/compare_top.png)

Left: committed baseline; middle: detail candidate; right: changed pixels in red.

![Orthographic views and turnaround](sheet/sheet.png)

![Game scales: 32, 48, 64, 96 pixels, each enlarged with nearest-neighbor sampling](scale/scale.png)

## Preservation and checks

- `ship_check` passes with no findings and no exemptions for both baseline and candidate.
- `audit/preservation-audit.json` checks all 44 original meshes: exact vertex positions, corner and triangle indices, corner normals, UVs, world transforms, and live modifier parameters. Zero original meshes changed; exactly three new meshes were added.
- `audit/detail-audit.json` checks the production parts and the inactive editable scene against the prepared baseline. The existing hidden service and wear overlays remain excluded from the visual roles.
- `ship_render` comparison, sheet and game-scale runs pass. Source hashes are in their reports.

## Sources

`source/Vanguard.blend` and `source/ship.json` hold the candidate on the canonical role conventions. `baseline/Vanguard.blend` is the committed baseline from before the new detail. The source retains the old atlas and UVs for now; recolorable region assignment, PaintUV correction, masks and runtime asset changes have not been done.

The production source's preparation is commit `2a5d688c220f849c15a6f3930d6ca928d916f4b4`; the detail candidate is `677ee2b6`. The next step requires the owner's detail-model approval, then a flight check and geometry lock before UV/paint work. Scratch producers are archived in `producers/`; they are not new shared ship tools.