"""Scratch: replace ValisMinion's placeholder prong mesh with Rooted Wedges (graphite blade, teal gem, lavender crystal), refit the collider.

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
    # top-view outlines (x, y) in SymmetryOrigin space, +X side
    "blade": [(0.48, -0.50), (0.64, -0.55), (0.72, -0.98), (0.97, -1.35), (0.90, -2.70),
              (0.74, -3.50), (0.60, -2.90), (0.57, -1.35), (0.45, -0.85)],
    "gem": [(0.68, -0.95), (0.90, -1.42), (0.84, -2.30), (0.74, -2.62), (0.64, -2.30), (0.62, -1.42)],
    "crystal": [(0.74, -2.66), (0.80, -2.96), (0.74, -3.30), (0.67, -2.96)],
    "z_front": [-0.55, 0.36], "z_back": [-3.50, 0.56],  # blade centre height along y
    "blade_half": 0.06, "gem_rise": 0.08, "crystal_rise": 0.07, "sink": 0.01,
}
if argv:
    K.update(json.loads(argv[0]))

IVORY, GRAPHITE, LAVENDER = 0, 1, 2


def zc(y):
    (y0, z0), (y1, z1) = K["z_front"], K["z_back"]
    return z0 + (y - y0) * (z1 - z0) / (y1 - y0)


def slab(outline, half):
    n = len(outline)
    verts = [(x, y, zc(y) + half) for x, y in outline] + [(x, y, zc(y) - half) for x, y in outline]
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    faces += [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    return verts, faces


def top_z(y):
    return zc(y) + K["blade_half"] - K["sink"]


def gem(outline, rise):
    """Hexagonal inset: pointed ends, a raised ridge between the two widest pairs."""
    base = [(x, y, top_z(y)) for x, y in outline]
    ridge = []
    for a, b in ((1, 5), (2, 4)):
        x, y = (outline[a][0] + outline[b][0]) / 2, (outline[a][1] + outline[b][1]) / 2
        ridge.append((x, y, top_z(y) + rise))
    r1, r2 = 6, 7
    faces = [(0, 1, r1), (0, r1, 5), (1, 2, r2, r1), (5, r1, r2, 4), (2, 3, r2), (3, 4, r2),
             (5, 4, 3, 2, 1, 0)]
    return base + ridge, faces


def crystal(outline, rise):
    """Diamond inset with one raised apex."""
    base = [(x, y, top_z(y)) for x, y in outline]
    cx = sum(x for x, _ in outline) / 4
    cy = sum(y for _, y in outline) / 4
    faces = [(i, (i + 1) % 4, 4) for i in range(4)] + [(3, 2, 1, 0)]
    return base + [(cx, cy, top_z(cy) + rise)], faces


def wedge():
    verts, faces, mats = [], [], []
    for (v, f), mat in ((slab(K["blade"], K["blade_half"]), GRAPHITE),
                        (gem(K["gem"], K["gem_rise"]), IVORY),
                        (crystal(K["crystal"], K["crystal_rise"]), LAVENDER)):
        offset = len(verts)
        verts += v
        faces += [tuple(i + offset for i in face) for face in f]
        mats += [mat] * len(f)
    return verts, faces, mats


def to_bmesh(verts, faces, mats=None):
    work = bmesh.new()
    made = [work.verts.new(v) for v in verts]
    for k, face in enumerate(faces):
        f = work.faces.new([made[i] for i in face])
        if mats:
            f.material_index = mats[k]
    bmesh.ops.triangulate(work, faces=[f for f in work.faces if len(f.verts) > 4])
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
