"""Scratch: replace ValisMinion's placeholder prong mesh with the Rooted Wedges design, refit the collider.

Run: blender -b --factory-startup --python-exit-code 1 -P build_wedge.py -- [json overrides]
The prong object, its parent and its live Mirror modifier are kept; only its mesh data and the
collider's mesh are replaced. Coordinates are SymmetryOrigin space (+Y nose, -Y aft, +Z up), +X side.
"""
import json
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np

REPO = Path("D:/amind/git/agent-1")
sys.path.insert(0, str(REPO / "art/tools/ship"))
import ship_contract as contract
import ship_source

SOURCE = REPO / "art/ships/valisminion/ValisMinion.blend"
PRONG = "50 minion aft prongs"
COLLIDER = "ValisMinion collider"
COLLIDER_LIMIT = 200

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
K = {
    # stations along the wedge: y, inner x, outer x, centre z, height
    "root": [-0.55, 0.52, 0.60, 0.30, 0.10],
    "shoulder": [-1.45, 0.40, 0.95, 0.38, 0.30],
    "waist": [-2.15, 0.55, 0.90, 0.48, 0.24],
    "band": [-2.50, 0.60, 0.85, 0.52, 0.20],
    "tip": [-3.45, 0.72, 0.72, 0.60, 0.0],
    "ridge": 0.42,  # ridge position across the width, 0 inner .. 1 outer
}
if argv:
    K.update(json.loads(argv[0]))


def ring(station):
    y, xi, xo, zc, h = station
    xr = xi + K["ridge"] * (xo - xi)
    # inner top, ridge top, outer edge, ridge bottom, inner bottom
    return [(xi, y, zc + 0.30 * h), (xr, y, zc + 0.5 * h), (xo, y, zc - 0.05 * h),
            (xr, y, zc - 0.5 * h), (xi, y, zc - 0.30 * h)]


IVORY, GRAPHITE, LAVENDER = 0, 1, 2
# per ring-side index i (edge i -> i+1): material by segment
TOP_INNER, TOP_OUTER, BOTTOM_OUTER, BOTTOM_INNER, INNER = range(5)
SIDE_MATERIAL = {TOP_INNER: IVORY, TOP_OUTER: GRAPHITE, BOTTOM_OUTER: GRAPHITE,
                 BOTTOM_INNER: GRAPHITE, INNER: GRAPHITE}


def wedge():
    stations = [K["root"], K["shoulder"], K["waist"], K["band"]]
    rings = [ring(s) for s in stations]
    verts = [v for r in rings for v in r]
    faces, mats = [], []
    root_point = len(verts)
    y, xi, xo, zc, _ = K["root"]
    verts.append((xi - 0.12, y + 0.35, zc))
    tip_point = len(verts)
    y, xi, _, zc, _ = K["tip"]
    verts.append((xi, y, zc))
    for i in range(5):
        j = (i + 1) % 5
        faces.append((root_point, j, i))
        mats.append(SIDE_MATERIAL[i])
    for r in range(len(rings) - 1):
        a, b = r * 5, (r + 1) * 5
        for i in range(5):
            j = (i + 1) % 5
            faces.append((a + i, a + j, b + j, b + i))
            mats.append(SIDE_MATERIAL[i])
    last = (len(rings) - 1) * 5
    for i in range(5):
        j = (i + 1) % 5
        faces.append((last + i, last + j, tip_point))
        mats.append(LAVENDER)
    return verts, faces, mats


def to_bmesh(verts, faces, mats=None):
    work = bmesh.new()
    made = [work.verts.new(v) for v in verts]
    for k, face in enumerate(faces):
        f = work.faces.new([made[i] for i in face])
        if mats:
            f.material_index = mats[k]
    bmesh.ops.recalc_face_normals(work, faces=work.faces[:])
    return work


def hull(points):
    while True:
        work = bmesh.new()
        for point in points:
            work.verts.new(point)
        bmesh.ops.convex_hull(work, input=work.verts[:])
        bmesh.ops.delete(work, geom=[v for v in work.verts if not v.link_faces], context="VERTS")
        bmesh.ops.triangulate(work, faces=work.faces[:])
        bmesh.ops.recalc_face_normals(work, faces=work.faces[:])
        mesh = bpy.data.meshes.new("hull")
        work.to_mesh(mesh)
        work.free()
        if len(mesh.polygons) <= COLLIDER_LIMIT:
            return mesh
        obj = bpy.data.objects.new("hull", mesh)
        bpy.context.scene.collection.objects.link(obj)
        decimate = obj.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = COLLIDER_LIMIT * 0.9 / len(mesh.polygons)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        reduced = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
        points = [tuple(v.co) for v in reduced.vertices]


verts, faces, mats = wedge()

# Collider points: every kept visual part except the old prong, plus the new wedge both sides.
scene = ship_source.open_source(SOURCE)
roles = ship_source.Roles(scene)
visual = sorted(n for role in contract.VISUAL_ROLES for n in roles.members.get(role, ())
                if scene.objects[n].type == "MESH" and n != PRONG)
evaluation = ship_source.Evaluation(scene)
points = []
for name in visual:
    mesh = evaluation.mesh(name)
    co = ship_source.mesh_arrays(mesh)[0]
    bpy.data.meshes.remove(mesh)
    matrix = np.array(evaluation.to_space(name), np.float64)
    points.append(co @ matrix[:3, :3].T + matrix[:3, 3])
plus = np.array(verts)
points += [plus, plus * np.array([-1.0, 1.0, 1.0])]
collider_mesh = hull([tuple(p) for p in np.concatenate(points)])
hull_verts = [tuple(v.co) for v in collider_mesh.vertices]
hull_faces = [tuple(p.vertices) for p in collider_mesh.polygons]

scene = ship_source.open_source(SOURCE)
prong = bpy.data.objects[PRONG]
assert prong.library is None and prong.parent.name == contract.SYMMETRY_ORIGIN
materials = [bpy.data.materials[n] for n in ("Ivory", "Graphite", "Lavender")]
assert all(m.library is not None for m in materials), "wedge materials must be the linked base materials"
prong.data.materials.clear()
for m in materials:
    prong.data.materials.append(m)
work = to_bmesh(verts, faces, mats)
work.to_mesh(prong.data)
work.free()

collider = bpy.data.objects[COLLIDER]
assert collider.library is None
work = to_bmesh(hull_verts, hull_faces)
work.to_mesh(collider.data)
work.free()

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_mainfile()
print("WEDGE", json.dumps({"faces": len(prong.data.polygons), "collider_faces": len(collider.data.polygons),
                           "knobs": K}), file=sys.stderr)
