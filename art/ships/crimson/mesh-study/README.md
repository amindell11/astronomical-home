# Crimson editable mesh

Open `CrimsonMesh.blend` to edit the untextured starting mesh. It opens in a solid
three-quarter view with the upper wing selected. Collections separate the hull,
upper wings, lower wings, fins/spars, and engines. Paired parts retain their Mirror
modifiers; the canopy retains one subdivision level over its editable cage.

The hidden reference collection holds the original AI mesh, original top drawing,
and the selected profile concept. Reference images are packed. The AI mesh is only
a rough guide and is excluded from export. New ship parts have no material slots,
textures, UV layouts, colliders, or gameplay components.

`CrimsonMesh.fbx` is a mesh-only export for interchange. Blender coordinates are
X across the wings, +Y toward the nose, +Z toward the canopy, with a 5.5-degree
nose-down pitch. The FBX uses Blender's standard -Z-forward/Y-up export conversion.

To export your edits, run Blender in background mode with `--python export_mesh.py`.
This reads the saved blend and exports only ship collections 01–05; it never saves
over your blend. `build_mesh.py` reconstructs the initial draft and **overwrites the
blend**, so do not run it over subsequent hand edits. It also renders top, side,
front, three-quarter, and underside inspection images in `results/crimson-mesh/`.

The build checks every evaluated part for nonmanifold edges and zero-area faces.
`mesh-manifest.json` records the initial control-mesh counts and reference hash;
`export-manifest.json` describes the latest export.
