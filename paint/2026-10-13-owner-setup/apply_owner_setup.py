import bpy
import hashlib
import json
import sys
from pathlib import Path

import numpy as np

root = Path('D:/amind/git/agent-3')
stage = root / 'results/bulwark/paint/2026-10-13-owner-setup'
sys.path.insert(0, str(root / 'art/tools/ship'))
import ship_source

assert Path(bpy.data.filepath).resolve() == (root / 'art/ships/bulwark/Bulwark.blend').resolve()
snapshot = json.loads((stage / 'live-before.json').read_text(encoding='utf-8'))
before = ship_source.fingerprint(bpy.context.scene)['components']
selection = sorted(o.name for o in bpy.context.selected_objects)
active = bpy.context.active_object.name
mode = bpy.context.mode
images = {key: bpy.data.images[value['name']] for key, value in snapshot['images'].items()}

def pixels(image):
    values = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(values)
    return values

def digest(image):
    return hashlib.sha256(pixels(image).tobytes()).hexdigest()

hashes_before = {key: digest(image) for key, image in images.items()}
for key in ('Light', 'Ink', 'BaseRegion'):
    assert hashes_before[key] == snapshot['images'][key]['sha256'], key + ' changed since preparation'
ready = {key: np.load(stage / ('ready-' + key + '.npy')) for key in ('Light', 'Ink', 'BaseRegion')}
for key, values in ready.items():
    assert values.size == len(images[key].pixels)
    assert np.isfinite(values).all()
plum = bpy.data.materials['Bulwark Plum']
bone = bpy.data.materials['Bulwark Bone']
bone_color = tuple(bone.node_tree.nodes['Alternate base coat'].inputs[2].default_value)
shader = plum.node_tree.nodes['Principled BSDF']
source_socket = shader.inputs['Base Color'].links[0].from_socket
assert plum.node_tree.nodes.get('Alternate base coat') is None
paint = bpy.context.scene.tool_settings.image_paint
brush = paint.brush
assert brush is not None and brush.is_editable

for key, values in ready.items():
    images[key].pixels.foreach_set(values.reshape(-1))
    images[key].update()

alternate = plum.node_tree.nodes.new('ShaderNodeMixRGB')
alternate.name = 'Alternate base coat'
alternate.label = 'Bone-white livery'
alternate.blend_type = 'MIX'
alternate.inputs[2].default_value = bone_color
alternate.location = (shader.location.x - 220, shader.location.y)
plum.node_tree.links.new(source_socket, alternate.inputs[1])
plum.node_tree.links.new(plum.node_tree.nodes['BaseRegion'].outputs['Color'], alternate.inputs[0])
plum.node_tree.links.new(alternate.outputs[0], shader.inputs['Base Color'])

for material in bpy.data.materials:
    if not material.use_nodes:
        continue
    node = material.node_tree.nodes.get('BaseRegion')
    if node is not None:
        for other in material.node_tree.nodes:
            other.select = False
        node.select = True
        material.node_tree.nodes.active = node

paint.mode = 'IMAGE'
paint.canvas = images['BaseRegion']
paint.seam_bleed = 6
paint.use_occlude = True
paint.use_backface_culling = True
paint.use_normal_falloff = False
unified = paint.unified_paint_settings
unified.use_unified_color = True
unified.color = (1, 1, 1)
unified.secondary_color = (0, 0, 0)
unified.use_unified_strength = True
unified.strength = 1
unified.use_unified_size = True
unified.size = 40
unified.use_color_jitter = False
brush.color = (1, 1, 1)
brush.secondary_color = (0, 0, 0)
brush.strength = 1
brush.blend = 'MIX'
brush.color_type = 'COLOR'
brush.curve_distance_falloff_preset = 'CONSTANT'
brush.use_pressure_strength = False
brush.spacing = 10

for area in bpy.context.screen.areas:
    if area.type == 'IMAGE_EDITOR':
        area.spaces.active.image = images['BaseRegion']
        area.spaces.active.mode = 'PAINT'
    elif area.type == 'VIEW_3D':
        area.spaces.active.shading.type = 'MATERIAL'
    area.tag_redraw()

after = ship_source.fingerprint(bpy.context.scene)['components']
assert before['geometry'] == after['geometry']
assert before['uv'] == after['uv']
assert digest(images['Shadow']) == hashes_before['Shadow']
assert sorted(o.name for o in bpy.context.selected_objects) == selection
assert bpy.context.active_object.name == active and bpy.context.mode == mode
for image in images.values():
    image.save()
    image.pack()

save_version = bpy.context.preferences.filepaths.save_version
try:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, check_existing=False)
finally:
    bpy.context.preferences.filepaths.save_version = save_version

result = {
    'geometry_before': before['geometry'], 'geometry_after': after['geometry'],
    'uv_before': before['uv'], 'uv_after': after['uv'],
    'shadow_unchanged': digest(images['Shadow']) == hashes_before['Shadow'],
    'mode': bpy.context.mode, 'active': bpy.context.active_object.name,
    'canvas': paint.canvas.name, 'paint_mode': paint.mode,
    'brush': brush.name, 'falloff': brush.curve_distance_falloff_preset,
    'color': list(unified.color), 'secondary_color': list(unified.secondary_color),
    'strength': unified.strength, 'size': unified.size,
    'filepath': bpy.data.filepath,
    'image_hashes': {key: digest(image) for key, image in images.items()},
}
(stage / 'setup-result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
