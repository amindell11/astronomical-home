# Violet component geometry study

This is one untextured Blender prototype for geometry review. It is not paint-ready or integrated into Unity. The stone remains more planar and regular than the approved concept, particularly across the roof and rear wall. The study tests a thick rock body, a front fracture, and separately editable crystal and mineral components.

Open `violet-components.blend`. The saved camera and viewport show the plain-material hero angle. The visible collection contains one fused `Rock_Host`, four separate `Crystal_*` meshes, and three separate `Mineral_Patch_*` meshes, all beneath `Asteroid_Violet_Prototype`. Each component has ordinary named materials. No image textures or UV painting are present.

`Host construction volumes` is hidden from the viewport and renders. It retains the original core, roof, jaw, strata, and fracture cutting volumes. The visible host contains the applied unions and cuts; edit the construction meshes or `build_violet.py` to rebuild its form deliberately.

`violet-clay-board.png` compares the exact original concept crop with the clay and plain-material views, then shows the rear, side, isolated components, and a comparison resized to 150 pixels wide. `reference-asteroids-by-locale.png` is the complete original approved sheet. `reference-violet.png` is its unchanged crop at pixel bounds (407, 314, 696, 483). Clay uses one neutral material and no ink. Plain-material renders use Blender Freestyle for the outer contour. That contour is a render effect, not an exported drawing mesh. The small views are resized previews, not Unity captures.

The hero, reverse, and side PNGs retain transparency. `violet-isolated-host.png`, `violet-isolated-crystals.png`, and `violet-isolated-minerals.png` show the real separate meshes; their board thumbnails use different scales for readability.

`reopen-check.json` records a fresh Blender reopen, visible object/material structure, and hidden construction meshes. This checks editability only, not visual approval. No FBX/GLB exports, production tests, or other asteroid families are included.

To rebuild with Blender 5.1: `blender --background --factory-startup --python-exit-code 1 --python build_violet.py`. `render_components.py` renders the isolated parts. `make_board.py` assembles the comparison using the adjacent original concept sheet. All required source inputs are included.
