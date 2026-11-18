"""Fable spike concepts: lofted hex-section spikes on the Valis minion. Nothing is saved to the source."""
import sys, json, math
from pathlib import Path
sys.path.insert(0, r"D:/amind/git/agent-1/art/tools/ship")
import bpy, bmesh, numpy as np
import ship_source, ship_contract as contract, ship_render as R

argv = sys.argv[sys.argv.index("--") + 1:]
src, variants, out, size = Path(argv[0]), json.loads(Path(argv[1]).read_text()), Path(argv[2]), int(argv[3])
out.mkdir(parents=True, exist_ok=True)
scene0 = ship_source.open_source(src)
roles = ship_source.Roles(scene0)
visual = sorted(n for role in contract.VISUAL_ROLES for n in roles.members.get(role, ()) if scene0.objects[n].type == "MESH")
evaluation, bounds = R.prepare(src)
space = evaluation.space
low, high = bounds
low = low.copy(); high = high.copy(); low[1] = -5.5; high[2] = 2.0
bounds = (low, high)
MATS = {"ivory": "50 aft split prongs", "lavender": "12 upper wing lavender tips", "graphite": "01 central fuselage",
        "gray": "11 upper wing gray planes"}


def view(az, el):
    a, e = math.radians(az), math.radians(el)
    return ((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)), (0, 0, 1))


views = {"top": R.ORTHO["top"], "three": view(-45, 40), "rear": view(135, 40), "side": R.ORTHO["side"]}


def loft(spec):
    """sections: [y, half_width, z_edge, up, down, flat]; hex section: edge, top-flat pair, edge, bottom-flat pair.
    mats: per segment, a name or {"top":..,"bevel":..,"under":..}."""
    secs = spec["sections"]
    names = sorted({m for seg in spec["mats"] for m in ([seg] if isinstance(seg, str) else seg.values())})
    bm = bmesh.new()
    rings = []
    for y, w, zc, up, down, flat in secs:
        pts = [(-w, zc), (-w * flat, zc + up), (w * flat, zc + up), (w, zc), (w * flat, zc - down), (-w * flat, zc - down)]
        rings.append([bm.verts.new((x, y, z)) for x, z in pts])
    kinds = ["bevel", "top", "bevel", "under", "under", "under"]
    for i, seg in enumerate(spec["mats"]):
        a, b = rings[i], rings[i + 1]
        for k in range(6):
            quad = [a[k], a[(k + 1) % 6], b[(k + 1) % 6], b[k]]
            try:
                f = bm.faces.new(quad)
            except ValueError:
                continue
            name = seg if isinstance(seg, str) else seg.get(kinds[k], seg["bevel"])
            f.material_index = names.index(name)
    try:
        bm.faces.new(rings[0])
    except ValueError:
        pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("mock spike")
    bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new("mock spike", mesh)
    for n in names:
        obj.data.materials.append(next(m for m in evaluation.materials(MATS[n]) if m))
    obj.matrix_world = space.inverted()
    evaluation.scene.collection.objects.link(obj)
    return obj


for name, spec in variants.items():
    extras = [loft(s) for s in spec.get("lofts", [])]
    omit = spec.get("omit", [])
    evaluation.show_only([p for p in visual if p not in omit])
    evaluation.depsgraph.update()
    R.render(evaluation, bounds, views, size, out, f"{name}_", textured=False)
    for obj in extras:
        bpy.data.objects.remove(obj)
