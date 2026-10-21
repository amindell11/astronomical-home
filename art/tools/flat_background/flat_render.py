"""Render starless periodic clouds in Blender as a native repeating EXR plus JSON sidecar.

CLI: blender -b --python-exit-code 1 -P flat_render.py -- --out PATH
--stage draft|final, --width N, --height N, --samples N, --preset JSON,
--unity-project PATH and --name NAME are optional. Default sizes: 1024 draft,
2048 final, square unless height is supplied. Exit 0 means complete; Blender's
--python-exit-code makes failures nonzero.

Outputs <out>.exr (scene-linear half float) and <out>.json: schema_version,
stage, dimensions, four RGB palette roles (base/primary/secondary/accent) and
provenance including the migration report (non-empty only when a schema 1
preset was read). Unity publishing uses flat_unity.publish, which replaces the
sidecar before the image.
"""

import argparse
import json
import math
from pathlib import Path
import sys
import time

import bpy

if __package__:
    from . import flat_preset, flat_unity
else:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import flat_preset
    import flat_unity


def configure_scene(width, height, samples, out_path):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "16"
    scene.render.image_settings.exr_codec = "ZIP"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.render.filepath = out_path
    scene.cycles.samples = samples
    scene.cycles.sampling_pattern = "TABULATED_SOBOL"
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.seed = 0
    scene.cycles.max_bounces = 4
    scene.cycles.diffuse_bounces = 1
    scene.cycles.glossy_bounces = 1
    scene.cycles.transmission_bounces = 1
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

    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.name = "Flat Background Camera"
    camera.location = (0, 0, 2)
    camera.rotation_euler = (0, 0, 0)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2 * max(1, width / height)
    camera.data.clip_start = 0.01
    camera.data.clip_end = 1000.0
    scene.camera = camera


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


def build_scene(preset, out_base, width=1024, height=1024, samples=8):
    out_base = str(Path(out_base).resolve())
    preset = flat_preset.parse(preset)
    if not all(type(v) is int and 16 <= v <= 8192 for v in (width, height)):
        raise ValueError("Flat dimensions must be integers between 16 and 8192")
    if type(samples) is not int or not 1 <= samples <= 256:
        raise ValueError("Samples must be an integer between 1 and 256")
    scene = bpy.data.scenes.new("Flat Cloud Render")
    previous = bpy.context.window.scene
    bpy.context.window.scene = scene
    try:
        configure_scene(width, height, samples, out_base + ".exr")
        bpy.ops.mesh.primitive_plane_add(size=2)
        plane = bpy.context.object
        plane.scale.x = width / height
        for vertex in plane.data.vertices:
            vertex.co *= 1.1
        for loop in plane.data.uv_layers.active.data:
            loop.uv = ((loop.uv.x - 0.5) * 1.1 + 0.5, (loop.uv.y - 0.5) * 1.1 + 0.5)
        mat = bpy.data.materials.new("Periodic Starless Clouds")
        mat.use_nodes = True
        plane.data.materials.append(mat)
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        nodes.clear()
        nebula = preset["nebula"]

        def math_node(operation, a, b=0):
            node = nodes.new("ShaderNodeMath")
            node.operation = operation
            for i, value in enumerate((a, b)):
                if isinstance(value, (float, int)):
                    node.inputs[i].default_value = value
                else:
                    links.new(value, node.inputs[i])
            return node.outputs[0]

        uv = nodes.new("ShaderNodeTexCoord")
        separate = nodes.new("ShaderNodeSeparateXYZ")
        links.new(uv.outputs["UV"], separate.inputs[0])
        coords = []
        for axis in range(2):
            phase = math_node("ADD", math_node("MULTIPLY", separate.outputs[axis], math.tau), nebula["rotation"][axis])
            for op in ("COSINE", "SINE"):
                coords.append(math_node("MULTIPLY", math_node(op, phase), nebula["stretch"][axis] * 1.8))
        combine = nodes.new("ShaderNodeCombineXYZ")
        offset = (nebula["variation"] % 104729) * 0.61803398875 + nebula["rotation"][2]
        for i in range(3):
            links.new(math_node("ADD", coords[i], offset + i * 17.13), combine.inputs[i])
        noise = nodes.new("ShaderNodeTexNoise")
        noise.noise_dimensions = "4D"
        noise.inputs["Scale"].default_value = nebula["scale"] * 0.65
        noise.inputs["Detail"].default_value = min(8, 3 + nebula["stretch"][2] * 2)
        noise.inputs["Roughness"].default_value = 0.65
        links.new(combine.outputs[0], noise.inputs["Vector"])
        links.new(math_node("ADD", coords[3], offset + 51.39), noise.inputs["W"])
        fine = nodes.new("ShaderNodeTexNoise")
        fine.noise_dimensions = "4D"
        fine.inputs["Scale"].default_value = nebula["scale"] * 2.6
        fine.inputs["Detail"].default_value = 5
        fine.inputs["Roughness"].default_value = 0.74
        fine.inputs["Distortion"].default_value = 0.8
        links.new(combine.outputs[0], fine.inputs["Vector"])
        links.new(math_node("ADD", coords[3], offset + 51.39), fine.inputs["W"])
        density = math_node("ADD", math_node("MULTIPLY", noise.outputs["Fac"], fine.outputs["Fac"]), nebula["coverage"])
        mask = math_node("MINIMUM", math_node("MAXIMUM", math_node("MULTIPLY", math_node("SUBTRACT", density, 0.22), 5), 0), 1)
        strength = math_node("ADD", math_node("MULTIPLY", math_node("POWER", mask, 1.5), nebula["core_emission"]), 0.005)
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "EASE"
        ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
        for i, (position, color) in enumerate(zip((0.18, 0.40, 0.60, 0.80), nebula["palette"])):
            element = ramp.color_ramp.elements[0] if i == 0 else ramp.color_ramp.elements.new(position)
            element.position = position
            element.color = (*color, 1)
        links.new(noise.outputs["Fac"], ramp.inputs[0])
        emission = nodes.new("ShaderNodeEmission")
        links.new(ramp.outputs[0], emission.inputs[0])
        links.new(strength, emission.inputs[1])
        output = nodes.new("ShaderNodeOutputMaterial")
        links.new(emission.outputs[0], output.inputs["Surface"])
    except Exception:
        bpy.context.window.scene = previous
        dispose_scene(scene)
        raise
    return scene


def save_outputs(scene, preset, out_base, stage, elapsed, migration=()):
    out_base = str(Path(out_base).resolve())
    if stage not in ("draft", "final"):
        raise ValueError("Stage must be draft or final")
    sidecar = {
        "schema_version": 1, "stage": stage,
        "width": scene.render.resolution_x, "height": scene.render.resolution_y,
        "palette": dict(zip(("base", "primary", "secondary", "accent"), preset["nebula"]["palette"])),
        "provenance": {
            "blender_version": bpy.app.version_string, "generator": "4D torus clouds v1",
            "samples": scene.cycles.samples, "render_seconds": elapsed, "preset": preset, "migration": list(migration),
        },
    }
    Path(out_base + ".json").write_text(json.dumps(sidecar, indent=2, allow_nan=False), encoding="utf-8")
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    bpy.data.images["Render Result"].save_render(out_base + "_preview.png", scene=scene)
    print("FLATBG_MIGRATION=" + json.dumps(list(migration)))
    print("FLATBG_PATH=" + out_base + ".exr")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preset")
    parser.add_argument("--out", required=True, help="Output basename without extension")
    parser.add_argument("--stage", choices=("draft", "final"), default="draft")
    parser.add_argument("--width", type=int)
    parser.add_argument("--height", type=int)
    parser.add_argument("--samples", type=int, default=8)
    parser.add_argument("--unity-project")
    parser.add_argument("--name", default="clouds")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    preset, migration = flat_preset.load(args.preset) if args.preset else (flat_preset.defaults(), [])
    width = args.width or (2048 if args.stage == "final" else 1024)
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    previous = bpy.context.window.scene
    scene = build_scene(preset, args.out, width, args.height or width, args.samples)
    try:
        started = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        save_outputs(scene, preset, args.out, args.stage, time.perf_counter() - started, migration)
        if args.unity_project:
            published = flat_unity.publish(args.out, args.unity_project, args.name, args.stage == "final")
            print("FLATBG_PUBLISHED=" + str(published))
    finally:
        bpy.context.window.scene = previous
        dispose_scene(scene)


if __name__ == "__main__":
    main()
