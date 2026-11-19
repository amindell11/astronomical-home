"""Measure pod corners and glass visibility with Blender, without saving the scene.

Run after build_deck.py. The first run reads .tmp/original.blend and records its
snapshot in previews/recess-baseline.json; later runs reuse that baseline.
Writes recess-audit.json, preservation-check.json and structure-components.json.
Exit 1 means a geometry, visibility or preservation assertion failed.
"""
from pathlib import Path
import json
import math
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'


def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    scene = bpy.context.scene
    hardware = [o for o in scene.objects if o.type == 'MESH' and not o.get('preview_only')]
    objects = {}
    for obj in hardware:
        if obj.name == 'Deck | joined closed structure':
            continue
        objects[obj.name] = {
            'role': obj.get('export_role'), 'matrix': [list(row) for row in obj.matrix_world],
            'vertices': [list(v.co) for v in obj.data.vertices],
            'faces': [list(p.vertices) for p in obj.data.polygons],
            'materials': [m.name for m in obj.data.materials],
            'face_materials': [p.material_index for p in obj.data.polygons]}
    palette = {}
    for material in bpy.data.materials:
        if not material.name.startswith('Deck |'):
            continue
        shader = material.node_tree.nodes.get('Principled BSDF')
        palette[material.name] = {
            name: list(shader.inputs[name].default_value) if name.endswith('Color') else shader.inputs[name].default_value
            for name in ('Base Color', 'Emission Color', 'Emission Strength', 'Roughness', 'Specular IOR Level')}
    lines = {}
    for line in bpy.context.view_layer.freestyle_settings.linesets:
        lines[line.name] = {'collection': line.collection.name,
            'silhouette': line.select_silhouette, 'crease': line.select_crease,
            'thickness': line.linestyle.thickness, 'position': line.linestyle.thickness_position,
            'color': list(line.linestyle.color), 'alpha': line.linestyle.alpha}
    cam = scene.camera
    return {'objects': objects, 'palette': palette, 'linesets': lines,
            'camera': {'matrix': [list(row) for row in cam.matrix_world],
                'lens': cam.data.lens, 'sensor': [cam.data.sensor_width, cam.data.sensor_height, cam.data.sensor_fit]},
            'names': sorted(o.name for o in hardware),
            'modifiers': [(o.name, m.type) for o in hardware for m in o.modifiers]}


baseline_path = OUT / 'recess-baseline.json'
if not baseline_path.exists():
    baseline = snapshot(ROOT / '.tmp/original.blend')
    baseline['build'] = json.loads((ROOT / '.tmp/original-build.json').read_text())
    baseline_path.write_text(json.dumps(baseline, indent=2))
baseline = json.loads(baseline_path.read_text())
current = snapshot(ROOT / 'deck.blend')
build = json.loads((OUT / 'build-report.json').read_text())
preserved = {k + '_unchanged': baseline[k] == current[k]
             for k in ('palette', 'linesets', 'camera', 'names', 'modifiers')}
preserved['controls_and_accents_unchanged'] = all(
    value == current['objects'][name] for name, value in baseline['objects'].items()
    if not value['role'].endswith('ScreenGlass'))
screen_preserved = True
for name, obj in current['objects'].items():
    if not obj['role'].endswith('ScreenGlass'):
        continue
    old = baseline['objects'][name]
    screen_preserved &= all(obj[k] == old[k] for k in obj if k != 'vertices')
    screen_preserved &= all(a[:2] == b[:2] and abs(a[2] + .2) < 1e-7
                            for a, b in zip(obj['vertices'], old['vertices']))
preserved['glass_changed_only_in_depth'] = bool(screen_preserved)
(OUT / 'preservation-check.json').write_text(json.dumps(preserved, indent=2))
assert all(preserved.values()), preserved

scene = bpy.context.scene
camera = scene.camera
eye = camera.matrix_world.translation
body = bpy.data.objects['Deck | joined closed structure']
world_vertices = [body.matrix_world @ v.co for v in body.data.vertices]
tree = BVHTree.FromPolygons(world_vertices, [list(p.vertices) for p in body.data.polygons])


def pixel(p):
    p = world_to_camera_view(scene, camera, Vector(p))
    return Vector((p.x * scene.render.resolution_x, (1 - p.y) * scene.render.resolution_y))


def boundary_distance(p, polygon):
    distances = []
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        edge = b - a
        t = max(0, min(1, (p - a).dot(edge) / edge.length_squared))
        distances.append((p - a - t * edge).length)
    return min(distances)


def original_pod(screen, hand, height):
    u, v, n = (Vector(screen[k]) for k in ('u', 'v', 'normal'))
    origin = Vector(screen['origin']) + n * .008 + u * (hand * .2) - v * height
    p = [(-2.475, -1.52), (2.475, -1.52), (2.925, -1.07), (2.925, 2.43),
         (2.475, 2.88), (-2.475, 2.88), (-2.925, 2.43), (-2.925, -1.07)]
    if hand < 0:
        p[3:5] = [(2.925, 1.93), (1.575, 2.88)]
    else:
        p[5:7] = [(-1.575, 2.88), (-2.925, 1.93)]
    return [origin + u * x + v * y - n * .1 for x, y in p]


old_screens = baseline['build']['screens']
old_pods = [original_pod(old_screens[0], -1, 1.2), original_pod(old_screens[2], 1, 1.9)]
new_pods = [[Vector(p) for p in pod['front_world']] for pod in build['pods']]
report = {'corners': [], 'views': {}, 'recess_depth': .1, 'wall_angle_degrees': 45,
          'glass_edge_clearance': .008, 'face_bars_local_width': {'Left': .044, 'Right': .084},
          'pod_world_z_bounds': {}, 'measured_wall_angles_degrees': {}}
for i, label in enumerate(('Left', 'Right')):
    report['pod_world_z_bounds'][label] = {
        'before': [min(p.z for p in old_pods[i]), max(p.z for p in old_pods[i])],
        'after': [min(p.z for p in new_pods[i]), max(p.z for p in new_pods[i])]}
    assert max(p.z for p in new_pods[i]) <= max(p.z for p in old_pods[i]) + 1e-6
    assert min(p.z for p in new_pods[i]) >= min(p.z for p in old_pods[i]) - 1e-6

for s_index, (old, new) in enumerate(zip(old_screens, build['screens'])):
    corner_indices = {'bottom left': 7, 'bottom right': 1, 'top right': 3, 'top left': 5}
    for corner, index in corner_indices.items():
        row = {'screen': new['label'], 'corner': corner}
        for label, screen, pods in [('before', old, old_pods), ('after', new, new_pods)]:
            u, v, n = (Vector(screen[k]) for k in ('u', 'v', 'normal'))
            points = [pixel(Vector(screen['origin']) + u*x + v*y) for x, y in screen['outline']]
            p = (points[index] + points[(index+1) % 8]) / 2
            outline = [pixel(q) for q in pods[s_index // 2]]
            row[label + '_px'] = boundary_distance(p, outline)
        report['corners'].append(row)

for name, width, height in [('16x9', 1920, 1080), ('16x10', 1920, 1200),
                             ('21x9', 2560, 1080), ('32x9', 3840, 1080), ('4x3', 1440, 1080)]:
    scene.render.resolution_x, scene.render.resolution_y = width, height
    camera.data.sensor_fit = 'HORIZONTAL'
    camera.data.lens = 33 * min(1, (16 / 9) / (width / height))
    view = {}
    for screen in build['screens']:
        obj = bpy.data.objects[screen['name']]
        u, v, n = (Vector(screen[k]) for k in ('u', 'v', 'normal'))
        origin = Vector(screen['origin'])
        outline = [Vector(p) for p in screen['outline']]
        if name == '16x9':
            wall_angles = []
            for a, b in zip(outline, outline[1:] + outline[:1]):
                edge = (b - a).normalized()
                outward = Vector((edge.y, -edge.x))
                p = (a + b) / 2 + outward * .058
                location, normal, index, distance = tree.find_nearest(origin + u*p.x + v*p.y + n*.05)
                angle = math.degrees(math.acos(min(1, abs(normal.dot(n)))))
                assert distance < .001 and abs(angle - 45) < .01, (screen['label'], distance, angle)
                assert body.data.materials[body.data.polygons[index].material_index].name == 'Deck | charcoal wells and bezels'
                wall_angles.append(angle)
            report['measured_wall_angles_degrees'][screen['label']] = wall_angles
        samples = [a.lerp(b, t/64) * .99999 for a, b in zip(outline, outline[1:] + outline[:1])
                   for t in range(65)]
        samples += [p * fraction for p in samples[::8] for fraction in (.25, .5, .75)]
        blocked = []
        for p in samples:
            world = origin + u * p.x + v * p.y
            ray = world - eye
            hit, _, _, distance = tree.ray_cast(eye, ray.normalized(), ray.length - .00001)
            if hit is not None:
                blocked.append([p.x, p.y, ray.length - distance])
        mouth = [pixel(origin + u*x + v*y + n*.1) for x, y in screen['mouth']]
        glass = [pixel(origin + u*p.x + v*p.y) for p in outline]
        for p in glass:
            assert all((b-a).cross(p-a) < 0 for a, b in zip(mouth, mouth[1:] + mouth[:1])), (name, screen['label'])
        clearance = min(boundary_distance(p, mouth) for p in glass)
        angles = []
        for a, b, c, d in zip(glass, glass[1:]+glass[:1], mouth, mouth[1:]+mouth[:1]):
            angles.append(math.degrees((b-a).angle(d-c)))
        view[screen['label']] = {'samples': len(samples), 'occluded_samples': len(blocked),
            'minimum_projected_mouth_clearance_px': clearance, 'edge_angle_difference_max_degrees': max(angles),
            'glass_projected_polygon': [list(p) for p in glass]}
        assert not blocked, (name, screen['label'], blocked[:8])
    report['views'][name] = view

bm = bmesh.new(); bm.from_mesh(body.data)
remaining = set(bm.verts)
groups = []
while remaining:
    first = remaining.pop()
    connected, pending = {first}, [first]
    while pending:
        vertex = pending.pop()
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other in remaining:
                remaining.remove(other); connected.add(other); pending.append(other)
    faces = {face for vertex in connected for face in vertex.link_faces}
    volume = 0
    for face in faces:
        points = [v.co for v in face.verts]
        volume += sum(points[0].dot(points[i].cross(points[i+1])) / 6 for i in range(1, len(points)-1))
    groups.append({'vertices': len(connected), 'signed_volume': volume,
                   'bounds': [[min(v.co[i] for v in connected), max(v.co[i] for v in connected)] for i in range(3)]})
bm.free()
(OUT / 'structure-components.json').write_text(json.dumps(sorted(groups, key=lambda g: -g['signed_volume']), indent=2))
(OUT / 'recess-audit.json').write_text(json.dumps(report, indent=2))
print(json.dumps({'preservation': preserved, 'corners': report['corners'],
                  'visibility': 'all glass perimeter and interior samples visible in five camera shapes'}))
