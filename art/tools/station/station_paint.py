"""Bake a station's brush and hatching into the drawn shader's Shadow, Light and Ink masks.

CLI: blender -b --factory-startup --python-exit-code 1 -P station_paint.py -- --station BLEND
--out DIR [--unity DIR]
Needs station_uv's PaintUV layer and never saves the .blend. Every hull mesh bakes (Cycles, emission)
in its own local space with the outpost preview's brush and hatch node setups, so the pattern lands
where the preview drew it. Written to layers/ beside the source, 8-bit grey on the PaintUV atlas,
0 away from the islands:
  Shadow = clamp(3.2 * (0.625 - brush))   Light = clamp(3.2 * (brush - 0.625))   Ink = (hatch r + hatch g) / 2
Strengths stay on the Unity material. --unity also writes the three masks into DIR. The review image
<out>/<station>-flat.png shades the hull with ../outpost-colour.json the
way the shader does, top view beside a tilted one. Prose to stderr; stdout trailers
STATION_MASKS=<layers dir>, STATION_PREVIEW=<png>. Exit 0 written, 1 error.
"""

import json
import math
from pathlib import Path
import shutil
import sys
import tempfile
import time

import bpy
from mathutils import Euler
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "ship"))
sys.path.insert(0, str(HERE))
import ship_contract as contract
import ship_paint
import ship_source
import station_uv

BRUSH_IMAGE = HERE.parents[1] / "ships" / "crimson" / "textures" / "painted-brush-source.png"
BRUSH_TILE = 7.0  # world units one repeat of the brush tile covers
HATCH_SCALE = 0.9  # wave scale: strokes about 0.4 units apart
BRUSH_CONTRAST = 3.2  # the ships' contrast for the same tile
BAKE_MARGIN = 16
MASKS = ("Shadow", "Light", "Ink")
PREVIEW_SIZE = 1520  # 40 px per unit at the station camera's 38 unit frame
TILTED = Euler((math.radians(-55), math.radians(15), math.radians(30)))


def linear(hex_colour):
    srgb = np.array([int(hex_colour.lstrip("#")[i:i + 2], 16) / 255 for i in (0, 2, 4)])
    return tuple(float(c) for c in np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4))


def load_settings(path, keys):
    data = json.loads(path.read_text(encoding="utf-8"))
    try:
        settings = {"hull": {key: linear(data["hull"][key]) for key in keys}, "ink": linear(data["ink"]),
                    "brush": float(data["brush"]), "hatch": float(data["hatch"])}
    except (KeyError, TypeError, ValueError, IndexError) as error:
        raise contract.UsageError(f"{path}: missing or malformed {error}")
    return settings


def bake_material(brush_image):
    """The outpost preview's brush and hatch node setups, both read in Object coordinates."""
    material = bpy.data.materials.new("station bake")
    g = ship_paint.Graph(material)
    coords = g.node("ShaderNodeTexCoord").outputs["Object"]

    def mapped(**inputs):
        mapping = g.node("ShaderNodeMapping")
        for name, value in inputs.items():
            mapping.inputs[name].default_value = value
        g.feed(mapping.inputs["Vector"], coords)
        return mapping.outputs["Vector"]

    def noise(scale, shift):
        texture = g.node("ShaderNodeTexNoise")
        texture.inputs["Scale"].default_value = scale
        texture.inputs["Detail"].default_value = 0.0
        g.feed(texture.inputs["Vector"], mapped(Location=(shift, shift * 0.7, -shift)))
        return texture.outputs[0]

    brush = g.node("ShaderNodeTexImage", image=brush_image, projection="BOX", projection_blend=0.25)
    g.feed(brush.inputs["Vector"], mapped(Scale=(1 / BRUSH_TILE,) * 3))
    strokes = []
    for flip, patch, shift in (((1, 1, 1), 0.5, 0.0), ((-1, 1, 0.6), 0.57, 13.0)):
        wave = g.node("ShaderNodeTexWave", wave_type="BANDS", bands_direction="DIAGONAL")
        for name, value in (("Scale", HATCH_SCALE), ("Distortion", 1.6), ("Detail", 1.0), ("Detail Scale", 0.5)):
            wave.inputs[name].default_value = value
        g.feed(wave.inputs["Vector"], mapped(Scale=flip))
        lines = g.math("MULTIPLY", g.math("GREATER_THAN", wave.outputs[0], 0.88),
                       g.math("GREATER_THAN", noise(3.0, shift + 5.0), 0.38))
        strokes.append(g.math("MULTIPLY", lines, g.math("GREATER_THAN", noise(0.36, shift), patch)))
    emitter = g.node("ShaderNodeEmission")
    g.tree.links.new(emitter.outputs[0], g.node("ShaderNodeOutputMaterial").inputs["Surface"])
    g.tree.nodes.active = g.node("ShaderNodeTexImage", name="BAKE TARGET")
    # Blue marks every baked texel, so texels away from the islands can stay 0.
    outputs = {"brush": brush.outputs["Color"], "hatch": g.colour(strokes[0], strokes[1], 1.0)}
    return material, (g, emitter, outputs)


def bake_surface(objects, material):
    """Every hull mesh joined in its own local space, so Object coordinates are each mesh's own."""
    scene = bpy.data.scenes.new("station bake")
    copies = []
    for obj in objects:
        copy = bpy.data.objects.new(f"bake {obj.name}", obj.data.copy())
        scene.collection.objects.link(copy)
        copies.append(copy)
    bpy.context.window.scene = scene
    for copy in copies:
        copy.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    surface = bpy.context.view_layer.objects.active
    mesh = surface.data
    mesh.materials.clear()
    mesh.materials.append(material)
    mesh.polygons.foreach_set("material_index", np.zeros(len(mesh.polygons), np.int32))
    layer = mesh.uv_layers[contract.PAINT_UV]
    mesh.uv_layers.active = layer
    layer.active_render = True
    return scene, surface


def bake_masks(objects):
    brush_image = bpy.data.images.load(str(BRUSH_IMAGE), check_existing=False)
    brush_image.colorspace_settings.name = "Non-Color"
    material, graph = bake_material(brush_image)
    scene, surface = bake_surface(objects, material)
    size = station_uv.TEXTURE_SIZE
    image = bpy.data.images.new("station bake", size, size, alpha=False, float_buffer=True)
    image.colorspace_settings.name = "Non-Color"
    graphs = {"surface": (material, graph)}
    brush = ship_paint.bake(scene, surface, graphs, image, "brush", BAKE_MARGIN, quantize=False)[..., 0]
    hatch = ship_paint.bake(scene, surface, graphs, image, "hatch", BAKE_MARGIN, quantize=False)
    baked = hatch[..., 2] > 0.5
    masks = {"Shadow": np.clip(BRUSH_CONTRAST * (ship_paint.BRUSH_MID - brush), 0, 1),
             "Light": np.clip(BRUSH_CONTRAST * (brush - ship_paint.BRUSH_MID), 0, 1),
             "Ink": (hatch[..., 0] + hatch[..., 1]) / 2}
    return {name: np.rint(np.where(baked, mask, 0) * 255).astype(np.uint8) for name, mask in masks.items()}, baked


def preview_material(key, settings, images):
    """The drawn shader's paint-layer albedo over a diffuse surface, sampled through PaintUV."""
    material = bpy.data.materials.new(f"station preview {key}")
    g = ship_paint.Graph(material)
    uv = g.node("ShaderNodeUVMap", uv_map=contract.PAINT_UV).outputs["UV"]

    def sample(mask, strength):
        texture = g.node("ShaderNodeTexImage", image=images[mask])
        g.feed(texture.inputs["Vector"], uv)
        return g.math("MULTIPLY", texture.outputs["Color"], strength)

    base = settings["hull"][key]
    albedo = g.vector("SCALE", base, scale=g.math("SUBTRACT", 1.0, sample("Shadow", settings["brush"])))
    albedo = g.mix_colour(sample("Light", settings["brush"]), albedo, tuple(min(1.0, 2 * c) for c in base))
    albedo = g.mix_colour(sample("Ink", 2 * settings["hatch"]), albedo, settings["ink"])
    diffuse = g.node("ShaderNodeBsdfDiffuse")
    g.feed(diffuse.inputs["Color"], albedo)
    g.tree.links.new(diffuse.outputs[0], g.node("ShaderNodeOutputMaterial").inputs["Surface"])
    return material


def render_preview(scene, objects, settings, layers, path):
    bpy.context.window.scene = scene
    images = {}
    for mask in MASKS:
        images[mask] = bpy.data.images.load(str(layers / f"{mask}.png"), check_existing=False)
        images[mask].colorspace_settings.name = "Non-Color"
    materials = {key: preview_material(key, settings, images) for key in settings["hull"]}
    for obj in objects:
        for slot in range(len(obj.data.materials)):
            obj.data.materials[slot] = materials[obj["key"]]
    for obj in scene.objects:
        if obj.type == "MESH" and obj.get("key") in (None, "ink"):
            obj.hide_render = True
    root = objects[0]
    while root.parent is not None:
        root = root.parent
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.film_transparent = False
    scene.render.resolution_x = scene.render.resolution_y = PREVIEW_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    panels = []
    with tempfile.TemporaryDirectory() as scratch:
        for index, rotation in enumerate((root.rotation_quaternion.copy(), TILTED.to_quaternion())):
            root.rotation_quaternion = rotation
            scene.render.filepath = str(Path(scratch) / f"view{index}.png")
            bpy.ops.render.render(write_still=True)
            panels.append(ship_source.load_pixels(scene.render.filepath))
    path.parent.mkdir(parents=True, exist_ok=True)
    ship_paint.write_png(path, np.rint(np.concatenate(panels, axis=1) * 255).astype(np.uint8))


def main(argv):
    parser = contract.ArgumentParser(description="Bake a station's Shadow, Light and Ink masks.")
    parser.add_argument("--station", required=True, help="Path to the station .blend (read only)")
    parser.add_argument("--unity", help="Existing folder that also receives the three masks")
    parser.add_argument("--out", required=True, help="Folder for the review image (outside the source tree)")
    args = parser.parse_args(argv)
    source = Path(args.station).resolve()
    settings_path = source.parent.parent / "outpost-colour.json"
    for path in (source, settings_path, BRUSH_IMAGE):
        if not path.is_file():
            raise contract.UsageError(f"Not found: {path}")
    if args.unity and not Path(args.unity).is_dir():
        raise contract.UsageError(f"--unity: not a folder: {args.unity}")
    scene = ship_source.open_source(source)
    objects = station_uv.hull_objects(scene)
    if not objects:
        raise contract.UsageError(f"No hull meshes (key in {station_uv.HULL_KEYS}) in {source}")
    missing = [o.name for o in objects if contract.PAINT_UV not in o.data.uv_layers]
    if missing:
        raise contract.UsageError(f"{len(missing)} hull meshes have no {contract.PAINT_UV} (run station_uv): {missing[:3]}")
    settings = load_settings(settings_path, sorted({o["key"] for o in objects}))

    started = time.perf_counter()
    masks, baked = bake_masks(objects)
    seconds = time.perf_counter() - started
    layers = source.parent / contract.LAYERS_DIR
    layers.mkdir(exist_ok=True)
    for name, pixels in masks.items():
        ship_paint.write_png(layers / f"{name}.png", pixels)
        inside = pixels[baked]
        print(f"{name}: mean {inside.mean() / 255:.3f} over baked texels, {np.count_nonzero(inside) / inside.size:.3f} "
              f"of them non-zero, {len(np.unique(inside))} levels", file=sys.stderr)
    if args.unity:
        for name in MASKS:
            shutil.copyfile(layers / f"{name}.png", Path(args.unity) / f"{name}.png")
    print(f"{source.name}: baked {len(objects)} hull meshes at {station_uv.TEXTURE_SIZE} in {seconds:.1f} s, "
          f"{baked.mean():.3f} of the atlas baked", file=sys.stderr)
    preview = Path(args.out).resolve() / f"{source.stem}-flat.png"
    render_preview(scene, objects, settings, layers, preview)
    return contract.EXIT_PASS, [("STATION_MASKS", str(layers)), ("STATION_PREVIEW", str(preview))]


if __name__ == "__main__":
    contract.run(main)
