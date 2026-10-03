import argparse
import json
import math
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

OUT = Path(__file__).resolve().parent
FPS = 20
TRANSITION_FRAMES = 10
HOLD_FRAMES = 22
MAIN_IDLE = 0.08
FIN_IDLE = 0.50
MAIN_PREFIXES = ('10 ', '11 ', '12 ', '13 ')
FIN_PREFIXES = ('30 ', '31 ', '70 forward', '71 forward')

parser = argparse.ArgumentParser()
parser.add_argument('--full', action='store_true')
args = parser.parse_args(__import__('sys').argv[__import__('sys').argv.index('--') + 1:])
forward = json.loads((OUT / 'forward-reference.json').read_text())['parts']
scene = bpy.context.scene
objects = {o.name: o for o in scene.objects if o.type == 'MESH'}
base = {name: o.matrix_basis.copy() for name, o in objects.items()}
original_vertices = {name: [list(v.co) for v in o.data.vertices] for name, o in objects.items()}
models = {}
report = {}

for name, o in objects.items():
    assert original_vertices[name] == forward[name]['vertices'], name + ': reference reshaped mesh'
    if name.startswith(MAIN_PREFIXES):
        idle = MAIN_IDLE
    elif name.startswith(FIN_PREFIXES):
        idle = FIN_IDLE
    else:
        continue
    target = Matrix(forward[name]['matrix_basis'])
    delta = target @ base[name].inverted()
    rotation = delta.to_quaternion().normalized()
    r = np.array(rotation.to_matrix(), dtype=float)
    translation = np.array(delta.to_translation(), dtype=float)
    pivot, _, _, _ = np.linalg.lstsq(np.eye(3) - r, translation, rcond=0.00001)
    residual = translation - (np.eye(3) - r) @ pivot
    models[name] = (rotation, Vector(pivot), Vector(residual), idle)
    report[name] = {'idle_fraction': idle, 'angle_degrees': math.degrees(rotation.angle),
                    'axis': list(rotation.axis), 'effective_pivot': pivot.tolist(),
                    'axis_translation': residual.tolist()}

def pose(command):
    for name, (rotation, pivot, residual, idle) in models.items():
        amount = idle + (1 - idle) * command if command >= 0 else idle * (1 + command)
        q = Quaternion((1, 0, 0, 0)).slerp(rotation, amount)
        o = objects[name]
        o.matrix_basis = (Matrix.Translation(pivot + residual * amount)
                          @ q.to_matrix().to_4x4()
                          @ Matrix.Translation(-pivot) @ base[name])
    bpy.context.view_layer.update()

for command in (-1, 1):
    pose(command)
    for name in models:
        expected = base[name] if command < 0 else Matrix(forward[name]['matrix_basis'])
        error = max(abs(objects[name].matrix_basis[i][j] - expected[i][j]) for i in range(4) for j in range(4))
        assert error < 0.00002, (name, command, error)
for name, o in objects.items():
    assert original_vertices[name] == [list(v.co) for v in o.data.vertices]

scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 460
scene.render.resolution_y = 460
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.render.fps = FPS
scene.eevee.taa_render_samples = 12
scene.render.use_file_extension = True
scene.render.use_compositing = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
camera = scene.camera
camera.data.type = 'ORTHO'
center = Vector((0, 0.25, 0))

def view(kind):
    if kind == 'top':
        camera.location = center + Vector((0, 0, 30))
        camera.rotation_euler = (0, 0, math.pi)
        camera.data.ortho_scale = 14.2
    else:
        camera.location = center + Vector((8, -12, 16))
        camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.ortho_scale = 14.2

def render(command, key):
    pose(command)
    for kind in ('top', 'quarter'):
        path = OUT / 'frames' / f'{key}-{kind}.png'
        if path.exists():
            continue
        view(kind)
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
    print('MOTION_FRAME', key, command, flush=True)

(OUT / 'frames').mkdir(exist_ok=True)
commands = [('Idle', 0), ('Forward thrust', 1), ('Idle', 0), ('Reverse thrust', -1), ('Idle', 0)]
timeline = []
samples = {}
for stage, (label, target) in enumerate(commands):
    if stage:
        previous = commands[stage - 1][1]
        for step in range(1, TRANSITION_FRAMES + 1):
            u = step / TRANSITION_FRAMES
            smooth = u * u * (3 - 2 * u)
            command = previous + (target - previous) * smooth
            key = f't{stage}-{step:02}'
            samples[key] = command
            timeline.append({'key': key, 'command': command, 'label': label})
    key = {0: 'idle', 1: 'forward', -1: 'reverse'}[target]
    samples[key] = target
    timeline.extend([{'key': key, 'command': target, 'label': label}] * HOLD_FRAMES)

manifest = {'fps': FPS, 'frames': timeline, 'main_idle_fraction': MAIN_IDLE,
            'fin_idle_fraction': FIN_IDLE, 'pose_models': report,
            'geometry_vertices_unchanged': True, 'source': bpy.data.filepath,
            'reference': str(OUT / 'forward-reference.json')}
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2))
samples_to_render = samples if args.full else {key: samples[key] for key in ('idle', 'forward', 'reverse')}
for key, command in samples_to_render.items():
    render(command, key)

if args.full:
    for name in models:
        objects[name].rotation_mode = 'QUATERNION'
    for frame, item in enumerate(timeline, 1):
        pose(item['command'])
        for name in models:
            o = objects[name]
            o.keyframe_insert(data_path='location', frame=frame)
            o.keyframe_insert(data_path='rotation_quaternion', frame=frame)
    scene.frame_start = 1
    scene.frame_end = len(timeline)
    scene.frame_set(1)
    view('quarter')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-wing-motion-v01.blend'))
print('MOTION_PREVIEW_COMPLETE', len(samples_to_render), flush=True)
