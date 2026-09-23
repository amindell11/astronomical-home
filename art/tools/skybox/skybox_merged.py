"""Procedural HDR space skybox generator.

Renders a seamless 360 equirectangular skybox (volumetric nebula + procedural
star field + explicit HDR anchor stars) to a scene-linear EXR or Radiance HDR,
plus an AgX-tonemapped PNG preview. Run: blender -b -P skybox_merged.py [-- args].
"""

import argparse
import array
import hashlib
import json
import random
from pathlib import Path
import math
import os
import sys
import time

import bpy
from mathutils import Vector


if __package__:
    from . import skybox_preset
else:
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import skybox_preset


def socket(node, names, output=False):
    sockets = node.outputs if output else node.inputs
    for name in names:
        if name in sockets:
            return sockets[name]
    raise KeyError(f"No socket {names!r} on {node.bl_idname}: {[s.name for s in sockets]}")


def set_input(node, names, value):
    socket(node, names).default_value = value


def new_node(nodes, node_type, name, x, y):
    node = nodes.new(node_type)
    node.name = name
    node.label = name
    node.location = (x, y)
    return node


def make_emission_material(name, color, strength):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = new_node(nodes, "ShaderNodeOutputMaterial", "Material Output", 300, 0)
    emission = new_node(nodes, "ShaderNodeEmission", "HDR Emission", 0, 0)
    set_input(emission, ("Color",), (*color, 1.0))
    set_input(emission, ("Strength",), strength)
    links.new(socket(emission, ("Emission",), True), socket(out, ("Surface",)))
    return mat


def build_world(preset):
    STAR_BRIGHTNESS = preset["tiny_stars"]["brightness"]
    SEED = preset["tiny_stars"]["legacy_seed"]
    world = bpy.data.worlds.new("Procedural Seamless Star World")
    bpy.context.scene.world = world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    links = world.node_tree.links
    nodes.clear()

    out = new_node(nodes, "ShaderNodeOutputWorld", "World Output", 1100, 80)
    bg = new_node(nodes, "ShaderNodeBackground", "HDR Star Background", 860, 80)
    set_input(bg, ("Strength",), 1.0)

    texcoord = new_node(nodes, "ShaderNodeTexCoord", "Direction Coordinates", -1100, 100)

    galaxy_noise = new_node(nodes, "ShaderNodeTexNoise", "Deep Space Structure", -850, -300)
    galaxy_noise.noise_dimensions = "3D"
    set_input(galaxy_noise, ("Scale",), 3.2)
    set_input(galaxy_noise, ("Detail",), 5.0)
    set_input(galaxy_noise, ("Roughness",), 0.72)
    set_input(galaxy_noise, ("Distortion",), 0.32)
    galaxy_ramp = new_node(nodes, "ShaderNodeValToRGB", "Space Palette", -570, -300)
    galaxy_ramp.color_ramp.elements.remove(galaxy_ramp.color_ramp.elements[1])
    e0 = galaxy_ramp.color_ramp.elements[0]
    e0.position = 0.20
    e0.color = (0.00012, 0.00018, 0.00055, 1.0)
    e1 = galaxy_ramp.color_ramp.elements.new(0.50)
    e1.color = (0.0011, 0.00035, 0.0035, 1.0)
    e2 = galaxy_ramp.color_ramp.elements.new(0.72)
    e2.color = (0.006, 0.0013, 0.010, 1.0)
    e3 = galaxy_ramp.color_ramp.elements.new(0.88)
    e3.color = (0.0010, 0.0035, 0.009, 1.0)

    # Fine dense stars: cool blue-white.
    star_noise_a = new_node(nodes, "ShaderNodeTexNoise", "Fine Stars", -850, 250)
    star_noise_a.noise_dimensions = "3D"
    set_input(star_noise_a, ("Scale",), 310.0)
    set_input(star_noise_a, ("Detail",), 2.0)
    set_input(star_noise_a, ("Roughness",), 0.52)
    star_ramp_a = new_node(nodes, "ShaderNodeValToRGB", "Fine Star Threshold", -570, 250)
    star_ramp_a.color_ramp.interpolation = "CONSTANT"
    star_ramp_a.color_ramp.elements[0].position = 0.748
    star_ramp_a.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1.0)
    star_ramp_a.color_ramp.elements[1].position = 0.750
    star_ramp_a.color_ramp.elements[1].color = tuple(c * STAR_BRIGHTNESS for c in (2.8, 3.6, 5.2)) + (1.0,)

    # Sparse stars: soft warm-white, for gentle color variety across the field.
    star_noise_b = new_node(nodes, "ShaderNodeTexNoise", "Warm Sparse Stars", -850, 540)
    star_noise_b.noise_dimensions = "3D"
    set_input(star_noise_b, ("Scale",), 740.0)
    set_input(star_noise_b, ("Detail",), 1.0)
    set_input(star_noise_b, ("Roughness",), 0.45)
    set_input(star_noise_b, ("Distortion",), SEED % 1.0)
    star_ramp_b = new_node(nodes, "ShaderNodeValToRGB", "Warm Star Threshold", -570, 540)
    star_ramp_b.color_ramp.interpolation = "CONSTANT"
    star_ramp_b.color_ramp.elements[0].position = 0.785
    star_ramp_b.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1.0)
    star_ramp_b.color_ramp.elements[1].position = 0.787
    star_ramp_b.color_ramp.elements[1].color = tuple(c * STAR_BRIGHTNESS for c in (4.2, 3.6, 2.4)) + (1.0,)

    add_stars = new_node(nodes, "ShaderNodeMixRGB", "Add Star Layers", -260, 370)
    add_stars.blend_type = "ADD"
    add_stars.inputs[0].default_value = 1.0
    add_all = new_node(nodes, "ShaderNodeMixRGB", "Add Deep Space", 150, 130)
    add_all.blend_type = "ADD"
    add_all.inputs[0].default_value = 1.0

    normal = socket(texcoord, ("Normal",), True)
    for noise in (galaxy_noise, star_noise_a, star_noise_b):
        links.new(normal, socket(noise, ("Vector",)))
    links.new(socket(galaxy_noise, ("Fac", "Factor"), True), socket(galaxy_ramp, ("Fac", "Factor")))
    links.new(socket(star_noise_a, ("Fac", "Factor"), True), socket(star_ramp_a, ("Fac", "Factor")))
    links.new(socket(star_noise_b, ("Fac", "Factor"), True), socket(star_ramp_b, ("Fac", "Factor")))
    links.new(socket(star_ramp_a, ("Color",), True), add_stars.inputs[1])
    links.new(socket(star_ramp_b, ("Color",), True), add_stars.inputs[2])
    links.new(socket(galaxy_ramp, ("Color",), True), add_all.inputs[1])
    links.new(socket(add_stars, ("Color",), True), add_all.inputs[2])
    links.new(socket(add_all, ("Color",), True), socket(bg, ("Color",)))
    links.new(socket(bg, ("Background",), True), socket(out, ("Surface",)))


def build_nebula_volume(preset):
    settings = preset["nebula"]
    NEBULA_CORE_EMISSION = settings["core_emission"]
    bpy.ops.mesh.primitive_cube_add(size=2.0, location=(0.0, 0.0, 0.0))
    cube = bpy.context.object
    cube.name = "Nebula Volume Domain"
    cube.scale = (13.0, 13.0, 13.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    mat = bpy.data.materials.new("Wispy Procedural Emissive Nebula")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = new_node(nodes, "ShaderNodeOutputMaterial", "Volume Output", 850, 0)
    volume = new_node(nodes, "ShaderNodeVolumePrincipled", "Principled Nebula Volume", 560, 0)
    set_input(volume, ("Density",), 0.0)
    set_input(volume, ("Color",), (0.12, 0.025, 0.30, 1.0))
    set_input(volume, ("Anisotropy",), 0.25)
    set_input(volume, ("Emission Strength", "Blackbody Intensity"), 0.0)

    texcoord = new_node(nodes, "ShaderNodeTexCoord", "Object Space Coordinates", -1050, 80)
    mapping = new_node(nodes, "ShaderNodeMapping", "Stretch Nebula", -850, 80)
    set_input(mapping, ("Scale",), tuple(v * settings["scale"] for v in settings["stretch"]))
    set_input(mapping, ("Rotation",), settings["rotation"])
    if settings["variation"]:
        rng = random.Random(settings["variation"])
        set_input(mapping, ("Location",), tuple(rng.uniform(-100.0, 100.0) for _ in range(3)))

    noise_large = new_node(nodes, "ShaderNodeTexNoise", "Billowing 3D Noise", -620, 180)
    noise_large.noise_dimensions = "3D"
    set_input(noise_large, ("Scale",), 1.9)
    set_input(noise_large, ("Detail",), 7.0)
    set_input(noise_large, ("Roughness",), 0.68)
    set_input(noise_large, ("Lacunarity",), 2.1)
    set_input(noise_large, ("Distortion",), 0.55)

    noise_fine = new_node(nodes, "ShaderNodeTexNoise", "Filament 3D Noise", -620, -140)
    noise_fine.noise_dimensions = "3D"
    set_input(noise_fine, ("Scale",), 7.5)
    set_input(noise_fine, ("Detail",), 5.0)
    set_input(noise_fine, ("Roughness",), 0.74)
    set_input(noise_fine, ("Distortion",), 0.80)

    multiply_noise = new_node(nodes, "ShaderNodeMath", "Carve Filaments", -350, 100)
    multiply_noise.operation = "MULTIPLY"
    threshold = new_node(nodes, "ShaderNodeValToRGB", "Wispy Density Threshold", -100, 120)
    threshold.color_ramp.interpolation = "EASE"
    threshold.color_ramp.elements[0].position = 0.255 - settings["coverage"]
    threshold.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1.0)
    threshold.color_ramp.elements[1].position = 0.450 - settings["coverage"]
    threshold.color_ramp.elements[1].color = (0.040, 0.040, 0.040, 1.0)
    density_scale = new_node(nodes, "ShaderNodeMath", "Density Scale", 140, 130)
    density_scale.operation = "MULTIPLY"
    density_scale.inputs[1].default_value = 0.30
    emission_scale = new_node(nodes, "ShaderNodeMath", "Masked Emission Scale", 330, -80)
    emission_scale.operation = "MULTIPLY"
    emission_scale.inputs[1].default_value = 30.0

    palette = new_node(nodes, "ShaderNodeValToRGB", "Nebula Emission Palette", -80, -240)
    palette.color_ramp.elements.remove(palette.color_ramp.elements[1])
    p0 = palette.color_ramp.elements[0]
    p0.position = 0.18
    p0.color = (*settings["palette"][0], 1.0)
    p1 = palette.color_ramp.elements.new(0.40)
    p1.color = (*settings["palette"][1], 1.0)
    p2 = palette.color_ramp.elements.new(0.60)
    p2.color = (*settings["palette"][2], 1.0)
    p3 = palette.color_ramp.elements.new(0.80)
    p3.color = (*settings["palette"][3], 1.0)

    links.new(socket(texcoord, ("Generated",), True), socket(mapping, ("Vector",)))
    for noise in (noise_large, noise_fine):
        links.new(socket(mapping, ("Vector",), True), socket(noise, ("Vector",)))
    links.new(socket(noise_large, ("Fac", "Factor"), True), multiply_noise.inputs[0])
    links.new(socket(noise_fine, ("Fac", "Factor"), True), multiply_noise.inputs[1])
    links.new(socket(multiply_noise, ("Value",), True), socket(threshold, ("Fac", "Factor")))
    links.new(socket(threshold, ("Color",), True), density_scale.inputs[0])
    links.new(socket(density_scale, ("Value",), True), socket(volume, ("Density",)))
    links.new(socket(density_scale, ("Value",), True), emission_scale.inputs[0])
    core_gain = new_node(nodes, "ShaderNodeMath", "Density-Weighted Core Gain", 140, -430)
    core_gain.operation = "MULTIPLY_ADD"
    max_density = threshold.color_ramp.elements[1].color[0] * density_scale.inputs[1].default_value
    luminance_weights = (0.2126, 0.7152, 0.0722)
    max_luminance = max(sum(e.color[i] * w for i, w in enumerate(luminance_weights))
                        for e in palette.color_ramp.elements)
    emitted_luminance = new_node(nodes, "ShaderNodeVectorMath", "Nebula Color Luminance", -100, -500)
    emitted_luminance.operation = "DOT_PRODUCT"
    emitted_luminance.inputs[1].default_value = luminance_weights
    core_mask = new_node(nodes, "ShaderNodeMath", "Bright Dense Core Mask", 140, -600)
    core_mask.operation = "MULTIPLY"
    core_gain.inputs[1].default_value = (NEBULA_CORE_EMISSION - 1.0) / (max_density * max_luminance)
    core_gain.inputs[2].default_value = 1.0
    core_emission = new_node(nodes, "ShaderNodeMath", "Core Emission", 330, -300)
    core_emission.operation = "MULTIPLY"
    links.new(socket(palette, ("Color",), True), emitted_luminance.inputs[0])
    links.new(socket(density_scale, ("Value",), True), core_mask.inputs[0])
    links.new(socket(emitted_luminance, ("Value",), True), core_mask.inputs[1])
    links.new(socket(core_mask, ("Value",), True), core_gain.inputs[0])
    links.new(socket(emission_scale, ("Value",), True), core_emission.inputs[0])
    links.new(socket(core_gain, ("Value",), True), core_emission.inputs[1])
    links.new(socket(core_emission, ("Value",), True), socket(volume, ("Emission Strength", "Blackbody Intensity")))
    links.new(socket(noise_large, ("Fac", "Factor"), True), socket(palette, ("Fac", "Factor")))
    links.new(socket(palette, ("Color",), True), socket(volume, ("Emission Color", "Blackbody Tint")))
    links.new(socket(volume, ("Volume",), True), socket(out, ("Volume",)))
    cube.data.materials.append(mat)


def build_anchor_stars(preset):
    brightness = preset["anchor_brightness"]
    if brightness == 0.0:
        return

    for i, anchor in enumerate(preset["anchors"], 1):
        direction, radius = anchor["direction"], anchor["radius"]
        color, strength = anchor["color"], anchor["strength"]
        pos = Vector(direction).normalized() * 8.0
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius, location=pos)
        star = bpy.context.object
        star.name = f"HDR Anchor Star {i:02d}"
        star.data.materials.append(make_emission_material(f"Anchor {i:02d} HDR", color, strength * brightness))


def configure_scene(WIDTH, HEIGHT, SAMPLES, FORMAT, OUT_PATH):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.render.resolution_x = WIDTH
    scene.render.resolution_y = HEIGHT
    scene.render.resolution_percentage = 100
    if FORMAT == "HDR":
        scene.render.image_settings.file_format = "HDR"
        scene.render.image_settings.color_mode = "RGB"
    else:
        scene.render.image_settings.file_format = "OPEN_EXR"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.image_settings.color_depth = "32"
        scene.render.image_settings.exr_codec = "ZIP"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.render.filepath = OUT_PATH
    scene.cycles.samples = SAMPLES
    scene.cycles.sampling_pattern = "TABULATED_SOBOL"
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.adaptive_threshold = 0.05
    scene.cycles.max_bounces = 4
    scene.cycles.diffuse_bounces = 1
    scene.cycles.glossy_bounces = 1
    scene.cycles.transmission_bounces = 1
    scene.cycles.volume_bounces = 2
    scene.cycles.volume_step_rate = 1.5
    scene.cycles.use_denoising = False

    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for backend in ("OPTIX", "HIP", "ONEAPI", "METAL", "CUDA"):
            try:
                prefs.compute_device_type = backend
                prefs.get_devices()
                enabled = False
                for device in prefs.devices:
                    device.use = device.type != "CPU"
                    enabled = enabled or device.use
                if enabled:
                    scene.cycles.device = "GPU"
                    print(f"Using Cycles GPU backend: {backend}")
                    break
            except Exception:
                continue
    except Exception as exc:
        print(f"Cycles GPU selection unavailable; using CPU: {exc}")

    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0

    bpy.ops.object.camera_add(location=(0.0, 0.0, 0.0))
    camera = bpy.context.object
    camera.name = "Equirectangular Camera at Origin"
    camera.data.type = "PANO"
    camera.data.panorama_type = "EQUIRECTANGULAR"
    camera.data.longitude_min = -math.pi
    camera.data.longitude_max = math.pi
    camera.data.latitude_min = -math.pi / 2.0
    camera.data.latitude_max = math.pi / 2.0
    camera.data.clip_start = 0.01
    camera.data.clip_end = 1000.0
    scene.camera = camera


def save_outputs(scene, preset, out_base, file_format, elapsed):
    WIDTH, HEIGHT = scene.render.resolution_x, scene.render.resolution_y
    SAMPLES = scene.cycles.samples
    FORMAT = file_format
    OUT_PATH = out_base + {"HDR": ".hdr", "EXR": ".exr"}[FORMAT]
    PNG_PATH, REPORT_PATH = out_base + "_preview.png", out_base + "_report.txt"
    STAR_BRIGHTNESS = preset["tiny_stars"]["brightness"]
    ANCHOR_BRIGHTNESS = preset["anchor_brightness"]
    NEBULA_CORE_EMISSION = preset["nebula"]["core_emission"]

    result = bpy.data.images.get("Render Result")
    if result is None:
        raise RuntimeError("Blender did not create a Render Result")
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    result.save_render(PNG_PATH, scene=scene)

    exr = bpy.data.images.load(OUT_PATH, check_existing=False)
    pixels = array.array("f", [0.0]) * len(exr.pixels)
    exr.pixels.foreach_get(pixels)
    max_rgb = 0.0
    above1 = 0
    total = 0
    for i in range(0, len(pixels), 4):
        r, g, b = pixels[i], pixels[i + 1], pixels[i + 2]
        max_rgb = max(max_rgb, r, g, b)
        total += 1
        if max(r, g, b) > 1.0:
            above1 += 1
    bpy.data.images.remove(exr)

    report = (
        f"resolution={WIDTH}x{HEIGHT}\n"
        f"engine={scene.render.engine}\n"
        f"samples={SAMPLES}\n"
        f"star_brightness={STAR_BRIGHTNESS}\n"
        f"anchor_brightness={ANCHOR_BRIGHTNESS}\n"
        f"nebula_core_emission={NEBULA_CORE_EMISSION}\n"
        f"render_seconds={elapsed:.3f}\n"
        f"max_rgb={max_rgb:.9g}\n"
        f"pct_pixels_above_1={100.0 * above1 / total:.4f}\n"
        f"format={FORMAT}\n"
        f"out={OUT_PATH}\n"
        f"png={PNG_PATH}\n"
    )
    with open(REPORT_PATH, "w", encoding="utf-8") as handle:
        handle.write(report)
    skybox_preset.save(out_base + "_preset.json", preset)
    sources = [Path(__file__), Path(skybox_preset.__file__)]
    provenance = {
        "blender_version": bpy.app.version_string,
        "generator_sha256": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
        "width": WIDTH, "height": HEIGHT, "samples": SAMPLES,
        "format": FORMAT, "render_seconds": elapsed,
        "max_rgb": max_rgb, "pct_pixels_above_1": 100.0 * above1 / total,
        "preset": preset, "out": OUT_PATH,
    }
    Path(out_base + "_render.json").write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
    print("\nSKYBOX_RENDER_REPORT\n" + report)


def build_scene(preset, out_base, width=1024, samples=32, file_format="HDR"):
    """Build in a separate scene; the caller restores its scene and disposes this one."""
    scene = bpy.data.scenes.new("Skybox Render")
    bpy.context.window.scene = scene
    try:
        configure_scene(width, width // 2, samples, file_format,
                        out_base + {"HDR": ".hdr", "EXR": ".exr"}[file_format])
        build_world(preset)
        build_nebula_volume(preset)
        build_anchor_stars(preset)
    except Exception:
        dispose_scene(scene)
        raise
    return scene


def dispose_scene(scene):
    for obj in list(scene.objects):
        data = obj.data
        materials = list(data.materials) if hasattr(data, "materials") else []
        bpy.data.objects.remove(obj, do_unlink=True)
        if data.users == 0:
            (bpy.data.cameras if isinstance(data, bpy.types.Camera) else bpy.data.meshes).remove(data)
        for material in materials:
            if material.users == 0:
                bpy.data.materials.remove(material)
    world = scene.world
    bpy.data.scenes.remove(scene)
    if world and world.users == 0:
        bpy.data.worlds.remove(world)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preset", help="Versioned JSON preset; CLI brightness flags override it")
    parser.add_argument("--width", type=int, default=2048)
    parser.add_argument("--height", type=int)
    parser.add_argument("--samples", type=int, default=32)
    parser.add_argument("--format", type=str.upper, choices=("EXR", "HDR"), default="EXR")
    parser.add_argument("--out", default=str(Path(__file__).with_suffix("")))
    parser.add_argument("--seed", type=float)
    parser.add_argument("--star-brightness", type=float)
    parser.add_argument("--anchor-brightness", type=float)
    parser.add_argument("--nebula-core-emission", type=float)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if args.width < 2 or args.width % 2 or (args.height is not None and args.height != args.width // 2):
        parser.error("width/height must describe a positive 2:1 panorama")
    if args.samples < 1:
        parser.error("samples must be positive")
    preset = skybox_preset.load(args.preset) if args.preset else skybox_preset.defaults()
    for value, mapping, key in (
        (args.seed, preset["tiny_stars"], "legacy_seed"),
        (args.star_brightness, preset["tiny_stars"], "brightness"),
        (args.anchor_brightness, preset, "anchor_brightness"),
        (args.nebula_core_emission, preset["nebula"], "core_emission"),
    ):
        if value is not None:
            mapping[key] = value
    preset = skybox_preset.parse(preset)
    out_base = os.path.abspath(os.path.splitext(args.out)[0])
    Path(out_base).parent.mkdir(parents=True, exist_ok=True)
    previous = bpy.context.window.scene
    scene = build_scene(preset, out_base, args.width, args.samples, args.format)
    try:
        start = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        save_outputs(scene, preset, out_base, args.format, time.perf_counter() - start)
    finally:
        bpy.context.window.scene = previous
        dispose_scene(scene)


if __name__ == "__main__":
    main()
