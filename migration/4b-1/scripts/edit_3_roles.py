"""4b-1 edit 3: declare Valis's roles, add today's collider, and ignore the joined previews.

blender -b --factory-startup --python-exit-code 1 -P edit_3_roles.py -- <Valis.blend> <Valis collider.asset>
- Export roles/role.hull: all 30 visual parts. role.move.{upper,lower}_wing.L/.R: the parts under
  the 'Upper wings' / 'Lower wings' empties, each linked into both halves of its pair.
- role.debris.<id>: today's 14 approved breakup pieces (evidence/valis-breakup, v03 grouping).
- role.collider: 'Valis collider', today's 44-triangle footprint inverse-mapped from Unity space
  (unity = (blender_old.x, -blender_old.y, -blender_old.z) - centre, times scale), then turned
  with the ship. Parented to SymmetryOrigin with an identity transform.
- ignore: holds the 'Reference - joined preview' collection, which keeps its viewport hide.
Saves the file in place.
"""
import re
import struct
import sys

import bpy
from mathutils import Matrix
import numpy as np

path, asset = sys.argv[sys.argv.index("--") + 1:]
SCALE = 0.228625789
CENTRE_Y = -0.335398436
DEBRIS = {
    "fuselage_canopy": (1, 2, 3, 4, 5, 6, 7, 8),
    "forward_outrigger.L": (40, 41, 42), "forward_outrigger.R": (40, 41, 42),
    "aft_prong.L": (50, 51, 52, 53), "aft_prong.R": (50, 51, 52, 53),
    "engine_spine": (60, 61, 62, 64),
    "upper_wing.L": (10, 11, 12, 70, 71), "upper_wing.R": (10, 11, 12, 70, 71),
    "upper_spar.L": (13,), "upper_spar.R": (13,),
    "lower_blade.L": (20, 21), "lower_blade.R": (20, 21),
    "lower_inner_spar.L": (22,), "lower_inner_spar.R": (22,),
}
MOVE = {"upper_wing": "Upper wings", "lower_wing": "Lower wings"}


def unity_collider(asset_path):
    text = open(asset_path, encoding="utf-8").read()
    field = lambda name: re.search(rf"^\s*{name}: (\S+)\s*$", text, re.M).group(1)
    assert field("m_IndexFormat") == "0" and int(field("indexCount")) == 132
    count = int(field("m_VertexCount"))
    data = bytes.fromhex(field("_typelessdata"))
    assert count == 24 and len(data) == count * 24 and int(field("m_DataSize")) == len(data)
    channels = re.findall(r"- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)", text)
    assert channels[0] == ("0", "0", "0", "3") and channels[1] == ("0", "12", "0", "3"), channels[:2]
    assert all(c[3] == "0" for c in channels[2:])
    positions = [struct.unpack_from("<3f", data, i * 24) for i in range(count)]
    indices = struct.unpack(f"<{132}H", bytes.fromhex(field("m_IndexBuffer")))
    return positions, [indices[i:i + 3] for i in range(0, len(indices), 3)]


bpy.ops.wm.open_mainfile(filepath=path, load_ui=True, use_scripts=False)
scene = bpy.context.scene
root = bpy.data.objects["SymmetryOrigin"]
reference = bpy.data.collections["Reference - joined preview"]
parts = sorted((o for o in scene.objects if o.type == "MESH" and o.name not in reference.all_objects),
               key=lambda o: o.name)
assert len(parts) == 30
by_id = {}
for obj in parts:
    by_id.setdefault(int(obj.name[:2]), []).append(obj)
assert not any(name.startswith("role.") or name == "ignore" for name in bpy.data.collections.keys())


def collection(name, parent):
    created = bpy.data.collections.new(name)
    parent.children.link(created)
    return created


def ancestors(obj):
    names = []
    while obj.parent:
        obj = obj.parent
        names.append(obj.name)
    return names


export = collection("Export roles", scene.collection)
hull = collection("role.hull", export)
for obj in parts:
    hull.objects.link(obj)

positions, triangles = unity_collider(asset)
points = [(-x / SCALE, y / SCALE + CENTRE_Y, -z / SCALE) for x, y, z in positions]
mesh = bpy.data.meshes.new("Valis convex footprint")
mesh.from_pydata(points, [], triangles)
mesh.validate()
co = np.array(points)
volume = sum(float(np.dot(co[a], np.cross(co[b], co[c]))) for a, b, c in triangles)
if volume < 0:
    mesh.flip_normals()
assert len(mesh.vertices) == 24 and len(mesh.polygons) == 44
collider = bpy.data.objects.new("Valis collider", mesh)
collider.parent = root
collider.matrix_parent_inverse = Matrix.Identity(4)
collider.display_type = "WIRE"
collider.hide_render = True
collection("role.collider", export).objects.link(collider)

move_counts = {}
for ident, empty in MOVE.items():
    members = [obj for obj in parts if empty in ancestors(obj)]
    move_counts[ident] = [obj.name for obj in members]
    for side in ("L", "R"):
        group = collection(f"role.move.{ident}.{side}", export)
        for obj in members:
            group.objects.link(obj)

covered = []
for ident, ids in DEBRIS.items():
    group = collection(f"role.debris.{ident}", scene.collection)
    for part_id in ids:
        for obj in by_id[part_id]:
            group.objects.link(obj)
            covered.append(obj.name)
whole = [n for i, ids in DEBRIS.items() if not i.endswith(".R") for p in ids for n in (o.name for o in by_id[p])]
assert sorted(whole) == [o.name for o in parts], "every part in exactly one debris piece"

ignore = collection("ignore", scene.collection)
layer = bpy.context.view_layer.layer_collection.children[reference.name]
hidden = layer.hide_viewport
scene.collection.children.unlink(reference)
ignore.children.link(reference)
bpy.context.view_layer.layer_collection.children["ignore"].children[reference.name].hide_viewport = hidden

bpy.ops.wm.save_mainfile(filepath=path, compress=True)
print(f"EDIT3 hull={len(parts)} collider_tris={len(mesh.polygons)} flipped={volume < 0} "
      f"move={ {k: len(v) for k, v in move_counts.items()} } move_parts={move_counts} "
      f"debris={len(DEBRIS)} ignore_hidden={hidden}", file=sys.stderr)
