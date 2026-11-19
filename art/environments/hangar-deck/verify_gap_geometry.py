"""Compare visible hardware against a transient pre-edit Blender snapshot.

Run Blender headlessly with --python verify_gap_geometry.py -- --reference PATH.
The default reference is .tmp/baseline.blend; its SHA256 remains in the JSON audit
after scratch cleanup. No scene is saved and no reference is reconstructed.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import VIEWS, game_projection

parser = argparse.ArgumentParser()
parser.add_argument('--reference', type=Path, default=ROOT / '.tmp/baseline.blend')
parser.add_argument('--current', type=Path, default=ROOT / 'deck.blend')
parser.add_argument('--output', type=Path, default=ROOT / 'previews/gap-geometry.json')
parser.add_argument('--diagnose-only', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
BODY = 'Deck | joined closed structure'
POSITION_TOLERANCE = 1e-4
NORMAL_TOLERANCE = 1e-3


def value(item):
    if isinstance(item, (str, bool, int, float)) or item is None:
        return item
    if isinstance(item, set):
        return sorted(item)
    return list(item)


def properties(item):
    return {prop.identifier: value(getattr(item, prop.identifier))
            for prop in item.bl_rna.properties
            if prop.type in {'BOOLEAN', 'INT', 'FLOAT', 'STRING', 'ENUM'}
            and not prop.is_readonly and prop.identifier != 'rna_type'}


def material_snapshot(material):
    nodes = material.node_tree.nodes if material.use_nodes else []
    return {'properties': properties(material),
            'nodes': {node.name: {'type': node.bl_idname, 'properties': properties(node),
                        'inputs': {socket.identifier: value(socket.default_value)
                                   for socket in node.inputs if hasattr(socket, 'default_value')}}
                      for node in nodes},
            'links': sorted((link.from_node.name, link.from_socket.identifier,
                             link.to_node.name, link.to_socket.identifier)
                            for link in material.node_tree.links) if material.use_nodes else []}


def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path.resolve()))
    scene = bpy.context.scene
    hardware = sorted((obj for obj in scene.objects if obj.type == 'MESH'
                       and not obj.get('preview_only') and not obj.hide_render), key=lambda obj: obj.name)
    objects, vertices, triangles, owners, materials = {}, [], [], [], []
    used_materials = {}
    for obj in hardware:
        data = obj.data
        data.calc_loop_triangles()
        record = {'role': obj.get('export_role'), 'matrix': [list(row) for row in obj.matrix_world],
                  'materials': [material.name for material in data.materials],
                  'modifiers': [(modifier.type, properties(modifier)) for modifier in obj.modifiers]}
        if obj.name != BODY:
            record['geometry'] = {'vertices': [list(vertex.co) for vertex in data.vertices],
                                  'edges': [list(edge.vertices) for edge in data.edges],
                                  'faces': [list(face.vertices) for face in data.polygons],
                                  'face_materials': [face.material_index for face in data.polygons],
                                  'smooth_faces': [face.use_smooth for face in data.polygons],
                                  'uv_layers': {layer.name: [list(loop.uv) for loop in layer.data]
                                                for layer in data.uv_layers}}
        objects[obj.name] = record
        offset = len(vertices)
        vertices.extend(obj.matrix_world @ vertex.co for vertex in data.vertices)
        for triangle in data.loop_triangles:
            triangles.append(tuple(offset + index for index in triangle.vertices))
            owners.append(obj.name)
            material = data.materials[triangle.material_index]
            materials.append(material.name)
        for material in data.materials:
            used_materials[material.name] = material_snapshot(material)
    linesets = {}
    for lineset in bpy.context.view_layer.freestyle_settings.linesets:
        linesets[lineset.name] = {
            'selection': properties(lineset),
            'collection': lineset.collection.name if lineset.collection else None,
            'collection_members': sorted(obj.name for obj in lineset.collection.all_objects)
                                  if lineset.collection else [],
            'style': properties(lineset.linestyle)}
    camera = scene.camera
    camera_record = {'matrix': [list(row) for row in camera.matrix_world], 'data': properties(camera.data)}
    game_projection(scene, camera, 1920, 1080)
    matrix = camera.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(), x=1920, y=1080)
    settings = {'objects': objects, 'materials': used_materials, 'linesets': linesets,
                'ink': {'enabled': scene.render.use_freestyle,
                        'thickness': scene.render.line_thickness,
                        'thickness_mode': scene.render.line_thickness_mode,
                        'crease_angle': bpy.context.view_layer.freestyle_settings.crease_angle},
                'camera': camera_record}
    return {'tree': BVHTree.FromPolygons(vertices, triangles, all_triangles=True),
            'owners': owners, 'materials': materials, 'settings': settings,
            'vertices': vertices, 'triangles': triangles,
            'eye': camera.matrix_world.translation.copy(),
            'rotation': camera.matrix_world.to_3x3().copy(), 'projection': matrix.copy()}


def components(mask):
    parents, records, previous = [], [], []

    def root(index):
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    for y, row in enumerate(mask):
        edges = np.flatnonzero(np.diff(np.concatenate(([False], row, [False]))))
        current = []
        for start, end in zip(edges[::2], edges[1::2]):
            index = len(parents)
            parents.append(index)
            records.append([int(end - start), int(start), y, int(end - 1), y])
            current.append((start, end, index))
            for old_start, old_end, old_index in previous:
                if start < old_end and old_start < end:
                    parents[root(index)] = root(old_index)
        previous = current
    totals = {}
    for index, record in enumerate(records):
        key = root(index)
        if key not in totals:
            totals[key] = record.copy()
        else:
            total = totals[key]
            total[0] += record[0]
            total[1] = min(total[1], record[1])
            total[2] = min(total[2], record[2])
            total[3] = max(total[3], record[3])
            total[4] = max(total[4], record[4])
    return sorted(({'pixels': record[0], 'bbox_xyxy_inclusive': record[1:]}
                   for record in totals.values()), key=lambda record: -record['pixels'])


def mask_report(mask):
    return {'pixels': int(mask.sum()), 'components': components(mask)}


def ray(rotation, projection, x, y, width, height):
    camera_direction = Vector((((2 * (x + .5) / width - 1) + projection[0][2]) / projection[0][0],
                               ((1 - 2 * (y + .5) / height) + projection[1][2]) / projection[1][1], -1))
    return (rotation @ camera_direction).normalized()


def hit_detail(snapshot, index, position, normal):
    vertices = [snapshot['vertices'][vertex] for vertex in snapshot['triangles'][index]]
    distances = []
    for a, b in zip(vertices, vertices[1:] + vertices[:1]):
        edge = b - a
        fraction = max(0., min(1., (position - a).dot(edge) / edge.length_squared)) if edge.length_squared else 0.
        distances.append((position - a - edge * fraction).length)
    return {'position': list(position), 'normal': list(normal),
            'triangle': [list(vertex) for vertex in vertices], 'triangle_edge_distance': min(distances)}


def surface_choices(snapshot, position):
    hits, choices = [], []
    for index, indices in enumerate(snapshot['triangles']):
        vertices = [snapshot['vertices'][vertex] for vertex in indices]
        if any(position[axis] < min(vertex[axis] for vertex in vertices) - 1e-5
               or position[axis] > max(vertex[axis] for vertex in vertices) + 1e-5 for axis in range(3)):
            continue
        a, b, c = vertices
        edge_a, edge_b, delta = b - a, c - a, position - a
        cross = edge_a.cross(edge_b)
        if cross.length_squared < 1e-20:
            continue
        normal = cross.normalized()
        plane_distance = abs(normal.dot(delta))
        if plane_distance > 1e-5:
            continue
        aa, ab, bb = edge_a.dot(edge_a), edge_a.dot(edge_b), edge_b.dot(edge_b)
        da, db = delta.dot(edge_a), delta.dot(edge_b)
        denominator = aa * bb - ab * ab
        if denominator <= 0:
            continue
        v, w = (bb * da - ab * db) / denominator, (aa * db - ab * da) / denominator
        barycentric = [1 - v - w, v, w]
        if min(barycentric) < -1e-5 or max(barycentric) > 1 + 1e-5:
            continue
        choice = {'object': snapshot['owners'][index], 'material': snapshot['materials'][index],
                  'normal': list(normal)}
        hits.append({'triangle_index': index, 'vertices': [list(vertex) for vertex in vertices],
                     'plane_distance': plane_distance, 'barycentric': barycentric, **choice})
        if not any(item['object'] == choice['object'] and item['material'] == choice['material']
                   and (Vector(item['normal']) - normal).length <= NORMAL_TOLERANCE for item in choices):
            choices.append(choice)
    return {'plane_tolerance_units': 1e-5, 'barycentric_tolerance': 1e-5,
            'triangles': hits, 'choices': choices}


def matching_choices(before, after):
    def present(choice, options):
        return any(choice['object'] == option['object'] and choice['material'] == option['material']
                   and (Vector(choice['normal']) - Vector(option['normal'])).length <= NORMAL_TOLERANCE
                   for option in options)
    return bool(before and after) and all(present(choice, after) for choice in before) and all(
        present(choice, before) for choice in after)


def identical_containing_triangles(before, after):
    def signatures(records):
        return sorted((triangle['object'], triangle['material'],
                       tuple(sorted(tuple(vertex) for vertex in triangle['vertices'])))
                      for triangle in records['triangles'])
    return signatures(before) == signatures(after)


reference = snapshot(args.reference)
current = snapshot(args.current)
if args.diagnose_only:
    previous = json.loads(args.output.read_text(encoding='utf-8'))
    pixels = [tuple(sample['pixel']) for sample in previous['changed_surface_samples']]
    pixels += [(527, 91), (960, 91), (1387, 91)]
    diagnostics = []
    for x, y in pixels:
        records = []
        for dx, dy in ((0, 0), (-.001, -.001), (0, -.001), (.001, -.001), (-.001, 0), (.001, 0),
                       (-.001, .001), (0, .001), (.001, .001)):
            direction = ray(reference['rotation'], reference['projection'], x + dx, y + dy, 1920, 1080)
            old_position, old_normal, old_index, _ = reference['tree'].ray_cast(reference['eye'], direction)
            new_position, new_normal, new_index, _ = current['tree'].ray_cast(current['eye'], direction)
            records.append({'offset_pixels': [dx, dy],
                            'before': hit_detail(reference, old_index, old_position, old_normal)
                                      if old_index is not None else None,
                            'after': hit_detail(current, new_index, new_position, new_normal)
                                     if new_index is not None else None,
                            'position_error': (new_position - old_position).length
                                              if old_index is not None and new_index is not None else None,
                            'normal_error': (new_normal - old_normal).length
                                            if old_index is not None and new_index is not None else None})
        centre = records[0]
        before_choices = surface_choices(reference, Vector(centre['before']['position'])) if centre['before'] else None
        after_choices = surface_choices(current, Vector(centre['after']['position'])) if centre['after'] else None
        diagnostics.append({'pixel': [x, y], 'rays': records,
                            'before_surface_choices': before_choices, 'after_surface_choices': after_choices,
                            'same_surface_choices': matching_choices(before_choices['choices'], after_choices['choices'])
                                                    if before_choices and after_choices else False,
                            'containing_triangles_unchanged': identical_containing_triangles(before_choices, after_choices)
                                                             if before_choices and after_choices else False})
    output = args.output.with_name('gap-ray-diagnostics.json')
    output.write_text(json.dumps(diagnostics, indent=2), encoding='utf-8')
    print('GAP RAY DIAGNOSTICS', str(output), flush=True)
    raise SystemExit(0)
settings_before, settings_after = reference['settings'], current['settings']
preserved = {key: settings_before[key] == settings_after[key]
             for key in ('objects', 'materials', 'linesets', 'ink', 'camera')}
object_differences = [name for name in sorted(set(settings_before['objects']) | set(settings_after['objects']))
                      if settings_before['objects'].get(name) != settings_after['objects'].get(name)]
width, height = 1920, 1080
masks = {name: np.zeros((height, width), dtype=np.bool_)
         for name in ('background_to_structure', 'hardware_removed', 'position_changed',
                      'normal_changed', 'material_changed', 'object_changed', 'remaining_background')}
maximum = {'position_units': 0., 'normal_vector_difference': 0.}
samples = []
old_tree, new_tree = reference['tree'], current['tree']
for y in range(height):
    for x in range(width):
        direction = ray(reference['rotation'], reference['projection'], x, y, width, height)
        old_position, old_normal, old_index, old_distance = old_tree.ray_cast(reference['eye'], direction)
        new_position, new_normal, new_index, new_distance = new_tree.ray_cast(current['eye'], direction)
        if new_index is None:
            masks['remaining_background'][y, x] = True
        if old_index is None:
            masks['background_to_structure'][y, x] = new_index is not None
            continue
        if new_index is None:
            masks['hardware_removed'][y, x] = True
            continue
        position_error = (new_position - old_position).length
        normal_error = (new_normal - old_normal).length
        maximum['position_units'] = max(maximum['position_units'], position_error)
        maximum['normal_vector_difference'] = max(maximum['normal_vector_difference'], normal_error)
        changed = {'position_changed': position_error > POSITION_TOLERANCE,
                   'normal_changed': normal_error > NORMAL_TOLERANCE,
                   'material_changed': reference['materials'][old_index] != current['materials'][new_index],
                   'object_changed': reference['owners'][old_index] != current['owners'][new_index]}
        for name, state in changed.items():
            masks[name][y, x] = state
        if any(changed.values()) and len(samples) < 32:
            samples.append({'pixel': [x, y], 'position_error': position_error, 'normal_error': normal_error,
                            'before_object': reference['owners'][old_index],
                            'after_object': current['owners'][new_index],
                            'before_material': reference['materials'][old_index],
                            'after_material': current['materials'][new_index],
                            'before_hit': hit_detail(reference, old_index, old_position, old_normal),
                            'after_hit': hit_detail(current, new_index, new_position, new_normal)})
    if y % 180 == 0:
        print('VISIBLE SURFACES', y, '/', height, flush=True)

for sample in samples:
    x, y = sample['pixel']
    nearby = []
    for dx, dy in ((-.001, -.001), (0, -.001), (.001, -.001), (-.001, 0), (.001, 0),
                   (-.001, .001), (0, .001), (.001, .001)):
        direction = ray(reference['rotation'], reference['projection'], x + dx, y + dy, width, height)
        old_position, old_normal, old_index, _ = old_tree.ray_cast(reference['eye'], direction)
        new_position, new_normal, new_index, _ = new_tree.ray_cast(current['eye'], direction)
        nearby.append({'offset_pixels': [dx, dy], 'before_hit': old_index is not None,
                       'after_hit': new_index is not None,
                       'position_error': (new_position - old_position).length
                                         if old_index is not None and new_index is not None else None,
                       'normal_error': (new_normal - old_normal).length
                                       if old_index is not None and new_index is not None else None})
    sample['nearby_rays'] = nearby
    sample['shared_triangle_edge_ambiguity'] = (
        sample['position_error'] <= POSITION_TOLERANCE
        and sample['before_material'] == sample['after_material']
        and sample['before_object'] == sample['after_object']
        and sample['before_hit']['triangle_edge_distance'] < 1e-5
        and sample['after_hit']['triangle_edge_distance'] < 1e-5
        and all(item['before_hit'] == item['after_hit']
                and (not item['before_hit'] or (item['position_error'] <= POSITION_TOLERANCE
                                               and item['normal_error'] <= NORMAL_TOLERANCE))
                for item in nearby))
    sample['before_surface_choices'] = surface_choices(reference, Vector(sample['before_hit']['position']))
    sample['after_surface_choices'] = surface_choices(current, Vector(sample['after_hit']['position']))
    sample['containing_triangles_unchanged'] = identical_containing_triangles(
        sample['before_surface_choices'], sample['after_surface_choices'])
    sample['coincident_surface_ambiguity'] = (
        sample['position_error'] <= POSITION_TOLERANCE
        and sample['before_material'] == sample['after_material']
        and sample['before_object'] == sample['after_object']
        and len(sample['before_surface_choices']['choices']) > 1
        and sample['containing_triangles_unchanged']
        and matching_choices(sample['before_surface_choices']['choices'], sample['after_surface_choices']['choices']))

bands = {}
scene = bpy.context.scene
for name, width, height in VIEWS:
    game_projection(scene, scene.camera, width, height)
    projection = scene.camera.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(), x=width, y=height)
    rows = math.ceil(height * .03)
    counts = {}
    for edge, row_range in [('top', range(rows)), ('bottom', range(height - rows, height))]:
        missing = 0
        for y in row_range:
            for x in range(width):
                direction = ray(current['rotation'], projection, x, y, width, height)
                missing += new_tree.ray_cast(current['eye'], direction)[2] is None
        counts[edge + '_background_rays'] = missing
    bands[name] = {'resolution': [width, height], 'band_rows': rows, **counts}
    print('EDGE RAYS', name, counts, flush=True)

report = {'reference': {'path': str(args.reference), 'sha256': hashlib.sha256(args.reference.read_bytes()).hexdigest(),
                        'transient_snapshot': True},
          'current': {'path': str(args.current), 'sha256': hashlib.sha256(args.current.read_bytes()).hexdigest()},
          'resolution': [1920, 1080], 'sample': 'pixel centers; combined hardware triangle BVHs; no render antialiasing',
          'position_tolerance_units': POSITION_TOLERANCE, 'normal_vector_tolerance': NORMAL_TOLERANCE,
          'exact_preservation': preserved, 'object_differences': object_differences,
          'maximum_existing_surface_error': maximum, 'changed_surface_samples': samples,
          'pixels': {name: mask_report(mask) for name, mask in masks.items()}, 'edge_band_rays': bands}
failures = [name for name, state in preserved.items() if not state]
failures.extend(name for name in ('hardware_removed', 'position_changed', 'normal_changed', 'material_changed',
                                 'object_changed') if masks[name].any())
failures.extend(name + ' edge band' for name, item in bands.items()
                if item['top_background_rays'] or item['bottom_background_rays'])
report['raw_strict_verdict'] = {'passed': not failures, 'failures': failures}
edge_ambiguities = [sample['pixel'] for sample in samples if sample['shared_triangle_edge_ambiguity']]
coincident_ambiguities = [sample['pixel'] for sample in samples if sample['coincident_surface_ambiguity']]
normal_ambiguities = {tuple(pixel) for pixel in edge_ambiguities + coincident_ambiguities}
stable_failures = [failure for failure in failures
                   if failure != 'normal_changed' or int(masks['normal_changed'].sum()) != len(normal_ambiguities)]
report['stable_surface_verdict'] = {
    'passed': not stable_failures, 'failures': stable_failures,
    'shared_triangle_edge_pixels': edge_ambiguities,
    'same_coincident_surface_choice_pixels': coincident_ambiguities,
    'rule': 'Only equal-position/material/object hits within 0.00001 units of both triangle edges, '
            'with eight matching rays offset by 0.001 pixel, or hits with the same multiple containing '
            'normal/material/object choices and exactly unchanged containing triangle geometry in both meshes; '
            'raw counts and strict verdict are retained.'}
report['failures'] = stable_failures
report['passed'] = not stable_failures
args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'preserved': preserved, 'maximum': maximum,
                  'pixels': {name: int(mask.sum()) for name, mask in masks.items()},
                  'raw_strict_failures': failures, 'failures': stable_failures}), flush=True)
assert not stable_failures, stable_failures
