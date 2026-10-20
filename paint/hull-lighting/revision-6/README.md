# Broader Valis brush strokes

Source commit: `2df014076305fd3a4a9714f1d626d307f5be1ec7`, PR #1029. This feedback pass changes only Valis brush.json and the resulting runtime Brush.png.

The brush-only export frequency is 0.4 instead of 0.8: the same source strokes cover twice the surface distance in each direction. Contrast, offsets, materials, shader, geometry, UVs and existing masks are unchanged.

Left: previous 0.8 bake. Right: new 0.4 bake. Both sample their complete baked atlas with identical material UV settings and lighting. The closeups and 160-frame comparison cover frontal/angled light, banking, light sweep, camera orbit and wing articulation, including approximately 100-pixel ship views.

The source is Crimson's `painted-brush-source.png`, also referenced by Vanguard and byte-identical to Bulwark's copied tile. The separate atlas is enabled only on the two Valis prototype armor materials. Other current painted ships still combine brush darkening/brightening with authored shading in their Shadow/Light masks.

## Validation

The new brush-only export passed its geometry lock, independent-side and source-preservation checks. `preservation.json` verifies the source blend, three edited masks and source tile match main, and the imported brush equals the exporter output.

The actual wing-bone probe moves vertices by 0.6552084 units while all 99,006 covered brush samples remain identical (maximum delta 0/255). Both old/new capture shaders compiled without errors. Controlled graphics-batch renders were inspected at close and game sizes.

The previous code revision's five graphics cases, nineteen Python tests and Blender continuity/legacy checks remain linked from revision-5; this asset-only change does not alter their code. ReSharper passed on this revision. The size-S asset adjustment received a local scope/comment check; no additional quality subagent was needed.

## Reproduction

Rebake with `ship_paint.py --ship art/ships/valis/ship.json --out <scratch> --brush-only`. `ScaleCapture.Revision` and `.Direct` compare the old revision-5 bake with the new material-bound atlas; `ScaleWingProbe.Run` checks real wing attachment. `publish_scale.py` labels/encodes the renders. The batch child runs only through the Unity access coordinator.
