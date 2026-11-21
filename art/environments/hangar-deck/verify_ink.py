"""Reproduce and test exporter ink bounds without exporting or saving the scene.

Run in headless Blender. Writes paired 21:9 paint-only previews and ink-verification.json.
Exit 1 means the pre-bound bars did not reproduce, the bound retained bars, or the
central 16:9 region changed. The temporary ink image stays under .temp.
"""
from pathlib import Path
import ast
import json
import sys
import bpy
import numpy as np

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import game_projection

(ROOT / '.temp').mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene, camera = bpy.context.scene, bpy.context.scene.camera
for obj in scene.objects:
    if obj.name == '25 | Viewport glass' or obj.get('preview_only') or obj.get('export_role', '').endswith(('Button', 'Legend')) or obj.get('export_role') == 'SelectorKnob':
        obj.hide_render = True
white = bpy.data.materials.new('Ink evidence white')
white.use_nodes = True
nodes = white.node_tree.nodes
nodes.clear()
emission = nodes.new('ShaderNodeEmission')
emission.inputs[0].default_value = (1, 1, 1, 1)
output = nodes.new('ShaderNodeOutputMaterial')
white.node_tree.links.new(emission.outputs[0], output.inputs[0])
bpy.context.view_layer.material_override = white
background = scene.world.node_tree.nodes.get('Background')
background.inputs[0].default_value = (1, 1, 1, 1)
background.inputs[1].default_value = 1
scene.cycles.samples = 1
scene.cycles.use_denoising = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
game_projection(scene, camera, 4096, 2304)
for lines in bpy.context.view_layer.freestyle_settings.linesets:
    lines.linestyle.thickness *= 4096 / 1672
    if lines.name == 'Lit legend rim':
        lines.show_render = False
scene.render.filepath = str(ROOT / '.temp/ink-body.png')
bpy.ops.render.render(write_still=True)
body_ink = bpy.data.images.load(scene.render.filepath, check_existing=False)
control_ink = body_ink
bpy.context.view_layer.material_override = None
scene.render.use_freestyle = False
aspect = 16 / 9
tree = ast.parse((ROOT.parent / 'hangar-console/export_console.py').read_text())
function = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'projected_ink')
exec(compile(ast.Module(body=[function], type_ignores=[]), '<exporter projected_ink>', 'exec'))
graphs = []
for material in {m for obj in scene.objects if obj.type == 'MESH' and not obj.hide_render for m in obj.data.materials if m}:
    nodes = material.node_tree.nodes
    principled = nodes.get('Principled BSDF')
    output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
    multiply = nodes.new('ShaderNodeMixRGB')
    multiply.blend_type = 'MULTIPLY'
    multiply.inputs[0].default_value = 1
    multiply.inputs[1].default_value = tuple(principled.inputs['Base Color'].default_value)
    bounded = projected_ink(material)
    assert bounded.node.type == 'MIX_RGB', 'Shared projected ink must end with its bounds mix'
    assert tuple(bounded.node.inputs[2].default_value) == (1, 1, 1, 1)
    unbounded = bounded.node.inputs[1].links[0].from_socket
    material.node_tree.links.new(unbounded, multiply.inputs[2])
    emission = nodes.new('ShaderNodeEmission')
    material.node_tree.links.new(multiply.outputs[0], emission.inputs[0])
    material.node_tree.links.new(emission.outputs[0], output.inputs[0])
    graphs.append((material, bounded, multiply))
background.inputs[0].default_value = (1, 0, 1, 1)
game_projection(scene, camera, 2560, 1080)
images = []
for label in ('unbounded', 'bounded'):
    if label == 'bounded':
        for material, bounded, multiply in graphs:
            material.node_tree.links.new(bounded, multiply.inputs[2])
    scene.render.filepath = str(ROOT / 'previews' / ('deck-ink-' + label + '-21x9.png'))
    bpy.ops.render.render(write_still=True)
    image = bpy.data.images.load(scene.render.filepath, check_existing=False)
    values = np.empty(2560 * 1080 * 4, dtype=np.float32)
    image.pixels.foreach_get(values)
    images.append(values.reshape(1080, 2560, 4)[::-1, :, :3])
before, after = images
counts = []
for values in images:
    counts.append([int((values[450:490, :80].max(axis=2) < .005).sum()),
                   int((values[450:490, -80:].max(axis=2) < .005).sum())])
report = {'black_bar_pixel_boxes_xyxy': [[0, 450, 80, 490], [2480, 450, 2560, 490]],
          'black_linear_rgb_max': .005, 'unbounded_black_pixels': counts[0],
          'bounded_black_pixels': counts[1],
          'central_16x9_changed_pixels': int(np.any(before[:, 320:2240] != after[:, 320:2240], axis=2).sum()),
          'test': 'Shared projected_ink return compared with its pre-bound input; AST function extraction only, no exporter top-level execution, atlas bake or Unity import'}
(ROOT / 'previews/ink-verification.json').write_text(json.dumps(report, indent=2))
assert min(counts[0]) > 2000, report
assert counts[1] == [0, 0], report
assert report['central_16x9_changed_pixels'] == 0, report
print(json.dumps(report))
