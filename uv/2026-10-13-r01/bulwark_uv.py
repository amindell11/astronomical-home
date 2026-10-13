import bpy
import hashlib
import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, 'D:/amind/git/agent-3/art/tools/ship')
import ship_source
import ship_uv

source = Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
out = Path('D:/amind/git/agent-3/results/bulwark/uv/2026-10-13-r01')
out.mkdir(parents=True, exist_ok=True)
src = ship_source.open_source(source)
baseline = ship_source.fingerprint(src)
lock = json.loads(source.with_name('lock.json').read_text())
assert baseline['components']['geometry'] == lock['geometry']
selection = [o.name for o in bpy.context.selected_objects]
active = bpy.context.view_layer.objects.active.name if bpy.context.view_layer.objects.active else None
roles = ship_source.Roles(src)
names = sorted(n for role in ('hull', 'canopy', 'cores', 'ink')
               for n in roles.members.get(role, ()) if src.objects[n].type == 'MESH')
assert len(names) == 20, names


def remove_evaluation(evaluation):
    for obj in list(evaluation.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(evaluation.scene)


def get_uv(obj):
    return ship_source.read_array(obj.data.uv_layers['PaintUV'].uv, 'vector', np.float32, 2).reshape(-1, 2)


def set_uv(obj, values):
    obj.data.uv_layers['PaintUV'].uv.foreach_set('vector', np.asarray(values, np.float32).ravel())
    obj.data.update()


evaluation = ship_source.Evaluation(src)
matrices = {n: evaluation.to_space(n).copy() for n in names}
remove_evaluation(evaluation)
work = bpy.data.scenes.new('UV authoring only')
bpy.context.window.scene = work
copies = {}
for name in names:
    original = src.objects[name]
    assert original.data.users == 1, name
    mesh = bpy.data.meshes.new(name + ' UV workspace')
    mesh.from_pydata([matrices[name] @ v.co for v in original.data.vertices], [],
                     [tuple(p.vertices) for p in original.data.polygons])
    assert [l.vertex_index for l in mesh.loops] == [l.vertex_index for l in original.data.loops]
    mesh.uv_layers.new(name='PaintUV')
    obj = bpy.data.objects.new(name + ' UV workspace', mesh)
    work.collection.objects.link(obj)
    copies[name] = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(30 if name == '03 Lower hull' else 66), island_margin=0,
                             area_weight=0.2, correct_aspect=False, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')

bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = copies[names[0]]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale(scale_uv=False, shear=False)
bpy.ops.object.mode_set(mode='OBJECT')
for name, copy in copies.items():
    original = src.objects[name]
    original.data.uv_layers.active_index = original.data.uv_layers.find('PaintUV')
    original.data.uv_layers['PaintUV'].active_render = True
    set_uv(original, get_uv(copy))
    for modifier in original.modifiers:
        if modifier.type != 'MIRROR':
            continue
        assert tuple(modifier.use_axis) == (True, False, False), name
        modifier.use_mirror_u = False
        modifier.use_mirror_v = False
        modifier.use_mirror_udim = False
        modifier.mirror_offset_u = modifier.mirror_offset_v = 0
        modifier.offset_u, modifier.offset_v = 0.5, 0

evaluation = ship_source.Evaluation(src)
densities = {}
for name in names:
    mesh = evaluation.mesh(name)
    densities[name] = ship_uv.density(mesh, evaluation.to_space(name), 2048)
    bpy.data.meshes.remove(mesh)
    assert densities[name] and math.isfinite(densities[name]), name
remove_evaluation(evaluation)
for name in names:
    set_uv(copies[name], get_uv(copies[name]) * (1000 / densities[name]))

maximum_span = max(float(np.ptp(get_uv(o), axis=0).max()) for o in copies.values())
for index, name in enumerate(names):
    obj = copies[name]
    uv = get_uv(obj) * (0.4 / maximum_span)
    low, high = uv.min(axis=0), uv.max(axis=0)
    if index == 0:
        uv += np.array([0.01, 0.01]) - low
    elif index == len(names) - 1:
        uv += np.array([0.49, 0.99]) - high
    else:
        uv += np.array([0.25, 0.5]) - (low + high) / 2
    set_uv(obj, uv)

bpy.context.window.scene = work
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = copies[names[0]]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(udim_source='ORIGINAL_AABB', rotate=True, rotate_method='CARDINAL',
                        scale=True, merge_overlap=False, margin_method='FRACTION', margin=0.004,
                        shape_method='CONVEX')
bpy.ops.object.mode_set(mode='OBJECT')
for name, obj in copies.items():
    uv = get_uv(obj)
    assert uv.min() >= 0.009, (name, uv.min())
    assert uv[:, 0].max() <= 0.491 and uv[:, 1].max() <= 0.991, (name, uv.max(axis=0))
    set_uv(src.objects[name], uv)

for obj in list(work.objects):
    mesh = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(mesh)
bpy.data.scenes.remove(work)
bpy.context.window.scene = src
after = ship_source.fingerprint(src)
assert after['components']['geometry'] == baseline['components']['geometry'] == lock['geometry']
assert after['components']['materials'] == baseline['components']['materials']
assert all(after['parts'][n]['uv'] == p['uv'] for n, p in baseline['parts'].items() if n not in names)
for name in ('Upper flap deployment', 'Lower flap deployment'):
    assert src.objects[name].rotation_euler.x == 0
for obj in src.objects:
    obj.select_set(obj.name in selection)
bpy.context.view_layer.objects.active = src.objects.get(active) if active else None
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()

evaluation = ship_source.Evaluation(src)
guide, measured = [], {}
for name in names:
    mesh = evaluation.mesh(name)
    co, corners, _, triangles = ship_source.mesh_arrays(mesh)
    uv = ship_source.read_array(mesh.uv_layers['PaintUV'].uv, 'vector', np.float32, 2).reshape(-1, 2)
    measured[name] = ship_uv.density(mesh, evaluation.to_space(name), 2048)
    assert np.isfinite(uv).all() and uv.min() >= 0 and uv.max() <= 1
    guide.append({'name': name, 'uv': uv.tolist(), 'triangles': triangles.tolist()})
    bpy.data.meshes.remove(mesh)
remove_evaluation(evaluation)
bpy.context.window.scene = src
assert max(measured.values()) / min(measured.values()) <= 2
(out / 'uv-triangles.json').write_text(json.dumps(guide, separators=(',', ':')))
(out / 'uv-audit.json').write_text(json.dumps({
    'source_sha256': source_hash, 'baseline_commit': '4d2e0dd1',
    'owner_instruction': "Lock geometry and fix UV's", 'flight_check_claimed': False,
    'geometry_unchanged': True, 'materials_unchanged': True, 'unmodified_hidden_uvs': True,
    'geometry': after['components']['geometry'], 'edited_parts': names,
    'authored_half': [0.01, 0.01, 0.49, 0.99], 'mirrored_half_offset': [0.5, 0],
    'density_ratio': max(measured.values()) / min(measured.values()), 'densities': measured,
    'upper_and_lower_flap_deployment': 0, 'exemptions_added': []
}, indent=2))
print(json.dumps({'source_sha256': source_hash, 'geometry_unchanged': True,
                  'density_ratio': max(measured.values()) / min(measured.values())}))
