"""Check pedestal scope, protected parts and the lens contract without saving.

Run headless Blender with -- --capture once before editing. Normal runs compare
the frozen pedestal-baseline.json and write pedestal-audit.json. Failures exit 1
when Blender is run with --python-exit-code 1.
"""
from pathlib import Path
import json
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
BODY = 'Deck | joined closed structure'
LENS = '09 | Projector lens'
source = (ROOT / 'verify_finish5.py').read_text(encoding='utf-8')
scope = {'__file__': str(ROOT / 'verify_finish5.py')}
exec(compile(source.split('\nsnapshot = json.loads')[0], str(ROOT / 'verify_finish5.py'), 'exec'), scope)
current = json.loads(json.dumps(scope['scope']['snapshot']()))
current['body_components'] = scope['components']()
body = bpy.data.objects[BODY]
protected_faces = []
for poly in body.data.polygons:
    points = [body.matrix_world @ body.data.vertices[i].co for i in poly.vertices]
    if any(abs(p.x) <= 1.3 and abs(p.y) <= 1.1 and 1.1 <= p.z <= 2.6 for p in points):
        continue
    protected_faces.append((sorted(tuple(round(c, 6) for c in p) for p in points),
                            body.data.materials[poly.material_index].name))
current['protected_body_faces'] = scope['scope']['digest'](sorted(protected_faces))
current['protected_body_face_count'] = len(protected_faces)
body.data.calc_loop_triangles()
protected_surface = []
for triangle in body.data.loop_triangles:
    points = [body.matrix_world @ body.data.vertices[i].co for i in triangle.vertices]
    if any(abs(p.x) <= 1.3 and abs(p.y) <= 1.1 and 1.1 <= p.z <= 2.6 for p in points):
        continue
    poly = body.data.polygons[triangle.polygon_index]
    protected_surface.append(([list(p) for p in points], body.data.materials[poly.material_index].name))
current['protected_body_surface'] = protected_surface
baseline_path = OUT / 'pedestal-baseline.json'
if '--capture' in sys.argv:
    assert not baseline_path.exists(), 'Pedestal baseline is already frozen.'
    baseline_path.write_text(json.dumps(current, indent=2), encoding='utf-8')
    print('PEDESTAL_BASELINE=complete', flush=True)
    sys.exit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
preserved = {n: value == current['objects'].get(n) for n, value in baseline['objects'].items()
             if n not in {BODY, LENS}}
materials = {n: value == current['materials'].get(n) for n, value in baseline['materials'].items()}
old_components = {c['hash']: c for c in baseline['body_components']}
new_components = {c['hash']: c for c in current['body_components']}
removed = [old_components[k] for k in old_components.keys() - new_components.keys()]
added = [new_components[k] for k in new_components.keys() - old_components.keys()]


def surface_distance(source, target):
    def key(points, material):
        return tuple(sorted(tuple(round(c, 6) for c in p) for p in points)), material

    unchanged = {key(points, material) for points, material in target}
    by_material = {}
    for points, material in target:
        by_material.setdefault(material, []).append(points)
    targets = {}
    for material, polygons in by_material.items():
        triangles = np.asarray(polygons, dtype=np.float64)
        a, b, c = triangles[:, 0], triangles[:, 1], triangles[:, 2]
        normal = np.cross(b - a, c - a)
        targets[material] = a, b, c, normal, np.sum(normal * normal, axis=1)
    maximum = 0
    worst = None
    for points, material in source:
        if key(points, material) in unchanged:
            continue
        vertices = [Vector(p) for p in points]
        samples = vertices + [(vertices[i] + vertices[(i + 1) % 3]) / 2 for i in range(3)]
        samples.append(sum(vertices, Vector()) / 3)
        for point in samples:
            a, b, c, normal, area2 = targets[material]
            p = np.asarray(point, dtype=np.float64)
            distance = np.full(len(a), np.inf)
            valid = area2 > 0
            signed = np.sum((p - a) * normal, axis=1)
            q = p - normal * np.divide(signed, area2, out=np.zeros_like(signed), where=valid)[:, None]
            inside = valid.copy()
            for start, end in ((a, b), (b, c), (c, a)):
                inside &= np.sum(np.cross(end - start, q - start) * normal, axis=1) >= 0
                edge = end - start
                length2 = np.sum(edge * edge, axis=1)
                t = np.clip(np.divide(np.sum((p - start) * edge, axis=1), length2,
                                      out=np.zeros_like(length2), where=length2 > 0), 0, 1)
                edge_distance = np.sum((p - start - t[:, None] * edge) ** 2, axis=1)
                distance = np.minimum(distance, edge_distance)
            distance[inside] = np.minimum(distance[inside], signed[inside] ** 2 / area2[inside])
            closest = float(np.sqrt(distance.min()))
            if closest > maximum:
                maximum = closest
                worst = {'point': list(point), 'material': material}
    if maximum > .0001:
        print('PROTECTED_SURFACE_DISTANCE', maximum, worst, flush=True)
    return maximum


surface_error = max(surface_distance(baseline['protected_body_surface'], protected_surface),
                    surface_distance(protected_surface, baseline['protected_body_surface']))
obj = bpy.data.objects[LENS]
bm = bmesh.new()
bm.from_mesh(obj.data)
bm.verts.ensure_lookup_table()
pending = [bm.verts[0]]
seen = set(pending)
while pending:
    for edge in pending.pop().link_edges:
        for vertex in edge.verts:
            if vertex not in seen:
                seen.add(vertex)
                pending.append(vertex)
lens_closed = all(e.is_manifold and e.is_contiguous for e in bm.edges)
lens_connected = len(seen) == len(bm.verts)
lens_volume = bm.calc_volume(signed=True)
bm.free()
before = baseline['objects'][LENS]['centre']
after = current['objects'][LENS]['centre']
edge_material = bpy.data.materials['Deck | pedestal white-cyan edge emission']
shader = edge_material.node_tree.nodes.get('Principled BSDF')
bm = bmesh.new()
bm.from_mesh(body.data)
remaining = {f for f in bm.faces if body.data.materials[f.material_index] == edge_material}
strips = []
while remaining:
    pending = [remaining.pop()]
    faces = set(pending)
    while pending:
        for edge in pending.pop().edges:
            for face in edge.link_faces:
                if face in remaining:
                    remaining.remove(face)
                    faces.add(face)
                    pending.append(face)
    if any(sum(f in faces for f in edge.link_faces) != 2 for face in faces for edge in face.edges):
        continue
    points = [body.matrix_world @ v.co for f in faces for v in f.verts]
    strips.append([[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)])
bm.free()
emission = list(shader.inputs['Emission Color'].default_value[:3])
strength = shader.inputs['Emission Strength'].default_value
mirrors = all(any(max(abs(a[0][0] + b[0][1]), abs(a[0][1] + b[0][0]),
                     *(abs(a[i][j] - b[i][j]) for i in (1, 2) for j in (0, 1))) < 1e-6
                 for b in strips) for a in strips)
checks = {
    'protected_objects_exact': all(preserved.values()),
    'object_names_exact': set(baseline['objects']) == set(current['objects']),
    'existing_materials_exact': all(materials.values()),
    'linesets_exact': current['lines'] == baseline['lines'] and current['ink'] == baseline['ink'],
    'glass_shader_exact': current['glass_shader'] == baseline['glass_shader'],
    'body_surface_outside_pedestal_preserved': surface_error <= .0001,
    'lens_role_exact': obj.get('export_role') == 'ProjectorLens',
    'lens_transform_exact': current['objects'][LENS]['matrix'] == baseline['objects'][LENS]['matrix'],
    'lens_centre_within_002': (Vector(before) - Vector(after)).length <= .02,
    'lens_single_closed_solid': lens_closed and lens_connected and lens_volume > 0,
    'lamp_names_exact': {n for n in baseline['objects'] if n.endswith(' lens')} ==
                        {n for n in current['objects'] if n.endswith(' lens')},
    'eight_mirrored_edge_strips': len(strips) == 8 and mirrors,
    'edge_emission_white_cyan': max(abs(a - b) for a, b in zip(emission, (.55, .85, 1))) < 1e-6,
    'edge_emission_restrained': 0 < strength <= 1 and not shader.inputs['Emission Color'].is_linked,
    'edge_strips_thin': all(.03 <= bounds[2][1] - bounds[2][0] <= .04 for bounds in strips),
}
report = {'checks': checks, 'preserved_objects': preserved, 'preserved_materials': materials,
          'unchanged_body_components': len(old_components.keys() & new_components.keys()),
          'protected_body_face_count': current['protected_body_face_count'],
          'protected_body_faces_exact': current['protected_body_faces'] == baseline['protected_body_faces'],
          'protected_body_surface_max_distance': surface_error,
          'removed_body_components': removed, 'added_body_components': added,
          'lens_centre_before': before, 'lens_centre_after': after,
          'lens_signed_volume': lens_volume, 'edge_strip_bounds': strips,
          'edge_emission_linear': emission, 'edge_emission_strength': strength}
(OUT / 'pedestal-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(checks), flush=True)
assert all(checks.values()), [n for n, passed in checks.items() if not passed]
print('PEDESTAL_AUDIT=complete', flush=True)
