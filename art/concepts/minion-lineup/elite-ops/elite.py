"""Scratch: turn a one-ship lineup file (local copies of a rival) into an elite blockout by a list of ops.
argv: <one-ship lineup .blend> <RivalName> <EliteName> <ops.json> <out.blend>
All coordinates are in the ship's SymmetryOrigin space, written for the +X side.
Ops: del | xf | dup | box | cyl | remap
"""
import bpy, sys, json, math
import bmesh
import numpy as np
from mathutils import Matrix, Vector, Euler

src, rival, elite, ops, out = sys.argv[-5], sys.argv[-4], sys.argv[-3], json.load(open(sys.argv[-2])), sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=src)
parts = {o["lineup_part"]: o for o in bpy.data.objects if o.get("lineup_ship") == rival}
origin = parts["SymmetryOrigin"]
bpy.context.view_layer.update()
OW = origin.matrix_world.copy()
FLIP = Matrix.Diagonal((-1, 1, 1, 1))
holder = bpy.data.collections[rival]


def rel(obj):
    bpy.context.view_layer.update()
    return OW.inverted() @ obj.matrix_world


def side_of(obj):
    m = rel(obj)
    xs = [(m @ v.co).x for v in obj.data.vertices]
    return 1.0 if sum(xs) >= 0 else -1.0


def affine(op):
    pivot = Vector(op.get("pivot", (0, 0, 0)))
    scale = op.get("scale", 1.0)
    scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    rot = Euler([math.radians(a) for a in op.get("rot", (0, 0, 0))]).to_matrix().to_4x4()
    return (Matrix.Translation(Vector(op.get("move", (0, 0, 0))) + pivot) @ rot
            @ Matrix.Diagonal((*scale, 1.0)) @ Matrix.Translation(-pivot))


def apply(obj, m, mirrored=True):
    if mirrored and obj.type == "MESH" and side_of(obj) < 0:
        m = FLIP @ m @ FLIP
    obj.matrix_world = OW @ m @ rel(obj)


def material(name):
    found = bpy.data.materials.get(name)
    assert found is not None, f"no material {name}: {[m.name for m in bpy.data.materials]}"
    return found


def register(obj, name):
    obj["lineup_ship"], obj["lineup_part"] = rival, name
    parts[name] = obj
    if obj.name not in holder.objects:
        holder.objects.link(obj)


def new_mesh_object(name, bm, mat, mirror):
    mesh = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh); bm.free()
    mesh.materials.append(material(mat))
    obj = bpy.data.objects.new(name, mesh)
    obj.parent = origin
    obj.matrix_parent_inverse = Matrix.Identity(4)
    if mirror:
        mod = obj.modifiers.new("Mirror", "MIRROR")
        mod.mirror_object = origin
    register(obj, name)
    return obj


for op in ops:
    kind = op["op"]
    if kind == "del":
        for name in op["parts"]:
            bpy.data.objects.remove(parts.pop(name))
    elif kind == "xf":
        for name in op["parts"]:
            apply(parts[name], affine(op))
    elif kind == "dup":
        source = parts[op["part"]]
        made = source.copy(); made.data = source.data.copy()
        register(made, op["as"])
        if "mat" in op:
            for i in range(len(made.data.materials)):
                made.data.materials[i] = material(op["mat"])
        single = op.get("single")
        if single:
            for mod in [m for m in made.modifiers if m.type == "MIRROR"]:
                made.modifiers.remove(mod)
            want = 1.0 if single == "R" else -1.0
            if side_of(made) != want:
                made.matrix_world = OW @ FLIP @ rel(made)
            m = affine(op)
            if want < 0:
                m = FLIP @ m @ FLIP
            made.matrix_world = OW @ m @ rel(made)
        else:
            apply(made, affine(op))
    elif kind == "box":
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        sx, sy, sz = op["size"]
        taper = op.get("taper")
        for v in bm.verts:
            if taper and v.co.y > 0:
                v.co.x *= taper[0]; v.co.z *= taper[1]
            if taper and len(taper) > 2 and v.co.y < 0:
                v.co.x *= taper[2]; v.co.z *= taper[3]
            v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
        if op.get("bevel"):
            bmesh.ops.bevel(bm, geom=bm.edges[:], offset=op["bevel"], segments=1, affect="EDGES", profile=0.5)
        obj = new_mesh_object(op["name"], bm, op["mat"], op.get("mirror", True))
        place = Matrix.Translation(Vector(op["center"])) @ Euler([math.radians(a) for a in op.get("rot", (0, 0, 0))]).to_matrix().to_4x4()
        if op.get("single") == "L":
            place = FLIP @ place @ FLIP
        obj.matrix_parent_inverse = place
    elif kind == "cyl":
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=op.get("segs", 12), radius1=op["radius"],
                              radius2=op.get("radius2", op["radius"]), depth=op["depth"])
        if op.get("bevel"):
            bmesh.ops.bevel(bm, geom=[e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-5],
                            offset=op["bevel"], segments=1, affect="EDGES", profile=0.5)
        axis = {"z": Matrix.Identity(4), "y": Matrix.Rotation(math.radians(90), 4, "X"), "x": Matrix.Rotation(math.radians(90), 4, "Y")}[op.get("axis", "y")]
        obj = new_mesh_object(op["name"], bm, op["mat"], op.get("mirror", True))
        place = (Matrix.Translation(Vector(op["center"])) @ Euler([math.radians(a) for a in op.get("rot", (0, 0, 0))]).to_matrix().to_4x4() @ axis)
        if op.get("single") == "L":
            place = FLIP @ place @ FLIP
        obj.matrix_parent_inverse = place
    elif kind == "poly":
        # an outline in plan (x, y), extruded between two heights; "at"/"rot" place a locally drawn outline
        rot = Matrix.Rotation(math.radians(op.get("rot", 0.0)), 4, "Z")
        at = Vector((*op.get("at", (0, 0)), 0))
        flip = -1.0 if op.get("flipx") else 1.0
        sign = -1.0 if op.get("single") == "L" else 1.0
        z0, z1 = op["z"]
        inset = op.get("top_inset", 0.0)
        pts = [Vector((x * flip, y, 0)) for x, y in op["pts"]]
        centre = sum(pts, Vector()) / len(pts)
        bm = bmesh.new()
        def ring(z, shrink):
            made = []
            for p in pts:
                q = centre + (p - centre) * (1.0 - shrink / max((p - centre).length, 1e-6)) if shrink else p
                w = at + rot @ q
                made.append(bm.verts.new((w.x * sign, w.y, z)))
            return made
        low, high = ring(z0, op.get("bottom_inset", 0.0)), ring(z1, inset)
        n = len(pts)
        for i in range(n):
            bm.faces.new((low[i], low[(i + 1) % n], high[(i + 1) % n], high[i]))
        bm.faces.new(high); bm.faces.new(low[::-1])
        if op.get("bevel"):
            bmesh.ops.bevel(bm, geom=bm.edges[:], offset=op["bevel"], segments=1, affect="EDGES", profile=0.5)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
        new_mesh_object(op["name"], bm, op["mat"], op.get("mirror", True))
    elif kind == "dupfull":
        # a centre part's whole (mirrored) shape as one mesh, so it can leave the centre line
        source = parts[op["part"]]
        bpy.context.view_layer.update()
        mesh = bpy.data.meshes.new_from_object(source.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        made = bpy.data.objects.new(op["as"], mesh)
        made.parent = origin; made.matrix_parent_inverse = Matrix.Identity(4)
        register(made, op["as"])
        made.matrix_world = OW @ affine(op) @ rel(source)
        if op.get("mirror", True):
            mod = made.modifiers.new("Mirror", "MIRROR"); mod.mirror_object = origin
    elif kind == "loft":
        # a cross-section in (x, z) carried along y; each station is [y, scale about the section centre, dx, dz]
        prof = [Vector((x, 0, z)) for x, z in op["profile"]]
        centre = sum(prof, Vector()) / len(prof)
        bm = bmesh.new(); rings = []
        for y, scale, dx, dz in op["stations"]:
            rings.append([bm.verts.new((centre.x + (p.x - centre.x) * scale + dx, y, centre.z + (p.z - centre.z) * scale + dz)) for p in prof])
        n = len(prof)
        for a, b in zip(rings[:-1], rings[1:]):
            for i in range(n):
                bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
        new_mesh_object(op["name"], bm, op["mat"], op.get("mirror", True))
    elif kind == "remap":
        T = op["target"]
        group = [parts[n] for n in op["parts"]]
        mats = {o: rel(o) for o in group}
        P = np.array([list(m @ v.co) for o, m in mats.items() for v in o.data.vertices])
        top, tip = P[:, 1].max(), P[:, 1].min()
        t_all = (top - P[:, 1]) / (top - tip); ax = np.abs(P[:, 0])
        bins = np.linspace(0, 1, op.get("bins", 25)); centers = (bins[:-1] + bins[1:]) / 2
        lo, hi, zs = [], [], []
        for a, b in zip(bins[:-1], bins[1:]):
            sel = (t_all >= a) & (t_all <= b)
            lo.append(ax[sel].min() if sel.any() else np.nan); hi.append(ax[sel].max() if sel.any() else np.nan)
            zs.append(P[sel, 2].mean() if sel.any() else np.nan)
        lo, hi, zs = np.array(lo), np.array(hi), np.array(zs)
        ok = ~np.isnan(lo); lo, hi, zs, centers = lo[ok], hi[ok], zs[ok], centers[ok]
        deg = op.get("fit", 3)
        if deg:
            lo = np.polyval(np.polyfit(centers, lo, deg), centers); hi = np.polyval(np.polyfit(centers, hi, deg), centers)
        zline = np.polyval(np.polyfit(centers, zs, 1), centers)
        to, tx = zip(*T["outer"]); ti, ix = zip(*T["inner"])
        flat = T.get("z")
        zscale = T.get("z_scale", 1.0)
        for o, m in mats.items():
            inv = m.inverted()
            for v in o.data.vertices:
                p = m @ v.co
                t = min(max((top - p.y) / (top - tip), 0.0), 1.0)
                a, b = np.interp(t, centers, lo), np.interp(t, centers, hi)
                u = min(max((abs(p.x) - a) / max(b - a, 1e-5), -0.15), 1.15)
                na, nb = np.interp(t, ti, ix), np.interp(t, to, tx)
                s = 1.0 if p.x >= 0 else -1.0
                z = p.z if flat is None else flat + (p.z - np.interp(t, centers, zline)) * zscale
                v.co = inv @ Vector((s * (na + u * (nb - na)), T["top"] - t * T["length"], z))
    else:
        raise SystemExit(f"unknown op {kind}")

for obj in bpy.data.objects:
    if obj.get("lineup_ship") == rival:
        obj["lineup_ship"] = elite
        obj.name = f"{elite} | {obj['lineup_part']}"[:63]
holder.name = elite
for child in holder.children:
    child.name = child.name.replace(rival, elite, 1)
place = bpy.data.objects[f"{rival} placement"]
place.name = f"{elite} placement"
bpy.ops.wm.save_as_mainfile(filepath=out)
print("ELITE", elite, len(ops), "ops", file=sys.stderr)
