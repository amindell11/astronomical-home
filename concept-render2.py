"""Scratch concept renders: per variant hide parts, scale parts, recolour parts, add generated prongs.
Nothing is saved to the source. Output <out>/<variant>_<view>.png"""
import sys, json, math
from pathlib import Path
sys.path.insert(0, r"D:/amind/git/agent-1/art/tools/ship")
import bpy, numpy as np
from mathutils import Matrix
import ship_source, ship_contract as contract, ship_render as R

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


views = {"top": R.ORTHO["top"], "side": R.ORTHO["side"], "three": view(-45, 40), "rear": view(135, 40)}


def part_points(name):
    mesh = evaluation.mesh(name)
    co = ship_source.mesh_arrays(mesh)[0]
    bpy.data.meshes.remove(mesh)
    M = np.array(evaluation.to_space(name), np.float64)
    return co @ M[:3, :3].T + M[:3, 3]


def scale_about(name, factor):
    p = part_points(name)
    c = (p.min(0) + p.max(0)) / 2
    c[0] = 0.0
    T = Matrix.Translation(c) @ Matrix.Diagonal((*factor, 1.0)) @ Matrix.Translation(-c)
    copy = evaluation.copies[name]
    copy.matrix_world = space.inverted() @ T @ space @ copy.matrix_world


def material(rgb):
    m = bpy.data.materials.new(f"mock {rgb}")
    m.diffuse_color = (*rgb, 1.0)
    return m


def recolor(name, mat, only=None):
    for slot in evaluation.copies[name].material_slots:
        if only is None or (slot.material and slot.material.name in only):
            slot.link = "OBJECT"
            slot.material = mat


def prong(spec):
    """A plain tapered slab per side along the principal top-view axis of the source part's +X half."""
    p = part_points(spec["part"])
    side = p[p[:, 0] > 0.05]
    xy = side[:, :2]
    mean = xy.mean(0)
    _, _, vt = np.linalg.svd(xy - mean)
    axis = vt[0] if vt[0][1] < 0 else -vt[0]  # points aft (-Y)
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
            verts.append((q[0], q[1], z + sz * hh / 2))
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    mirrored = [(-x, y, zz) for x, y, zz in verts]
    allv = verts + mirrored
    allf = faces + [tuple(i + 8 for i in reversed(f)) for f in faces]
    mesh = bpy.data.meshes.new("mock prong")
    mesh.from_pydata(allv, [], allf)
    obj = bpy.data.objects.new("mock prong", mesh)
    obj.data.materials.append(evaluation.materials(spec["part"])[0])
    obj.matrix_world = space.inverted()
    evaluation.scene.collection.objects.link(obj)
    return obj


for name, spec in variants.items():
    saved = {n: (c.matrix_world.copy(), [(s.link, s.material) for s in c.material_slots])
             for n, c in evaluation.copies.items()}
    extras = []
    omit = spec.get("omit", [])
    missing = [p for p in omit + spec.get("scale", {}).get("parts", []) + spec.get("recolor", {}).get("parts", [])
               if p not in visual]
    if missing:
        raise SystemExit(f"{name}: unknown parts {missing}")
    if "scale" in spec:
        for part in spec["scale"]["parts"]:
            scale_about(part, spec["scale"]["factor"])
    if "recolor" in spec:
        mat = material(spec["recolor"]["rgb"])
        for part in spec["recolor"]["parts"]:
            recolor(part, mat, spec["recolor"].get("only"))
    if "prong" in spec:
        extras.append(prong(spec["prong"]))
    evaluation.show_only([p for p in visual if p not in omit])
    evaluation.depsgraph.update()
    R.render(evaluation, bounds, views, size, out, f"{name}_", textured=False)
    for obj in extras:
        bpy.data.objects.remove(obj)
    for n, (mw, slots) in saved.items():
        c = evaluation.copies[n]
        c.matrix_world = mw
        for s, (link, mat) in zip(c.material_slots, slots):
            s.link = link
            if link == "OBJECT":
                s.material = mat
    evaluation.depsgraph.update()
