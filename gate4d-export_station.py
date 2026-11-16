"""Export a detailed outpost station .blend to the FBX the game imports (gate 4 of #1384).

blender -b --factory-startup --python export_station.py -- <in.blend> <out.fbx> [bands]

- Drops the preview-only collections, the camera and the sun.
- Drops the hash-mark meshes: station_paint bakes the marks into the Ink mask.
- Names materials by colour key (bone, canvas, ..., win, svc, core, mark).
- Gives each part a contour mesh with joined normals for DrawnContour: "<part> / contour" from the
  hull meshes and "<part> / contour fine" from the "/ fine" hardware, which carries the thin outline.
- Splits every service-light mesh into chase bands by each lamp's distance from the station's
  centre: band 0 is the outermost, so a pulse stepping through the bands runs in toward the berth.
"""
import sys

import bmesh
import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = args[0], args[1]
BANDS = int(args[2]) if len(args) > 2 else 5
HULL = {"bone", "canvas", "slate", "ochre", "brick", "blue"}

bpy.ops.wm.open_mainfile(filepath=SRC)

for col in list(bpy.data.collections):
    if col.name.startswith("Ink preview") or col.name == "Reference":
        for ob in list(col.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.collections.remove(col)
for ob in list(bpy.data.objects):
    if ob.type in {"CAMERA", "LIGHT"}:
        bpy.data.objects.remove(ob, do_unlink=True)

for mat in bpy.data.materials:
    if mat.name in ("ink", "ship"):
        continue
    mat.name = mat.name.split(".")[0]


def material(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


root = next(o for o in bpy.data.objects if o.parent is None and o.type == "EMPTY")
station_col = root.users_collection[0]
smooth_faces = sum(p.use_smooth for o in bpy.data.objects if o.type == "MESH" for p in o.data.polygons)
all_faces = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
print(f"ROOT {root.name}; smooth faces {smooth_faces}/{all_faces}")


def key(ob):
    return ob.get("key") or ob.name.split(" / ")[1]


marks = [o for o in bpy.data.objects if o.type == "MESH" and key(o) == "mark"]
for ob in marks:
    bpy.data.objects.remove(ob, do_unlink=True)
print(f"MARKS dropped {len(marks)}")


def gather(part, meshes, space):
    """One bmesh of the given meshes, in the space of `space`."""
    bm = bmesh.new()
    to_space = space.matrix_world.inverted()
    for ob in meshes:
        piece = bmesh.new()
        piece.from_mesh(ob.data)
        piece.transform(to_space @ ob.matrix_world)
        scratch = bpy.data.meshes.new("scratch")
        piece.to_mesh(scratch)
        piece.free()
        bm.from_mesh(scratch)
        bpy.data.meshes.remove(scratch)
    return bm


def add_child(part, name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    station_col.objects.link(ob)
    ob.parent = part
    ob.matrix_parent_inverse.identity()
    ob.matrix_basis.identity()
    return ob


contours = 0
for part in [o for o in bpy.data.objects if o.type == "EMPTY"]:
    meshes = [c for c in part.children if c.type == "MESH" and key(c) in HULL]
    for fine, suffix in ((False, "contour"), (True, "contour fine")):
        group = [m for m in meshes if m.name.endswith("/ fine") == fine]
        if not group:
            continue
        bm = gather(part, group, part)
        for layer in list(bm.loops.layers.uv.values()):
            bm.loops.layers.uv.remove(layer)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        for f in bm.faces:
            f.smooth = True
        for e in bm.edges:
            e.smooth = True
        add_child(part, f"{part.name} / {suffix}", bm, material(suffix))
        contours += 1


def islands(bm):
    bm.verts.ensure_lookup_table()
    seen, out = set(), []
    for start in bm.faces:
        if start.index in seen:
            continue
        stack, island = [start], []
        seen.add(start.index)
        while stack:
            f = stack.pop()
            island.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        out.append(island)
    return out


lamps = []
for ob in [o for o in bpy.data.objects if o.type == "MESH" and key(o) == "svc"]:
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    world = ob.matrix_world
    root_inv = root.matrix_world.inverted()
    for island in islands(bm):
        verts = list({v for f in island for v in f.verts})
        centre = sum((v.co for v in verts), Vector()) / len(verts)
        lamps.append((ob, [f.index for f in island], (root_inv @ world @ centre).length))
    bm.free()

near, far = min(l[2] for l in lamps), max(l[2] for l in lamps)
span = max(far - near, 1e-4)
raw = [min(BANDS - 1, int((far - l[2]) / span * BANDS)) for l in lamps]
used = sorted(set(raw))
band_of = [used.index(r) for r in raw]
by_mesh = {}
for (ob, faces, dist), band in zip(lamps, band_of):
    by_mesh.setdefault(ob, {}).setdefault(band, []).extend(faces)

svc_mat = material("svc")
for ob, bands in by_mesh.items():
    part = ob.parent
    for band, faces in sorted(bands.items()):
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.faces.ensure_lookup_table()
        keep = set(faces)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context="FACES")
        bm.transform(part.matrix_world.inverted() @ ob.matrix_world)
        add_child(part, f"{part.name} / svc / chase {band}", bm, svc_mat)
    bpy.data.objects.remove(ob, do_unlink=True)
print(f"CONTOURS {contours}; LAMPS {len(lamps)} from {near:.1f} to {far:.1f} u in {len(used)} bands")
for band in range(len(used)):
    print(f"  band {band}: {band_of.count(band)} lamps")

tris = 0
for ob in bpy.data.objects:
    if ob.type == "MESH":
        ob.data.calc_loop_triangles()
        tris += len(ob.data.loop_triangles)
print(f"TRIS with contours {tris}")

bpy.ops.export_scene.fbx(
    filepath=OUT,
    check_existing=False,
    use_selection=False,
    use_active_collection=False,
    object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_custom_props=True,
    bake_anim=False,
    apply_scale_options="FBX_SCALE_ALL",
)
print("EXPORTED", OUT)
