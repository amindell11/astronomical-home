"""Evidence-only: Vanguard onto the ship-tool layout from the study's in-game (MVP) scene, with
palette regions classified per face from today's atlas. Writes art/ships/vanguard/Vanguard.blend,
ship.json, palettes.json and paint.json (slot-local; never committed by #1014).

blender -b --factory-startup --python-exit-code 1 -P vanguard_source.py -- <art/ships/vanguard>
"""
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

folder = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
study = folder / "drawn-study" / "VanguardPainted.blend"
target = folder / "Vanguard.blend"
bpy.ops.wm.open_mainfile(filepath=str(study), load_ui=False)
scene = bpy.data.scenes["Vanguard - Texture MVP"]
bpy.context.window.scene = scene
for other in [s for s in bpy.data.scenes if s is not scene]:
    bpy.data.scenes.remove(other)

HULL = ["MVP Fuselage", "MVP Cockpit.001", "MVP Sparrow Tail", "MVP Wing", "MVP Tail", "MVP wing_armature",
        "MVP wing_armor", "MVP wing_tail", "MVP Power Nacelle Housing"]
CANOPY = ["MVP Canopy"]
IGNORE = ["MVP Power Nacelle Core", "Vanguard service panels", "Vanguard structural ink", "Vanguard surface wear"]

atlas = bpy.data.images["Vanguard vivid"]
width, height = atlas.size
pixels = np.empty(len(atlas.pixels), np.float32)
atlas.pixels.foreach_get(pixels)
pixels = pixels.reshape(height, width, 4)[..., :3]


def srgb_to_linear(c):
    return [v / 12.92 if v < 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c]


def sample(uv):
    u, v = uv
    return pixels[min(height - 1, max(0, int(v * height))), min(width - 1, max(0, int(u * width)))]


# Each face takes the palette colour nearest to most of its atlas samples (ink lines ignored).
PALETTE_SRGB = {"Ivory": (0.88, 0.84, 0.78), "Orange": (0.97, 0.35, 0.12), "Slate": (0.33, 0.38, 0.42),
                "Dark": (0.19, 0.20, 0.28)}
names = list(PALETTE_SRGB)
centres = np.array([PALETTE_SRGB[n] for n in names], np.float64)
labels = []
for name in HULL:
    mesh = bpy.data.objects[name].data
    layer = mesh.uv_layers["MVP_Atlas"]
    for polygon in mesh.polygons:
        corners = [Vector(layer.uv[i].vector) for i in polygon.loop_indices]
        centroid = sum(corners, Vector((0.0, 0.0))) / len(corners)
        points = [centroid] + [centroid * 0.5 + corner * 0.5 for corner in corners]
        votes = []
        for point in points:
            colour = sample(point)
            if colour @ [0.2126, 0.7152, 0.0722] < 0.08:
                continue
            votes.append(int(np.argmin(((centres - colour) ** 2).sum(axis=1))))
        labels.append(max(set(votes), key=votes.count) if votes else names.index("Dark"))
labels = np.array(labels)
for k in range(4):
    print(f"REGION {names[k]:7s} srgb={centres[k].tolist()} faces={int((labels == k).sum())}", file=sys.stderr)

materials = {}
for k in range(4):
    material = bpy.data.materials.new(names[k])
    material.use_nodes = True
    material.diffuse_color = (*srgb_to_linear(centres[k]), 1.0)
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb_to_linear(centres[k]), 1.0)
    materials[names[k]] = material
canopy_material = bpy.data.materials.new("Canopy")
canopy_material.use_nodes = True
canopy_material.diffuse_color = (0.02, 0.03, 0.06, 1.0)
canopy_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.03, 0.06, 1.0)

origin = bpy.data.objects.new("SymmetryOrigin", None)
scene.collection.objects.link(origin)
collections = {}
for name in ("role.hull", "role.canopy", "role.cores", "role.ink", "role.collider", "role.sockets", "ignore"):
    collections[name] = bpy.data.collections.new(name)
    scene.collection.children.link(collections[name])


def move(obj, collection):
    for existing in list(obj.users_collection):
        existing.objects.unlink(obj)
    collection.objects.link(obj)
    obj.parent = origin


face_index = 0
for name in HULL:
    obj = bpy.data.objects[name]
    mesh = obj.data
    mesh.uv_layers["MVP_Atlas"].name = "PaintUV"
    for layer in [l for l in mesh.uv_layers if l.name != "PaintUV"]:
        mesh.uv_layers.remove(layer)
    mesh.materials.clear()
    slot_names = ["Ivory", "Orange", "Slate", "Dark"]
    for slot in slot_names:
        mesh.materials.append(materials[slot])
    for polygon in mesh.polygons:
        polygon.material_index = slot_names.index(names[int(labels[face_index])])
        face_index += 1
    move(obj, collections["role.hull"])
for name in CANOPY:
    obj = bpy.data.objects[name]
    mesh = obj.data
    mesh.uv_layers["UVMap"].name = "PaintUV"
    for layer in [l for l in mesh.uv_layers if l.name != "PaintUV"]:
        mesh.uv_layers.remove(layer)
    surround = [i for i, slot in enumerate(obj.material_slots) if "orange" in slot.material.name.lower()]
    indices = [p.material_index for p in mesh.polygons]
    mesh.materials.clear()
    mesh.materials.append(canopy_material)
    mesh.materials.append(materials["Orange"])
    for polygon, index in zip(mesh.polygons, indices):
        polygon.material_index = 1 if index in surround else 0
    move(obj, collections["role.canopy"])
for name in IGNORE:
    move(bpy.data.objects[name], collections["ignore"])
for obj in list(scene.objects):
    if obj.type == "MESH" and obj.name not in HULL + CANOPY + IGNORE:
        move(obj, collections["ignore"])

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(target), check_existing=False)

palette = {names[k]: [*srgb_to_linear(centres[k]), 1.0] for k in range(4)}
palette["Canopy"] = [0.02, 0.03, 0.06, 1.0]
(folder / "ship.json").write_text(json.dumps({
    "schema_version": 1, "name": "Vanguard", "source": "Vanguard.blend", "texture_size": 4096,
    "uv_density_max_ratio": 2.0, "contour_roles": ["hull"], "exemptions": []}, indent=2) + "\n", encoding="utf-8")
(folder / "palettes.json").write_text(json.dumps({"color_space": "linear-sRGB", "default": "vivid",
                                                  "palettes": {"vivid": palette}}, indent=2) + "\n", encoding="utf-8")
(folder / "paint.json").write_text(json.dumps({
    "schema_version": 1,
    "brush": {"image": "textures/painted-brush-source.png", "scale": 1.3, "contrast": 3.2, "blend": 0.25},
    "ramp": [[0.10, 0.38], [0.55, 0.64], [0.92, 1.0]],
    "sweep": {"weight_x": 0.65, "dark_below": 0.33, "light_above": 0.66, "jitter": 0.5, "dark": 0.61, "light": 1.0},
    "ink": {"inset": 0.003, "rim": 0.0065, "rim_light": 0.35, "wall_facing": 0.65},
    "plates": {"cap_facing": 0.9, "thin_wall": 0.12},
    "regions": {"Ivory": {"plate": True, "sweep": True, "ink_walls": False},
                "Orange": {"plate": True, "sweep": False, "ink_walls": True},
                "Slate": {"plate": False, "sweep": False, "ink_walls": False},
                "Dark": {"plate": False, "sweep": False, "ink_walls": False},
                "Canopy": {"plate": False, "sweep": False, "ink_walls": False}},
    "accents": [{"part": "MVP Canopy", "target": "light", "where": {"x": [0.015, 0.035]}, "value": 1.0}],
    "material": {"shadow_strength": 1.0, "light_strength": 1.0, "ink_strength": 1.0, "light_lift": 0.25,
                 "ink_color": [0.019, 0.023, 0.029], "emission_blend": 1.0,
                 "lighting": {"_ShadowThreshold": 0.55, "_ShadowSoftness": 0.18, "_CastShadowStrength": 0.96,
                              "_AmbientStrength": 0.35, "_PaletteLighting": 1, "_SpecularStrength": 0}}},
    indent=2) + "\n", encoding="utf-8")
print("VANGUARD_SOURCE_DONE", file=sys.stderr)
