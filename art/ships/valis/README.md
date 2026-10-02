# Valis

`Valis.blend` is the approved editable source: 36 mesh parts with live bilateral symmetry, forward -Y and up +Z. `mesh-approval.json` locks the geometry. Paint is independent on the two sides: the atlas's upper half carries right-side charts and the lower half the mirrored left-side charts.

`layers/Shadow.png`, `Light.png`, and `Ink.png` are independent grayscale masks, packed into the Blender file. `Valis-Paint-Layers.ora` and `UV-guide.png` support hand edits. Save external image edits, repack them, and save a new Blender revision; generators must not overwrite artist edits. `paint-settings.json` records shadow 1.02, light 0.20, and ink 1.50. Base coat nodes remain independently recolorable; `palettes.json` preserves all three palettes, with jade + iris the default.

Valis is a separate playable roster entry with Ship 3's mass, engine and weapon settings. The saved Unity hull uses seven palette regions plus a contour submesh. Its illustrated shader's separate-paint variant preserves damage tint and lighting; iris emission gives a small bloom through the existing gameplay profile. Palette selection follows in [#823](https://github.com/amindell11/astronomical-home/issues/823).

Authoring helpers and validation tests are preserved on `codex/valis-paint-authoring-archive` at commit `2d433ca2e6790ab6f767198a59f7d6d3657405ad`. Construction experiments and review evidence live separately on `evidence/valis-geometry`.
