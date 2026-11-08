# Current asteroid review, four active locales

Twenty editable models, five each for Copper / turquoise (1), Illustrated blue / Chalk (3), Verdant Giant (6), and Blue Crescent / Ice (7). This is the current art review, not an approved production replacement.

Copper01 is the accepted source preserved byte-for-byte. Copper02–05 and Ice01–05 retain the preceding draft geometry. The ten Chalk and Verdant models are revised candidates. Violet / NebulaGlow is excluded following the owner's current ruling; its five models, including the accepted Violet prototype, remain preserved in the historical 25-model archive. Coral Sunset and Cyan Aurora remain outside this concept scope.

The owner selected “More thin, broken layers like the reference.” Chalk now uses unequal tapered fracture sheets, fan layers, a peeled lid, and a faulted flake. Cream coverage varies from substantial broken exposures to a narrow seam. The earlier solid Chalk shoulder is preserved separately as a rejected direction.

Verdant now varies the kind of mineral exposure: an interrupted crescent joined to intact stone, a narrow vein, two unequal windows, a mostly exposed mineral core, and a nearly sealed crust with a small green end seam. Its source components retain separate material roles for game review calibration.

Each model folder contains its editable Blender source, metadata, hero, native 150-by-110-pixel render, clay, side, and reverse views. Hidden construction volumes remain available. Original paint swatches and prompt provenance are shared under `paint`; active maps are packed into the sources. Accepted Copper has its original adjacent support maps as well. Its source status is governed by this README rather than historical per-prototype notes.

Internal drawing is retained mesh geometry. Blender's outer contour uses Freestyle and is render-only. The separate scratch Unity review export uses the existing Drawn Surface and Drawn Contour shaders, with an independent contour mesh carrying joined normals. No source surface normals or shared shaders are changed.

The game review uses the real Observer camera, locale backdrops and a ship for scale, with neutral review lighting: a white 1.15 directional key and neutral 0.55 ambient probe. Cloned preview materials use neutral shadow tint, softened shadow response, and a 1.4 linear gain only for declared `verdant_dark` material roles. Cream, ochre and ink keep their original color values. The contour clone uses a uniform four-pixel width. These are review settings, not the locales' shipping lighting. Original PNGs and Blender colors are unchanged by game calibration.

Source checks report closed, nondegenerate solids, one connected primary host per model, directional UVs, packed images, and matching hashes. Intentional open drawing ribbons are reported separately. Verdant02's single zero-area Boolean triangle was dissolved without changing its visible vein. These checks do not establish aesthetic approval: forms and cream boundaries remain cleaner and simpler than the original concepts.

`catalog.json` declares all source and preview paths. `validation-summary.json` summarizes the fresh reopen checks. `deliverable-files.json` is the curated file list and `file-hashes.json` records exact content hashes. The family sheets and four-row overview are Blender previews. Scratch game exports and actual game captures are separate evidence, not production spawn routing or runtime integration. No debris, production scene changes, PR or merge is included.

Authoring scripts use adjacent paint and local output paths. `build_chalk01.py` and `build_chalk_companion.py -- 2` through `-- 5` build the Chalk candidates; `build_model.py -- verdant N` builds the revised Verdant models. Use Blender background mode with `--python-exit-code 1`. Accepted Copper01 is supplied as an immutable source, not a regeneration target.
