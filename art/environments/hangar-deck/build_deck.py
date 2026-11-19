"""Build deck.blend, six camera shapes and four construction views from local D13.

Run Blender with --python-exit-code 1 --python build_deck.py. Options after --:
--no-render saves geometry and its audit; --camera-only renders the six game views;
--samples sets Cycles samples. Invalid geometry raises before saving.
"""
from pathlib import Path
import argparse
import json
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import VIEWS, game_projection
PREVIEWS = ROOT / 'previews'
PREVIEWS.mkdir(exist_ok=True)
os.environ['OPTIX_CACHE_PATH'] = str(ROOT / '.cache' / 'optix')
parser = argparse.ArgumentParser()
parser.add_argument('--no-render', action='store_true')
parser.add_argument('--camera-only', action='store_true')
parser.add_argument('--samples', type=int, default=32)
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])

# The approved generator owns the dimensions; its output and audit section never runs.
source = ROOT / 'massing' / 'build_D13.py'
text = source.read_text(encoding='utf-8')
text = text.split('# Regression and clearance validation is evidence')[0]
saved_argv = sys.argv
sys.argv = [str(source)]
D = {'__file__': str(source), '__name__': '__deck_dimensions__'}
exec(compile(text, str(source), 'exec'), D)
sys.argv = saved_argv
scene = bpy.context.scene
bpy.context.preferences.filepaths.save_version = 0
scene.render.use_file_extension = True
scene.cycles.samples = ARGS.samples
scene.render.threads = 10
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.color_mode = 'RGB'
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.render.use_freestyle = True
scene.render.line_thickness_mode = 'ABSOLUTE'
scene.render.line_thickness = 1.0
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'


def collection(name):
    result = bpy.data.collections.new(name)
    scene.collection.children.link(result)
    return result


body_collection = collection('01 | Deck closed construction')
parts_collection = collection('02 | Exporter parts')
accent_collection = collection('03 | Cyan structural accents')
ink_collection = collection('21 - Console hardware ink')
legend_ink_collection = collection('22 - Lit legends ink')


def material(name, rgb, emission=0):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    result.diffuse_color = (*rgb, 1)
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = 1
    shader.inputs['Specular IOR Level'].default_value = .05
    shader.inputs['Emission Color'].default_value = (*rgb, 1)
    shader.inputs['Emission Strength'].default_value = emission
    return result


paint = material('Deck | deep ink blue', (.019, .029, .045))
face = material('Deck | blue grey front', (.040, .057, .078))
shoulder = material('Deck | slate chamfers', (.073, .096, .124))
top = material('Deck | cool upper planes', (.092, .119, .153))
well = material('Deck | charcoal wells and bezels', (.006, .011, .019))
keymat = material('Deck | cool grey controls', (.23, .29, .34))
orange = material('Deck | orange launch resin', (.95, .20, .018), 1)
cyan = material('Deck | emissive cyan accents', (.035, .48, .72), 1.65)
screenmat = material('Deck | placeholder emissive blue', (.023, .105, .222), .8)
legendmat = material('Deck | ivory inlaid legends', (.49, .65, .72), .3)
for emissive in (orange, cyan, screenmat):
    emissive.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (0, 0, 0, 1)
PALETTE = [paint, face, shoulder, top, well, keymat, orange, cyan, screenmat, legendmat]
old_palette = {
    D['body']: paint, D['face']: face, D['shoulder']: shoulder, D['side']: face,
    D['top_mat']: top, D['dark']: well, D['cyan']: cyan, D['keytop']: keymat,
    D['key_mat']: keymat, D['orange']: orange, D['screen_mat']: screenmat,
}
chamfer = D['chamfer']
point = D['point']
shifted = D['shifted']
horizontal_basis = D['horizontal_basis']
BODY = []
AFTER_WELLS = []
POD_NATIVE = {}
ACCENTS = []
PARTS = {}
SCREEN_RECORDS = []
CONTROL_RECORDS = []
DEPARTURES = []


def repair(obj, triangulate=False):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    tolerance = 1e-5 if triangulate and obj.get('boolean_result') else 1e-6
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=tolerance)
    if not obj.get('boolean_result'):
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if triangulate:
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def mesh(name, vertices, faces, mat=face, col=body_collection, ids=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata([tuple(v) for v in vertices], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    col.objects.link(obj)
    for item in PALETTE:
        data.materials.append(item)
    for index, poly in enumerate(data.polygons):
        poly.material_index = ids[index] if ids else PALETTE.index(mat)
    repair(obj)
    return obj


def local_mesh(name, outline, front, back, basis, mat=face, col=body_collection):
    count = len(outline)
    verts = [(x, y, z) for z in (front, back) for x, y in outline]
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    obj = mesh(name, verts, faces, mat, col)
    repair(obj, True)
    obj.matrix_world = Matrix((basis[1], basis[2], basis[3])).transposed().to_4x4()
    obj.matrix_world.translation = basis[0]
    return obj


def world_loft(name, loops, mat=face, side_ids=None):
    count = len(loops[0])
    vertices = [p for loop in loops for p in loop]
    faces = [tuple(reversed(range(count)))]
    ids = [PALETTE.index(mat)]
    for layer in range(len(loops) - 1):
        for i in range(count):
            j = (i + 1) % count
            faces.append((layer * count + i, layer * count + j, (layer + 1) * count + j, (layer + 1) * count + i))
            ids.append(side_ids[i % len(side_ids)] if side_ids else PALETTE.index(mat))
    faces.append(tuple(range((len(loops) - 1) * count, len(loops) * count)))
    ids.append(PALETTE.index(mat))
    obj = mesh(name, vertices, faces, mat, ids=ids)
    repair(obj, True)
    return obj


def boolean(target, other, operation):
    def volume(obj):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        result = bm.calc_volume(signed=True) * obj.matrix_world.determinant()
        bm.free()
        return result
    original_volume = volume(target)
    other_volume = volume(other)
    bpy.context.view_layer.objects.active = target
    mod = target.modifiers.new('Construction ' + operation.lower(), 'BOOLEAN')
    mod.operation = operation
    mod.solver = 'EXACT'
    mod.object = other
    bpy.ops.object.modifier_apply(modifier=mod.name)
    target['boolean_result'] = True
    repair(target)
    check = bmesh.new()
    check.from_mesh(target.data)
    bad = sum(not e.is_manifold for e in check.edges)
    if bad:
        print('BOOLEAN NONMANIFOLD', operation, target.name, other.name, bad, flush=True)
    result_volume = check.calc_volume(signed=True) * target.matrix_world.determinant()
    if operation == 'UNION':
        assert result_volume >= max(original_volume, other_volume) - .0001, (target.name, other.name, 'union lost volume')
    else:
        assert result_volume <= original_volume + .0001, (target.name, other.name, 'difference added volume')
    check.free()


def remove(obj):
    bpy.data.objects.remove(obj, do_unlink=True)


def combine(target, other):
    boolean(target, other, 'UNION')
    remove(other)


def subtract(target, cutter, retain=False):
    boolean(target, cutter, 'DIFFERENCE')
    if not retain:
        remove(cutter)


def register(obj, role, legend=False):
    PARTS[role] = obj
    obj['export_role'] = role
    if legend:
        legend_ink_collection.objects.link(obj)
    else:
        ink_collection.objects.link(obj)
    return obj


def copy_closed(obj):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    body_collection.objects.link(obj)
    for i, mat in enumerate(obj.data.materials):
        obj.data.materials[i] = old_palette.get(mat, face)
    obj.data.calc_loop_triangles()
    vertices = [v.co.copy() for v in obj.data.vertices]
    triangles = [tuple(t.vertices) for t in obj.data.loop_triangles]
    indices = [obj.data.polygons[t.polygon_index].material_index for t in obj.data.loop_triangles]
    materials = list(obj.data.materials)
    data = bpy.data.meshes.new(obj.name + ' preserved tessellation')
    data.from_pydata(vertices, [], triangles)
    data.update()
    for mat in materials:
        data.materials.append(mat)
    for poly, index in zip(data.polygons, indices):
        poly.material_index = index
    obj.data = data
    repair(obj)
    BODY.append(obj)


keep_fragments = ('independent slim canopy strut', 'strut foot to rear dash',
                  'slim continuous outer pillar', 'low dash side bolster', 'stepped faceted brow')
originals = list(scene.objects)
for obj in originals:
    if obj.type in {'LIGHT', 'CAMERA'}:
        continue
    if obj.name.startswith('Hologram |'):
        obj.name = '19 | Vanguard preview ' + obj.name.split('| ', 1)[1]
        obj['preview_only'] = True
        continue
    if obj.name.startswith('Projector | faint'):
        obj.name = '11 | Neutral projector frustum'
        obj['preview_only'] = True
        continue
    if obj.name.startswith('Window |'):
        obj.name = '19 | Distant space'
        obj['preview_only'] = True
        continue
    if 'mirrored deep faceted housing' in obj.name:
        obj.data.calc_loop_triangles()
        POD_NATIVE[obj.name.split(' | ')[0]] = (
            [v.co.copy() for v in obj.data.vertices],
            [tuple(t.vertices) for t in obj.data.loop_triangles],
            [PALETTE.index(old_palette[obj.data.materials[obj.data.polygons[t.polygon_index].material_index]])
             for t in obj.data.loop_triangles])
    if any(fragment in obj.name for fragment in keep_fragments):
        copy_closed(obj)
    else:
        remove(obj)

# Cross-sections include their underside; D13's top and front apron stay exact.
columns = []
for x, y, z in D['BAND_PATH']:
    section = [(x, y + dy, z + dz) for dy, dz in D['BAND_SECTION']]
    section += [(x, D['BAND_SILL_Y'], D['BAND_SILL_Z'])]
    section += [(x, D['BAND_SILL_Y'], D['BAND_SILL_Z'] - .22)]
    section += [(x, y + dy, z + dz - .22) for dy, dz in reversed(D['BAND_SECTION'][2:])]
    columns.append(section)
band = world_loft('Deck | continuous wraparound band and sill', columns, face,
                  [0, 1, 1, 2, 1, 1, 0, 0, 0, 0, 0, 0])
BODY.append(band)
underside_columns = []
for x, y, z in D['BAND_PATH']:
    underside = [(x, y - 1, z - .91)]
    underside += [(x, y + dy, z + dz - .22) for dy, dz in D['BAND_SECTION'][2:]]
    underside += [(x, 2.35, .64), (x, 2.35, -10), (x, y - 1, -10)]
    underside_columns.append(underside)
under_band = world_loft('Construction | beneath band underside', underside_columns, face)
front_columns = []
for x, y, z in D['BAND_PATH']:
    front_columns.append([(x, -20, -10), (x, y - .995, -10),
                          (x, y - .995, 20), (x, -20, 20)])
front_band = world_loft('Construction | beyond band front return', front_columns, face)


def panel(name, basis, width, height, edge=.18, depth=.22, floor=-.075):
    outer = chamfer(width, height, min(.22, width * .12, height * .15))
    mouth = chamfer(width - edge * 2, height - edge * 2, min(.16, height * .1))
    loops = [[point(basis, x, y, z) for x, y in outline]
             for outline, z in [(outer, -depth), (outer, 0), (mouth, .055)]]
    obj = world_loft(name, loops, face, [1, 2, 1, 2, 1, 2, 1, 2])
    return obj, mouth, floor


WELL_CUTS = []


def grown_panel(name, x, size):
    base = shifted(D['band_basis'](x, D['RAISED_PANEL_LIFT']), y=-D['RAISED_PANEL_FORWARD_SHIFT'])
    amplitude = math.hypot(base[2].z, base[3].z)
    turn = math.asin(math.sin(math.radians(45)) / amplitude) - math.atan2(base[2].z, base[3].z)
    rotation = Matrix.Rotation(turn, 3, base[1])
    basis = (base[0], base[1], rotation @ base[2], rotation @ base[3])
    width, height = size
    outline = chamfer(width, height, min(.22, width * .12, height * .15))
    upper = [point(basis, u, v) for u, v in outline]
    footprint = []
    for u, v in outline:
        p = point(basis, u * (width + .54) / width, v * (height + .54) / height)
        py = max(p.y, D['band_origin'](p.x)[0].y - .99)
        footprint.append(Vector((p.x, py, D['band_surface_z'](p.x, py) - .025)))
    lower = [p - Vector((0, 0, .22)) for p in footprint]
    support = world_loft(name + ' shoulder', [lower, footprint, upper], face, [1, 1, 2, 2, 1, 2, 2, 1])
    housing, mouth, floor = panel(name, basis, width, height, .16, .18, -.125)
    combine(support, housing)
    subtract(support, under_band, True)
    subtract(support, front_band, True)
    BODY.append(support)
    WELL_CUTS.append((basis, mouth, floor, name))
    return basis


ship_basis = grown_panel('Deck | ship keys raised ramp', -3.6, (1.85, 1.60))
selector_basis = grown_panel('Deck | selector raised ramp', 3.6, (1.85, 1.60))
remove(under_band)
remove(front_band)
switch_bases = []
for hand in (-1, 1):
    basis = D['band_basis'](hand * 5.85)
    housing, mouth, floor = panel('Deck | ' + ('left' if hand < 0 else 'right') + ' four-switch bank', basis, 2.63, 1.20)
    BODY.append(housing)
    WELL_CUTS.append((basis, mouth, floor, 'switch bank'))
    switch_bases.append(basis)

launch_basis = D['rake'](D['LAUNCH_D13_CENTRE'], 27)
housing, mouth, floor = panel('Deck | launch housing', launch_basis, 2.52, 1.248, .152, .24, -.125)
BODY.append(housing)
WELL_CUTS.append((launch_basis, mouth, floor, 'launch'))

spine_columns = []
for y, width, z in D['SPINE_STATIONS']:
    spine_columns.append([(-width - .16, y, z - .20), (-width, y, z),
                          (width, y, z), (width + .16, y, z - .20),
                          (width + .16, y, z - .40), (-width - .16, y, z - .40)])
BODY.append(world_loft('Deck | short launch projector spine', spine_columns, face))
pedestal_loops = [[(x, y, z) for x, y in chamfer(width, height, min(width, height) * .16)]
                 for width, height, z in D['PEDESTAL_LOOPS']]
BODY.append(world_loft('Deck | three-step pedestal', pedestal_loops, face, [1, 2, 1, 2, 1, 2, 1, 2]))

for hand, label, landing in zip((-1, 1), ('Left', 'Right'), D['STRUT_LANDING_POINTS']):
    x = landing[0]
    outline = chamfer(1.90, 3.49, .32)
    base = []
    for u, v in outline:
        px = x + hand * .30 + u
        py = 1.395 + v
        base.append(Vector((px, py, D['band_surface_z'](px, min(py, 2.35)))))
    cap = [Vector((x + u, 2.43 + v, 1.32)) for u, v in chamfer(.88, 1.12, .17)]
    bottom = [p - Vector((0, 0, .22)) for p in base]
    BODY.append(world_loft('Deck | ' + label.lower() + ' strut shoulder', [bottom, base, cap], face,
                           [1, 2, 1, 2, 1, 2, 1, 2]))

for hand, label in ((-1, 'Left'), (1, 'Right')):
    pb = D['pod_bases'][label][0]
    join_v = D['POD_PROFILE_BOTTOM'] + (.50 if hand > 0 else 0)
    upper = [point(pb, hand * u, join_v, -.10) for u in (-2.705, 2.705)]
    edge = [Vector((hand * x, y, z)) for x, y, z in [(4.35, -.05, 1.24), (8.6, -2.55, .74)]]
    middle = [p * .55 + q * .45 + Vector((0, 0, .06)) for p, q in zip(upper, edge)]
    loops = [[a, b, b - Vector((0, 0, .22)), a - Vector((0, 0, .22))]
             for a, b in (upper, middle, edge)]
    BODY.append(world_loft('Deck | ' + label.lower() + ' pod heel', loops, face))


def screen_outline(width, height, hand=None):
    outline = chamfer(width, height, .20)
    return D['inner_top_clip'](outline, hand, (.85, .60)) if hand else outline


def offset_outline(outline, distance):
    lines = []
    for a, b in zip(outline, outline[1:] + outline[:1]):
        a, b = Vector(a), Vector(b)
        edge = (b - a).normalized()
        normal = Vector((edge.y, -edge.x))
        lines.append((normal, normal.dot(a) + distance))
    result = []
    for i, (normal, intercept) in enumerate(lines):
        previous, previous_intercept = lines[i - 1]
        p = Matrix((previous, normal)).inverted() @ Vector((previous_intercept, intercept))
        result.append(tuple(p))
    return result


POD_FACE_DEPTH = -.10
GLASS_DEPTH = POD_FACE_DEPTH - .10
SCREEN_EDGE_CLEARANCE = .008
POD_RECORDS = []
SCREEN_CUTS = []

for hand, label in ((-1, 'Left'), (1, 'Right')):
    basis, _, rearward = D['pod_bases'][label]
    profile = [(x, y + .68) for x, y in chamfer(5.85, 4.40, .45)]
    profile = D['inner_top_clip'](profile, hand, (1.02, .72))
    # Banking must not turn the wider corner into a taller pod.
    top_drop = .33 * abs(basis[1].z) / basis[2].z
    profile = [(x, y - top_drop if y > 1.9 else y) for x, y in profile]
    front = [point(basis, x, y, POD_FACE_DEPTH) for x, y in profile]
    middle = [point(basis, x + (math.copysign(.20, x) if hand * x > 0 else 0), y, POD_FACE_DEPTH) + rearward * .42
              for x, y in profile]
    rear = [p + rearward * .83 for p in middle]
    native_vertices, native_triangles, native_indices = POD_NATIVE[label]
    pod_matrix = Matrix((basis[1], basis[2], basis[3])).transposed().to_4x4()
    pod_matrix.translation = basis[0]
    inverse = pod_matrix.inverted()
    updated_vertices = [(x, y, POD_FACE_DEPTH) for x, y in profile]
    updated_vertices += [inverse @ p for p in middle + rear]
    pod = mesh('Deck | ' + label.lower() + ' deep pod shell', updated_vertices,
               native_triangles + [tuple(range(8))], face, ids=native_indices + [1])
    pod.matrix_world = pod_matrix
    repair(pod, True)
    POD_RECORDS.append({'label': label, 'profile': profile, 'face_depth': POD_FACE_DEPTH,
                        'origin': list(basis[0]), 'u': list(basis[1]), 'v': list(basis[2]),
                        'normal': list(basis[3]), 'front_world': [list(p) for p in front]})
    # A sealed inner cavity gives the housing walls genuine thickness.
    inside = [((x * .91), .68 + (y - .68) * .91) for x, y in profile]
    cavity = [[point(basis, x, y, -.23) + rearward * distance for x, y in inside]
              for distance in (.13, 1.04)]
    cutter = world_loft(label + ' internal shell cavity', cavity, well)
    subtract(pod, cutter)
    specifications = [('SHIP', (4.25, 3.00), 1.20, hand, '07 | Main rectangular display', 'MainScreenGlass'),
                      ('DETAIL', (3.76, .80), -.96, None, '07 | Detail rectangular display', 'DetailScreenGlass')] if hand < 0 else [
                      ('PRIMARY', (4.25, 1.60), 1.90, hand, '30 | Primary blank glass', 'PrimaryScreenGlass'),
                      ('ORDNANCE', (4.25, 1.60), 0, None, '30 | Ordnance blank glass', 'OrdnanceScreenGlass')]
    for label_screen, size, v, clip, objname, role in specifications:
        sb = shifted(basis, -hand * .20, v)
        outline = screen_outline(*size, clip)
        floor_outline = offset_outline(outline, SCREEN_EDGE_CLEARANCE)
        mouth = offset_outline(outline, SCREEN_EDGE_CLEARANCE + .10)
        cutter = world_loft(label_screen + ' display pocket', [
            [(x, y, z) for x, y in loop]
            for loop, z in [(floor_outline, GLASS_DEPTH - .015),
                            (floor_outline, GLASS_DEPTH), (mouth, POD_FACE_DEPTH),
                            (mouth, .20)]], well)
        cutter.matrix_world = pod_matrix.copy()
        cutter.matrix_world.translation = sb[0]
        SCREEN_CUTS.append(cutter)
        vertices = [(x, y, GLASS_DEPTH) for x, y in outline]
        glass = mesh(objname, vertices, [tuple(range(8))], screenmat, parts_collection)
        if glass.data.polygons[0].normal.z < 0:
            bm = bmesh.new()
            bm.from_mesh(glass.data)
            bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
            bm.to_mesh(glass.data)
            bm.free()
        glass.matrix_world = Matrix((sb[1], sb[2], sb[3])).transposed().to_4x4()
        glass.matrix_world.translation = sb[0]
        glass['export_role'] = role
        glass['planar_screen'] = True
        PARTS[role] = glass
        glass['true_width'], glass['true_height'] = size
        SCREEN_RECORDS.append({'role': role, 'name': objname, 'label': label_screen, 'size': list(size),
                               'outline': outline, 'origin': list(point(sb, d=GLASS_DEPTH)),
                               'mouth': mouth, 'face_depth': POD_FACE_DEPTH,
                               'glass_depth': GLASS_DEPTH, 'wall_angle_degrees': 45,
                               'u': list(sb[1]), 'v': list(sb[2]), 'normal': list(sb[3])})
    BODY.append(pod)


def control(role, name, basis, outline, front, back, mat=keymat):
    obj = local_mesh(name, outline, front, back, basis, mat, parts_collection)
    register(obj, role)
    obj['local_actuation_axis'] = '+Z outward; press along -Z'
    CONTROL_RECORDS.append({'role': role, 'name': name, 'origin': list(basis[0]),
                            'u': list(basis[1]), 'v': list(basis[2]), 'outward_axis': list(basis[3]),
                            'local_axis': '+Z', 'front': front})
    return obj


def legend(role, name, control_obj, outline, front, mat=legendmat):
    basis = (control_obj.matrix_world.translation,
             control_obj.matrix_world.to_3x3().col[0], control_obj.matrix_world.to_3x3().col[1],
             control_obj.matrix_world.to_3x3().col[2])
    obj = local_mesh(name, outline, front, front - .004, basis, mat, parts_collection)
    subtract(control_obj, obj, True)
    register(obj, role, True)
    return obj


chevron = [(-.06, 0), (.035, .10), (.075, .065), (.015, 0), (.075, -.065), (.035, -.10)]
for action, x in [('Previous', -.37), ('Next', .37)]:
    basis = shifted(ship_basis, x=x)
    obj = control(action + 'Button', '08 | ' + action + ' chunky keycap', basis, chamfer(.54, .49, .055), .015, -.07)
    outline = chevron if action == 'Previous' else [(-x, y) for x, y in chevron]
    legend(action + 'Legend', '08 | ' + action + ' inlaid chevron', obj, outline, .015)

knob_rotation = selector_basis[3].to_track_quat('Z', 'Y').to_matrix()
knob_basis = (selector_basis[0], knob_rotation.col[0], knob_rotation.col[1], knob_rotation.col[2])
outline = [(math.cos(i * math.pi / 6) * .34, math.sin(i * math.pi / 6) * .34) for i in range(12)]
knob = control('SelectorKnob', '08 | Selector faceted grip', knob_basis, outline, .20, -.04)
knob['local_actuation_axis'] = '+Z turn axis'
collar_outline = [(math.cos(i * math.pi / 6) * .44, math.sin(i * math.pi / 6) * .44) for i in range(12)]
collar = local_mesh('Deck | selector recessed collar', collar_outline, -.015, -.085, knob_basis, well)
knob_outline = [(math.cos(i * math.pi / 6) * .34, math.sin(i * math.pi / 6) * .34) for i in range(12)]
subtract(collar, local_mesh('Construction | selector travel bore', knob_outline, .30, -.15, knob_basis, well))
AFTER_WELLS.append(collar)

launch = control('LaunchButton', '08 | Launch broad beveled button', launch_basis, chamfer(2.08, .864, .136), .015, -.06, orange)
letters = [
    [(0,0),(.1,0),(.1,.018),(.018,.018),(.018,.14),(0,.14)],
    [(0,0),(.018,0),(.031,.05),(.069,.05),(.082,0),(.1,0),(.062,.14),(.038,.14)],
    [(0,.14),(0,.025),(.025,0),(.075,0),(.1,.025),(.1,.14),(.082,.14),(.082,.033),(.068,.018),(.032,.018),(.018,.033),(.018,.14)],
    [(0,0),(.018,0),(.018,.105),(.082,0),(.1,0),(.1,.14),(.082,.14),(.082,.035),(.018,.14),(0,.14)],
    [(.1,0),(0,0),(0,.14),(.1,.14),(.1,.122),(.018,.122),(.018,.018),(.1,.018)],
    [(0,0),(.018,0),(.018,.061),(.082,.061),(.082,0),(.1,0),(.1,.14),(.082,.14),(.082,.079),(.018,.079),(.018,.14),(0,.14)],
]
letter_objects = []
for i, shape in enumerate(letters):
    xoff = -.50 + i * .18
    shape = [(x + xoff, y - .07) for x, y in shape]
    letter = local_mesh('Launch letter ' + str(i), shape, .015, .011, launch_basis, legendmat, parts_collection)
    if i == 1:
        hole = [(x + xoff, y - .07) for x,y in [(.037,.068),(.05,.113),(.063,.068)]]
        subtract(letter, local_mesh('A counter', hole, .02, .0, launch_basis, legendmat))
    letter_objects.append(letter)
text_obj = letter_objects[0]
for letter in letter_objects[1:]:
    combine(text_obj, letter)
text_obj.name = '19 | Launch legend'
subtract(launch, text_obj, True)
register(text_obj, 'LaunchLegend', True)

right_basis = D['pod_bases']['Right'][0]
for screen_name, centre in [('Primary', 1.35), ('Ordnance', 0)]:
    for action, dv in [('Previous', .28), ('Next', -.28)]:
        basis = shifted(right_basis, -2.65, centre + dv, .02)
        role = screen_name + action
        obj = control(role + 'Button', '30 | ' + screen_name + ' ' + action + ' key', basis,
                      chamfer(.43, .40, .06), .10, 0)
        outline = chevron if action == 'Previous' else [(-x, y) for x, y in chevron]
        legend(role + 'Legend', '30 | ' + screen_name + ' ' + action + ' ivory inlay', obj, outline, .10)
        well_obj, mouth, floor = panel('Deck | ' + role + ' well', shifted(basis, d=-.015), .53, .50, .02, .15, -.04)
        BODY.append(well_obj)
        WELL_CUTS.append((shifted(basis, d=-.015), mouth, floor, role))

for bank_index, basis in enumerate(switch_bases):
    for i in range(4):
        obj = local_mesh('Deck | decorative switch ' + str(bank_index * 4 + i + 1), chamfer(.34, .49, .055),
                         .015, -.09, shifted(basis, x=(i - 1.5) * .52), keymat)
        AFTER_WELLS.append(obj)

projector_basis = horizontal_basis((0, 0, 2.48))
outline = [(math.cos(i * math.pi / 6) * .55, math.sin(i * math.pi / 6) * .55) for i in range(12)]
rim = local_mesh('Deck | projector rim', outline, -.005, -.085, projector_basis, well)
BODY.append(rim)
outline = [(math.cos(i * math.pi / 6) * .43, math.sin(i * math.pi / 6) * .43) for i in range(12)]
lens = local_mesh('09 | Projector lens', outline, .0125, -.0125, projector_basis, cyan, parts_collection)
register(lens, 'ProjectorLens')

camera_forward = Vector((0, .9936, -.1132)).normalized()
camera_up = Vector((0, -camera_forward.z, camera_forward.y))
camera_eye = Vector((0, -18, 6.8))
side_clip = world_loft('Construction | protected horizontal camera span', [
    [camera_eye + camera_forward * depth + Vector((u * depth, 0, 0)) + camera_up * v * depth
     for u, v in [(-18 / 33 * 1.0001, -10), (18 / 33 * 1.0001, -10),
                  (18 / 33 * 1.0001, 10), (-18 / 33 * 1.0001, 10)]]
    for depth in (1, 100)])


def side_extension(obj, protect_camera_span=False):
    if protect_camera_span:
        subtract(obj, side_clip, True)
    BODY.append(obj)


for hand, label in ((-1, 'left'), (1, 'right')):
    header_sections = []
    for x, lower in ((12.35, 8.4132), (15.5, 8.40), (27, 8.40)):
        header_sections.append([(hand * x, y, z) for y, z in [
            (3.35, 10.62), (7.65, 10.62), (7.65, lower + .60),
            (3.90, lower), (3.35, lower + .30)]])
    side_extension(world_loft('Deck | ' + label + ' header continuation', header_sections,
                            face, [0, 0, 3, 2, 1]))

    band_sections = []
    for x, y, z in ((8.80, -2.471, .5574), (11.3, -3.5, .35),
                    (15.0, -4.5, .35), (27, -4.5, .35)):
        section = [(hand * x, y + dy, z + dz) for dy, dz in D['BAND_SECTION']]
        section += [(hand * x, 2.35, .86), (hand * x, 2.35, .64)]
        section += [(hand * x, y + dy, z + dz - .22) for dy, dz in reversed(D['BAND_SECTION'][2:])]
        band_sections.append(section)
    side_extension(world_loft('Deck | ' + label + ' band continuation', band_sections,
                            face, [0, 1, 1, 2, 1, 1, 0, 0, 0, 0, 0, 0]), True)

    housing_sections = []
    for x, front_y, shoulder_y, height in ((10.25, -2.0, .20, 4.36),
                                           (11.3, -3.0, .30, 4.25),
                                           (14.5, -4.4, -.80, 3.55),
                                           (27, -4.4, -.80, 3.55)):
        housing_sections.append([(hand * x, y, z) for y, z in [
            (front_y, .10), (front_y, 1.0), (shoulder_y - .30, height - .20),
            (shoulder_y, height), (2.55, height - .50), (2.55, .10)]])
    side_extension(world_loft('Deck | ' + label + ' outer housing shoulder', housing_sections,
                            face, [1, 1, 2, 3, 0, 0]), True)

    wall_sections = []
    for outer in (False, True):
        wall_sections.append([(hand * x, y, z) for x, y, z in [
            (27 if outer else 13.3, -4.0, -.5),
            (27 if outer else 13.3, -4.0, 1.4),
            (27 if outer else 14.4, -.6, 4.0),
            (27 if outer else 15.4, 3.35, 8.75),
            (27 if outer else 15.8, 4.15, 10.5),
            (27 if outer else 15.8, 5.0, 10.5),
            (27 if outer else 13.8, 3.3, -.5)]])
    side_extension(world_loft('Deck | ' + label + ' canopy side wall', wall_sections,
                            face, [0, 1, 2, 1, 0, 0, 0]))

remove(side_clip)

reference_tangent = math.tan(math.radians(34.11393) / 2)
reference_clip = world_loft('Construction | protected reference frustum', [
    [camera_eye + camera_forward * depth + Vector((u * depth, 0, 0)) + camera_up * v * depth
     for u, v in [(-reference_tangent * 16 / 9 * 1.005, -reference_tangent * 1.005),
                  (reference_tangent * 16 / 9 * 1.005, -reference_tangent * 1.005),
                  (reference_tangent * 16 / 9 * 1.005, reference_tangent * 1.005),
                  (-reference_tangent * 16 / 9 * 1.005, reference_tangent * 1.005)]]
    for depth in (1, 100)])
for name, section, mat in [
        ('header crown', [(3.35, 10.3), (7.65, 10.3), (7.65, 15), (3.35, 15)], face),
        ('console lower return', [(-3.0, -5), (2.35, -5), (2.35, .85), (-3.0, .85)], paint)]:
    extension = world_loft('Deck | ' + name,
                           [[(x, y, z) for y, z in section] for x in (-27, 27)], mat)
    subtract(extension, reference_clip, True)
    BODY.append(extension)
remove(reference_clip)

# A single union removes hidden interfaces between all intersecting structure pieces.
body = BODY[0]
body.name = 'Deck | joined closed structure'
for i, obj in enumerate(BODY[1:], 1):
    print('UNION', i, '/', len(BODY) - 1, obj.name, flush=True)
    combine(body, obj)
for basis, mouth, floor, name in WELL_CUTS:
    cut_outline = [(x * 1.003, y * 1.003) for x, y in mouth]
    if name in {'PrimaryPrevious', 'PrimaryNext', 'OrdnancePrevious', 'OrdnanceNext'}:
        lower = offset_outline(cut_outline, -.012)
        cutter = world_loft(name + ' final well cut', [
            [point(basis, x, y, z) for x, y in loop]
            for loop, z in [(lower, floor), (cut_outline, .055), (cut_outline, .40)]], well)
    else:
        cutter = local_mesh(name + ' final well cut', cut_outline, .40, floor, basis, well)
    subtract(body, cutter)
for obj in AFTER_WELLS:
    combine(body, obj)
for role, obj in PARTS.items():
    if role == 'ProjectorLens':
        subtract(body, obj, True)
body['export_role'] = 'ConsoleBody'
ink_collection.objects.link(body)


def accent(name, outline, front, back, basis):
    obj = local_mesh(name, outline, front, back, basis, cyan, accent_collection)
    subtract(body, obj, True)
    obj['export_role'] = 'ConsoleBody'
    ACCENTS.append(obj)
    return obj


for hand, label in ((-1, 'Left'), (1, 'Right')):
    basis = shifted(D['pod_bases'][label][0], hand * (5.85 / 2 - .12), -.60, .02)
    accent('Deck | ' + label.lower() + ' pod cyan strip', chamfer(.075, .66, .018), 0, -.15, basis)
    lo = Vector(D['STRUT_LANDING_POINTS'][0 if hand < 0 else 1])
    hi = Vector(D['STRUT_TOP_POINTS'][0 if hand < 0 else 1])
    along = (hi - lo).normalized()
    u = Vector((1, 0, 0))
    u = (u - along * u.dot(along)).normalized()
    normal = u.cross(along)
    basis = (hi - along * 2.05 + normal * (.15 + .008), u, along, normal)
    accent('Deck | ' + label.lower() + ' strut cyan strip', chamfer(.045, .42, .012), 0, -.025, basis)

for centre in (-8.6, 0, 8.6):
    lo, hi = centre - .475, centre + .475
    z0, z1 = D['brow_z'](lo) + .43, D['brow_z'](hi) + .43
    u = Vector((hi - lo, 0, z1 - z0)).normalized()
    v = Vector((0, 0, 1))
    normal = Vector((0, -1, 0))
    basis = (Vector(((lo + hi) / 2, 3.335, (z0 + z1) / 2)), u, v, normal)
    accent('Deck | header cyan strip ' + str(centre), [(-.475, -.0325), (.475, -.0325), (.475, .0325), (-.475, .0325)],
           0, -.035, basis)



for cutter in SCREEN_CUTS:
    subtract(body, cutter)
repair(body, True)
bm = bmesh.new()
bm.from_mesh(body.data)
bm.faces.ensure_lookup_table()
tiny_indices = {p.index for p in body.data.polygons if p.area < 1e-10}
tiny_indices.update(f.index for f in bm.faces if f.calc_area() == 0)
for face_zero in [bm.faces[index] for index in tiny_indices]:
    if not face_zero.is_valid:
        continue
    edge = max(face_zero.edges, key=lambda item: max((f.calc_area() for f in item.link_faces if f != face_zero), default=0))
    region = bmesh.ops.dissolve_edges(bm, edges=[edge], use_verts=False)['region']
    for poly in region:
        vertices = list(poly.verts)
        centre = bm.verts.new(sum((v.co for v in vertices), Vector()) / len(vertices))
        material_index = poly.material_index
        bm.faces.remove(poly)
        for a, b in zip(vertices, vertices[1:] + vertices[:1]):
            triangle = bm.faces.new((a, b, centre))
            triangle.material_index = material_index
bm.to_mesh(body.data)
bm.free()
body.data.update()
for name, section, mat in [
        ('header crown closure', [(3.35, 10.61), (7.65, 10.61), (7.65, 15), (3.35, 15)], face),
        ('console lower skirt', [(-2.0, -5), (2.35, -5), (2.35, .65), (-2.0, .20)], paint)]:
    extension = world_loft('Construction | ' + name,
                           [[(x, y, z) for y, z in section] for x in (-27, 27)], mat)
    combine(body, extension)
repair(body, True)
DEPARTURES.extend([
    'Pod faces stay at -0.10; unchanged glass outlines sit at -0.20 in 45-degree charcoal recesses, without raised lips.',
    'Mirrored upper inner housing chamfers follow the screen slope; the rear shell follows the same outline without the old notch.',
    'Hidden band undersides, pod cavity walls, joined heels and ramp returns close the open massing.',
    'Weapon keys retain their exact visible tops; their surrounding wells provide real depth.',
    'Pedestal dark riser strips omitted as surface detail reserved for slice 2.',
    'Thin inlaid key chevrons and launch lettering added as required moving legends.',
    'Camera uses old console AUTO sensor, 36 x 24 mm and 33 mm lens: vertical FOV 34.113931 degrees.',
    'Ship, selector and launch well floors are -.125 in their local frames to clear existing press travel.',
    'Raised housing skirts terminate inside the band front return; eight decorative switches extend invisibly into their well floors.',
    'Well cutter mouths expand 0.3 percent to prevent coincident edges at the bevel boundary.'
])
DEPARTURES.append('Header, pod shoulders, band and canopy side walls continue to X = +/-27 for wide cameras.')

plate = material('Deck | secondary satin blue plate', (.055, .076, .102))
inset = material('Deck | darker service plate', (.026, .040, .060))
edgepaint = material('Deck | lightly rubbed paint', (.112, .141, .172))
stencilpaint = material('Deck | muted service stencils', (.19, .23, .26))
hazardpaint = material('Deck | muted amber caution paint', (.39, .24, .070))
lamp_red = material('Deck | armed red emission', (1.0, .025, .008), 3.2)
lamp_green = material('Deck | status green emission', (.025, .70, .055), 2.2)
lamp_amber = material('Deck | status amber emission', (1.0, .28, .02), 1.8)
lamp_white = material('Deck | warm white downlight emission', (1.0, .78, .48), 1.5)
PALETTE.extend([plate, inset, edgepaint, stencilpaint, hazardpaint,
                lamp_red, lamp_green, lamp_amber, lamp_white])
for lamp_material in (lamp_red, lamp_green, lamp_amber, lamp_white):
    lamp_material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (0, 0, 0, 1)
finish_solids = []
finish_lamps = []
lamp_collection = collection('04 | Warm status and header lamps')


def finish_solid(name, basis, width, height, front=.014, back=-.018, mat=plate, clip=.035):
    obj = local_mesh('Finish | ' + name, chamfer(width, height, min(clip, width / 4, height / 4)),
                     front, back, basis, mat)
    finish_solids.append(obj)
    return obj


def finish_ring(name, basis, outer, inner, front, back, mat):
    count = len(outer)
    vertices = [point(basis, x, y, z) for outline, z in
                ((outer, front), (inner, front), (outer, back), (inner, back)) for x, y in outline]
    faces = []
    for i in range(count):
        j = (i + 1) % count
        faces.extend([(i, j, count + j, count + i),
                      (count * 2 + i, count * 3 + i, count * 3 + j, count * 2 + j),
                      (i, count * 2 + i, count * 2 + j, j),
                      (count + i, count + j, count * 3 + j, count * 3 + i)])
    obj = mesh('Finish | ' + name, vertices, faces, mat)
    repair(obj, True)
    finish_solids.append(obj)
    return obj


def fastener(name, basis, radius=.043, depth=.019):
    outline = [(math.cos(i * math.pi / 3) * radius, math.sin(i * math.pi / 3) * radius)
               for i in range(6)]
    obj = local_mesh('Finish | ' + name, outline, depth, depth - .016, basis, shoulder)
    finish_solids.append(obj)


def paint_mark(name, basis, width, height, mat=stencilpaint):
    return finish_solid(name, basis, width, height, .0006, -.002, mat, 0)


def stencil(name, basis, scale=1):
    xs = [-.25, -.20, -.04, .035, .22, .25]
    ys = [-.07, -.026, -.012, .012, .026, .07]
    count = len(xs) * len(ys)
    vertices = [point(basis, x * scale, y * scale, depth)
                for depth in (.0006, -.002) for y in ys for x in xs]
    faces, ids = [], []
    for j in range(len(ys) - 1):
        for i in range(len(xs) - 1):
            a = j * len(xs) + i
            cap = (a, a + 1, a + len(xs) + 1, a + len(xs))
            x, y = (xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2
            marked = (-.20 < x < -.04 and -.026 < y < .026) or (.035 < x < .22 and -.012 < y < .012)
            faces.extend([cap, tuple(index + count for index in reversed(cap))])
            ids.extend([PALETTE.index(stencilpaint if marked else inset), PALETTE.index(inset)])
    perimeter = list(range(len(xs)))
    perimeter += [j * len(xs) + len(xs) - 1 for j in range(1, len(ys))]
    perimeter += [(len(ys) - 1) * len(xs) + i for i in range(len(xs) - 2, -1, -1)]
    perimeter += [j * len(xs) for j in range(len(ys) - 2, 0, -1)]
    for a, b in zip(perimeter, perimeter[1:] + perimeter[:1]):
        faces.append((a, a + count, b + count, b))
        ids.append(PALETTE.index(inset))
    obj = mesh('Finish | ' + name, vertices, faces, inset, ids=ids)
    repair(obj, True)
    finish_solids.append(obj)


def lamp(name, basis, width, height, mat, depth=0, socket=True, socket_padding=.09, cap=.048):
    if socket:
        finish_solid(name + ' socket', basis, width + .10, height + socket_padding,
                     depth + .016, depth - .040, well, .045)
        finish_ring(name + ' socket lip', basis,
                    chamfer(width + .10, height + socket_padding, .035),
                    chamfer(width + .035, height + .025, .018),
                    depth + .021, depth + .012, shoulder)
    obj = local_mesh('Deck | ' + name + ' lens', chamfer(width, height, min(.035, height / 3)),
                     depth + cap, depth + .018, basis, mat, lamp_collection)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.material_index = 0
    obj.active_material_index = 0
    obj['export_role'] = 'ConsoleBody'
    finish_lamps.append(obj)
    return obj


for record in SCREEN_RECORDS:
    basis = (Vector(record['origin']) - Vector(record['normal']) * GLASS_DEPTH,
             Vector(record['u']), Vector(record['v']), Vector(record['normal']))
    finish_ring(record['label'] + ' thin stepped recess edge', basis,
                offset_outline(record['mouth'], .017), offset_outline(record['mouth'], .004),
                POD_FACE_DEPTH + .006, POD_FACE_DEPTH - .009, shoulder)

for hand, label in ((-1, 'Left'), (1, 'Right')):
    pb, _, rearward = D['pod_bases'][label]
    outer_face = shifted(pb, x=hand * 2.55, y=.80, d=POD_FACE_DEPTH)
    finish_solid(label + ' pod outer plate reveal', outer_face, .48, 2.70, .005, -.018, well)
    finish_solid(label + ' pod outer service plate', outer_face, .40, 2.62, .013, .003, inset)
    for y in (-1.12, 1.12):
        fastener(label + ' outer plate captive screw', shifted(outer_face, y=y), .045, .021)
    finish_solid(label + ' service latch', shifted(outer_face, y=-.58), .15, .20,
                 .027, .012, shoulder, .025)
    stencil(label + ' pod maintenance code', shifted(outer_face, y=.35, d=.013), .58)
    top_y = max(y for x, y in POD_RECORDS[0 if hand < 0 else 1]['profile'])
    top_u = pb[1]
    top_v = (rearward - top_u * rearward.dot(top_u)).normalized()
    top_normal = top_u.cross(top_v).normalized()
    brow_v = (top_v * .5 + top_normal * math.sqrt(.75)).normalized()
    top_basis = (point(pb, hand * .65, top_y, POD_FACE_DEPTH) + rearward * .10 + top_normal * .024,
                 top_u, brow_v, top_u.cross(brow_v))
    lamp(label + ' pod amber brow', top_basis, .46, .07, lamp_amber, socket_padding=.035, cap=.036)

    lo = Vector(D['STRUT_LANDING_POINTS'][0 if hand < 0 else 1])
    hi = Vector(D['STRUT_TOP_POINTS'][0 if hand < 0 else 1])
    along = (hi - lo).normalized()
    across = Vector((1, 0, 0))
    across = (across - along * across.dot(along)).normalized()
    normal = across.cross(along)
    length = (hi - lo).length
    groove_length = length - 3.55
    strut_basis = (lo + along * (.75 + groove_length / 2) + normal * .15, across, along, normal)
    finish_solid(label + ' strut centre channel', strut_basis, .055, groove_length,
                 .004, -.012, well, .024)
    for distance in (.48, length - .45):
        clamp_basis = (lo + along * distance + normal * .15, across, along, normal)
        finish_solid(label + ' strut clamp', clamp_basis, .38, .18, .025, -.014, shoulder, .025)
        finish_solid(label + ' strut clamp inset', shifted(clamp_basis, y=.01), .20, .055,
                     .030, .024, inset, .012)

for index, start in enumerate((1, 3, 5), 1):
    w0, h0, z0 = D['PEDESTAL_LOOPS'][start]
    w1, h1, z1 = D['PEDESTAL_LOOPS'][start + 1]
    v = Vector((0, (h0 - h1) / 2, z1 - z0)).normalized()
    u = Vector((1, 0, 0))
    basis = (Vector((0, -(h0 + h1) / 4, (z0 + z1) / 2)), u, v, u.cross(v))
    finish_solid('pedestal riser slot ' + str(index), basis, (w0 + w1) * .29, .11,
                 .005, -.018, well, .025)
    for hand in (-1, 1):
        fastener('pedestal riser screw', shifted(basis, x=hand * (w0 + w1) * .19), .035, .008)

for hand, label, basis in ((-1, 'Left', ship_basis), (1, 'Right', selector_basis)):
    status_basis = shifted(basis, y=.59, d=-.07)
    finish_solid(label + ' status strip seat', status_basis, .92, .23, .010, -.060, inset)
    for index, (x, mat, colour) in enumerate(((-.27, lamp_green, 'green'),
                                             (0, lamp_green, 'green'), (.27, lamp_amber, 'amber')), 1):
        lamp(label + ' status ' + str(index) + ' ' + colour,
             shifted(status_basis, x=x), .12, .095, mat)
    for x in (-.73, .73):
        fastener(label + ' ramp upper screw', shifted(basis, x=x, y=.61), .041, .017)
    for x, y, width in ((-.52, -.62, .23), (-.21, -.65, .10), (.47, -.63, .17)):
        paint_mark(label + ' light control scuff', shifted(basis, x=x, y=y, d=.012),
                   width, .015, edgepaint)
    stencil(label + ' ramp service code', shifted(basis, x=.05, y=-.51, d=-.122), .65)

for hand, label in ((-1, 'Left'), (1, 'Right')):
    guard_basis = shifted(launch_basis, x=hand * 1.43, y=.11)
    finish_solid(label + ' launch guard foot', guard_basis, .21, .97, .016, -.15, inset)
    finish_solid(label + ' launch guard rail', guard_basis, .12, .83, .095, .012, shoulder, .040)
    paint_mark(label + ' guard rubbed edge', shifted(guard_basis, y=.04, d=.095),
               .024, .45, edgepaint)
    lamp(label + ' Launch armed red', shifted(launch_basis, x=hand * 1.43, y=-.49),
         .15, .16, lamp_red, depth=.015)
    caution_basis = shifted(launch_basis, x=hand * 1.77, y=-.02, d=-.058)
    for index, y in enumerate((-.26, 0, .26)):
        shape = [(-.07, y - .08), (.07, y), (-.07, y + .08),
                 (-.07, y + .036), (.002, y), (-.07, y - .036)]
        if hand < 0:
            shape = [(-x, yy) for x, yy in reversed(shape)]
        mark = local_mesh('Finish | ' + label + ' launch caution chevron ' + str(index),
                          shape, .001, -.004, caution_basis, hazardpaint)
        finish_solids.append(mark)

for hand, label in ((-1, 'Left'), (1, 'Right')):
    basis = D['band_basis'](hand * 7.82, .016)
    finish_solid(label + ' band utility plate reveal', basis, .98, 1.04, .008, -.022, well)
    finish_solid(label + ' band utility plate', basis, .90, .96, .018, .006, plate)
    vent_basis = shifted(basis, y=.06, d=.018)
    finish_solid(label + ' vent recess', vent_basis, .59, .49, .003, -.010, well)
    for index in range(4):
        finish_solid(label + ' vent louvre ' + str(index), shifted(vent_basis, y=(index - 1.5) * .10),
                     .53, .035, .024, .002, shoulder, .012)
    for y in (-.38, .38):
        fastener(label + ' utility plate screw', shifted(basis, x=hand * .34, y=y), .035, .025)
    stencil(label + ' utility plate code', shifted(basis, y=-.32, d=.019), .65)

for bank_index, basis in enumerate(switch_bases):
    for index in range(3):
        finish_solid('switch bank separator ' + str(bank_index) + ' ' + str(index),
                     shifted(basis, x=(index - 1) * .52), .048, .46,
                     -.035, -.083, shoulder, .012)

front_basis = (Vector((0, 3.35, 10.47)), Vector((1, 0, 0)),
               Vector((0, 0, 1)), Vector((0, -1, 0)))
lamp('Header centre amber marker', front_basis, .42, .085, lamp_amber)
for hand, label in ((-1, 'Left'), (1, 'Right')):
    basis = shifted(front_basis, x=hand * 3.05, y=.015)
    finish_solid(label + ' header long plate reveal', basis, 3.75, .14, .006, -.015, well, .025)
    finish_solid(label + ' header long face plate', basis, 3.64, .09, .014, .004, plate, .018)
    for x in (-1.68, 1.68):
        fastener(label + ' header plate screw', shifted(basis, x=x), .030, .021)
    stencil(label + ' header code', shifted(basis, x=hand * 1.03, y=-.14, d=.003), .80)

for index, x in enumerate((-8.2, -5.0, -2.3, 2.3, 5.0, 8.2), 1):
    y = 3.87
    z = D['brow_z'](x) + .025
    u = Vector((1, 0, 0))
    v = Vector((0, -math.sqrt(.75), .5))
    basis = (Vector((x, y, z)), u, v, u.cross(v))
    obj = lamp('Header downlight ' + str(index).zfill(2) + ' warm white',
               basis, .56, .12, lamp_white, socket_padding=.035)
    light_data = bpy.data.lights.new('Preview | header warm pool ' + str(index), 'SPOT')
    light_data.energy = 165
    light_data.color = (1.0, .78, .48)
    light_data.spot_size = math.radians(66)
    light_data.spot_blend = .65
    light_data.shadow_soft_size = .22
    light_obj = bpy.data.objects.new(light_data.name, light_data)
    scene.collection.objects.link(light_obj)
    light_obj.location = point(basis, d=.07)
    target = Vector((x * .75, -1.0, 1.25))
    light_obj.rotation_euler = (target - light_obj.location).to_track_quat('-Z', 'Y').to_euler()
    light_obj['preview_only'] = True


def clip_plate(outline, axis, limit, keep_less):
    result = []
    for a, b in zip(outline, outline[1:] + outline[:1]):
        inside_a = (a[axis] <= limit) == keep_less
        inside_b = (b[axis] <= limit) == keep_less
        if inside_a:
            result.append(a)
        if inside_a != inside_b:
            t = (limit - a[axis]) / (b[axis] - a[axis])
            result.append(tuple(a[i] + (b[i] - a[i]) * t for i in range(2)))
    return result


def surface_plate(name, corners, mat=plate, margin=.025, relief=.03):
    corners = [Vector(p) for p in corners]
    origin = sum(corners, Vector()) / len(corners)
    u = (corners[1] - corners[0]).normalized()
    n = u.cross(corners[-1] - corners[0]).normalized()
    v = n.cross(u)
    basis = (origin, u, v, n)
    outline = [((p - origin).dot(u), (p - origin).dot(v)) for p in corners]
    outline = offset_outline(outline, -margin)
    heights = [(p - origin).dot(n) for p in corners]
    loops = [[point(basis, x, y, height + depth)
              for (x, y), height in zip(outline, heights)] for depth in (-.006, relief)]
    obj = world_loft('Finish 2 | ' + name, loops, mat)
    finish_solids.append(obj)
    return obj, basis


def rubbed_paint(obj, basis, x_range, y_range):
    frame = Matrix((basis[1], basis[2], basis[3])).transposed().to_4x4()
    frame.translation = basis[0]
    transform = frame.inverted() @ obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(transform)
    for axis, bounds in ((0, x_range), (1, y_range)):
        normal = Vector((1, 0, 0)) if axis == 0 else Vector((0, 1, 0))
        for value in bounds:
            bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                                  dist=1e-7, plane_co=normal*value, plane_no=normal)
    bm.normal_update()
    for f in bm.faces:
        centre = f.calc_center_median()
        if (f.normal.z > .95 and x_range[0] < centre.x < x_range[1]
                and y_range[0] < centre.y < y_range[1]):
            f.material_index = PALETTE.index(edgepaint)
    bm.transform(transform.inverted())
    bm.to_mesh(obj.data)
    bm.free()
    repair(obj, True)


def service_hatch(name, basis, width, height):
    finish_solid(name + ' gasket', basis, width, height, .008, -.008, well, .09)
    finish_solid(name + ' cover', basis, width - .09, height - .09, .025, .005, inset, .07)
    for y in (-height * .29, height * .29):
        finish_solid(name + ' hinge', shifted(basis, x=-width / 2 + .075, y=y),
                     .15, .19, .052, .018, shoulder, .025)
    finish_solid(name + ' latch', shifted(basis, x=width / 2 - .15),
                 .15, .24, .055, .024, shoulder, .03)
    stencil(name + ' code', shifted(basis, x=-.03, y=height * .27, d=.026), .85)


def vent_grille(name, basis, width, height, rows=4):
    finish_solid(name + ' recess', basis, width, height, .008, -.008, well, .045)
    pitch = (height - .10) / rows
    for row in range(rows):
        finish_solid(name + ' slat ' + str(row),
                     shifted(basis, y=(row - (rows - 1) / 2) * pitch),
                     width - .12, pitch * .44, .032, .005, shoulder, .012)


def trunk_segment(name, a, b, normal, width=.13, depth=.07):
    a, b, normal = Vector(a), Vector(b), Vector(normal).normalized()
    along = (b - a).normalized()
    across = along.cross(normal).normalized()
    basis = ((a + b) / 2, across, along, across.cross(along))
    finish_solid(name, basis, width, (b - a).length, depth, -.012, inset, .035)
    for y in (-(b - a).length * .32, (b - a).length * .32):
        finish_solid(name + ' saddle', shifted(basis, y=y),
                     width + .065, .10, depth + .018, -.016, shoulder, .018)


for hand, label in ((-1, 'Left'), (1, 'Right')):
    pb, _, rearward = D['pod_bases'][label]
    record = POD_RECORDS[0 if hand < 0 else 1]
    profile = offset_outline(record['profile'], -.035)
    split_y = -.43 if hand < 0 else .95
    for row, (low, high) in enumerate(((-3, split_y - .022), (split_y + .022, 4))):
        for col, (left, right) in enumerate(((-4, -.022), (.022, 4))):
            outline = profile
            for axis, value, less in ((1, low, False), (1, high, True),
                                      (0, left, False), (0, right, True)):
                outline = clip_plate(outline, axis, value, less)
            skin = local_mesh('Finish 2 | ' + label + ' frame plate', outline,
                              POD_FACE_DEPTH + .028, POD_FACE_DEPTH - .006, pb,
                              plate if (row + col) % 2 else shoulder)
            for screen in SCREEN_RECORDS[0:2] if hand < 0 else SCREEN_RECORDS[2:4]:
                sb = (Vector(screen['origin']), pb[1], pb[2], pb[3])
                cutter = local_mesh('Construction | quiet screen border',
                                   offset_outline(screen['mouth'], .035), .5, -.2, sb)
                subtract(skin, cutter)
            exclusions = [(hand * 2.55, .80, .53, 2.77),
                          (hand * 2.805, -.60, .15, .74)]
            if hand > 0:
                exclusions.append((-2.65, .675, .64, 2.67))
            for x, y, width, height in exclusions:
                cutter = local_mesh('Construction | retained pod hardware',
                                   chamfer(width, height, .04), .3, -.3, shifted(pb, x=x, y=y))
                subtract(skin, cutter)
            rubbed_paint(skin, pb, tuple(sorted((hand*2.845, hand*2.875))), (1.25, 1.65))
            finish_solids.append(skin)

    front = [Vector(p) for p in record['front_world']]
    mid = [point(pb, x + (math.copysign(.20, x) if hand * x > 0 else 0), y, POD_FACE_DEPTH)
           + rearward * .42 for x, y in record['profile']]
    rear = [p + rearward * .83 for p in mid]
    for index in (4, 2 if hand > 0 else 6):
        j = (index + 1) % 8
        for start, end in ((.035, .49), (.51, .965)):
            a, b = mid[index].lerp(mid[j], start), mid[index].lerp(mid[j], end)
            c, d = rear[index].lerp(rear[j], end), rear[index].lerp(rear[j], start)
            panel_obj, basis = surface_plate(label + ' cap and flank plate', [b, a, d, c],
                                              plate if start < .1 else inset, .035)
            if index != 4 and start < .1:
                vent_grille(label + ' flank cooling', shifted(basis, d=.031), 1.05, .49, 3)

    x = hand * 2.31
    trunk_segment(label + ' pod cable trunk', point(pb, x, -.90, -.055),
                  point(pb, x, 2.18, -.055), pb[3], .095, .045)
    finish_solid(label + ' upper corner block', shifted(pb, x=hand*2.70, y=2.42, d=-.10),
                 .24, .22, .039, -.005, inset, .035)
    fastener(label + ' corner captive screw', shifted(pb, x=hand*2.70, y=2.42, d=-.059), .036)
    if hand < 0:
        stencil(label + ' lower frame code', shifted(pb, x=2.57, y=-.80, d=-.070), .65)
    else:
        stencil(label + ' lower frame code', shifted(pb, x=.78, y=-1.25, d=-.070), 1.0)


def band_point(x, section):
    origin, _ = D['band_origin'](x)
    dy, dz = D['BAND_SECTION'][section]
    return origin + Vector((0, dy, dz))


for segment, (a, b) in enumerate(zip(D['BAND_PATH'], D['BAND_PATH'][1:])):
    divisions = 2 if b[0] - a[0] > 3 else 1
    for division in range(divisions):
        x0 = a[0] + (b[0] - a[0]) * division / divisions
        x1 = a[0] + (b[0] - a[0]) * (division + 1) / divisions
        for lower, upper in ((1, 2), (2, 3)):
            obj, basis = surface_plate('band ' + str((segment, division, lower)),
                                       [band_point(x0, lower), band_point(x1, lower),
                                        band_point(x1, upper), band_point(x0, upper)],
                                       plate if (segment + division) % 2 else face, .023)
            if lower == 2:
                for wb, mouth, floor, name in WELL_CUTS:
                    cutter = local_mesh('Construction | control frame clearance',
                                       offset_outline(mouth, .23), .8, -.8, wb)
                    subtract(obj, cutter)
            if lower == 1:
                width = min(.55, (x1 - x0) * .35)
                rubbed_paint(obj, basis, (-width/2, width/2), (.026, .054))

for hand, label in ((-1, 'Left'), (1, 'Right')):
    pb = D['pod_bases'][label][0]
    join_v = D['POD_PROFILE_BOTTOM'] + (.50 if hand > 0 else 0)
    upper = [point(pb, hand * u, join_v, -.10) for u in (-2.705, 2.705)]
    edge = [Vector((hand * x, y, z)) for x, y, z in [(4.35, -.05, 1.24), (8.6, -2.55, .74)]]
    middle = [p * .55 + q * .45 + Vector((0, 0, .06)) for p, q in zip(upper, edge)]
    for row, (back, front) in enumerate(((upper, middle), (middle, edge))):
        for low, high in ((.02, .49), (.51, .98)):
            corners = [front[0].lerp(front[1], low), front[0].lerp(front[1], high),
                       back[0].lerp(back[1], high), back[0].lerp(back[1], low)]
            if hand < 0:
                corners.reverse()
            _, basis = surface_plate(label + ' wing plate', corners,
                                     plate if row == 0 else inset, .025)
            if row == 0 and low > .1:
                stencil(label + ' wing code', shifted(basis, d=.031), 1.1)
                for x in (-.62, .62):
                    fastener(label + ' wing captive screw', shifted(basis, x=x, d=.03), .035)

    bb = D['band_basis'](hand * 5.85, 0)
    vent_grille(label + ' under-switch intake', shifted(bb, y=-.76, d=.052), 1.90, .30, 2)
    for y in (-.96, .81):
        stencil(label + ' intake identifier', shifted(bb, x=hand * 1.04, y=y, d=.02), .65)
    for a, b in ((3.95, 5.25), (5.25, 7.1), (7.1, 8.25)):
        aa = band_point(hand * a, 3) - bb[2] * .32
        zz = band_point(hand * b, 3) - bb[2] * .32
        trunk_segment(label + ' rear band cable trunk', aa, zz, bb[3], .13, .065)

    x = D['STRUT_LANDING_POINTS'][0 if hand < 0 else 1][0]
    base = []
    for u, v in chamfer(1.90, 3.49, .32):
        px, py = x + hand * .30 + u, 1.395 + v
        base.append(Vector((px, py, D['band_surface_z'](px, min(py, 2.35)))))
    cap = [Vector((x + u, 2.43 + v, 1.32)) for u, v in chamfer(.88, 1.12, .17)]
    for i in (0, 1, 7):
        j = (i + 1) % 8
        corners = [base[i], base[j], cap[j], cap[i]]
        _, basis = surface_plate(label + ' strut shoulder armour', corners, plate, .025)
        if i == 0:
            finish_solid(label + ' shoulder keeper', shifted(basis, d=.031), .34, .17,
                         .025, -.008, inset)
            fastener(label + ' shoulder pin', shifted(basis, d=.057), .047)

for hand in (-1, 1):
    w0, h0, z0 = D['PEDESTAL_LOOPS'][1]
    w1, h1, z1 = D['PEDESTAL_LOOPS'][2]
    side_corners = [(hand * w0 / 2, -.56, z0), (hand * w0 / 2, .56, z0),
                    (hand * w1 / 2, .48, z1), (hand * w1 / 2, -.48, z1)]
    if hand < 0:
        side_corners.reverse()
    _, basis = surface_plate('pedestal base cheek', side_corners, plate, .035)
    finish_solid('pedestal base latch', shifted(basis, d=.031), .23, .12, .028, -.008, inset)

header_basis = (Vector((0, 3.35, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0)))
for i, ((x0, z0), (x1, z1)) in enumerate(zip(D['HEADER_STATIONS'], D['HEADER_STATIONS'][1:])):
    if abs(z1-z0) > .1:
        hv = D['hv']
        surface_plate('header stepped cheek', [hv[i*5+4], hv[(i+1)*5+4], hv[(i+1)*5], hv[i*5]],
                      shoulder, .05, .025)
for hand in (-1, 1):
    for inner, outer in ((.58, 3.2), (3.2, 5.95), (7.5, 10.04), (10.04, 12.34),
                         (12.42, 15.4), (15.4, 19.5), (19.5, 26.8)):
        x0, x1 = sorted((hand * inner, hand * outer))
        lower0 = D['brow_z'](x0) + (.32 if inner < 6 else .53) if abs(x0) < 12.4 else 8.94
        lower1 = D['brow_z'](x1) + (.32 if inner < 6 else .53) if abs(x1) < 12.4 else 8.94
        ceiling = 10.59
        outline = [(x0 + .023, lower0), (x1 - .023, lower1),
                   (x1 - .023, ceiling), (x0 + .023, ceiling)]
        obj = local_mesh('Finish 2 | header broad face cassette', outline, .032, -.005,
                         header_basis, plate if inner < 4 else inset)
        if inner < 6:
            for x, y, width, height in ((hand * 3.05, 10.485, 3.84, .20),
                                         (hand * 4.08, 10.345, .52, .18)):
                cutter = local_mesh('Construction | retained header trim', chamfer(width, height, .02),
                                   .2, -.2, shifted(header_basis, x=x, y=y))
                subtract(obj, cutter)
        finish_solids.append(obj)
        if inner > 7:
            service_hatch('header junction access', shifted(header_basis, x=(x0+x1)/2,
                                                            y=(max(lower0, lower1)+ceiling)/2, d=.033),
                          min(1.35, outer-inner-.3), .55)
    for x0, x1 in ((.15, 2.0), (2.65, 4.65), (5.35, 6.0), (8.7, 9.9)):
        a, b = hand*x0, hand*x1
        corners = [Vector((a, 3.90, D['brow_z'](a))), Vector((b, 3.90, D['brow_z'](b))),
                   Vector((b, 3.35, D['brow_z'](b)+.30)), Vector((a, 3.35, D['brow_z'](a)+.30))]
        if hand < 0:
            corners.reverse()
        _, basis = surface_plate('header underside cassette', corners, inset, .055, .025)
        if x1-x0 > 1.8:
            vent_grille('header underside cooling', shifted(basis, d=.026), .95, .26, 2)

for hand, label in ((-1, 'Left'), (1, 'Right')):
    for x0, x1, y0, y1, z0, z1, top0, top1 in (
            (10.30, 11.3, -2.05, -3.0, 1.0, 1.0, .195, .30),
            (11.3, 14.5, -3.0, -4.4, 1.0, 1.0, .30, -.80),
            (14.5, 19.0, -4.4, -4.4, 1.0, 1.0, -.80, -.80),
            (19.0, 26.8, -4.4, -4.4, 1.0, 1.0, -.80, -.80)):
        height0 = 4.3547619 if x0 < 11 else (4.25 if x0 < 12 else 3.55)
        height1 = 4.25 if x1 < 12 else 3.55
        corners = [Vector((hand*x0, y0, z0)), Vector((hand*x1, y1, z1)),
                   Vector((hand*x1, top1-.30, height1-.20)),
                   Vector((hand*x0, top0-.30, height0-.20))]
        if hand < 0:
            corners.reverse()
        for low, high in ((.025, .49), (.51, .975)):
            a, b, c, d = corners
            _, basis = surface_plate(label + ' side console cover',
                                     [a.lerp(d, low), b.lerp(c, low), b.lerp(c, high), a.lerp(d, high)],
                                     plate if low < .1 else inset, .035)
            if x0 < 11 and low > .1:
                service_hatch(label + ' inner console access', shifted(basis, d=.031), .68, .58)
            elif x0 < 11:
                for x in (-.30, .30):
                    finish_solid(label + ' inner console grip foot', shifted(basis, x=x, d=.031),
                                 .14, .22, .05, -.008, shoulder)
                finish_solid(label + ' inner console grip', shifted(basis, d=.031), .71, .11,
                             .10, .065, inset, .025)
            elif x1-x0 > 2 and low > .1:
                service_hatch(label + ' side console service', shifted(basis, x=-.48, d=.031), .95, .68)
                vent_grille(label + ' side console exhaust', shifted(basis, x=.78, d=.031), .63, .60, 4)
            elif x1-x0 > 2:
                for x in (-.47, .47):
                    finish_solid(label + ' side handhold foot', shifted(basis, x=x, d=.031),
                                 .17, .24, .05, -.008, shoulder)
                finish_solid(label + ' side handhold', shifted(basis, d=.031), 1.05, .12,
                             .12, .07, inset, .025)

bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
for obj in finish_solids:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
body['finish_slice'] = 'Clean light use; cyan structure, warm status'
DEPARTURES.remove('Pedestal dark riser strips omitted as surface detail reserved for slice 2.')
DEPARTURES.append('Finish detail uses closed shallow solids joined into ConsoleBody; original controls and glasses are unchanged.')
DEPARTURES.append('Stencils, caution marks and local scuffs use flat material geometry, without textures or linked colour nodes.')
DEPARTURES.append('Warm header spotlights are preview-only; named lamp lenses provide the exporter anchors and emission colours.')

freestyle = bpy.context.view_layer.freestyle_settings
freestyle.crease_angle = math.radians(134.43)
for lineset in list(freestyle.linesets):
    freestyle.linesets.remove(lineset)
for name, width, target, crease, position in [
        ('Outer ink contour', 2.2, ink_collection, False, 'CENTER'),
        ('Internal ink folds', 1.15, ink_collection, True, 'CENTER'),
        ('Lit legend rim', 1.32, legend_ink_collection, False, 'OUTSIDE')]:
    lineset = freestyle.linesets.new(name)
    lineset.select_by_collection = True
    lineset.collection = target
    lineset.collection_negation = 'INCLUSIVE'
    lineset.select_by_image_border = True
    lineset.select_silhouette = not crease
    lineset.select_border = False
    lineset.select_crease = crease
    lineset.select_contour = False
    lineset.select_external_contour = False
    lineset.select_material_boundary = False
    lineset.linestyle.color = (.009, .004, .002)
    lineset.linestyle.alpha = 1
    lineset.linestyle.thickness = width
    lineset.linestyle.thickness_position = position
    lineset.linestyle.caps = 'ROUND'

gamecam = D['gamecam']
gamecam.name = '07 Camera - perspective'
gamecam.data.lens = 33
gamecam.data.sensor_width = 36
gamecam.data.sensor_height = 24
gamecam.data.sensor_fit = 'AUTO'
gamecam.location = (0, -18, 6.8)
gamecam.rotation_euler = Vector((0, .9936, -.1132)).to_track_quat('-Z', 'Y').to_euler()
scene.camera = gamecam
game_projection(scene, gamecam, 1920, 1080)
bpy.context.view_layer.update()

subject = [obj.matrix_world @ Vector(corner) for obj in scene.objects
           if obj.type == 'MESH' and not obj.get('preview_only') for corner in obj.bound_box]
for camera in (D['threecam'], D['topcam'], D['sidecam'], D['frontcam']):
    inverse = camera.matrix_world.inverted()
    local = [inverse @ p for p in subject]
    xmin, xmax = min(p.x for p in local), max(p.x for p in local)
    ymin, ymax = min(p.y for p in local), max(p.y for p in local)
    camera.location += camera.matrix_world.to_3x3() @ Vector(((xmin + xmax) / 2, (ymin + ymax) / 2, 0))
    camera.data.ortho_scale = max((xmax - xmin) / .89, (ymax - ymin) * (16 / 9) / .89)
bpy.context.view_layer.update()


def audit():
    records = []
    for obj in scene.objects:
        if obj.type != 'MESH' or obj.get('preview_only'):
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        boundary = sum(e.is_boundary for e in bm.edges)
        nonmanifold = sum(not e.is_manifold for e in bm.edges)
        loose = sum(not v.link_faces for v in bm.verts)
        volume = bm.calc_volume(signed=True)
        bm.free()
        obj.data.calc_loop_triangles()
        bm_check = bmesh.new()
        bm_check.from_mesh(obj.data)
        zero_area = sum(f.calc_area() == 0 for f in bm_check.faces)
        bm_check.free()
        records.append({'name': obj.name, 'role': obj.get('export_role', 'Body'),
                        'triangles': len(obj.data.loop_triangles), 'vertices': len(obj.data.vertices),
                        'closed': nonmanifold == 0, 'manifold': nonmanifold == 0,
                        'boundary_edges': boundary, 'nonmanifold_edges': nonmanifold, 'loose_vertices': loose,
                        'volume': volume, 'planar_screen': bool(obj.get('planar_screen')),
                        'zero_area_faces': max(zero_area, sum(p.area == 0 for p in obj.data.polygons))})
    lamp_records = []
    for obj in finish_lamps:
        centre = obj.matrix_world @ (sum((Vector(corner) for corner in obj.bound_box), Vector()) / 8)
        shader = obj.active_material.node_tree.nodes.get('Principled BSDF')
        lamp_records.append({'name': obj.name, 'anchor': obj.name.split('| ', 1)[1],
                             'position': list(centre),
                             'color': list(shader.inputs['Emission Color'].default_value[:3]),
                             'downlight': 'Header downlight' in obj.name})
    (PREVIEWS / 'finish-lamps.json').write_text(json.dumps(lamp_records, indent=2), encoding='utf-8')
    report = {'mesh_objects': records, 'triangles': sum(r['triangles'] for r in records), 'lamps': lamp_records,
              'parts': {role: obj.name for role, obj in PARTS.items()}, 'screens': SCREEN_RECORDS,
              'controls': CONTROL_RECORDS, 'pods': POD_RECORDS, 'departures': DEPARTURES}
    (PREVIEWS / 'build-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    failures = [r['name'] for r in records if (not r['closed'] and not r['planar_screen'])
                or r['loose_vertices'] or r['zero_area_faces']]
    assert not failures, failures
    assert report['triangles'] < 40000, report['triangles']
    print('MESH AUDIT', report['triangles'], 'triangles;', len(records), 'objects', flush=True)


audit()
for col in list(bpy.data.collections):
    if not col.objects and not col.children:
        bpy.data.collections.remove(col)
bpy.data.orphans_purge(do_recursive=True)
scene.render.filepath = str(PREVIEWS / 'deck-camera.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'deck.blend'))
if not ARGS.no_render:
    bpy.ops.render.render(write_still=True)
    for suffix, width, height in VIEWS:
        if suffix == '16x9':
            continue
        game_projection(scene, gamecam, width, height)
        scene.render.filepath = str(PREVIEWS / ('deck-camera-' + suffix + '.png'))
        bpy.ops.render.render(write_still=True)
    game_projection(scene, gamecam, 1920, 1080)
    if not ARGS.camera_only:
        backdrop = bpy.data.objects['19 | Distant space']
        backdrop.hide_render = True
        background = scene.world.node_tree.nodes.get('Background')
        background.inputs['Color'].default_value = (.19, .22, .27, 1)
        background.inputs['Strength'].default_value = .55
        for camera, suffix in [(D['threecam'], 'three-quarter'), (D['topcam'], 'top'),
                               (D['sidecam'], 'side'), (D['frontcam'], 'front')]:
            scene.camera = camera
            scene.render.filepath = str(PREVIEWS / ('deck-' + suffix + '.png'))
            bpy.ops.render.render(write_still=True)
        backdrop.hide_render = False
        background.inputs['Color'].default_value = (.09, .12, .17, 1)
        background.inputs['Strength'].default_value = .30
    scene.camera = gamecam
    scene.render.filepath = str(PREVIEWS / 'deck-camera.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'deck.blend'))
print('DECK COMPLETE', flush=True)
