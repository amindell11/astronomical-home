# Vanguard native rendering study

`VanguardStudy.blend` preserves the September 23 textured working scene and its
original editable source scene. Required images are packed. `source-manifest.json`
records the original file hash and image provenance; the primary-tree originals
remain untouched. This is separate from the committed production Vanguard export.
`VanguardStructure.blend` adds selected tapered panel seams; their editable paths
are in `structural-lines.json`. The original paint remains unchanged.
Its separate service-panel mesh defines exposed wing-root frames, core collars,
aft pod access covers, fitted native cockpit vents and blue-gray inner hull
panels from `service-panels.json`.
The covers follow the pod armor edges; all detail fits the native triangles.
The paired hangar captures show this layer enabled and disabled.
`wear-lines.json` places asymmetric glancing impacts on the forward pod, canopy
armor and aft spar. Torn entry chips lead into directional gouges and scrape
trails. This is one battle-worn finish; damage-level variants remain a future art
pass. Paired hangar captures isolate the wear layer.

Run `export_study.py` with Blender in background mode to export the packed study
to Unity. An optional path after `--` selects a different source. Export reads the
`Vanguard - Texture MVP` scene and retains its paint UV as the exported UV channel.
Blender's evaluated triangles and corner normals are frozen into the export:
the source contains nonplanar panels whose alternative triangulations intersect
the blue disks. The original editable polygons remain in the baseline source.
The export manifest describes that run; it does not replace source provenance.

`VanguardStudyPlayModeTests` produces matched native Unity captures under
`results/vanguard-study` through the graphics-enabled test runner. The conventional
lighting baseline uses the same textured mesh as the drawn candidate. The separate
production captures use the committed Vanguard rig's mesh and materials, staged
with the hull top facing the canonical camera and normalized to the same length.
The context captures combine that ship with the ten drawn asteroid assets and
`NebulaBackground-v2.png`, an AI background plate with foreground objects removed.
The ship and asteroids are native Unity meshes; the backdrop is a static image.
`background-prompt.txt` records the built-in image-generation edit request.
The hero captures use hangar and green-planet plates derived from the two
user-supplied concept references. Their UI remains in the static images;
`ui-background-prompts.txt` records those edits. The hangar is a flat
shadow-receiving plate, not a modeled environment or functional menu.

Contour-only mesh copies join normals at shared positions and use a uniform
black screen width. The painted mesh retains its authored normals. Three native
poses verify a two-pixel contact band at the reduced contour width. A material gain shifts the
original gold-orange toward the reference orange without editing the texture.
HDR emission and camera bloom light the blue pods; paired space captures isolate
the bloom effect. The UI plates retain their native resolution without mipmaps.

The rendering brief and visual decisions live on
[#685](https://github.com/amindell11/astronomical-home/issues/685#issuecomment-5853114008).

## Selected Vivid production paint

`VanguardPainted.blend` is the selected editable source for the production rig.
It packs the 4096-pixel Vivid atlas and preserves every original scene's geometry,
triangulation, corner normals, transforms, UV coordinates and symmetry modifiers.
`VanguardStudy.blend` and `VanguardStructure.blend` remain the original baselines.
The separate canopy and blue cores retain their materials.

Damage and service-overlay objects remain recoverable but hidden in the painted
source and absent from the live production prefab. Vent and service-panel color is
baked into the hull atlas, removing the overlay's triangular gray seams. The hull
uses `Vanguard vivid paint.mat` with neutral orange gain; the texture GUID is
unchanged. The production FBX is unchanged.

The [repeatable paint pipeline](https://github.com/amindell11/astronomical-home/blob/3da0da5de39c07b98b49f203a13427ce2dbaf13d/pipeline/README.md)
includes the recovered Crimson recipes, generation helpers and preservation
checks. Use that pipeline for this source; `export_study.py` regenerates the older
study overlays and is not the selected paint pipeline. The breakup work in
[#795](https://github.com/amindell11/astronomical-home/issues/795) should use this
painted source and material, retain the separate canopy/cores, and leave the
rejected overlay objects out of the live breakup pieces.
`DrawnStudy/Meshes/Vanguard painted hull.asset` combines the nine painted hull
meshes, and `Vanguard contour.asset` combines eleven silhouette meshes. The live
rig keeps five art renderers: hull, contour, structural ink, canopy and cores.
The saved meshes preserve triangle indices, painted UVs and transformed positions
and normals without welding or recalculation. Original source parts remain
separate in `VanguardPainted.blend` for breakup; the consolidated intact meshes
are runtime assets rather than the breakup authoring source. The existing
`Ship_1.prefab` continues to reference the updated illustrated rig.