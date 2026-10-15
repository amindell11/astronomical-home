import bpy
import json
import runpy
from pathlib import Path

stage = Path(__file__).parent
capture = runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
assert bpy.context.mode == 'OBJECT' and not bpy.data.is_dirty
objects = {o.name: o for o in bpy.context.scene.objects}
before = capture(objects)
(stage / 'before-data.json').write_text(json.dumps(before))
schemes = {
    'Armor - Upper shoulder guards': {'faces': 21, 'plum': [3, 9], 'structure': [0, 6, 7, 12]},
    'Armor - Lower skids': {'faces': 24, 'plum': [9, 20], 'structure': [0, 7, 16]},
}
colors = {'Bone': (0.6866853237, 0.6104955673, 0.4564110339, 1),
          'Plum': (0.1328683197, 0.0802198201, 0.1070231050, 1)}
materials = {}
for label, color in colors.items():
    material = bpy.data.materials['Bulwark Armor Charcoal'].copy()
    material.name = 'Bulwark Guard ' + label
    material.diffuse_color = color
    rgb = next(n for n in material.node_tree.nodes if n.type == 'RGB')
    rgb.name = 'Paint color'
    rgb.label = label + ' - edge-aligned paint'
    rgb.outputs[0].default_value = color
    materials[label] = material
for name, scheme in schemes.items():
    obj = objects[name]
    assert len(obj.data.polygons) == scheme['faces']
    slots = {}
    for label, material in materials.items():
        obj.data.materials.append(material)
        slots[label] = len(obj.data.materials) - 1
    for face in obj.data.polygons:
        face.material_index = (2 if face.index in scheme['structure'] else
                               slots['Plum'] if face.index in scheme['plum'] else slots['Bone'])
after = capture(objects)
changed = {}
for name in before['objects']:
    fields = [k for k in before['objects'][name] if before['objects'][name][k] != after['objects'][name][k]]
    if fields:
        changed[name] = fields
assert set(changed) == set(schemes), changed
assert all(set(fields) == {'materials', 'face_materials'} for fields in changed.values()), changed
assert before['images'] == after['images']
(stage / 'verification.json').write_text(json.dumps({'changed': changed, 'paint_pixels_unchanged': True, 'geometry_uv_transforms_preserved': True, 'after': after}, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
