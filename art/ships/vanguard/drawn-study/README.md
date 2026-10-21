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

`export_study.py` and `build_structure.py` are retired to branch `evidence/vanguard`
(`producers/drawn-study/`). These Blender-to-Unity producers read the
`Vanguard - Texture MVP` scene and froze Blender's evaluated triangles and corner
normals into the export: the source contains nonplanar panels whose alternative
triangulations intersect the blue disks. The original editable polygons remain in the
baseline source. `export-manifest.json` describes the last export run; it does not
replace source provenance.

Native Unity captures came from the saved study scenes. The scenes, their materials
and meshes, and the two capture tools are on branch `evidence/vanguard` under
`studies/drawn-art/`, with `.meta` files and a revive recipe. No current tool
reproduces the paired comparison captures this README mentions (layer on/off,
bloom on/off, contour proofs). The backdrop plates that the scenes used
(`NebulaBackground-v2.png`, `HangarBackground-v2-ui.png`, `PlanetBackground-v1.png`,
`PlanetBackground-v2-ui.png`) and the `ShadowedPlate.shader` that drew the hangar
plate sit in this folder.
The `AsteroidField` scene combines the ship with the ten drawn asteroid assets and
`NebulaBackground-v2.png`, an AI background plate with foreground objects removed.
The ship and asteroids are native Unity meshes; the backdrop is a static image.
`background-prompt.txt` records the built-in image-generation edit request.
The `HangarHero` and `SpaceHero` scenes use hangar and green-planet plates derived
from the two user-supplied concept references. Their UI remains in the static images;
`ui-background-prompts.txt` records those edits. The hangar is a flat
shadow-receiving plate, not a modeled environment or functional menu.

Contour-only mesh copies join normals at shared positions and use a uniform
black screen width. The painted mesh retains its authored normals. Three native
poses verified a two-pixel contact band at the reduced contour width. A material gain shifts the
original gold-orange toward the reference orange without editing the texture.
HDR emission and camera bloom light the blue pods; paired space captures isolate
the bloom effect. The UI plates retain their native resolution without mipmaps.

The rendering brief and visual decisions live on
[#685](https://github.com/amindell11/astronomical-home/issues/685#issuecomment-5853114008).

## Production source

The editable production source is [`../Vanguard.blend`](../Vanguard.blend).
Its livery and paint masks follow [ship tools](../../../tools/ship/README.md).
The study scenes and atlases here preserve provenance for the original artwork;
production decisions and approved comparisons live on
[#1016](https://github.com/amindell11/astronomical-home/issues/1016).
