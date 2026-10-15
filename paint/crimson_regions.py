"""Evidence-only prep of the slot's Crimson.blend for the ship_paint proof (#1014).

Gives every part its palette-region material by the seed script's role map (geometry untouched),
writes palettes.json and paint.json at the seed's numbers, and optionally points every region
material's active image texture at a composite preview so `ship_render --textured` shows it.

blender -b --factory-startup --python-exit-code 1 -P crimson_regions.py -- <art/ships/crimson> [--composite PNG]
"""

import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
folder = Path(args[0]).resolve()
composite = Path(args[args.index("--composite") + 1]).resolve() if "--composite" in args else None
source = folder / "Crimson.blend"

SRGB = {"Silver": (.94, .92, .86), "Red": (.92, .038, .023), "Charcoal": (.14, .15, .17),
        "Glass": (.028, .034, .041), "Rim": (.57, .58, .55)}
INK, GREEN_FLOOR, GREEN_YOKE = (.019, .023, .029), (.10, .66, .13), (.18, .72, .12)


def linear(c):
    return [v / 12.92 if v < .04045 else ((v + .055) / 1.055) ** 2.4 for v in c]


def role(name):
    if name == "Canopy":
        return "Glass"
    if name in ("Canopy perimeter rim", "Canopy transverse frame", "Dorsal service spine", "Service pod cap",
                "Engine housing") or "wing foundation" in name or "fin" in name.lower():
        return "Red"
    if any(w in name for w in ("armor", "prow", "Cockpit surround", "Rear cockpit yoke", "Engine neck",
                               "Engine support frame", "Wing root beam")):
        return "Silver"
    if name == "Engine forward cowling":
        return "Rim"
    return "Charcoal"


bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
scene = bpy.context.scene
materials = {}
for region, colour in SRGB.items():
    material = bpy.data.materials.get(region) or bpy.data.materials.new(region)
    material.use_nodes = True
    material.diffuse_color = (*linear(colour), 1.0)
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*linear(colour), 1.0)
        bsdf.inputs["Roughness"].default_value = .88
    if composite is not None:
        image = bpy.data.images.get("Composite") or bpy.data.images.load(str(composite))
        image.name = "Composite"
        node = material.node_tree.nodes.get("Composite preview") or material.node_tree.nodes.new("ShaderNodeTexImage")
        node.name = "Composite preview"
        node.image = image
        material.node_tree.nodes.active = node
    materials[region] = material
counts = {}
nozzle_y = None
for obj in scene.objects:
    if obj.type != "MESH":
        continue
    region = role(obj.name)
    counts[region] = counts.get(region, 0) + 1
    obj.data.materials.clear()
    obj.data.materials.append(materials[region])
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    if obj.name == "Engine nozzle":
        ys = [(obj.matrix_world @ v.co).y for v in obj.data.vertices]
        nozzle_y = (min(ys), max(ys))
print("REGION COUNTS", counts, "NOZZLE Y", nozzle_y, file=sys.stderr)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)

(folder / "palettes.json").write_text(json.dumps({
    "color_space": "linear-sRGB", "default": "crimson",
    "palettes": {"crimson": {region: [*linear(colour), 1.0] for region, colour in SRGB.items()}}}, indent=2) + "\n",
    encoding="utf-8")
lo, hi = nozzle_y
paint = {
    "schema_version": 1,
    "brush": {"image": "textures/painted-brush-source.png", "scale": 1.3, "contrast": 3.2, "blend": 0.25},
    "ramp": [[0.10, 0.38], [0.55, 0.64], [0.92, 1.0]],
    "sweep": {"weight_x": 0.65, "dark_below": 0.33, "light_above": 0.66, "jitter": 0.5, "dark": 0.61, "light": 1.0},
    "ink": {"inset": 0.003, "rim": 0.0065, "rim_light": 0.35, "wall_facing": 0.65},
    "plates": {"cap_facing": 0.9, "thin_wall": 0.12},
    "regions": {"Silver": {"plate": True, "sweep": True, "ink_walls": False},
                "Red": {"plate": True, "sweep": False, "ink_walls": True},
                "Charcoal": {"plate": False, "sweep": False, "ink_walls": False},
                "Glass": {"plate": False, "sweep": False, "ink_walls": False},
                "Rim": {"plate": False, "sweep": False, "ink_walls": False}},
    "accents": [
        {"part": "Canopy", "target": "light", "where": {"x": [0.025, 0.050]}, "value": 1.0},
        {"part": "Service channel floor", "target": "emission", "where": {"x": [0.068, None]}, "value": 1.0,
         "color": linear(GREEN_FLOOR)},
        {"part": "Rear cockpit yoke", "target": "emission", "where": {"x": [0.115, 0.142], "y": [-0.203, -0.178]},
         "value": 1.0, "color": linear(GREEN_YOKE)},
        {"part": "Engine nozzle", "target": "light", "where": {"y": [lo + 0.016, lo + 0.024]}, "value": 1.0},
        {"part": "Engine nozzle", "target": "light", "where": {"y": [hi - 0.029, hi - 0.021]}, "value": 1.0},
    ],
    "material": {"shadow_strength": 1.0, "light_strength": 1.0, "ink_strength": 1.0, "light_lift": 0.25,
                 "ink_color": linear(INK), "emission_blend": 1.0,
                 "lighting": {"_ShadowThreshold": 0.55, "_ShadowSoftness": 0.18, "_CastShadowStrength": 0.96,
                              "_AmbientStrength": 0.35, "_PaletteLighting": 1, "_SpecularStrength": 0,
                              "_ShadowColor": [0.35, 0.45, 0.8]}},
}
(folder / "paint.json").write_text(json.dumps(paint, indent=2) + "\n", encoding="utf-8")
print("CRIMSON_REGIONS_DONE", file=sys.stderr)
