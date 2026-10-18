"""Generate the small fixture ship: ship_new, then parts added with bmesh, saved at ship.json's source,
with palettes.json, paint.json and a brush tile beside it so ship_paint runs on it.

Run: blender -b --factory-startup --python-exit-code 1 -P fixture_ship.py -- --ship SHIP_JSON
[--variant solidify_first|cube_name|scaled_uv|moved_hidden_vertex]
"""

import argparse
import json
from pathlib import Path
import subprocess
import sys

import bmesh
import bpy
import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import ship_contract as contract
import ship_source

UV_SCALE = 0.15
VARIANTS = ("solidify_first", "cube_name", "scaled_uv", "moved_hidden_vertex", "repainted_weight")
PALETTES = {"color_space": "linear-sRGB", "default": "zinc",
            "palettes": {"zinc": {"Zinc": [0.7, 0.7, 0.72, 1], "Amber": [0.8, 0.45, 0.1, 1],
                                  "Glass": [0.03, 0.04, 0.06, 1], "Glow": [0.2, 0.8, 0.3, 1]},
                         "jade": {"Zinc": [0.03, 0.35, 0.28, 1], "Amber": [0.43, 0.3, 0.67, 1],
                                  "Glass": [0.03, 0.04, 0.06, 1], "Glow": [0.2, 0.8, 0.3, 1]}}}
PAINT = {
    "schema_version": 1,
    "brush": {"image": "brush.png", "scale": 1.3, "contrast": 3.2, "blend": 0.25},
    "ramp": [[0.1, 0.38], [0.55, 0.65], [0.92, 1.0]],
    "shadow_coverage": 0.33, "shadow_depth": 0.39, "side_wall_depth": 1.0,
    "sweep": {"weight_x": 0.65, "light_above": 0.66, "jitter": 0.5, "light": 1.0},
    "ink": {"inset": 0.01, "rim": 0.02, "rim_light": 0.35, "wall_facing": 0.65},
    "plates": {"cap_facing": 0.9, "thin_wall": 0.06},
    "regions": {"Zinc": {"plate": True, "sweep": True, "ink_walls": False},
                "Amber": {"plate": True, "sweep": False, "ink_walls": True},
                "Glass": {"plate": False, "sweep": False, "ink_walls": False},
                "Glow": {"plate": False, "sweep": False, "ink_walls": False}},
    "canopy_glass": ["Canopy"], "canopy_frames": [],
    "accents": [{"part": "Core", "target": "light", "where": {"x": [0.0, 0.05]}, "value": 1.0},
                {"part": "Core", "target": "emission", "where": {"y": [None, -0.9]}, "value": 1.0, "color": [0.2, 0.8, 0.2]}],
    "material": {"shadow_strength": 1.0, "light_strength": 1.0, "ink_strength": 1.0, "light_lift": 0.25,
                 "ink_color": [0.019, 0.023, 0.029], "emission_blend": 1.0, "lighting": {"_ShadowThreshold": 0.35}},
}


def write_paint_settings(folder):
    """palettes.json, paint.json and a deterministic 64x64 brush tile beside the source."""
    (folder / "palettes.json").write_text(json.dumps(PALETTES, indent=2), encoding="utf-8")
    (folder / "paint.json").write_text(json.dumps(PAINT, indent=2), encoding="utf-8")
    noise = np.random.default_rng(0).uniform(0.55, 0.75, (64, 64, 1)).astype(np.float32)
    ship_source.save_pixels(folder / "brush.png", np.concatenate([noise, noise, noise, np.ones_like(noise)], axis=2))


def box(low, high, skip=()):
    """Corner list and outward quads of an axis-aligned box; skip drops faces such as "-x"."""
    (x0, y0, z0), (x1, y1, z1) = low, high
    verts = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
             (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    faces = {"-z": (0, 3, 2, 1), "+z": (4, 5, 6, 7), "-y": (0, 1, 5, 4),
             "+y": (2, 3, 7, 6), "-x": (0, 4, 7, 3), "+x": (1, 2, 6, 5)}
    return verts, [face for key, face in faces.items() if key not in skip]


def planar_uv(mesh, tile):
    """Project each face onto its own plane at one texel density, centred in the part's atlas tile
    (a 3 by 2 grid) so parts never overwrite each other's texels when baked."""
    work = bmesh.new()
    work.from_mesh(mesh)
    work.normal_update()
    layer = work.loops.layers.uv.new(contract.PAINT_UV)
    centre_u, centre_v = (tile % 3 + 0.5) / 3, (tile // 3 + 0.5) / 2
    for face in work.faces:
        origin = face.verts[0].co
        u_axis = (face.verts[1].co - origin).normalized()
        v_axis = face.normal.cross(u_axis)
        for loop in face.loops:
            offset = loop.vert.co - origin
            loop[layer].uv = (offset.dot(u_axis) * UV_SCALE + centre_u, offset.dot(v_axis) * UV_SCALE + centre_v)
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
        planar_uv(mesh, len([o for o in bpy.data.objects if o.type == "MESH" and o.data.uv_layers]))
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
    write_paint_settings(source.parent)


if __name__ == "__main__":
    main()
