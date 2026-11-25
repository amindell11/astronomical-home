"""Render six game projections with a camera-only magenta world and no hologram.

Blender --python verify_coverage.py [-- --baseline] writes coverage images and
projection checks. Baseline evidence lives in previews/coverage-reference.
assemble_coverage.py measures saved PNGs and writes the labelled contact sheet.
"""
from pathlib import Path
import json
import math
import sys
import bpy

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import VIEWS, game_projection

baseline = '--baseline' in sys.argv
out = ROOT / 'previews' / 'coverage-reference' if baseline else ROOT / 'previews'
bpy.ops.wm.open_mainfile(filepath=str(ROOT / ('.tmp/baseline.blend' if baseline else 'deck.blend')))
scene = bpy.context.scene
for obj in scene.objects:
    if obj.get('preview_only') or obj.name.startswith(('07 reuse | Vanguard', '19 | Vanguard',
            '19 | Distant space', '11 | Neutral projector frustum', '25 | Viewport glass')):
        obj.hide_render = True

nodes = scene.world.node_tree.nodes
links = scene.world.node_tree.links
background = nodes.get('Background')
output = next(n for n in nodes if n.type == 'OUTPUT_WORLD')
magenta = nodes.new('ShaderNodeBackground')
magenta.inputs['Color'].default_value = (1, 0, 1, 1)
magenta.inputs['Strength'].default_value = 1
path = nodes.new('ShaderNodeLightPath')
mix = nodes.new('ShaderNodeMixShader')
links.new(path.outputs['Is Camera Ray'], mix.inputs[0])
links.new(background.outputs[0], mix.inputs[1])
links.new(magenta.outputs[0], mix.inputs[2])
links.new(mix.outputs[0], output.inputs['Surface'])
scene.render.film_transparent = False
scene.render.resolution_percentage = 100
scene.render.image_settings.color_mode = 'RGB'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.cycles.samples = 16
scene.cycles.use_denoising = False
records = {}
for label, width, height in VIEWS:
    record = game_projection(scene, scene.camera, width, height)
    matrix = scene.camera.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(), x=width, y=height)
    expected_y = 1 / math.tan(math.radians(record['effective_vertical_fov_degrees']) / 2)
    errors = [abs(matrix[0][0] - expected_y / record['aspect']),
              abs(matrix[1][1] - expected_y), abs(matrix[1][2] - record['m12'])]
    record['matrix_max_error'] = max(errors)
    assert max(errors) < 1e-6, (label, errors)
    record['image'] = 'deck-coverage-' + label + '.png'
    record['coverage_samples'] = scene.cycles.samples
    record['coverage_denoising'] = scene.cycles.use_denoising
    scene.render.filepath = str(out / record['image'])
    bpy.ops.render.render(write_still=True)
    records[label] = record
    print('PROJECTION', label, json.dumps(record), flush=True)
(out / 'coverage-projections.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
