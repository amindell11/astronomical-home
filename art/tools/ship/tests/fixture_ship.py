"""Generate the small fixture ship: ship_new, then parts added with bmesh, saved at ship.json's source.

Run: blender -b --factory-startup --python-exit-code 1 -P fixture_ship.py -- --ship SHIP_JSON
[--variant solidify_first|cube_name|scaled_uv|moved_hidden_vertex]
"""

import argparse
from pathlib import Path
import subprocess
import sys

import bmesh
import bpy

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import ship_contract as contract

UV_SCALE = 0.25
VARIANTS = ("solidify_first", "cube_name", "scaled_uv", "moved_hidden_vertex", "repainted_weight")


def box(low, high, skip=()):
    """Corner list and outward quads of an axis-aligned box; skip drops faces such as "-x"."""
    (x0, y0, z0), (x1, y1, z1) = low, high
    verts = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
             (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    faces = {"-z": (0, 3, 2, 1), "+z": (4, 5, 6, 7), "-y": (0, 1, 5, 4),
             "+y": (2, 3, 7, 6), "-x": (0, 4, 7, 3), "+x": (1, 2, 6, 5)}
    return verts, [face for key, face in faces.items() if key not in skip]


def planar_uv(mesh):
    """Project each face onto its own plane so every face has the same texel density."""
    work = bmesh.new()
    work.from_mesh(mesh)
    work.normal_update()
    layer = work.loops.layers.uv.new(contract.PAINT_UV)
    for face in work.faces:
        origin = face.verts[0].co
        u_axis = (face.verts[1].co - origin).normalized()
        v_axis = face.normal.cross(u_axis)
        for loop in face.loops:
            offset = loop.vert.co - origin
            loop[layer].uv = (offset.dot(u_axis) * UV_SCALE + 0.5, offset.dot(v_axis) * UV_SCALE + 0.5)
    work.to_mesh(mesh)
    work.free()


def part(name, geometry, parent, collections, materials=(), face_materials=None, uv=True):
    verts, faces = geometry
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.shade_flat()
    for material in materials:
        mesh.materials.append(material)
    if face_materials:
        mesh.polygons.foreach_set("material_index", face_materials)
    if uv:
        planar_uv(mesh)
    obj = bpy.data.objects.new(name, mesh)
    obj.parent = parent
    for collection in collections:
        bpy.data.collections[collection].objects.link(obj)
    return obj


def build(ship_json, out):
    ship, source = contract.load_ship(ship_json)
    result = subprocess.run([bpy.app.binary_path, "-b", "--factory-startup", "--python-exit-code", "1",
                             "-P", str(TOOLS / "ship_new.py"), "--", "--ship", str(ship_json), "--out", str(out)],
                            capture_output=True, text=True)
    assert result.returncode == 0 and "SHIP_VERDICT=pass" in result.stdout, result.stdout + result.stderr
    bpy.ops.wm.open_mainfile(filepath=str(source))
    origin = bpy.data.objects[contract.SYMMETRY_ORIGIN]
    editing = bpy.data.collections.new("editing")
    bpy.context.scene.collection.children.link(editing)
    body = bpy.data.objects.new("Body", None)
    body.parent = origin
    body.location = (0.0, 0.1, 0.0)
    wings = bpy.data.objects.new("Wings", None)
    wings.parent = body
    for empty in (body, wings):
        editing.objects.link(empty)
    zinc, amber = bpy.data.materials.new("Zinc"), bpy.data.materials.new("Amber")
    glass, glow = bpy.data.materials.new("Glass"), bpy.data.materials.new("Glow")

    fuselage = part("Fuselage", box((0, -1, -0.15), (0.3, 1, 0.15), skip=("-x",)), body, ["role.hull"],
                    (zinc, amber), [0, 1, 0, 0, 0])
    mirror = fuselage.modifiers.new("Mirror", "MIRROR")
    mirror.mirror_object, mirror.use_clip = origin, True
    fuselage.modifiers.new("Bevel", "BEVEL").width = 0.03

    wing = part("Wing", ([(0.3, -0.2, 0), (1.0, -0.6, 0), (1.0, -0.8, 0), (0.3, -0.7, 0)], [(0, 3, 2, 1)]),
                wings, ["role.hull", "editing"], (zinc,))
    wing.modifiers.new("Mirror", "MIRROR").mirror_object = origin
    wing.vertex_groups.new(name="Taper").add(range(4), 1.0, "REPLACE")
    solidify = wing.modifiers.new("Solidify", "SOLIDIFY")
    solidify.thickness, solidify.vertex_group = 0.04, "Taper"

    fin = part("Fin", box((-0.02, -0.8, 0.15), (0.02, -0.3, 0.5)), body, ["role.hull", "editing"], (amber,))
    fin.hide_viewport = True

    spine = part("Spine", box((-0.05, 0.0, 0.15), (0.05, 0.6, 0.22)), body, ["role.hull"], (zinc,))
    spine.shape_key_add(name="Basis")
    raise_key = spine.shape_key_add(name="Raise")
    for index in range(4, 8):
        raise_key.data[index].co.z += 0.05
    raise_key.value = 0.5

    part("Canopy", box((-0.12, 0.3, 0.15), (0.12, 0.7, 0.3)), body, ["role.canopy"], (glass,))
    part("Core", box((-0.1, -1.05, -0.08), (0.1, -0.95, 0.08)), body, ["role.cores"], (glow,))
    part("Hitbox", box((-1, -1, -0.2), (1, 1, 0.5)), origin, ["role.collider"], uv=False)
    part("Scratch", box((5, 0, 0), (5.2, 0.2, 0.2)), origin, ["role.hull", contract.IGNORE], uv=False)
    for name, x in (("engine.L", -0.2), ("engine.R", 0.2)):
        socket = bpy.data.objects.new(name, None)
        socket.parent = body
        socket.location = (x, -1.1, 0.0)
        socket.rotation_euler = (1.5707963, 0.0, 0.0)
        bpy.data.collections["role.sockets"].objects.link(socket)
    return source


def apply_variant(variant):
    objects = bpy.data.objects
    if variant == "solidify_first":
        objects["Wing"].modifiers.move(1, 0)
    elif variant == "cube_name":
        objects["Spine"].name = "Cube"
    elif variant == "scaled_uv":
        for loop_uv in objects["Canopy"].data.uv_layers[contract.PAINT_UV].uv:
            loop_uv.vector *= 0.2
    elif variant == "moved_hidden_vertex":
        objects["Fin"].data.vertices[0].co.z += 0.01
    elif variant == "repainted_weight":
        objects["Wing"].vertex_groups["Taper"].add([1], 0.5, "REPLACE")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ship", required=True)
    parser.add_argument("--variant", choices=VARIANTS)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    out = Path(args.ship).resolve().parent / "fixture-new"
    source = build(args.ship, out)
    if args.variant:
        apply_variant(args.variant)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)


if __name__ == "__main__":
    main()
