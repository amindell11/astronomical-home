# Bulwark Blender finish

Source commit: `e6fe13b9fde5229d4c2387c2f191f71e6495b992`. Base coat approved by the owner; owner explicitly selected finishing Blender textures first and deferring flight/UV prerequisites.

The base-coat region now blends before lighting, so white and plum receive consistent shading. New 2048-square grayscale FinishShadow and FinishLight masks use broad face shading and deterministic local occlusion with six-pixel UV padding. The two UV halves match. There is no automatic wear, stipple, noise or chip layer. Original canopy masks and ink are retained. Canisters use neutral metal with restrained highlights; glass uses a soft reflection.

Fresh-file verification confirms every object's geometry, transforms, modifiers, UVs and material assignments is unchanged. All original image pixel hashes and packed-byte hashes are unchanged. The two new masks are packed with relative external paths. `verification.json` records the exact BaseRegion hash and reload result. No Unity code changes or Unity tests were needed.

Review final-front.png, final-top.png, final-side.png and final-underside.png for coverage; game-scale-top.png shows the small top view. These are Blender lighting previews, not an in-game approval. The old geometry lock, UV density ratio 2.294:1 versus the 2:1 limit, and existing ship-check findings remain deferred; gameplay integration and breakup are pending.

Reproduction: build_fields.py and package_fields.py generate masks from the prior source; finish_materials.py and apply.py apply the pass once to source commit 6dcba140. render_saved.py renders the saved result. before-data.json and after-data.json record preservation evidence.
