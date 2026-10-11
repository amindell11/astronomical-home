"""4b-1 edit 1: wrap the scaled, tilted origin under an identity SymmetryOrigin and turn the nose to +Y.

blender -b --factory-startup --python-exit-code 1 -P edit_1_wrap_turn.py -- <Valis.blend>
Saves the file in place. No part's transform, mesh or modifier stack changes except each
Mirror's mirror_object, which moves to the new root (the mirror plane stays world x = 0).
"""
import math
import sys

import bpy
from mathutils import Matrix

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.open_mainfile(filepath=path, load_ui=True, use_scripts=False)
scene = bpy.context.scene
assert scene.name == "Valis geometry", scene.name
assert "SymmetryOrigin" not in bpy.data.objects

old = bpy.data.objects["Valis symmetry origin"]
assert old.type == "EMPTY" and old.parent is None
assert old.matrix_parent_inverse == Matrix.Identity(4)
assert tuple(old.location) == (0.0, 0.0, 0.0)
assert old.rotation_mode == "XYZ" and old.rotation_euler.y == 0.0 and old.rotation_euler.z == 0.0

root = bpy.data.objects.new("SymmetryOrigin", None)
root.empty_display_type = old.empty_display_type
root.empty_display_size = old.empty_display_size
for collection in old.users_collection:
    collection.objects.link(root)

old.parent = root
old.matrix_parent_inverse = Matrix.Identity(4)
old.rotation_euler.z = math.pi

retargeted = 0
for obj in scene.objects:
    for modifier in obj.modifiers:
        if modifier.type == "MIRROR":
            assert modifier.mirror_object == old, (obj.name, modifier.mirror_object)
            modifier.mirror_object = root
            retargeted += 1
assert retargeted == 30, retargeted

bpy.ops.wm.save_mainfile(filepath=path, compress=True)
print(f"EDIT1 wrapped under SymmetryOrigin, turned 180 deg about Z, retargeted {retargeted} mirrors", file=sys.stderr)
