"""Verify finish additions against the frozen model without saving Blender state.

Blender --python verify_finish1.py -- --capture records the original contract.
The default reads deck.blend and writes previews/finish1-audit.json. Exit 1 means
a frozen part, glass clearance, lamp material, mesh or triangle limit failed.
"""
from pathlib import Path
import ast
import json
import math
import sys

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import VIEWS, game_projection

OUT = ROOT / 'previews'
CAPTURE = '--capture' in sys.argv
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene
hardware = [o for o in scene.objects if o.type == 'MESH' and not o.get('preview_only')
            and o.name != '25 | Viewport glass']


def snapshot():
    objects = {}
    for obj in hardware:
        objects[obj.name] = {
            'role': obj.get('export_role'),
            'matrix': [list(row) for row in obj.matrix_world],
            'vertices': [list(v.co) for v in obj.data.vertices],
            'faces': [list(p.vertices) for p in obj.data.polygons],
            'face_materials': [obj.data.materials[p.material_index].name for p in obj.data.polygons],
        }
    lines = {}
    for line in bpy.context.view_layer.freestyle_settings.linesets:
        lines[line.name] = {
            key: getattr(line, key) for key in (
                'select_by_collection', 'select_by_image_border', 'select_silhouette',
                'select_border', 'select_crease', 'select_contour', 'select_external_contour',
                'select_material_boundary')}
        lines[line.name].update({
            'collection': line.collection.name,
            'width': line.linestyle.thickness, 'position': line.linestyle.thickness_position,
            'rgb': list(line.linestyle.color), 'alpha': line.linestyle.alpha,
            'caps': line.linestyle.caps})
    camera = scene.camera
    return {'objects': objects, 'linesets': lines,
            'ink': [scene.render.line_thickness, scene.render.line_thickness_mode,
                    bpy.context.view_layer.freestyle_settings.crease_angle],
            'camera': {'matrix': [list(row) for row in camera.matrix_world],
                       'lens': camera.data.lens, 'sensor_fit': camera.data.sensor_fit,
                       'sensor': [camera.data.sensor_width, camera.data.sensor_height]}}


current = snapshot()
baseline_path = OUT / 'finish1-baseline.json'
if CAPTURE:
    baseline_path.write_text(json.dumps(current, indent=2), encoding='utf-8')
    print('FINISH BASELINE', len(current['objects']), 'hardware objects')
    sys.exit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
original_names = set(baseline['objects'])
body_name = 'Deck | joined closed structure'
unchanged = {name: current['objects'].get(name) == value
             for name, value in baseline['objects'].items() if name != body_name}
new_names = sorted(set(current['objects']) - original_names)
report = {'frozen_objects_unchanged': unchanged,
          'original_names_retained': original_names <= set(current['objects']),
          'linesets_unchanged': current['linesets'] == baseline['linesets'],
          'ink_unchanged': current['ink'] == baseline['ink'],
          'camera_unchanged': current['camera'] == baseline['camera'],
          'new_mesh_objects': new_names, 'meshes': [], 'materials': [], 'lamps': [], 'views': {}}

for obj in hardware:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    obj.data.calc_loop_triangles()
    report['meshes'].append({
        'name': obj.name, 'triangles': len(obj.data.loop_triangles),
        'planar_glass': bool(obj.get('planar_screen')),
        'closed_manifold': all(e.is_manifold for e in bm.edges),
        'consistent_winding': all(e.is_contiguous for e in bm.edges if e.is_manifold),
        'loose_vertices': sum(not v.link_faces for v in bm.verts),
        'zero_area_faces': sum(f.calc_area() == 0 for f in bm.faces),
        'volume': bm.calc_volume(signed=True),
        'modifiers': [m.type for m in obj.modifiers]})
    bm.free()
report['hardware_triangles'] = sum(m['triangles'] for m in report['meshes'])

exporter = ROOT.parent / 'hangar-console' / 'export_console.py'
tree = ast.parse(exporter.read_text(encoding='utf-8'))
functions = [n for n in tree.body if isinstance(n, ast.FunctionDef)
             and n.name in {'centred', 'emission_color', 'anchor', 'color_socket'}]
exec(compile(ast.Module(body=functions, type_ignores=[]), str(exporter), 'exec'))
for obj in hardware:
    if obj.name.endswith(' lens') and obj.name != '09 | Projector lens':
        record = anchor(obj)
        record.update({'object': obj.name,
                       'kind': 'downlight' if 'Header downlight' in obj.name else 'status',
                       'outward': list(obj.matrix_world.to_3x3().col[2]),
                       'active_material': obj.active_material.name})
        report['lamps'].append(record)
for mat in sorted({o.data.materials[p.material_index] for o in hardware for p in o.data.polygons},
                  key=lambda m: m.name):
    shader = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    base, _ = color_socket(mat, 'paint')
    emission, strength = color_socket(mat, 'emission')
    report['materials'].append({'name': mat.name,
                               'principled': shader is not None,
                               'base_unlinked': shader is not None and not shader.inputs['Base Color'].is_linked,
                               'emission_unlinked': shader is not None and not shader.inputs['Emission Color'].is_linked,
                               'export_paint': list(base.default_value),
                               'export_emission': list(emission.default_value), 'strength': strength})

body = bpy.data.objects[body_name]
bvh = BVHTree.FromPolygons([body.matrix_world @ v.co for v in body.data.vertices],
                           [list(p.vertices) for p in body.data.polygons])
eye = scene.camera.matrix_world.translation
blockers = []
for obj in hardware:
    if obj.get('planar_screen') or obj.name.endswith(' lens'):
        continue
    blockers.append((obj.name, BVHTree.FromPolygons(
        [obj.matrix_world @ v.co for v in obj.data.vertices],
        [list(p.vertices) for p in obj.data.polygons])))
for record in report['lamps']:
    obj = bpy.data.objects[record['object']]
    front = max(v.co.z for v in obj.data.vertices)
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    samples = [Vector((x, y, front + .0001))
               for x in (min(xs) * .5, 0, max(xs) * .5)
               for y in (min(ys) * .5, 0, max(ys) * .5)]
    blocked = []
    for sample in samples:
        world = obj.matrix_world @ sample
        ray = world - eye
        hits = [name for name, tree in blockers
                if tree.ray_cast(eye, ray.normalized(), ray.length - .0001)[0] is not None]
        if hits:
            blocked.append(hits)
    projected = world_to_camera_view(scene, scene.camera, Vector(record['position']))
    record['camera_pixel_16x9'] = [projected.x * 1920, (1 - projected.y) * 1080]
    record['face_samples'] = len(samples)
    record['occluded_face_samples'] = len(blocked)
    record['occluders'] = sorted({name for hits in blocked for name in hits})
for label, width, height in VIEWS:
    game_projection(scene, scene.camera, width, height)
    records = {}
    for glass in [o for o in hardware if o.get('planar_screen')]:
        outline = [v.co.copy() for v in glass.data.vertices]
        centre = sum(outline, Vector()) / len(outline)
        samples = [centre + (a.lerp(b, t / 64) - centre) * .99999
                   for a, b in zip(outline, outline[1:] + outline[:1]) for t in range(65)]
        samples += [centre + (p - centre) * f for p in samples[::8] for f in (.25, .5, .75)]
        blocked = []
        for point in samples:
            world = glass.matrix_world @ point
            ray = world - eye
            hit, _, _, distance = bvh.ray_cast(eye, ray.normalized(), ray.length - .00001)
            if hit is not None:
                blocked.append(list(point))
        records[glass.name] = {'samples': len(samples), 'occluded_samples': len(blocked)}
    report['views'][label] = records

checks = {
    'frozen_parts': all(unchanged.values()),
    'names': report['original_names_retained'] and all(name.endswith(' lens') for name in new_names),
    'ink': report['linesets_unchanged'] and report['ink_unchanged'],
    'camera': report['camera_unchanged'],
    'triangles': report['hardware_triangles'] < 40000,
    'closed_solids': all(m['planar_glass'] or (m['closed_manifold'] and m['consistent_winding'] and m['volume'] > 0)
                         for m in report['meshes']),
    'clean_meshes': all(not m['loose_vertices'] and not m['zero_area_faces'] and not m['modifiers'] for m in report['meshes']),
    'materials': all(m['principled'] and m['base_unlinked'] and m['emission_unlinked'] for m in report['materials']),
    'lamps': len(report['lamps']) == 17 and sum(l['kind'] == 'downlight' for l in report['lamps']) == 6,
    'lamp_visibility': all(l['occluded_face_samples'] == 0 for l in report['lamps']),
    'downlight_direction': all(l['outward'][2] < -.5 for l in report['lamps'] if l['kind'] == 'downlight'),
    'glass_visibility': all(s['occluded_samples'] == 0 for v in report['views'].values() for s in v.values()),
}
report['checks'] = checks
(OUT / 'finish1-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'checks': checks, 'hardware_triangles': report['hardware_triangles'], 'lamps': report['lamps']}))
assert all(checks.values()), checks
