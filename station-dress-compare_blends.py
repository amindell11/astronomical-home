"""Compare mesh objects between two station .blend files: name, key, vertex count and world bounds.

blender -b --factory-startup --python compare_blends.py -- <a.blend> <b.blend>
"""
import sys

import bpy
from mathutils import Vector

a_path, b_path = sys.argv[sys.argv.index("--") + 1:][:2]


def facts(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    out = {}
    for ob in bpy.data.objects:
        if ob.type != "MESH":
            continue
        if any(c.name == "Reference" or c.name.startswith("Ink preview") for c in ob.users_collection):
            continue
        corners = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
        hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
        out[ob.name] = (ob.get("key"), len(ob.data.vertices), len(ob.data.polygons), tuple(round(v, 3) for v in lo),
                        tuple(round(v, 3) for v in hi))
    return out


a, b = facts(a_path), facts(b_path)
only_a = sorted(set(a) - set(b))
only_b = sorted(set(b) - set(a))
diff = [n for n in sorted(set(a) & set(b)) if a[n] != b[n]]
print(f"COMPARE meshes a={len(a)} b={len(b)} only_a={len(only_a)} only_b={len(only_b)} differ={len(diff)}")
for n in only_a[:10]:
    print("ONLY_A", n, a[n])
for n in only_b[:10]:
    print("ONLY_B", n, b[n])
for n in diff[:10]:
    print("DIFF", n, a[n], b[n])
