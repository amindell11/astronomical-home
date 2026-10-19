# Bulwark Unity texture review

The shared palette-region shader now receives the owner's coat below shadow/light/ink. RegionWeights0 green is the exact BaseRegion grayscale; red is its complement. Original Shadow, Light, Ink and DetailEmission files were copied byte-for-byte. Blender source and geometry/UV/material assignments were not edited. Live Blender was clean after capture.

- lineup-top.png: current Crimson, Nightshade, Bulwark, Vanguard, Valis visuals; equal display length, not approved gameplay scale.
- bulwark-three-quarter.png: actual Unity shader close-up.
- No bloom, fog, particles or gameplay scripts in this asset comparison. Common directional light; current fleet materials.
- Valis's rigged mesh is baked for the still and oriented into the same presentation plane; no source change.
- unity-verification.json: all material assignments resolve, shader compiles without errors, masks are linear/uncompressed/mipmapped.
- package-verification.json and source-preservation.json: coat packing and source preservation evidence.
- export/: exporter manifest and FBX from the unchanged saved source; export validation passed.

Reproduction: run package.py from this checkout, then bind.cs via the leased Unity editor's eval_file. cleanup.cs removes unused exported-source materials. capture.cs and closeup.cs create isolated preview scenes and restore fog afterward. The generic painter bundle must not overwrite this adapted coat/emission binding. materials.json records the saved Blender node colors used for packaging.

No gameplay chassis/catalog integration, flight scale, breakup or bloom tuning was attempted. No gameplay-code tests were warranted by this asset-only change. Review and merge remain pending.
