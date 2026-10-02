# Valis

`Valis.blend` is the approved editable geometry source: 36 distinct mesh parts with live bilateral Mirror modifiers. Forward is -Y, up is +Z. `mesh-approval.json` records the approved geometry fingerprint; materials, UVs and paint may change while the shape stays fixed. Start edits from this file, not the historical procedural generator.

`palettes.json` preserves the three approved palettes in linear sRGB. Jade + iris is the default. The hand-painted finish is in development, with brush/value masks and technical detail kept separate from the base palette for future recoloring.

Valis is a separate playable roster entry using Ship 3's chassis, handling and weapon loadout. The Unity prefab uses saved render and convex collision meshes, with local +Y forward and -Z toward the gameplay camera. The current Unity material pass is flat color blocking; it is not the finished painted asset. Integration and texturing scope: [#828](https://github.com/amindell11/astronomical-home/issues/828). Palette selection follows in [#823](https://github.com/amindell11/astronomical-home/issues/823).

Approved concept C, top view and eight-view turnaround are the shape authority. The source includes the user's shape edits and fitted interface cleanup. Review evidence and historical construction helpers remain separate in the local `results/valis-evidence/` checkout on `evidence/valis-geometry`, last preserved commit `b6c8ce05f8bd36e745f3c6732050e4c40f9db6f7`. Its README indexes the review package; publication is not yet complete.
