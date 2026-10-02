# Valis

`Valis.blend` is the approved editable source: 36 mesh parts with live bilateral symmetry, forward -Y and up +Z. `mesh-approval.json` locks the geometry. Paint is independent on the two sides: the atlas's upper half carries right-side charts and the lower half the mirrored left-side charts.

`layers/Shadow.png`, `Light.png`, and `Ink.png` are independent grayscale masks, packed into the Blender file. `Valis-Paint-Layers.ora` and `UV-guide.png` support hand edits. Save external image edits, repack them, and save a new Blender revision; generators must not overwrite artist edits. `paint-settings.json` records shadow 1.02, light 0.20, and ink 1.50. Base coat nodes remain independently recolorable; `palettes.json` preserves all three palettes, with jade + iris the default.

Valis is a separate playable roster entry with Ship 3's mass, engine and weapon settings. The saved Unity hull uses seven palette regions plus a contour submesh. Its illustrated shader's separate-paint variant preserves damage tint and lighting; iris emission gives a small bloom through the existing gameplay profile. Palette selection follows in [#823](https://github.com/amindell11/astronomical-home/issues/823).

After an artist revision, export the evaluated UVs from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --python art/ships/valis/tools/export_paint_uv.py -- --source art/ships/valis/Valis.blend --output results/valis-integration/paint-meshes.json
```

Copy edited masks to `Assets/Visuals/Ships/Valis/Paint/`, then run `tools/ApplyValisPaint.cs` with the coordinated Unity CLI `run_script` command, entry `ApplyValisPaint.Main`. Pass the absolute exported JSON path as its required string argument via `--args '["<absolute-export-path>"]'`, and target the worktree's `src/Asteroids3D` with `--project-path`. The exporter owns the part/vertex/UV layout; the updater consumes its output, verifies saved vertices before updating UVs, imports masks as linear data and applies settings to all palettes. `Accent emission.png` is the saved uniform emission mask; purple coverage comes from the iris submesh.

Construction experiments and review evidence live separately on `evidence/valis-geometry`.
