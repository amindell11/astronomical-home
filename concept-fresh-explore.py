"""Scratch whole-ship explorer: render variants of a ship made by hiding, reshaping and clipping its own parts.
Nothing is saved. Ops act on the +X half of a part in SymmetryOrigin space and are mirrored back.

Run: blender -b --factory-startup --python-exit-code 1 -P explore.py -- <source.blend> <variants.json> <out> <size>
Variant: {"omit": [parts], "affine": [{"parts", "pivot", "scale", "rot_z", "move"}],
          "clip": [{"parts", "co", "no", "cap"}]}   clip removes the side `no` points to; cap = material name or null
"""
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, r"D:/amind/git/agent-1/art/tools/ship")
import bmesh
import bpy
from mathutils import Matrix, Vector

import ship_contract as contract
import ship_render as R
import ship_source

argv = sys.argv[sys.argv.index("--") + 1:]
src, variants, out, size = Path(argv[0]), json.loads(Path(argv[1]).read_text()), Path(argv[2]), int(argv[3])
out.mkdir(parents=True, exist_ok=True)
source_scene = ship_source.open_source(src)
roles = ship_source.Roles(source_scene)
visual = sorted(n for role in contract.VISUAL_ROLES for n in roles.members.get(role, ())
                if source_scene.objects[n].type == "MESH")
evaluation, bounds = R.prepare(src)
space = evaluation.space


def view(az, el):
    a, e = math.radians(az), math.radians(el)
    return ((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)), (0, 0, 1))


views = {"top": R.ORTHO["top"], "three": view(-45, 40), "side": R.ORTHO["side"]}


def half(name):
    mesh = evaluation.mesh(name)
    work = bmesh.new()
    work.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)
    work.transform(Matrix(evaluation.to_space(name)))
    bmesh.ops.bisect_plane(work, geom=work.verts[:] + work.edges[:] + work.faces[:], plane_co=(0, 0, 0),
                           plane_no=(-1, 0, 0), clear_outer=True)
    return work


def commit(name, work, materials):
    geom = work.verts[:] + work.edges[:] + work.faces[:]
    bmesh.ops.mirror(work, geom=geom, axis="X", merge_dist=-1.0)
    mesh = bpy.data.meshes.new(f"explore {name}")
    for material in materials:
        mesh.materials.append(material)
    work.to_mesh(mesh)
    work.free()
    obj = bpy.data.objects.new(f"explore {name}", mesh)
    obj.matrix_world = space.inverted()
    evaluation.scene.collection.objects.link(obj)
    return obj


def affine(spec):
    pivot = Vector(spec.get("pivot", (0, 0, 0)))
    scale = spec.get("scale", (1, 1, 1))
    T = (Matrix.Translation(Vector(spec.get("move", (0, 0, 0)))) @ Matrix.Translation(pivot)
         @ Matrix.Rotation(math.radians(spec.get("rot_z", 0)), 4, "Z")
         @ Matrix.Rotation(math.radians(spec.get("rot_y", 0)), 4, "Y")
         @ Matrix.Diagonal((*scale, 1.0)) @ Matrix.Translation(-pivot))
    return lambda work, materials: work.transform(T)


def clip(spec):
    def run(work, materials):
        result = bmesh.ops.bisect_plane(work, geom=work.verts[:] + work.edges[:] + work.faces[:],
                                        plane_co=spec["co"], plane_no=Vector(spec["no"]).normalized(),
                                        clear_outer=True)
        edges = [g for g in result["geom_cut"] if isinstance(g, bmesh.types.BMEdge)]
        if not edges:
            return
        filled = bmesh.ops.holes_fill(work, edges=edges, sides=0)["faces"]
        cap = spec.get("cap")
        if cap:
            material = bpy.data.materials[cap]
            if material not in materials:
                materials.append(material)
            for face in filled:
                face.material_index = materials.index(material)
    return run


def tint(spec):
    def run(work, materials):
        co, no = Vector(spec["co"]), Vector(spec["no"]).normalized()
        bmesh.ops.bisect_plane(work, geom=work.verts[:] + work.edges[:] + work.faces[:], plane_co=co, plane_no=no)
        material = bpy.data.materials[spec["material"]]
        if material not in materials:
            materials.append(material)
        for face in work.faces:
            if (face.calc_center_median() - co).dot(no) > 0:
                face.material_index = materials.index(material)
    return run


for name, spec in variants.items():
    omit = spec.get("omit", [])
    ops = {}
    for item in spec.get("ops", []):
        for part in item["parts"]:
            ops.setdefault(part, []).append({"affine": affine, "clip": clip, "tint": tint}[item["op"]](item))
    missing = [p for p in list(omit) + list(ops) if p not in visual]
    if missing:
        raise SystemExit(f"{name}: unknown parts {missing}")
    extras = []
    for part, steps in ops.items():
        work = half(part)
        materials = list(evaluation.materials(part))
        for step in steps:
            step(work, materials)
        extras.append(commit(part, work, materials))
    evaluation.show_only([p for p in visual if p not in omit and p not in ops])
    evaluation.depsgraph.update()
    R.render(evaluation, bounds, views, size, out, f"{name}_", textured=False)
    for obj in extras:
        mesh = obj.data
        bpy.data.objects.remove(obj)
        bpy.data.meshes.remove(mesh)
    evaluation.depsgraph.update()
