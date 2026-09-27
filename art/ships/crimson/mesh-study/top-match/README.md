# Crimson top-reference revision

Open `CrimsonTopMatch.blend`. This separate revision leaves the earlier
`../CrimsonMesh.blend` and any edits to it untouched.

The original top drawing controls the outline, cockpit footprint, split prow,
shoulder panels and engine spacing. The hull crests immediately behind the
cockpit, falling toward the nose, tail and outer edges. Upper and lower wing
volumes remain separate, with a slight nose-down slope. Port/starboard pairs
retain Mirror modifiers; the small asymmetries in the drawing are averaged.

There are 36 editable mesh objects, no materials and no textures. Each object is
a closed mesh; parts overlap at their joints rather than forming one fused hull.
The FBX contains only those authored mesh parts, with modifiers evaluated.

Expand `90 References` and use the eye beside `REF TOP` to show the packed top
image. The collection stays enabled in the viewport. Numpad 7 gives an aligned
orthographic top view; the image's nose points along Blender +Y. The selected
profile concept is a separate packed reference for depth only.

`build_top_mesh.py` reconstructs and **overwrites this revision**, including its
FBX, so do not run it over hand edits. It never reads or writes the earlier blend.
The script checks every evaluated part for nonmanifold edges and zero-area faces.
Inspection renders are written to `results/crimson-top-match/` at the repo root.
