# Warm shadows and worn paint

This copies illustrated-console-23/console.blend. Geometry, folds, object positions, modifiers, visibility and camera settings remain unchanged. The geometry is still provisional.

The lighting follows the dim control area in the ink reference. Broad fill and blue background brightness are lower. The terminal and projector cast warm local light, and the projection beam is softer and less saturated. The display remains readable without the previous bright cream casing.

The casing has variable ink pressure, small deviations in the drawn contours, uneven painted shadow shapes at existing folds and contact edges, and irregular chips. Shadow marks have lower contrast and feathered brush edges. Existing scuffs remain. These are material and rendered-line changes, with no added mesh details or paint-over.

Manufactured markings stay regular. The two dial scales use the actual knob centers and equal angular intervals in their panel coordinates. Shared Rajdhani text aligns with the panels and retains the font's proportions in world space. The display content and icons are unchanged.

## Editing

- Lights named `19 | Projection spill`, `19 | Screen bounce`, `19 | Faint starlight` and `19 | Soft ambient readability` control the local light and fill.
- Material ramps named `Three brush-mixed paint values` and `Painted shadow middle and light` control the painted palette.
- Materials prefixed `24 |` contain the added casing treatment. `Painted shadow amount` and `Painted chip amount` control its strength.
- Groups prefixed `24 | Painted casing and exact graphics on ...` contain local surface marks, paint shapes, dial marks and packed labels.
- Freestyle's `Uneven drawn edge` and thickness noise control the ink variation. They do not move mesh vertices.

All file images and fonts are packed. Marks follow their parts when moved.

## Reproducing and checking

`apply_paint.py` accepts a loaded illustrated-console-23 scene and `--name`. It reads that pass's drawing manifest and the two shared-font stencil masks beside this file, then writes a new packed scene and manifest. Existing output names and missed surface placements fail before saving.

`render_review.py` writes camera and neutral inspection renders without saving the temporary inspection setup. `verify.py` compares the saved scene against pass 23 for geometry, smoothing, modifiers, transforms, visibility, camera and object inventory. Lighting and materials are intentionally changed. The drawing manifest records dial centers, radii, line width and angles.

The page retains the previous/current comparison and both references. Sol's review rounds and delivered file hashes are recorded in review.json. Reviewer clearance does not imply owner approval.
