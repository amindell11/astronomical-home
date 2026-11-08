# Warm electronic light and drawn hardware

This copies wear-materials-22/console.blend. The geometry, folds, object positions, camera, modifiers and visibility stay unchanged. The geometry remains provisional.

The displays now use cream phosphor. Stronger warm light reaches the monitor frame and projector housing, while the fins, cables and chassis retain neutral charcoal faces. The projection beam brightens near its source and fades upward. Its lens has a cream center and amber edges.

Finer interior ink, broken warm strokes along the terminal lip, small corner hatching and broader paint variation reduce the molded appearance. Selector ticks, a service marking, flat fastener slots and subtle DISPLAY and PROJECTION stencils add purposeful detail. The existing scuffs remain. The stencils use the shared Rajdhani-Medium font; the screen exports and icons are unchanged.

All details remain native Blender materials. Surface drawings use each part's local coordinates and follow it when moved. There is no added decal geometry, displacement or raster paint-over. File images and fonts are packed.

## Editing

- The existing lights named `19 | Projection spill`, `19 | Screen bounce`, `19 | Faint starlight` and `19 | Soft ambient readability` control the lighting balance.
- Material ramps named `Three brush-mixed paint values` and `Painted shadow middle and light` control the palette and painted tones.
- Materials prefixed `23 |` have per-part surface drawings. Their `Drawn dark intensity`, `Drawn light intensity` and `Drawn brush intensity` nodes control the marks.
- The `23 | Surface drawing on ...` groups hold the local strokes and packed stencil masks.
- `Warm phosphor tint` controls the display tint. `Cream hot center to amber lens edges` controls the projector lens.
- Freestyle uses separate outer and interior lines. The compositor's glare node controls electronic glow.

## Reproducing and checking

`make_stencils.py` reads the existing shared font and writes the two stencil masks beside this file. `apply_illustrated.py` accepts a loaded wear-materials-22 scene and `--name`, then writes a new packed blend and manifest. It refuses to overwrite an existing blend. Missing drawing placements fail before saving.

`render_review.py` writes the camera and neutral angled inspection renders. Temporary inspection lights and visibility changes are not saved. `verify.py` compares the saved scene with wear-materials-22 for geometry, smoothing, modifiers, transforms, visibility, camera and object inventory. Lighting energy, color and materials are intentionally changed.

The review page compares the previous pass and both illustrated references with the current render. Sol's review rounds and the delivered file hashes are recorded in review.json. Reviewer clearance is separate from user approval.
