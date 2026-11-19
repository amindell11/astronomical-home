"""Check the cool finish against its frozen predecessor without saving the scene.

Blender --python verify_finish4.py -- --capture freezes the previous scene.
The default writes previews/finish4-audit.json and finish4-palette.json.
Exit 1 means a preserved object, lamp, material or geometry contract failed.
"""
from pathlib import Path
import hashlib
import json
import sys

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()


def srgb(rgb):
    return [round(255 * (12.92 * c if c <= .0031308 else 1.055 * c ** (1 / 2.4) - .055))
            for c in rgb]


def snapshot():
    objects = {}
    materials = {}
    for obj in scene.objects:
        record = {'type': obj.type, 'matrix': [list(row) for row in obj.matrix_world],
                  'properties': dict(obj.items())}
        if obj.type == 'MESH':
            record['geometry'] = digest([[list(v.co) for v in obj.data.vertices],
                                         [list(p.vertices) for p in obj.data.polygons]])
            record['assignments'] = digest([obj.data.materials[p.material_index].name for p in obj.data.polygons])
            record['uv'] = digest([[list(p.uv) for p in layer.data] for layer in obj.data.uv_layers])
            record['centre'] = list(obj.matrix_world @ (sum((Vector(p) for p in obj.bound_box), Vector()) / 8))
            record['size'] = [max(v.co[i] for v in obj.data.vertices) - min(v.co[i] for v in obj.data.vertices)
                              for i in range(3)]
        if obj.type == 'CAMERA':
            record['camera'] = [obj.data.lens, obj.data.sensor_width, obj.data.sensor_height,
                                obj.data.sensor_fit, obj.data.shift_x, obj.data.shift_y]
        objects[obj.name] = record
    for mat in bpy.data.materials:
        shader = mat.node_tree.nodes.get('Principled BSDF') if mat.use_nodes else None
        if shader:
            materials[mat.name] = {'base': list(shader.inputs['Base Color'].default_value[:3]),
                                   'emission': list(shader.inputs['Emission Color'].default_value[:3]),
                                   'strength': shader.inputs['Emission Strength'].default_value,
                                   'roughness': shader.inputs['Roughness'].default_value,
                                   'specular': shader.inputs['Specular IOR Level'].default_value}
    glass = bpy.data.objects['25 | Viewport glass'].active_material.node_tree
    glass_nodes = [(n.name, n.bl_idname, [(s.name, list(s.default_value) if hasattr(s.default_value, '__len__')
                                         else s.default_value) for s in n.inputs
                                        if hasattr(s, 'default_value') and s.type in {'VALUE', 'RGBA', 'VECTOR'}])
                   for n in glass.nodes]
    lines = [(l.name, l.collection.name, l.linestyle.thickness, list(l.linestyle.color),
              l.linestyle.alpha, l.linestyle.caps, l.linestyle.thickness_position,
              [getattr(l, key) for key in ('select_by_collection', 'select_by_image_border',
               'select_silhouette', 'select_border', 'select_crease', 'select_contour',
               'select_external_contour', 'select_material_boundary')])
             for l in bpy.context.view_layer.freestyle_settings.linesets]
    return {'objects': objects, 'materials': materials, 'lines': lines,
            'ink': [scene.render.line_thickness, scene.render.line_thickness_mode,
                    bpy.context.view_layer.freestyle_settings.crease_angle],
            'glass_shader': digest([glass_nodes, [(l.from_node.name, l.from_socket.name,
                                   l.to_node.name, l.to_socket.name) for l in glass.links]])}


current = json.loads(json.dumps(snapshot()))
baseline_path = OUT / 'finish4-baseline.json'
if '--capture' in sys.argv:
    baseline_path.write_text(json.dumps(current, indent=2), encoding='utf-8')
    print('FINISH4_BASELINE=complete', flush=True)
    sys.exit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
removed = {'Deck | Left pod amber brow lens', 'Deck | Right pod amber brow lens',
           'Deck | Header centre amber marker lens'}
modified = {'Deck | joined closed structure'} | {
    n for n in baseline['objects'] if 'cyan strip' in n or (' status ' in n and n.endswith(' lens'))}
preserved = {n: current['objects'].get(n) == v for n, v in baseline['objects'].items()
             if n not in modified | removed and v['type'] != 'LIGHT'}
checks = {'preserved_objects': all(preserved.values()),
          'removed_only_requested_objects': set(baseline['objects']) - set(current['objects']) == removed,
          'no_extra_objects': set(current['objects']) <= set(baseline['objects']),
          'linesets': current['lines'] == baseline['lines'] and current['ink'] == baseline['ink'],
          'glass_shader': current['glass_shader'] == baseline['glass_shader']}
for name in ('Deck | orange launch resin', 'Deck | armed red emission'):
    checks[name] = current['materials'][name] == baseline['materials'][name]
for name in modified - {'Deck | joined closed structure'}:
    before, after = baseline['objects'][name], current['objects'][name]
    checks[name + ' frame'] = before['matrix'] == after['matrix'] and before['properties'] == after['properties']
    if ' status ' in name:
        checks[name + ' half face'] = all(abs(after['size'][i] / before['size'][i] - .5) < 1e-5 for i in (0, 1))

meshes, lamps, palette = [], [], []
used = set()
for obj in scene.objects:
    if obj.type != 'MESH' or obj.get('preview_only') or obj.name == '25 | Viewport glass':
        continue
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    obj.data.calc_loop_triangles()
    meshes.append({'name': obj.name, 'triangles': len(obj.data.loop_triangles),
                   'closed': all(e.is_manifold and e.is_contiguous for e in bm.edges),
                   'volume': bm.calc_volume(signed=True), 'planar': bool(obj.get('planar_screen')),
                   'clean': not any(not v.link_faces for v in bm.verts)
                            and not any(f.calc_area() == 0 for f in bm.faces) and not obj.modifiers})
    bm.free()
    used.update(obj.data.materials[p.material_index] for p in obj.data.polygons)
    if obj.name.endswith(' lens') or 'cyan strip' in obj.name:
        mat = obj.data.materials[obj.data.polygons[0].material_index]
        shader = mat.node_tree.nodes.get('Principled BSDF')
        lamps.append({'name': obj.name, 'position': current['objects'][obj.name]['centre'],
                      'linear': list(shader.inputs['Emission Color'].default_value[:3]),
                      'strength': shader.inputs['Emission Strength'].default_value})
for mat in sorted(used, key=lambda m: m.name):
    shader = mat.node_tree.nodes.get('Principled BSDF')
    assert shader and not shader.inputs['Base Color'].is_linked and not shader.inputs['Emission Color'].is_linked, mat.name
    values = current['materials'][mat.name]
    rgb = values['emission'] if values['strength'] else values['base']
    palette.append({'name': mat.name, 'srgb': srgb(rgb), **values})
checks['closed_solids'] = all(m['planar'] or m['closed'] and m['volume'] > 0 for m in meshes)
checks['clean_meshes'] = all(m['clean'] for m in meshes)
checks['triangle_limit'] = sum(m['triangles'] for m in meshes) < 40000
checks['lamp_count'] = len([l for l in lamps if l['name'].startswith('Deck |') and l['name'].endswith(' lens')]) == 14
checks['downlight_colour'] = all(max(abs(a-b) for a, b in zip(l['linear'], (.75, .86, 1))) < 1e-6
                                for l in lamps if 'Header downlight' in l['name'])
checks['accent_colour'] = all(max(abs(a-b) for a, b in zip(l['linear'], (.45, .72, 1))) < 1e-6
                             for l in lamps if 'cyan strip' in l['name'] or 'amber lens' in l['name'])
checks['no_warm_paint'] = all(p['srgb'][0] <= p['srgb'][1] <= p['srgb'][2] for p in palette if not p['strength'])
checks['no_other_warm_emission'] = all(p['emission'][0] <= p['emission'][2] for p in palette
                                      if p['strength'] and p['name'] not in
                                      {'Deck | orange launch resin', 'Deck | armed red emission'})
report = {'checks': checks, 'preserved_objects': preserved, 'meshes': meshes, 'lamps': lamps,
          'hardware_triangles': sum(m['triangles'] for m in meshes),
          'preview_lights': [{'name': obj.name, 'position': list(obj.matrix_world.translation),
                              'linear': list(obj.data.color), 'energy': obj.data.energy}
                             for obj in scene.objects if obj.type == 'LIGHT']}
report['other_materials'] = []
for mat in sorted(bpy.data.materials, key=lambda m: m.name):
    if mat in used:
        continue
    for node in mat.node_tree.nodes:
        if node.type == 'EMISSION':
            rgb = list(node.inputs['Color'].default_value[:3])
            report['other_materials'].append({'name': mat.name, 'srgb': srgb(rgb), 'linear': rgb,
                                              'strength': node.inputs['Strength'].default_value})
(OUT / 'finish4-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
(OUT / 'finish4-palette.json').write_text(json.dumps(palette, indent=2), encoding='utf-8')
print(json.dumps(checks), flush=True)
assert all(checks.values()), [name for name, passed in checks.items() if not passed]
print('FINISH4_AUDIT=complete', flush=True)
