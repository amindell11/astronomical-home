"""4b-1 edit 2: swap the paint atlas halves in the source and rename the paint UV layer.

blender -b --factory-startup --python-exit-code 1 -P edit_2_uv_swap.py -- <Valis.blend>
Per visual part: native charts move from the upper atlas half to the lower (V - 0.5), the Mirror's
offset_v flips from -0.5 to +0.5, and ValisPaintUV is renamed PaintUV. Mask images are untouched.
Saves the file in place.
"""
import sys

import bpy
import numpy as np

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.open_mainfile(filepath=path, load_ui=True, use_scripts=False)
scene = bpy.context.scene
reference = bpy.data.collections["Reference - joined preview"]
parts = [o for o in scene.objects if o.type == "MESH" and o.name not in reference.all_objects]
assert len(parts) == 30, len(parts)

for obj in parts:
    mesh = obj.data
    assert mesh.users == 1, obj.name
    assert [layer.name for layer in mesh.uv_layers] == ["ValisPaintUV"], obj.name
    layer = mesh.uv_layers["ValisPaintUV"]
    uv = np.empty(len(layer.uv) * 2, np.float32)
    layer.uv.foreach_get("vector", uv)
    v = uv[1::2]
    assert v.min() >= 0.5 and v.max() <= 1.0, (obj.name, float(v.min()), float(v.max()))
    uv[1::2] = v - np.float32(0.5)
    layer.uv.foreach_set("vector", uv)
    layer.name = "PaintUV"
    (mirror,) = [m for m in obj.modifiers if m.type == "MIRROR"]
    assert (mirror.offset_u, mirror.offset_v) == (0.0, -0.5), obj.name
    assert not mirror.use_mirror_u and not mirror.use_mirror_v, obj.name
    mirror.offset_v = 0.5
    mesh.update()

bpy.ops.wm.save_mainfile(filepath=path, compress=True)
print(f"EDIT2 swapped atlas halves and renamed PaintUV on {len(parts)} parts", file=sys.stderr)
