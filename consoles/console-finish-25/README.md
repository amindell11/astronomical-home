# Console polish and viewport glass

This copies painted-console-24/console.blend. The 345 existing objects, console geometry, camera, modifiers and visibility remain unchanged. One transparent viewport surface restores the missing glass markings.

Ink pressure still varies, with less positional wobble and raggedness. The Display label is removed. Launch uses the existing shared-font lettering with a separate warm backlight material. Its amber button remains physical and editable.

The nebula is brighter. A smaller increase in warm console light retains the painted shadows from pass 24. The glass has faint perimeter etchings, small hex clusters and edge clouding, with a clear central view. It sits just behind the existing viewport lip and behind the hologram, so the scene occludes it correctly. Its marks are native material strokes, not a camera overlay or image paint-over.

## Editing

- `25 | Launch letter backlight` controls the lettering's color and brightness.
- `19 | Distant nebula` controls the background brightness.
- The existing `19 | Projection spill`, `19 | Screen bounce` and `19 | Soft ambient readability` lights control console illumination.
- Freestyle's noise settings control the remaining ink variation.
- `25 | Viewport glass` is parented to the existing viewport header. Its `25 | Etched glass perimeter` material contains `Etching visibility` and `Clouding visibility` controls. The center is transparent.
- `25 | Unlabelled brow paint and wear` preserves the brow treatment without the Display stencil.

All file images and fonts remain packed. The existing paint, wear, precise dial markings and screen exports remain editable and unchanged.

## Reproducing and checking

`apply_finish.py` accepts a loaded painted-console-24 scene and `--name`. It writes a new packed scene and a manifest, refusing an existing output name. The manifest records the native glass paths and its depth.

`render_review.py` writes camera and angled inspection images without saving the temporary inspection setup. The glass and distant space are hidden only for that inspection render. `verify.py` compares all 345 original objects with pass 24 and allows only the named new viewport glass surface.

The page retains previous/current, glass-reference and lighting-reference comparisons. Sol's review and final file hashes are recorded in review.json. Owner review remains separate from reviewer clearance.
