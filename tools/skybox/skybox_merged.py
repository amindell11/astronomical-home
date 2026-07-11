"""Merged procedural HDR space skybox: Codex's nebula/anchor architecture, with
wider nebula coverage and cooler (blue-white-leaning) field-star colors.

Run: blender --background --python skybox_merged.py
Outputs (EXR scene-linear 32-bit, PNG AgX preview) are written beside this script.
"""

import array
import math
import os
import sys
import time

import bpy
from mathutils import Vector

def _cli_args():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    out = {}
    i = 0
    while i < len(args):
        key = args[i].lstrip("-")
        out[key] = args[i + 1]
        i += 2
    return out


_ARGS = _cli_args()
OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))
WIDTH = int(_ARGS.get("width", 2048))
HEIGHT = int(_ARGS.get("height", 1024))
SAMPLES = int(_ARGS.get("samples", 32))
SEED = float(_ARGS.get("seed", 7319.0))
FORMAT = _ARGS.get("format", "EXR").upper()
_EXT = {"EXR": ".exr", "HDR": ".hdr"}[FORMAT]
_OUT_BASE = os.path.abspath(os.path.splitext(_ARGS.get("out", os.path.join(OUTPUT_DIR, "skybox_merged")))[0])
OUT_PATH = _OUT_BASE + _EXT
PNG_PATH = _OUT_BASE + "_preview.png"
REPORT_PATH = _OUT_BASE + "_report.txt"


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


def build_world():
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

    # Fine dense stars: cool blue-white (kept from Codex).
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
    star_ramp_a.color_ramp.elements[1].color = (2.8, 3.6, 5.2, 1.0)

    # Sparse layer: nudged from saturated orange -> soft warm-white (cooler, toward
    # the Claude-version star palette) so the field reads blue-white with gentle warmth.
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
    star_ramp_b.color_ramp.elements[1].color = (4.2, 3.6, 2.4, 1.0)

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


def build_nebula_volume():
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
    # Less extreme flattening -> gas spreads across more of the sphere.
    set_input(mapping, ("Scale",), (0.62, 1.05, 0.62))
    set_input(mapping, ("Rotation",), (0.20, -0.35, 0.58))

    noise_large = new_node(nodes, "ShaderNodeTexNoise", "Billowing 3D Noise", -620, 180)
    noise_large.noise_dimensions = "3D"
    set_input(noise_large, ("Scale",), 1.9)  # bigger billows (was 2.4)
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
    # Lower threshold -> much more of the noise becomes gas (wider coverage).
    threshold.color_ramp.elements[0].position = 0.255
    threshold.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1.0)
    threshold.color_ramp.elements[1].position = 0.450
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
    p0.color = (0.015, 0.001, 0.055, 1.0)
    p1 = palette.color_ramp.elements.new(0.40)
    p1.color = (0.35, 0.008, 0.52, 1.0)
    p2 = palette.color_ramp.elements.new(0.60)
    p2.color = (0.018, 0.18, 0.72, 1.0)
    p3 = palette.color_ramp.elements.new(0.80)
    p3.color = (0.75, 0.025, 0.16, 1.0)

    links.new(socket(texcoord, ("Generated",), True), socket(mapping, ("Vector",)))
    for noise in (noise_large, noise_fine):
        links.new(socket(mapping, ("Vector",), True), socket(noise, ("Vector",)))
    links.new(socket(noise_large, ("Fac", "Factor"), True), multiply_noise.inputs[0])
    links.new(socket(noise_fine, ("Fac", "Factor"), True), multiply_noise.inputs[1])
    links.new(socket(multiply_noise, ("Value",), True), socket(threshold, ("Fac", "Factor")))
    links.new(socket(threshold, ("Color",), True), density_scale.inputs[0])
    links.new(socket(density_scale, ("Value",), True), socket(volume, ("Density",)))
    links.new(socket(density_scale, ("Value",), True), emission_scale.inputs[0])
    links.new(socket(emission_scale, ("Value",), True), socket(volume, ("Emission Strength", "Blackbody Intensity")))
    links.new(socket(noise_large, ("Fac", "Factor"), True), socket(palette, ("Fac", "Factor")))
    links.new(socket(palette, ("Color",), True), socket(volume, ("Emission Color", "Blackbody Tint")))
    links.new(socket(volume, ("Volume",), True), socket(out, ("Volume",)))
    cube.data.materials.append(mat)


def build_anchor_stars():
    anchors = [
        ((0.78, -0.36, 0.51), 0.055, (0.55, 0.72, 1.00), 420.0),
        ((-0.43, -0.81, 0.40), 0.070, (1.00, 0.47, 0.17), 260.0),
        ((-0.70, 0.31, -0.64), 0.050, (0.52, 0.66, 1.00), 600.0),
        ((0.18, 0.91, 0.37), 0.062, (1.00, 0.78, 0.45), 360.0),
        ((0.52, 0.43, -0.74), 0.045, (0.70, 0.82, 1.00), 850.0),
    ]
    for i, (direction, radius, color, strength) in enumerate(anchors, 1):
        pos = Vector(direction).normalized() * 8.0
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius, location=pos)
        star = bpy.context.object
        star.name = f"HDR Anchor Star {i:02d}"
        star.data.materials.append(make_emission_material(f"Anchor {i:02d} HDR", color, strength))


def configure_scene():
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


def save_and_verify():
    scene = bpy.context.scene
    start = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    elapsed = time.perf_counter() - start

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
        f"render_seconds={elapsed:.3f}\n"
        f"max_rgb={max_rgb:.9g}\n"
        f"pct_pixels_above_1={100.0 * above1 / total:.4f}\n"
        f"format={FORMAT}\n"
        f"out={OUT_PATH}\n"
        f"png={PNG_PATH}\n"
    )
    with open(REPORT_PATH, "w", encoding="utf-8") as handle:
        handle.write(report)
    print("\nSKYBOX_RENDER_REPORT\n" + report)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    configure_scene()
    build_world()
    build_nebula_volume()
    build_anchor_stars()
    save_and_verify()


if __name__ == "__main__":
    main()
