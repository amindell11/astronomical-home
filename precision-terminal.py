import json
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


SCENE = bpy.context.scene
OBJECTS = bpy.data.objects
CAMERA = SCENE.camera
COLLECTION_NAME = '30 | Fitted weapon terminal'
WIDTH, HEIGHT = 5.45, 4.10
FRONT, BACK = 0.0, -.48
OUTER_CHAMFER, OUTER_BEVEL = .20, .04
SCREEN_WIDTH, SCREEN_HEIGHT = 4.22, 1.62
SCREEN_X, SCREEN_Y = .46, (.90, -.90)
SCREEN_CHAMFER, APERTURE_BEVEL = .09, .05
GASKET_CLEARANCE, GASKET_WIDTH = .025, .06
GLASS_CLEARANCE = .025
BUTTON_X, BUTTON_PITCH = -2.27, .49
WELL_WIDTH, WELL_HEIGHT = .62, .36
KEY_WIDTH, KEY_HEIGHT = .54, .28
KEY_FRONT, KEY_BACK = .018, -.095
ARROW_WIDTH, ARROW_HEIGHT, ARROW_STROKE = .10, .18, .025
SCREW_X, SCREW_Y = 2.45, 1.87
SCREW_WELL_RADIUS, SCREW_HEAD_RADIUS = .085, .058

old_names = [
    '29 | Weapon terminal armor casting',
    '29 | Weapon terminal recessed gasket',
    '29 | Weapon terminal rear enclosure',
    '29 | Weapon terminal brow',
    '29 | Weapon terminal left shoulder',
    '29 | Weapon terminal right shoulder',
    '29 | Weapon terminal divider',
    '29 | Primary blank glass',
    '29 | Ordnance blank glass',
    '29 | Primary recessed screen rim',
    '29 | Ordnance recessed screen rim',
]
if bpy.context.mode != 'OBJECT':
    raise RuntimeError('Run precision-terminal.py in Object mode.')
if CAMERA is None or CAMERA.data.type != 'PERSP':
    raise RuntimeError('The authored perspective camera is required.')
if COLLECTION_NAME in bpy.data.collections:
    raise RuntimeError('Reload the v5 checkpoint before another terminal pass.')
missing = [name for name in old_names if name not in OBJECTS]
if missing:
    raise RuntimeError('The v5 terminal is incomplete: ' + ', '.join(missing))
old_names += [obj.name for obj in SCENE.objects
              if obj.name.startswith(('29 | Weapon key ', '29 | Weapon fastener '))]
palette = list(OBJECTS['08 | Thick monitor armor frame'].data.materials)
if len(palette) <= 10 or palette[10].name != '21 | Taupe keys':
    raise RuntimeError('The authored monitor palette no longer matches the terminal palette.')
legend_material = OBJECTS['08 | Previous inlaid chevron'].data.materials[0]
if not legend_material:
    raise RuntimeError('The authored emissive chevron material is missing.')

bpy.context.view_layer.update()
protected = {obj.name: (obj.matrix_world.copy(), obj.hide_render, obj.hide_get())
             for obj in SCENE.objects if obj.name not in old_names}
axis_x = Vector((1, 0, 0))
axis_y = Vector((0, .22, 1)).normalized()
axis_z = Vector((0, -1, .22)).normalized()
camera_position = CAMERA.matrix_world.translation.copy()
tangent = math.tan(CAMERA.data.angle_x / 2)
ray = CAMERA.matrix_world.to_3x3() @ Vector((
    (2 * 1392 / 1672 - 1) * tangent,
    (1 - 2 * 619 / 941) * tangent / (1672 / 941), -1))
plane_constant = axis_z.dot(Vector((0, -.6, 3)))
denominator = axis_z.dot(ray)
if abs(denominator) < 1e-8:
    raise RuntimeError('The placement ray is parallel to the authored terminal plane.')
distance = (plane_constant - axis_z.dot(camera_position)) / denominator
if distance <= 0:
    raise RuntimeError('The terminal placement lies behind the camera.')
origin = camera_position + ray * distance
frame = Matrix((axis_x, axis_y, axis_z)).transposed().to_4x4()
frame.translation = origin
if abs(frame.to_3x3().determinant() - 1) > 1e-6:
    raise RuntimeError('The terminal basis must be orthonormal and right-handed.')

collection = bpy.data.collections.new(COLLECTION_NAME)
SCENE.collection.children.link(collection)
root = bpy.data.objects.new('30 | Weapon terminal frame', None)
collection.objects.link(root)
root.matrix_world = frame
for name in old_names:
    OBJECTS[name].hide_render = True
    OBJECTS[name].hide_set(True)


def cross(a, b):
    return a.x * b.y - a.y * b.x


def signed_area(points):
    return sum(cross(a, points[(i + 1) % len(points)]) for i, a in enumerate(points)) * .5


def rectangle(width, height, chamfer, x=0, y=0):
    if not 0 < chamfer < min(width, height) * .5:
        raise RuntimeError('The rectangle chamfer exceeds its dimensions.')
    a, b = width * .5, height * .5
    return [Vector((x + px, y + py)) for px, py in (
        (-a + chamfer, -b), (a - chamfer, -b), (a, -b + chamfer),
        (a, b - chamfer), (a - chamfer, b), (-a + chamfer, b),
        (-a, b - chamfer), (-a, -b + chamfer))]


def inset(points, clearance):
    if signed_area(points) <= 0:
        raise RuntimeError('Inset requires a counterclockwise outline.')
    result = []
    for index, point in enumerate(points):
        previous = (point - points[index - 1]).normalized()
        following = (points[(index + 1) % len(points)] - point).normalized()
        p = point + Vector((-previous.y, previous.x)) * clearance
        q = point + Vector((-following.y, following.x)) * clearance
        determinant = cross(previous, following)
        if abs(determinant) < 1e-8:
            raise RuntimeError('An inset corner has parallel edges.')
        result.append(p + previous * (cross(q - p, following) / determinant))
    if signed_area(result) <= 0:
        raise RuntimeError('Inset consumed the outline.')
    return result


def mesh(name, vertices, faces, material=0, materials=None):
    data = bpy.data.meshes.new('30 | ' + name)
    data.from_pydata(vertices, [], faces)
    data.update()
    for entry in palette if materials is None else materials:
        data.materials.append(entry)
    for face in data.polygons:
        face.material_index = material
        face.use_smooth = False
    obj = bpy.data.objects.new('30 | ' + name, data)
    collection.objects.link(obj)
    obj.parent = root
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_basis = Matrix.Identity(4)
    return obj


def prism(name, outline, front, back, material=0, rear_outline=None, materials=None):
    if front <= back or signed_area(outline) <= 0:
        raise RuntimeError('A prism needs positive depth and a counterclockwise outline.')
    rear = outline if rear_outline is None else rear_outline
    if len(rear) != len(outline):
        raise RuntimeError('The tapered chassis outlines must have matching corners.')
    count = len(outline)
    vertices = [(p.x, p.y, front) for p in outline] + [(p.x, p.y, back) for p in rear]
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    faces += [(i, count + i, count + (i + 1) % count, (i + 1) % count)
              for i in range(count)]
    return mesh(name, vertices, faces, material, materials)


def ring(name, outer, inner, front, back):
    count = len(outer)
    vertices = [(p.x, p.y, z) for contour, z in
                ((outer, front), (inner, front), (outer, back), (inner, back)) for p in contour]
    faces = []
    for i in range(count):
        j = (i + 1) % count
        faces += [(i, j, count + j, count + i),
                  (i, 2 * count + i, 2 * count + j, j),
                  (count + i, count + j, 3 * count + j, 3 * count + i),
                  (2 * count + i, 3 * count + i, 3 * count + j, 2 * count + j)]
    return mesh(name, vertices, faces, 4)


def recalculate(obj):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.update()


def apply_modifier(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    recalculate(obj)


def difference(obj, cutter, name):
    bpy.context.view_layer.update()
    modifier = obj.modifiers.new(name, 'BOOLEAN')
    modifier.operation = 'DIFFERENCE'
    modifier.solver = 'EXACT'
    modifier.material_mode = 'INDEX'
    modifier.object = cutter
    apply_modifier(obj, modifier)
    data = cutter.data
    bpy.data.objects.remove(cutter, do_unlink=True)
    bpy.data.meshes.remove(data)


def on_segment(point, a, b):
    edge = b - a
    along = (point - a).dot(edge) / edge.length_squared
    return (-1e-6 <= along <= 1 + 1e-6 and
            abs(cross(edge, point - a)) / edge.length < 1e-6)


def bevel(obj, width, material, exterior=None, front_apertures=None):
    modifier = obj.modifiers.new('Fitted edge bevel', 'BEVEL')
    modifier.width = width
    modifier.segments = 2
    modifier.material = material
    modifier.affect = 'EDGES'
    modifier.use_clamp_overlap = True
    if exterior is None and front_apertures is None:
        modifier.limit_method = 'ANGLE'
    else:
        outlines = [exterior] if exterior is not None else front_apertures
        attribute_name = 'terminal_outer_bevel' if exterior is not None else 'terminal_aperture_bevel'
        weights = obj.data.attributes.new(attribute_name, 'FLOAT', 'EDGE')
        selected = [0] * len(outlines)
        for edge in obj.data.edges:
            vertices = [obj.data.vertices[index].co for index in edge.vertices]
            if front_apertures is not None and any(abs(vertex.z - FRONT) > 1e-6 for vertex in vertices):
                weights.data[edge.index].value = 0
                continue
            a, b = [Vector(vertex[:2]) for vertex in vertices]
            matches = [any(on_segment(a, point, outline[(i + 1) % len(outline)]) and
                           on_segment(b, point, outline[(i + 1) % len(outline)])
                           for i, point in enumerate(outline)) for outline in outlines]
            weights.data[edge.index].value = int(any(matches))
            selected = [count + int(matches[i]) for i, count in enumerate(selected)]
        if any(count == 0 for count in selected):
            raise RuntimeError('A casting outline has no selected bevel edges: ' + attribute_name)
        modifier.limit_method = 'WEIGHT'
        modifier.edge_weight = weights.name
    apply_modifier(obj, modifier)


def circle(x, y, radius, sides):
    return [Vector((x + radius * math.cos(2 * math.pi * i / sides),
                    y + radius * math.sin(2 * math.pi * i / sides))) for i in range(sides)]


outer = rectangle(WIDTH, HEIGHT, OUTER_CHAMFER)
casting = prism('Weapon terminal casting', outer, FRONT, BACK)
screen_records = []
apertures = []
key_records = []
button_centers = []
for label, center_y in zip(('Primary', 'Ordnance'), SCREEN_Y):
    opening = rectangle(SCREEN_WIDTH, SCREEN_HEIGHT, SCREEN_CHAMFER, SCREEN_X, center_y)
    apertures.append(opening)
    difference(casting, prism(label + ' aperture cutter', opening, .10, -.58, 3), label + ' through aperture')
    gasket_outer = inset(opening, GASKET_CLEARANCE)
    gasket_inner = inset(gasket_outer, GASKET_WIDTH)
    ring(label + ' fitted gasket', gasket_outer, gasket_inner, -.035, -.21)
    glass_outline = inset(gasket_inner, GLASS_CLEARANCE)
    glass = prism(label + ' blank glass', glass_outline, -.16, -.20, 4)
    screen_records.append({'name': label, 'center': [SCREEN_X, center_y],
                           'opening': [SCREEN_WIDTH, SCREEN_HEIGHT], 'glass': glass})
    for step, direction in ((1, 'Previous'), (-1, 'Next')):
        y = center_y + step * BUTTON_PITCH * .5
        button_centers.append((BUTTON_X, y))
        well = rectangle(WELL_WIDTH, WELL_HEIGHT, .055, BUTTON_X, y)
        difference(casting, prism(label + ' ' + direction + ' pocket cutter', well, .10, -.12, 3),
                   label + ' ' + direction + ' button pocket')
        key = prism(label + ' ' + direction + ' key',
                    rectangle(KEY_WIDTH, KEY_HEIGHT, .045, BUTTON_X, y), KEY_FRONT, KEY_BACK, 10)
        bevel(key, .016, 1)
        arrow_half_width, arrow_half_height = ARROW_WIDTH * .5, ARROW_HEIGHT * .5
        arrow_slope = arrow_half_height / ARROW_WIDTH
        arrow_vertical_stroke = ARROW_STROKE * math.sqrt(1 + arrow_slope * arrow_slope)
        arrow_inner_y = arrow_half_height - arrow_vertical_stroke
        arrow_inner_tip = -arrow_half_width + arrow_vertical_stroke / arrow_slope
        arrow = [Vector((x, yy)) for x, yy in (
            (arrow_half_width, -arrow_half_height), (-arrow_half_width, 0),
            (arrow_half_width, arrow_half_height), (arrow_half_width, arrow_inner_y),
            (arrow_inner_tip, 0), (arrow_half_width, -arrow_inner_y))]
        if signed_area(arrow) < 0:
            arrow.reverse()
        if direction == 'Next':
            arrow = [Vector((-p.x, p.y)) for p in reversed(arrow)]
        recess = [p + Vector((BUTTON_X, y)) for p in arrow]
        difference(key, prism(label + ' ' + direction + ' legend cutter', recess, .08, .006, 3),
                   'Recessed chevron')
        inlay = [p + Vector((BUTTON_X, y)) for p in inset(arrow, .004)]
        prism(label + ' ' + direction + ' ivory inlay', inlay, KEY_FRONT - .003, .008,
              materials=[legend_material])
        key_records.append({'name': label + ' ' + direction, 'center': [BUTTON_X, y], 'object': key})

screw_centers = [(x, y) for y in (SCREW_Y, -SCREW_Y) for x in (-SCREW_X, SCREW_X)]
for index, (x, y) in enumerate(screw_centers, 1):
    difference(casting, prism('Screw well cutter ' + str(index), circle(x, y, SCREW_WELL_RADIUS, 32),
                              .1, -.07, 3), 'Recessed screw well ' + str(index))
    head = prism('Captive screw ' + str(index), circle(x, y, SCREW_HEAD_RADIUS, 10), -.02, -.045, 3)
    bevel(head, .004, 8)
bevel(casting, APERTURE_BEVEL, 1, front_apertures=apertures)
bevel(casting, OUTER_BEVEL, 1, exterior=outer)
rear = prism('Tapered rear chassis', rectangle(5.37, 4.02, .18), -.505, -.78, 2,
             rear_outline=rectangle(5.15, 3.80, .16))
bevel(rear, .025, 1)
bpy.context.view_layer.update()


def bounds(obj):
    points = [vertex.co for vertex in obj.data.vertices]
    low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    return low, high


def check_mesh(obj):
    recalculate(obj)
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        finite = all(math.isfinite(value) for vertex in bm.verts for value in vertex.co)
        nonmanifold = sum(not edge.is_manifold for edge in bm.edges)
        zero_faces = sum(face.calc_area() <= 1e-11 for face in bm.faces)
        zero_edges = sum(edge.calc_length() <= 1e-8 for edge in bm.edges)
        loose_vertices = sum(not vertex.link_edges for vertex in bm.verts)
        volume = bm.calc_volume(signed=True)
        if not finite or nonmanifold or zero_faces or zero_edges or loose_vertices or volume <= 1e-10:
            raise RuntimeError(json.dumps({'bad_geometry': obj.name, 'finite': finite,
                'nonmanifold_edges': nonmanifold, 'zero_area_faces': zero_faces,
                'zero_length_edges': zero_edges, 'loose_vertices': loose_vertices, 'volume': volume}))
        low, high = bounds(obj)
        return {'name': obj.name, 'vertices': len(bm.verts), 'faces': len(bm.faces),
                'dimensions': [round(value, 6) for value in high - low],
                'volume': round(volume, 8), 'nonmanifold_edges': 0, 'zero_area_faces': 0, 'finite': True}
    finally:
        bm.free()


mesh_checks = [check_mesh(obj) for obj in collection.objects if obj.type == 'MESH']
glass_sizes = []
for record in screen_records:
    low, high = bounds(record.pop('glass'))
    record['glass_dimensions'] = [round(value, 6) for value in high - low]
    glass_sizes.append(high - low)
if (glass_sizes[0] - glass_sizes[1]).length > 2e-6:
    raise RuntimeError('The two generated screen glasses do not have matching dimensions.')
for record in key_records:
    low, high = bounds(record.pop('object'))
    measured_center = (low + high) * .5
    if abs(measured_center.x - BUTTON_X) > 2e-6:
        raise RuntimeError('A key left the common button X plane.')
    if abs(high.x - low.x - KEY_WIDTH) > 2e-6 or abs(high.y - low.y - KEY_HEIGHT) > 2e-6:
        raise RuntimeError('A key changed dimensions while carving its chevron.')
    record['dimensions'] = [round(value, 6) for value in high - low]
if any(abs(button_centers[i][1] - button_centers[i + 1][1] - BUTTON_PITCH) > 1e-6 for i in (0, 2)):
    raise RuntimeError('The two button pairs have unequal pitch.')
clearances = {
    'gasket_to_aperture': GASKET_CLEARANCE,
    'glass_to_gasket': GLASS_CLEARANCE,
    'key_horizontal': (WELL_WIDTH - KEY_WIDTH) * .5,
    'key_vertical': (WELL_HEIGHT - KEY_HEIGHT) * .5,
    'key_floor': KEY_BACK - (-.12),
    'screw_radial': SCREW_WELL_RADIUS - SCREW_HEAD_RADIUS,
    'screw_floor': -.045 - (-.07),
    'screw_to_screen_web': SCREW_Y - SCREW_WELL_RADIUS - (SCREEN_Y[0] + SCREEN_HEIGHT * .5) - APERTURE_BEVEL,
    'screw_to_outer_bevel': HEIGHT * .5 - OUTER_BEVEL - SCREW_Y - SCREW_WELL_RADIUS,
    'center_web_after_bevel': SCREEN_Y[0] - SCREEN_Y[1] - SCREEN_HEIGHT - 2 * APERTURE_BEVEL,
    'aperture_to_outer_bevel': WIDTH * .5 - OUTER_BEVEL - SCREEN_X - SCREEN_WIDTH * .5 - APERTURE_BEVEL,
    'aperture_to_button_pocket': SCREEN_X - SCREEN_WIDTH * .5 - APERTURE_BEVEL - (BUTTON_X + WELL_WIDTH * .5),
    'rear_seam': BACK - (-.505),
}
if min(clearances.values()) < .025 - 1e-6:
    raise RuntimeError('A terminal fit clearance is below .025: ' + json.dumps(clearances))
for name, (matrix, render_hidden, viewport_hidden) in protected.items():
    obj = OBJECTS[name]
    if (max(abs(obj.matrix_world[i][j] - matrix[i][j]) for i in range(4) for j in range(4)) > 1e-7
            or obj.hide_render != render_hidden or obj.hide_get() != viewport_hidden):
        raise RuntimeError('Protected scene object changed: ' + name)

print('FITTED_TERMINAL=' + json.dumps({
    'origin': [round(value, 6) for value in origin],
    'basis': {'x': list(axis_x), 'y': list(axis_y), 'z': list(axis_z)},
    'casting': {'dimensions': [WIDTH, HEIGHT, FRONT - BACK], 'outer_chamfer': OUTER_CHAMFER,
                'outer_bevel': OUTER_BEVEL, 'front_aperture_bevel': APERTURE_BEVEL},
    'screens': screen_records,
    'buttons': key_records,
    'button_common_x': BUTTON_X,
    'button_pair_pitch': BUTTON_PITCH,
    'screws': {'centers': screw_centers, 'well_radius': SCREW_WELL_RADIUS,
               'head_radius': SCREW_HEAD_RADIUS, 'head_front': -.02, 'head_back': -.045},
    'rear_chassis': {'front': -.505, 'back': -.78, 'front_size': [5.37, 4.02], 'rear_size': [5.15, 3.8]},
    'clearances': {key: round(value, 6) for key, value in clearances.items()},
    'legend_dimensions': [ARROW_WIDTH, ARROW_HEIGHT], 'legend_stroke': ARROW_STROKE,
    'legend_recess_depth': .012, 'legend_face_inset': .003,
    'hidden_legacy_objects': len(old_names),
    'protected_objects_unchanged': len(protected),
    'meshes': mesh_checks,
}, separators=(',', ':')))
