"""Render starless periodic clouds in Blender as a native repeating EXR plus JSON sidecar.

CLI: blender -b --python-exit-code 1 -P skybox_flat.py -- --out PATH
--stage draft|final, --width N, --height N, --samples N, --preset JSON,
--unity-project PATH and --name NAME are optional. Default sizes: 1024 draft,
2048 final, square unless height is supplied. Exit 0 means complete; Blender's
--python-exit-code makes failures nonzero.

Outputs <out>.exr (scene-linear half float) and <out>.json: schema_version,
stage, dimensions, four RGB palette roles (base/primary/secondary/accent) and
provenance including the preset migration report. Unity publishing reuses
skybox_unity.publish, which replaces the sidecar before the image.
"""

import argparse
import json
import math
from pathlib import Path
import sys
import time

import bpy

if __package__:
    from . import skybox_merged, skybox_preset, skybox_unity
else:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import skybox_merged
    import skybox_preset
    import skybox_unity

MIGRATION = [
    "Palette, variation, scale, coverage and core emission retain their authoring roles.",
    "Stretch X/Y control torus radii; stretch Z controls cloud detail.",
    "Rotation X/Y shift periodic phases; rotation Z offsets the 4D field.",
    "Tiny stars, their seed, focal-star brightness and all anchors are ignored; stars belong to Unity.",
    "Spherical cloud positions are regenerated; historical panoramas are not visually reproduced.",
]


def build_scene(preset, out_base, width=1024, height=1024, samples=8):
    out_base = str(Path(out_base).resolve())
    preset = skybox_preset.parse(preset)
    if not all(type(v) is int and 16 <= v <= 8192 for v in (width, height)):
        raise ValueError("Flat dimensions must be integers between 16 and 8192")
    if type(samples) is not int or not 1 <= samples <= 256:
        raise ValueError("Samples must be an integer between 1 and 256")
    scene = bpy.data.scenes.new("Flat Cloud Render")
    previous = bpy.context.window.scene
    bpy.context.window.scene = scene
    try:
        skybox_merged.configure_scene(width, height, samples, "EXR", out_base + ".exr")
        scene.render.image_settings.color_depth = "16"
        scene.cycles.use_adaptive_sampling = False
        scene.cycles.seed = 0
        scene.camera.data.type = "ORTHO"
        scene.camera.data.ortho_scale = 2 * max(1, width / height)
        scene.camera.location = (0, 0, 2)
        scene.camera.rotation_euler = (0, 0, 0)
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
        skybox_merged.dispose_scene(scene)
        raise
    return scene


def save_outputs(scene, preset, out_base, stage, elapsed):
    out_base = str(Path(out_base).resolve())
    if stage not in ("draft", "final"):
        raise ValueError("Stage must be draft or final")
    sidecar = {
        "schema_version": 1, "stage": stage,
        "width": scene.render.resolution_x, "height": scene.render.resolution_y,
        "palette": dict(zip(("base", "primary", "secondary", "accent"), preset["nebula"]["palette"])),
        "provenance": {
            "blender_version": bpy.app.version_string, "generator": "4D torus clouds v1",
            "samples": scene.cycles.samples, "render_seconds": elapsed, "preset": preset, "migration": MIGRATION,
        },
    }
    Path(out_base + ".json").write_text(json.dumps(sidecar, indent=2, allow_nan=False), encoding="utf-8")
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    bpy.data.images["Render Result"].save_render(out_base + "_preview.png", scene=scene)
    print("FLATBG_MIGRATION=" + json.dumps(MIGRATION))
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
    preset = skybox_preset.load(args.preset) if args.preset else skybox_preset.defaults(glow=True)
    width = args.width or (2048 if args.stage == "final" else 1024)
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    previous = bpy.context.window.scene
    scene = build_scene(preset, args.out, width, args.height or width, args.samples)
    try:
        started = time.perf_counter()
        bpy.ops.render.render(write_still=True)
        save_outputs(scene, preset, args.out, args.stage, time.perf_counter() - started)
        if args.unity_project:
            published = skybox_unity.publish(args.out, args.unity_project, args.name,
                                             args.stage == "final", **skybox_unity.FLAT)
            print("FLATBG_PUBLISHED=" + str(published))
    finally:
        bpy.context.window.scene = previous
        skybox_merged.dispose_scene(scene)


if __name__ == "__main__":
    main()
