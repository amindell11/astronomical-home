"""Bake a station's brush, hatching and hash marks into the drawn shader's Shadow, Light and Ink masks.

CLI: blender -b --factory-startup --python-exit-code 1 -P station_paint.py -- --station BLEND
--out DIR [--unity DIR] [--hatch STRENGTH]
Needs station_uv's PaintUV layer and never saves the .blend. Every hull mesh bakes (Cycles, emission)
in its own local space with the outpost preview's brush and hatch node setups, so the pattern lands
where the preview drew it. The hash-mark meshes bake onto the hull in world space, selected to active.
Written to layers/ beside the source, 8-bit grey on the PaintUV atlas, 0 away from the islands:
  Shadow = clamp(3.2 * (0.625 - brush))   Light = clamp(3.2 * (brush - 0.625))
  Ink = max(hatch * (hatch r + hatch g), marks), hatch from --hatch or the nearest outpost-colour.json
Brush strength stays on the Unity material; ink strength there is 1. --unity also writes the three masks into DIR. The review image
<out>/<station>-flat.png shades the hull with the nearest outpost-colour.json the
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
MARK_SUPERSAMPLE = 2  # marks bake at twice the atlas size and box-filter down, so strokes come out antialiased
MARK_CAGE = 0.1  # marks float 0.035 u off the plate and are 0.03 u thick
MARK_REACH = 0.12
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


def world_join(scene, objects, label):
    """Copies of the objects with their transforms applied, joined into one mesh of `scene`."""
    copies = []
    for obj in objects:
        mesh = obj.data.copy()
        mesh.transform(obj.matrix_world)
        copy = bpy.data.objects.new(f"{label} {obj.name}", mesh)
        scene.collection.objects.link(copy)
        copies.append(copy)
    bpy.context.window.scene = scene
    for obj in scene.objects:
        obj.select_set(obj in copies)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.data.materials.clear()
    joined.data.polygons.foreach_set("material_index", np.zeros(len(joined.data.polygons), np.int32))
    return joined


def bake_marks(objects, marks):
    """Hash-mark coverage on PaintUV: the mark meshes baked onto the hull, selected to active."""
    scene = bpy.data.scenes.new("mark bake")
    surface = world_join(scene, objects, "hull")
    strokes = world_join(scene, marks, "marks")
    size = station_uv.TEXTURE_SIZE * MARK_SUPERSAMPLE
    image = bpy.data.images.new("mark bake", size, size, alpha=False, float_buffer=True)
    image.colorspace_settings.name = "Non-Color"
    target = bpy.data.materials.new("mark target")
    g = ship_paint.Graph(target)
    g.tree.nodes.active = g.node("ShaderNodeTexImage", name="BAKE TARGET", image=image)
    surface.data.materials.append(target)
    ink = bpy.data.materials.new("mark ink")
    g = ship_paint.Graph(ink)
    g.tree.links.new(g.node("ShaderNodeEmission").outputs[0], g.node("ShaderNodeOutputMaterial").inputs["Surface"])
    strokes.data.materials.append(ink)
    layer = surface.data.uv_layers[contract.PAINT_UV]
    surface.data.uv_layers.active = layer
    layer.active_render = True
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.cycles.device = "CPU"
    for obj in scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = surface
    # No margin: texels whose rays miss stay unwritten, and a margin would smear the marks across them.
    bpy.ops.object.bake(type="EMIT", use_clear=True, margin=0,
                        use_selected_to_active=True, cage_extrusion=MARK_CAGE, max_ray_distance=MARK_REACH)
    pixels = np.empty(len(image.pixels), np.float32)
    image.pixels.foreach_get(pixels)
    coverage = pixels.reshape(size, size, 4)[..., 0]
    atlas = station_uv.TEXTURE_SIZE
    return coverage.reshape(atlas, MARK_SUPERSAMPLE, atlas, MARK_SUPERSAMPLE).mean(axis=(1, 3))


def bake_masks(objects, marks, hatch_strength):
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
             "Ink": np.maximum(hatch_strength * (hatch[..., 0] + hatch[..., 1]), bake_marks(objects, marks))}
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
    albedo = g.mix_colour(sample("Ink", 1.0), albedo, settings["ink"])
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
    parser.add_argument("--hatch", type=float, help="Hatch strength baked into Ink, in place of outpost-colour.json's")
    args = parser.parse_args(argv)
    source = Path(args.station).resolve()
    settings_path = next((folder / "outpost-colour.json" for folder in source.parents
                          if (folder / "outpost-colour.json").is_file()), source.parent.parent / "outpost-colour.json")
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
    marks = [o for o in scene.objects if o.type == "MESH" and o.get("key") == "mark"]
    if not marks:
        raise contract.UsageError(f"No hash-mark meshes (key mark) in {source}")

    started = time.perf_counter()
    hatch = settings["hatch"] if args.hatch is None else args.hatch
    print(f"hatch strength {hatch}", file=sys.stderr)
    masks, baked = bake_masks(objects, marks, hatch)
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
    print(f"{source.name}: baked {len(objects)} hull meshes and {len(marks)} mark meshes at {station_uv.TEXTURE_SIZE} in {seconds:.1f} s, "
          f"{baked.mean():.3f} of the atlas baked", file=sys.stderr)
    preview = Path(args.out).resolve() / f"{source.stem}-flat.png"
    render_preview(scene, objects, settings, layers, preview)
    return contract.EXIT_PASS, [("STATION_MASKS", str(layers)), ("STATION_PREVIEW", str(preview))]


if __name__ == "__main__":
    contract.run(main)
