# Asteroid locale art, revision 2

This is a Blender art proof for the five approved asteroid directions. It is for visual review and has not been integrated into Unity.

| Locale | Family |
| --- | --- |
| 1 | Copper-bearing fractured stone with localized turquoise patina |
| 2 | A broken violet stone host with exposed amethyst |
| 3 | Pale layered chalk and slate |
| 6, Verdant Giant | A rounded mineral nodule with a banded green exposure |
| 7, Blue Crescent | Dirty fractured ice with embedded stone |

## Review the artwork

The proof sheet shows the chosen meshes under shared lighting, with reverse and side views. The game-scale sheet shows 128-pixel and 96-pixel render previews. These are Blender renders, not Unity captures.

The paint follows mineral regions authored on each mesh. Each mesh has a unique packed UV layout. Built-in Imagegen painted those layouts from the approved concept reference. The base-color maps contain pigment variation rather than baked scene lighting. Preview outlines use silhouette rendering and are not extra mesh parts in the exports.

## Open the files

Each family folder contains an editable BLEND source, FBX and GLB exports, the used albedo, an unlit color guide, preview images and a manifest. The BLEND files pack their textures. External albedo files are included for import into other tools.

Each family's prompts folder contains the exact built-in Imagegen prompts. The separate history archive preserves the source paintings, guides and editable rejected constructions.

See validation.json for source reopen, export reimport and appearance checks.

## Production work

New silhouettes still require their own collision, volume and covering-sphere bakes during integration. This proof changes no asteroid spawning, density, damage, loot or fragmentation behavior. Non-asteroid debris remains a separate deferred arc.

The approved directions and review record are on [the locale asteroid arc](https://github.com/amindell11/astronomical-home/issues/1377).

