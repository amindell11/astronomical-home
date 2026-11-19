"""Build deck.blend and five previews from the approved local massing/build_D13.py.

Run Blender with --python-exit-code 1 --python build_deck.py. Options after --:
--no-render saves geometry and its audit; --camera-only limits previews;
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
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
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


for hand, label in ((-1, 'Left'), (1, 'Right')):
    basis, _, rearward = D['pod_bases'][label]
    profile = [(x, y + .68) for x, y in chamfer(5.85, 4.40, .45)]
    profile = D['inner_top_clip'](profile, hand, (1.35, .95))
    front = [point(basis, x, y, -.10) for x, y in profile]
    middle = [point(basis, x + (math.copysign(.20, x) if hand * x > 0 else 0), y, -.10) + rearward * .42
              for x, y in profile]
    rear = [p + rearward * .83 for p in middle]
    native_vertices, native_triangles, native_indices = POD_NATIVE[label]
    pod = mesh('Deck | ' + label.lower() + ' deep pod shell', native_vertices,
               native_triangles + [tuple(range(8))], face, ids=native_indices + [1])
    repair(pod, True)
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
        outer = chamfer(size[0] + .26, size[1] + .26, .26)
        if clip:
            outer = D['inner_top_clip'](outer, clip, (.85, .60))
        bezel = local_mesh('Deck | ' + label_screen.lower() + ' recessed bezel', outer, .035, -.16, sb, well)
        combine(pod, bezel)
        cutter = local_mesh(label_screen + ' display pocket', outline, .20, -.03, sb, well)
        subtract(pod, cutter)
        vertices = [(x, y, -.008) for x, y in outline]
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
                               'outline': outline, 'origin': list(point(sb, d=-.008)),
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

# A single union removes hidden interfaces between all intersecting structure pieces.
body = BODY[0]
body.name = 'Deck | joined closed structure'
for i, obj in enumerate(BODY[1:], 1):
    print('UNION', i, '/', len(BODY) - 1, obj.name, flush=True)
    combine(body, obj)
for basis, mouth, floor, name in WELL_CUTS:
    cut_outline = [(x * 1.003, y * 1.003) for x, y in mouth]
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
DEPARTURES.extend([
    'Pod bezel lip rises to local depth +0.035 around exact D13 glass at -0.008; exterior pod profile stays at -0.10.',
    'Hidden band undersides, pod cavity walls, joined heels and ramp returns close the open massing.',
    'Weapon keys retain their exact visible tops; their surrounding wells provide real depth.',
    'Pedestal dark riser strips omitted as surface detail reserved for slice 2.',
    'Thin inlaid key chevrons and launch lettering added as required moving legends.',
    'Camera uses old console AUTO sensor, 36 x 24 mm and 33 mm lens: vertical FOV 34.113931 degrees.',
    'Ship, selector and launch well floors are -.125 in their local frames to clear existing press travel.',
    'Raised housing skirts terminate inside the band front return; eight decorative switches extend invisibly into their well floors.',
    'Well cutter mouths expand 0.3 percent to prevent coincident edges at the bevel boundary.'
])

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
    report = {'mesh_objects': records, 'triangles': sum(r['triangles'] for r in records),
              'parts': {role: obj.name for role, obj in PARTS.items()}, 'screens': SCREEN_RECORDS,
              'controls': CONTROL_RECORDS, 'departures': DEPARTURES}
    (PREVIEWS / 'build-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    failures = [r['name'] for r in records if (not r['closed'] and not r['planar_screen'])
                or r['loose_vertices'] or r['zero_area_faces']]
    assert not failures, failures
    assert report['triangles'] < 70000, report['triangles']
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
