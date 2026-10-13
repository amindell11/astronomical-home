# Bulwark — gray blockout

Stage: blockout. Proportions await owner approval; color and surface detail wait until that approval.

## Direction

A modified armored freighter: slow and powerful, built to survive punishment, with a reinforced forward bulkhead for ramming. The additional references inform broad masses and utility-ship proportions; their literal designs and surface details are not copied.

The unapproved detail pass was discarded by reopening the saved seven-part checkpoint at `00f3f6e5`. This round changes only those seven simple forms and their review material.

- Wider, thicker, blunter ram face.
- Slightly broader, lower main body.
- Lower, fuller tail tips instead of thin raised fins.
- Rounded-front teardrop cockpit seated lower on the hull.
- One neutral gray material across every part.

## Editing

Open `art/ships/bulwark/Bulwark.blend` and expand `Ship > SymmetryOrigin`. The seven groups remain Ram nose, Upper hull, Lower hull, Tail booms, Tail tips, Cockpit, and Side insets. All seven meshes retain live Mirrors. The cockpit's subdivision remains editable. No added armor plates, markings, rings, vents, frames or ribs remain.

## Validation

Source commit: `27903a228df9aebe3d5b3d87bcb836b6fe08f653`.
Source SHA256: `8efef69d19ec620fdc1190bd7da34e2fd2ee38a759a8957103433f0e06cdfa5b`.

`ship_check`, orthographic renders, scale renders and comparison against the prior checkpoint passed. Seven mesh parts and five closed structural parts were verified. ReSharper skipped automatically because there are no C# changes.

[Hero](hero.png) · [Top](top-studio.png) · [Side](side-studio.png) · [Front](front-studio.png) · [Source check](ship_check.json) · [Editing groups](editability.json)

The studio-lit views expose volume; the pipeline's flat views and reports are also preserved. These are Blender blockout previews, with no game-integration claim.

