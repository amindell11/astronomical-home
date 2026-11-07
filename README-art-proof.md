# Locale asteroid art proof

This is the first 3D asset pass for [the locale asteroid arc](https://github.com/amindell11/astronomical-home/issues/1377). It develops the five asteroid directions the owner approved on 2026-11-06. [Non-asteroid debris](https://github.com/amindell11/astronomical-home/issues/1378) remains a separate, deferred arc.

## What to review

Judge the silhouette, major fracture planes and material regions both large and at gameplay size. Compare the alternate angles before accepting a shape. The renders are Blender art previews, not Unity captures.

| Family | Locale | Intended distinction |
|---|---|---|
| Copper | 1, copper and turquoise | Iron-grey stone, copper plates, turquoise deposits, stepped cleft |
| Violet | 2, violet nebula | A broken stone host with a limited crystal exposure |
| Chalk | 3, illustrated blue | Pale layered stone with broad chipped terraces |
| Verdant | 6, Verdant Giant | A rounded mineral nodule with a cream crust and green bands |
| Ice | 7, Blue Crescent | A sheared ice wedge with darker stone inclusions |

## Sources and exports

The asset bundle contains the editable Blender sources, exported FBX and GLB models, albedo maps, supporting maps where used, previews, and validation results. The Blender sources pack the textures they use. FBX and GLB export the model parts separately from the preview cameras and lights.

The new albedo maps were generated with the built-in Imagegen tool from the current asteroid brush texture and the approved concept sheet. `texture-prompts.json` records the exact prompts. The original generated maps are 1254 by 1254 pixels. Their role is material color; the meshes, drawing geometry and renderer supply relief, graphite marks and shadows.

The current drawn asteroid sources in `art/asteroid-field/` supplied the starting art language. New slab forms and adapted rounded forms develop the approved differences. Original game assets remain unchanged.

## Integration boundary

These files do not include production collision meshes, volume or covering-sphere bakes, fragment authoring, locale spawn selection, or Unity material tuning. Those follow acceptance of the real 3D shapes. Nothing in this proof changes damage, density, loot, field layout or spawn rates.

Locale_4 Coral Sunset and Locale_5 Cyan Aurora were added to main after the reference inventory used for the approved five-concept sheet. This pass does not invent asteroid designs for them.
