"""Scratch: add ValisMinion's local parts to its freshly derived source and save it. Never saves the base.

Run: blender -b --factory-startup --python-exit-code 1 -P build_talon.py
Reads art/ships/valisminion/concepts/valisminion-cuts.json:
- wings: local objects that use the base's LINKED wing meshes, scaled about a pivot (the scale sits in
  matrix_basis; the base part's SymmetryOrigin-relative transform sits in matrix_parent_inverse).
- talon: a local copy of the base aft prong, baked into SymmetryOrigin space, cut by a plane and tip-tinted.
- collider: convex hull of every visual part's evaluated geometry, at most COLLIDER_LIMIT triangles.
"""
import json
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

REPO = Path("D:/amind/git/agent-1")
sys.path.insert(0, str(REPO / "art/tools/ship"))
import ship_contract as contract
import ship_source

COLLIDER_LIMIT = 200
minion_dir = REPO / "art/ships/valisminion"
ship, source = contract.load_ship(minion_dir / "ship.json")
_, base_source = contract.load_ship(REPO / "art/ships/valis/ship.json")
cuts = json.loads((minion_dir / "concepts/valisminion-cuts.json").read_text(encoding="utf-8"))
wings, talon = cuts["wings"], cuts["talon"]


def everything(work):
    return work.verts[:] + work.edges[:] + work.faces[:]


def stored_side(mesh, matrix):
    """+1 or -1: which side of the X plane a part's stored half sits on, in SymmetryOrigin space."""
    return 1.0 if sum((matrix @ v.co).x for v in mesh.vertices) >= 0 else -1.0


def on_side(vector, side):
    return Vector((vector[0] * side, vector[1], vector[2]))


def mirror_like(target, origin):
    mirror = target.modifiers.new("Mirror", "MIRROR")
    mirror.mirror_object = origin
    mirror.use_clip = True
    mirror.use_mirror_merge = True
    mirror.merge_threshold = 0.0001


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


scene = ship_source.open_source(source)
origin = bpy.data.objects[contract.SYMMETRY_ORIGIN]
assert origin.library is not None, "SymmetryOrigin must be the linked base root"
names = wings["parts"] + [talon["part"]]
with bpy.data.libraries.load(str(base_source), link=True, relative=True) as (_, linked):
    linked.objects = list(names)
base = dict(zip(names, linked.objects))
origin_world = ship_source.stored_world(origin)
hull_collection = bpy.data.collections["role.hull"]
summary = {}

for name in wings["parts"]:
    source_part = base[name]
    relative = origin_world.inverted() @ ship_source.stored_world(source_part)
    local_pivot = relative.inverted() @ on_side(wings["pivot"], stored_side(source_part.data, relative))
    part = bpy.data.objects.new(name, source_part.data)
    part.parent = origin
    part.matrix_parent_inverse = relative
    part.matrix_basis = (Matrix.Translation(local_pivot) @ Matrix.Scale(wings["scale"], 4)
                         @ Matrix.Translation(-local_pivot))
    mirror_like(part, origin)
    hull_collection.objects.link(part)
summary["wings"] = {"scale": wings["scale"], "meshes": "linked"}

source_part = base[talon["part"]]
work = bmesh.new()
work.from_mesh(source_part.data)
relative = origin_world.inverted() @ ship_source.stored_world(source_part)
side = stored_side(source_part.data, relative)
work.transform(relative)
if relative.determinant() < 0:
    bmesh.ops.reverse_faces(work, faces=work.faces[:])
cut, tip = talon["cut"], talon["tip"]
result = bmesh.ops.bisect_plane(work, geom=everything(work), plane_co=on_side(cut["co"], side),
                                plane_no=on_side(cut["no"], side).normalized(), clear_outer=True)
bmesh.ops.holes_fill(work, edges=[g for g in result["geom_cut"] if isinstance(g, bmesh.types.BMEdge)], sides=0)
tip_co, tip_no = on_side(tip["co"], side), on_side(tip["no"], side).normalized()
bmesh.ops.bisect_plane(work, geom=everything(work), plane_co=tip_co, plane_no=tip_no)
materials = [m for m in source_part.data.materials] + [bpy.data.materials[tip["material"]]]
assert all(m.library is not None for m in materials), "talon materials must be the linked base materials"
for face in work.faces:
    if (face.calc_center_median() - tip_co).dot(tip_no) > 0:
        face.material_index = len(materials) - 1
bmesh.ops.triangulate(work, faces=[f for f in work.faces if len(f.verts) > 4])
mesh = bpy.data.meshes.new(talon["name"])
for material in materials:
    mesh.materials.append(material)
work.to_mesh(mesh)
work.free()
part = bpy.data.objects.new(talon["name"], mesh)
part.parent = origin
mirror_like(part, origin)
hull_collection.objects.link(part)
summary["talon"] = {"faces": len(mesh.polygons), "uv": [u.name for u in mesh.uv_layers],
                    "materials": [m.name for m in materials]}

collider_name = f"{ship['name']} collider"
collider = bpy.data.objects.new(collider_name, bpy.data.meshes.new(collider_name))
collider.parent = origin
bpy.data.collections["role.collider"].objects.link(collider)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_mainfile()

scene = ship_source.open_source(source)
roles = ship_source.Roles(scene)
evaluation = ship_source.Evaluation(scene)
points = []
for name in sorted(n for role in contract.VISUAL_ROLES for n in roles.members.get(role, ())
                   if scene.objects[n].type == "MESH"):
    evaluated = evaluation.mesh(name)
    co = ship_source.mesh_arrays(evaluated)[0]
    bpy.data.meshes.remove(evaluated)
    matrix = np.array(evaluation.to_space(name), np.float64)
    points.append(co @ matrix[:3, :3].T + matrix[:3, 3])
hull_mesh = hull([tuple(p) for p in np.concatenate(points)])
hull_verts = [tuple(v.co) for v in hull_mesh.vertices]
hull_faces = [tuple(p.vertices) for p in hull_mesh.polygons]

scene = ship_source.open_source(source)
collider = bpy.data.objects[collider_name]
work = bmesh.new()
made = [work.verts.new(v) for v in hull_verts]
for face in hull_faces:
    work.faces.new([made[i] for i in face])
bmesh.ops.recalc_face_normals(work, faces=work.faces[:])
work.to_mesh(collider.data)
work.free()
summary["collider"] = {"faces": len(collider.data.polygons)}
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_mainfile()
print("TALON", json.dumps(summary), file=sys.stderr)
