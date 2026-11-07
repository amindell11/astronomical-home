# Violet, geometry and mapped-material proof

This is one Blender art study, not an accepted sculpt or production asset. Separate rock and crystal paint now follows the actual component meshes. The roof planes remain oversized and regular, and the crystal grouping is cleaner than the original concept. Ink is present but straighter and thinner than the reference. No other asteroid families, debris, Unity integration, or production exports are included.

Open `violet-painted.blend` for the mapped material proof, or `violet-refine.blend` for the neutral geometry and plain component materials. Both open at the hero camera. The visible collection contains one fused rock host, four separate crystals, and three separate warm mineral patches beneath one asteroid parent. The hidden `Editable host construction` collection retains the substantial rock volumes and fracture cutters.

`violet-material-proof.png` compares the original, revised clay, and actual mapped Blender render, with side/rear views and a close-up. `violet-refine-board.png` retains the fuller geometry comparison. Their 150-pixel rows are resized previews, not Unity captures. Clay hides the mineral overlays and uses no ink so the solid forms remain inspectable.

The two original 1254-square Imagegen swatches and exact prompts are in `paint/`. They are unchanged and packed inside the painted Blender file, with relative external paths. `DirectionalPaintUV` projects brush direction along the real roof fractures and each crystal's axis. UV faces intentionally reuse the swatches; these are not unique bake atlases. Each component remains independently editable. A material node lowers the rock's value and saturation. Warm deposits sample the rock brush pattern through an ochre color ramp. No lighting or occlusion is baked into the paint.

Dark outer contours and selected fracture strokes use Blender Freestyle. They are render effects, not exported geometry or a Unity shader. Crystal contours use thinner muted purple, and three short pale edge accents are separate tapered paint ribbons. Those ribbons are intentionally open surfaces. Mineral patches are near-flush polygons confined to individual host faces, with buried undersides; they do not receive heavy outlines.

`source-check.json` records fresh reopens, connected-component and boundary counts, material/UV presence, packed image paths, and source hashes. These checks establish source integrity, not visual acceptance. The main crystal's buried root ring was moved into the existing rock after the side view exposed a protruding root; its upper rings stayed in place.

Authoring order with Blender 5.1:

1. `blender --background --factory-startup --python-exit-code 1 --python build_violet.py`
2. `blender --background --factory-startup --python-exit-code 1 --python paint_violet.py`
3. `blender --background --factory-startup --python-exit-code 1 --python inspect_sources.py`
4. `python make_board.py` and `python make_material_board.py`

All comparison inputs are included. The original approved Violet crop is `reference-violet.png`; `previous-violet-clay.png` is the earlier component study used only for comparison.
