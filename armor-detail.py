import hashlib
import json
import math
import struct

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import convex_hull_2d


SCENE = bpy.context.scene
OBJECTS = bpy.data.objects
CAMERA = SCENE.camera
COLLECTION_NAME = '32 | Fitted console armor'
SIDE_CLEARANCE, SEAT_GAP = .012, .010
TERMINAL_BACK, TERMINAL_FLOOR = -.025, -.035
BASE_FRONT, BASE_BACK, BASE_FLOOR = .11, .025, .015
NORMAL = Vector((0, -.15, 1)).normalized()
DESK_Y = Vector((0, 1, .15)).normalized()
DESK_CONSTANT = .72 / Vector((0, -.15, 1)).length
if bpy.context.mode != 'OBJECT' or CAMERA is None or CAMERA.data.type != 'PERSP':
    raise RuntimeError('Armor requires Object mode and the authored perspective camera.')
if COLLECTION_NAME in bpy.data.collections:
    raise RuntimeError('Reload checkpoint2fe21f843 before another armor pass.')
terminal = OBJECTS['30 | Weapon terminal frame']
casting = OBJECTS['30 | Weapon terminal casting']
desk = OBJECTS['31 | Continuous desk casting']
gasket = OBJECTS['31 | Launch inset gasket']
launch = OBJECTS['07 | Launch assembly']
launch_key = OBJECTS['08 | Launch broad beveled button']
top_screws = [OBJECTS['30 | Captive screw ' + str(index)] for index in (1, 2)]
palette = list(OBJECTS['08 | Thick monitor armor frame'].data.materials)
secondary = next(material for material in OBJECTS['08 | Raised brow cap'].data.materials
                 if material.name == '21 | Secondary cover paint')
secondary_index = len(palette)
palette.append(secondary)
if palette[1].name != '21 | Paint edge' or palette[2].name != '21 | Warm grey casing':
    raise RuntimeError('The authored armor palette changed.')


def geometry_hash(obj):
    if obj.type != 'MESH':
        return None
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(struct.pack('<3d', *vertex.co))
    for polygon in obj.data.polygons:
        digest.update(struct.pack('<3i', len(polygon.vertices), polygon.material_index, polygon.use_smooth))
        digest.update(struct.pack('<' + 'i' * len(polygon.vertices), *polygon.vertices))
    for material in obj.data.materials:
        digest.update((material.name if material else '<empty>').encode())
    return digest.hexdigest()


bpy.context.view_layer.update()
protected = {obj.name: {'matrix': obj.matrix_world.copy(), 'geometry': geometry_hash(obj),
                       'hidden': (obj.hide_render, obj.hide_get()),
                       'modifiers': [(mod.name, mod.type) for mod in obj.modifiers]}
             for obj in SCENE.objects}
collection = bpy.data.collections.new(COLLECTION_NAME)
SCENE.collection.children.link(collection)
desk_root = bpy.data.objects.new('32 | Base armor frame', None)
collection.objects.link(desk_root)
desk_frame = Matrix((Vector((1, 0, 0)), DESK_Y, NORMAL)).transposed().to_4x4()
desk_frame.translation = NORMAL * DESK_CONSTANT
desk_root.matrix_world = desk_frame
desk_inverse = desk_frame.inverted()


def cross(a, b):
    return a.x * b.y - a.y * b.x


def area(points):
    return .5 * sum(cross(p, points[(i + 1) % len(points)]) for i, p in enumerate(points))


def outline(points):
    result = [Vector(point) for point in points]
    if area(result) < 0:
        result.reverse()
    if area(result) < 1e-8:
        raise RuntimeError('An armor outline has no area.')
    return result


def rectangle(x0, x1, y0, y1, chamfer):
    return outline(((x0 + chamfer, y0), (x1 - chamfer, y0), (x1, y0 + chamfer),
                    (x1, y1 - chamfer), (x1 - chamfer, y1), (x0 + chamfer, y1),
                    (x0, y1 - chamfer), (x0, y0 + chamfer)))


def expanded(points, distance):
    result = []
    for i, point in enumerate(points):
        previous = (point - points[i - 1]).normalized()
        following = (points[(i + 1) % len(points)] - point).normalized()
        p = point + Vector((previous.y, -previous.x)) * distance
        q = point + Vector((following.y, -following.x)) * distance
        determinant = cross(previous, following)
        if abs(determinant) < 1e-8:
            raise RuntimeError('An armor offset has parallel adjacent edges.')
        result.append(p + previous * (cross(q - p, following) / determinant))
    return outline(result)


def circle(x, y, radius, sides=32):
    return [Vector((x + radius * math.cos(i * math.tau / sides),
                    y + radius * math.sin(i * math.tau / sides))) for i in range(sides)]


def recalculate(obj):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.update()


def prism(name, points, front, back, parent, material=0):
    if front <= back or area(points) <= 0:
        raise RuntimeError('An armor prism has invalid winding or depth.')
    count = len(points)
    vertices = [(p.x, p.y, z) for z in (front, back) for p in points]
    faces = [tuple(range(count)), tuple(reversed(range(count, 2 * count)))]
    faces += [(i, count + i, count + (i + 1) % count, (i + 1) % count) for i in range(count)]
    data = bpy.data.meshes.new('32 | ' + name)
    data.from_pydata(vertices, [], faces)
    for entry in palette:
        data.materials.append(entry)
    for face in data.polygons:
        face.material_index = material
        face.use_smooth = False
    obj = bpy.data.objects.new('32 | ' + name, data)
    collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_basis = Matrix.Identity(4)
    recalculate(obj)
    return obj


def apply_modifier(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    recalculate(obj)


def boolean(obj, cutter, name, operation='DIFFERENCE'):
    bpy.context.view_layer.update()
    modifier = obj.modifiers.new(name, 'BOOLEAN')
    modifier.operation = operation
    modifier.solver = 'EXACT'
    modifier.material_mode = 'INDEX'
    modifier.object = cutter
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    while obj.modifiers.find(modifier.name) > 0:
        bpy.ops.object.modifier_move_up(modifier=modifier.name)
    apply_modifier(obj, modifier)
    data = cutter.data
    bpy.data.objects.remove(cutter, do_unlink=True)
    bpy.data.meshes.remove(data)


def bevel(obj, width):
    modifier = obj.modifiers.new('Armor edge machining', 'BEVEL')
    modifier.width = width
    modifier.segments = 2
    modifier.material = 1
    modifier.limit_method = 'ANGLE'
    modifier.use_clamp_overlap = True
    apply_modifier(obj, modifier)


def point(x, y):
    tangent = math.tan(CAMERA.data.angle_x / 2)
    ray = CAMERA.matrix_world.to_3x3() @ Vector(((2 * x / 1672 - 1) * tangent,
          (1 - 2 * y / 941) * tangent / (1672 / 941), -1))
    origin = CAMERA.matrix_world.translation
    denominator = NORMAL.dot(ray)
    if abs(denominator) < 1e-8:
        raise RuntimeError('A base armor placement ray is parallel to the desk.')
    distance = (DESK_CONSTANT - NORMAL.dot(origin)) / denominator
    if distance <= 0:
        raise RuntimeError('A base armor placement lies behind the camera.')
    return Vector((desk_inverse @ (origin + ray * distance))[:2])


def distance_to_edge(point, a, b):
    edge = b - a
    t = max(0, min(1, (point - a).dot(edge) / edge.length_squared))
    return (point - (a + t * edge)).length


def contour_clearance(first, second):
    return min(distance_to_edge(p, q, second[(j + 1) % len(second)])
               for p in first for j, q in enumerate(second))


apertures = [rectangle(-1.65, 2.57, y - .81, y + .81, .09) for y in (.90, -.90)]
button_wells = [rectangle(-2.58, -1.96, y + step * .245 - .18,
                          y + step * .245 + .18, .055)
                for y in (.90, -.90) for step in (1, -1)]


def protected_casting_vertices():
    result = []
    for vertex in casting.data.vertices:
        p = vertex.co
        if p.z >= -.0001:
            continue
        xy = Vector(p[:2])
        if any(min(distance_to_edge(xy, a, poly[(i + 1) % len(poly)])
                   for i, a in enumerate(poly)) < .051 for poly in apertures + button_wells):
            result.append(p.copy())
    return result


casting_contours = protected_casting_vertices()
if not casting_contours:
    raise RuntimeError('The source casting has no protected aperture or button contour vertices.')
panels = [
    ('Broad brow cap', rectangle(-1.58, 2.18, 1.82, 2.18, .10), .12, .035, secondary_index),
    ('Left corner armor', outline(((-2.79, 1.73), (-2.79, 2.08), (-2.55, 2.23),
        (-1.78, 2.23), (-1.62, 2.08), (-1.62, 1.82), (-2.55, 1.82), (-2.66, 1.73))), .10, .025, 0),
    ('Right corner armor', outline(((2.22, 1.82), (2.22, 2.14), (2.45, 2.23),
        (2.79, 2.08), (2.79, 1.73), (2.66, 1.73), (2.55, 1.82))), .10, .025, 0),
    ('Left side guard', rectangle(-2.83, -2.67, -1.77, 1.70, .045), .07, .025, 2),
    ('Right side guard', rectangle(2.67, 2.83, -1.77, 1.70, .045), .07, .025, 2),
    ('Lower heel armor', rectangle(-1.58, 2.18, -2.09, -1.80, .08), .075, .025, secondary_index),
]
for index, x in enumerate((-2.45, 2.45), 1):
    boolean(casting, prism('Top well infill ' + str(index), circle(x, 1.87, .086),
                           0, -.08, terminal), 'Fill covered top screw well', 'UNION')
for screw in top_screws:
    screw.hide_render = True
    screw.hide_set(True)

armor_objects = {}
armor_records = []
for name, poly, front, edge_width, material in panels:
    pocket = expanded(poly, SIDE_CLEARANCE)
    boolean(casting, prism(name + ' seat cutter', pocket, .25, TERMINAL_FLOOR, terminal),
            name + ' fitted seat')
    obj = prism(name, poly, front, TERMINAL_BACK, terminal, material)
    bevel(obj, edge_width)
    armor_objects[name] = obj
    armor_records.append({'name': obj.name, 'outline': [list(p) for p in poly],
                          'front': front, 'back': TERMINAL_BACK, 'bevel': edge_width,
                          'seat_side_clearance': round(contour_clearance(poly, pocket), 6)})


def fastener(panel, parent, label, xy, face, head_front, head_radius, well_floor):
    x, y = xy
    boolean(panel, prism(label + ' well cutter', circle(x, y, .085), face + .05,
                         well_floor, parent, 3), label + ' recessed well')
    head = prism(label + ' captive screw', circle(x, y, head_radius, 10),
                 head_front, head_front - .025, parent, 3)
    modifier = head.modifiers.new('Captive head edge', 'BEVEL')
    modifier.width = .004
    modifier.segments = 2
    modifier.material = 8
    modifier.limit_method = 'ANGLE'
    apply_modifier(head, modifier)
    return {'name': head.name, 'center': [x, y], 'head_front': head_front,
            'face_recess': face - head_front, 'radial_clearance': .085 - head_radius,
            'floor_clearance': head_front - .025 - well_floor}


fasteners = []
for side, x in (('Left', -2.45), ('Right', 2.45)):
    fasteners.append(fastener(armor_objects[side + ' corner armor'], terminal,
                             side + ' upper armor', (x, 1.95), .10, .08, .058, .03))

base_pixels = [(552, 810), (685, 810), (721, 828), (947, 828), (982, 847),
               (1173, 847), (1173, 922), (1149, 947), (558, 947), (535, 923)]
base_outline = outline([point(x, y) for x, y in base_pixels])
base_pocket = expanded(base_outline, SIDE_CLEARANCE)
desk_bevels = [modifier for modifier in desk.modifiers if modifier.type == 'BEVEL']
if len(desk_bevels) != 1 or desk_bevels[0].limit_method != 'ANGLE':
    raise RuntimeError('The source desk needs its single authored angle bevel.')
desk_bevel = desk_bevels[0]
desk_bm = bmesh.new()
try:
    desk_bm.from_mesh(desk.data)
    desk_bm.normal_update()
    original_desk_edges = [(edge.verts[0].co.copy(), edge.verts[1].co.copy())
                          for edge in desk_bm.edges
                          if edge.is_manifold and edge.calc_face_angle() >= desk_bevel.angle_limit]
finally:
    desk_bm.free()
if not original_desk_edges:
    raise RuntimeError('The source desk has no qualifying bevel edges.')
boolean(desk, prism('Base armor seat cutter', base_pocket, .25, BASE_FLOOR, desk_root),
        'Fitted base armor seat')
desk_weights = desk.data.attributes.new('armor_original_desk_bevel', 'FLOAT', 'EDGE')
weighted_desk_edges = 0
for edge in desk.data.edges:
    points = [desk.data.vertices[index].co for index in edge.vertices]
    surviving = any(all(distance_to_edge(p, a, b) <= 1e-5 for p in points)
                    for a, b in original_desk_edges)
    desk_weights.data[edge.index].value = int(surviving)
    weighted_desk_edges += int(surviving)
if not weighted_desk_edges:
    raise RuntimeError('The fitted desk lost every original bevel edge.')
desk_bevel.limit_method = 'WEIGHT'
desk_bevel.edge_weight = desk_weights.name
base = prism('Launch surround armor', base_outline, BASE_FRONT, BASE_BACK, desk_root, secondary_index)
bevel(base, .025)
relative = launch.matrix_world.inverted() @ launch_key.matrix_world
key_xy = [Vector((relative @ vertex.co)[:2]) for vertex in launch_key.data.vertices]
key_hull = outline([key_xy[i] for i in convex_hull_2d(key_xy)])
launch_cut = expanded(key_hull, .12)
launch_outline = outline([Vector((desk_inverse @ (launch.matrix_world @ Vector((p.x, p.y, 0))))[:2])
                          for p in launch_cut])
boolean(base, prism('Launch armor aperture cutter', launch_outline, .30, -.20, desk_root, 3),
        'Existing Launch hull aperture')
gasket_pose = gasket.matrix_world.copy()
gasket_pose.translation += NORMAL * .065
gasket.matrix_world = gasket_pose
base_screw_pixels = [(588, 847), (588, 922), (1137, 871), (1137, 922)]
for index, pixel in enumerate(base_screw_pixels, 1):
    fasteners.append(fastener(base, desk_root, 'Base armor ' + str(index), point(*pixel),
                             BASE_FRONT, .095, .06, .045))
bpy.context.view_layer.update()


def check_mesh(obj, evaluated=False):
    data = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh() if evaluated else obj.data
    bm = bmesh.new()
    try:
        bm.from_mesh(data)
        finite = all(math.isfinite(value) for vertex in bm.verts for value in vertex.co)
        bad_edges = sum(not edge.is_manifold for edge in bm.edges)
        zero_faces = sum(face.calc_area() <= 1e-11 for face in bm.faces)
        zero_edges = sum(edge.calc_length() <= 1e-8 for edge in bm.edges)
        loose = sum(not vertex.link_edges for vertex in bm.verts)
        volume = bm.calc_volume(signed=True)
        if not finite or bad_edges or zero_faces or zero_edges or loose or volume <= 1e-10:
            raise RuntimeError(json.dumps({'bad_geometry': obj.name, 'finite': finite,
                'nonmanifold': bad_edges, 'zero_faces': zero_faces, 'zero_edges': zero_edges,
                'loose_vertices': loose, 'volume': volume, 'evaluated': evaluated}))
        return {'name': obj.name, 'vertices': len(bm.verts), 'faces': len(bm.faces),
                'volume': round(volume, 8), 'finite': True, 'closed': True, 'zero_faces': 0,
                'evaluated': evaluated}
    finally:
        bm.free()
        if evaluated:
            obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()


mesh_checks = [check_mesh(obj) for obj in collection.objects if obj.type == 'MESH']
mesh_checks += [check_mesh(casting), check_mesh(desk), check_mesh(desk, True)]
after_contours = protected_casting_vertices()
if any(not any((old - new).length < 1e-5 for new in after_contours) for old in casting_contours):
    raise RuntimeError('An existing screen or button pocket contour changed.')
geometry_exceptions = {casting.name, desk.name}
hidden_exceptions = {obj.name for obj in top_screws}
for name, before in protected.items():
    obj = OBJECTS[name]
    expected_matrix = before['matrix'].copy()
    if name == gasket.name:
        expected_matrix.translation += NORMAL * .065
    if max(abs(obj.matrix_world[i][j] - expected_matrix[i][j]) for i in range(4) for j in range(4)) > 1e-6:
        raise RuntimeError('Protected object moved: ' + name)
    if name not in geometry_exceptions and geometry_hash(obj) != before['geometry']:
        raise RuntimeError('Protected geometry changed: ' + name)
    expected_hidden = (True, True) if name in hidden_exceptions else before['hidden']
    if (obj.hide_render, obj.hide_get()) != expected_hidden:
        raise RuntimeError('Protected visibility changed: ' + name)
    if [(mod.name, mod.type) for mod in obj.modifiers] != before['modifiers']:
        raise RuntimeError('An original modifier stack changed: ' + name)

gasket_front = max(NORMAL.dot(gasket.matrix_world @ vertex.co) - DESK_CONSTANT
                   for vertex in gasket.data.vertices)
clearances = {
    'brow_to_screen_front': 1.82 - 1.76,
    'heel_to_screen_front': 1.80 - 1.76,
    'rail_to_screen_front': 2.67 - 2.62,
    'left_rail_to_button_well': -2.58 - (-2.67),
    'brow_to_corner_seam': .04,
    'brow_to_corner_seat_web': .04 - 2 * SIDE_CLEARANCE,
    'terminal_panel_floor': TERMINAL_BACK - TERMINAL_FLOOR,
    'base_panel_floor': BASE_BACK - BASE_FLOOR,
    'seat_side_clearance': contour_clearance(base_outline, base_pocket),
    'gasket_below_base_armor': BASE_FRONT - gasket_front,
}
if min(clearances.values()) < .0099:
    raise RuntimeError('An armor fit clearance failed: ' + json.dumps(clearances))
if any(record['radial_clearance'] < .025 - 1e-6 or record['floor_clearance'] < .025 - 1e-6
       or record['face_recess'] < .015 - 1e-6 for record in fasteners):
    raise RuntimeError('An armor fastener is not recessed with the specified clearance.')
print('ARMOR_DETAIL=' + json.dumps({
    'terminal_armor': armor_records,
    'base_armor': {'pixels': base_pixels, 'front': BASE_FRONT, 'back': BASE_BACK,
                   'seat_floor': BASE_FLOOR, 'launch_aperture_local_offset': .12,
                   'fastener_pixels': base_screw_pixels, 'gasket_front': round(gasket_front, 6)},
    'fasteners': fasteners,
    'clearances': {key: round(value, 6) for key, value in clearances.items()},
    'corner_to_guard_seam': .03,
    'corner_to_guard_seat_web': round(.03 - 2 * SIDE_CLEARANCE, 6),
    'desk_bevel': {'width': desk_bevel.width, 'original_segments': len(original_desk_edges),
                   'surviving_edges': weighted_desk_edges,
                   'excluded_edges': len(desk.data.edges) - weighted_desk_edges},
    'protected_objects_verified': len(protected),
    'protected_casting_contour_vertices': len(casting_contours),
    'allowed_changes': {'geometry': sorted(geometry_exceptions), 'translation': {gasket.name: list(NORMAL * .065)},
                        'hidden': sorted(hidden_exceptions)},
    'meshes': mesh_checks,
}, separators=(',', ':')), flush=True)
