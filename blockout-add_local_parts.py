"""Scratch: add a minion's local parts to its derived source and save it. Never saves a base.

Run: blender -b --factory-startup --python-exit-code 1 -P add_local_parts.py -- <base lowercase name>
- ValisMinion only: `50 minion aft prongs`, the approved placeholder slab (render2.py prong(),
  {"part": "50 aft split prongs", "length": 0.6}) for +X only, a live Mirror across SymmetryOrigin,
  the base Ivory material, in local role.hull.
- Every minion: `<Name> collider`, the convex hull of the kept visual parts' evaluated geometry
  (prongs included) in SymmetryOrigin space, decimated to at most 200 triangles, in local role.collider.
Both are parented to the linked SymmetryOrigin with identity transforms, so local space is origin space.
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

COLLIDER_LIMIT = 200
PRONG_NAME = "50 minion aft prongs"

base = sys.argv[sys.argv.index("--") + 1]
minion_dir = REPO / "art/ships" / f"{base}minion"
ship, source = contract.load_ship(minion_dir / "ship.json")
_, base_source = contract.load_ship(REPO / "art/ships" / base / "ship.json")
cuts = json.loads((minion_dir / "concepts" / f"{base}minion-cuts.json").read_text(encoding="utf-8"))


def part_points(evaluation, name):
    mesh = evaluation.mesh(name)
    co = ship_source.mesh_arrays(mesh)[0]
    bpy.data.meshes.remove(mesh)
    matrix = np.array(evaluation.to_space(name), np.float64)
    return co @ matrix[:3, :3].T + matrix[:3, 3]


def prong(evaluation, spec):
    """render2.py prong(), +X side only: a tapered slab along the principal top-view axis of the part's +X half."""
    p = part_points(evaluation, spec["part"])
    side = p[p[:, 0] > 0.05]
    xy = side[:, :2]
    mean = xy.mean(0)
    _, _, vt = np.linalg.svd(xy - mean)
    axis = vt[0] if vt[0][1] < 0 else -vt[0]
    t = (xy - mean) @ axis
    t0, t1 = np.percentile(t, 1), np.percentile(t, 99)
    t1 = t0 + (t1 - t0) * spec.get("length", 1.0)
    normal = np.array([-axis[1], axis[0]])
    root = side[t < t0 + 0.25 * (t1 - t0)]
    w0 = float(np.ptp((root[:, :2] - mean) @ normal)) * spec.get("width", 0.7)
    z = float(np.median(side[:, 2]))
    h = spec.get("thickness", 0.22)
    tip_w = w0 * spec.get("taper", 0.25)
    verts = []
    for tt, w, hh in ((t0, w0, h), (t1, tip_w, h * 0.5)):
        c = mean + axis * tt
        for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            q = c + normal * (sx * w / 2)
            verts.append((float(q[0]), float(q[1]), z + sz * hh / 2))
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return verts, faces


def hull(points):
    """Convex hull triangles of the points; decimated and re-hulled until at most COLLIDER_LIMIT faces."""
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


def closed_outward(verts, faces):
    work = bmesh.new()
    made = [work.verts.new(v) for v in verts]
    for face in faces:
        work.faces.new([made[i] for i in face])
    bmesh.ops.recalc_face_normals(work, faces=work.faces[:])
    return work


prong_part = None
if "prong" in cuts:
    base_scene = ship_source.open_source(base_source)
    prong_part = prong(ship_source.Evaluation(base_scene), cuts["prong"])

scene = ship_source.open_source(source)
roles = ship_source.Roles(scene)
visual = sorted(n for role in contract.VISUAL_ROLES for n in roles.members.get(role, ())
                if scene.objects[n].type == "MESH")
evaluation = ship_source.Evaluation(scene)
points = [part_points(evaluation, name) for name in visual]
if prong_part:
    plus = np.array(prong_part[0])
    points += [plus, plus * np.array([-1.0, 1.0, 1.0])]
collider_mesh = hull([tuple(p) for p in np.concatenate(points)])
hull_verts = [tuple(v.co) for v in collider_mesh.vertices]
hull_faces = [tuple(p.vertices) for p in collider_mesh.polygons]

scene = ship_source.open_source(source)
origin = bpy.data.objects[contract.SYMMETRY_ORIGIN]
assert origin.library is not None, "SymmetryOrigin must be the linked base root"
summary = {"visual_parts": len(visual)}

if prong_part:
    material = bpy.data.materials.get("Ivory")
    if material is None:
        with bpy.data.libraries.load(str(base_source), link=True, relative=True) as (_, linked):
            linked.materials = ["Ivory"]
        material = linked.materials[0]
    mesh = bpy.data.meshes.new(PRONG_NAME)
    work = closed_outward(*prong_part)
    work.to_mesh(mesh)
    work.free()
    mesh.materials.append(material)
    obj = bpy.data.objects.new(PRONG_NAME, mesh)
    obj.parent = origin
    mirror = obj.modifiers.new("Mirror", "MIRROR")
    mirror.mirror_object = origin
    bpy.data.collections["role.hull"].objects.link(obj)
    summary["prong"] = {"material": f"{material.name} ({'linked' if material.library else 'local'})",
                        "vertices": [[round(c, 4) for c in v] for v in prong_part[0]]}

collider_name = f"{ship['name']} collider"
mesh = bpy.data.meshes.new(collider_name)
work = closed_outward(hull_verts, hull_faces)
work.to_mesh(mesh)
work.free()
collider = bpy.data.objects.new(collider_name, mesh)
collider.parent = origin
bpy.data.collections["role.collider"].objects.link(collider)
summary["collider"] = {"vertices": len(mesh.vertices), "faces": len(mesh.polygons)}

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_mainfile()
print("LOCAL_PARTS", json.dumps(summary), file=sys.stderr)
