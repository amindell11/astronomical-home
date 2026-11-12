"""One-off source edit for console.blend (issue 1476). Run in the scene; saves it in place.
Three selector ticks at the knob's detents, the pointer centred on the middle one, lit chevrons,
and a straight-walled Launch boot nested inside the cap. Idempotence is not attempted: run once."""
import math
import bpy
from mathutils import Matrix, Quaternion, Vector

D = bpy.data.objects
DETENT = 40.0
RING_INNER, TICK_LENGTH = .62, .10

# --- Selector pointer: rest at the panel's up, so the three detents fan evenly above the knob.
frame = D['08 | Thick monitor armor frame']
grip = D['08 | Selector faceted grip']
pointer = D['08 | Selector pointer']
grip_world = grip.matrix_world.copy()
local = grip_world.inverted() @ pointer.matrix_world
centre = sum((local @ v.co for v in pointer.data.vertices), Vector()) / len(pointer.data.vertices)
turn = math.pi / 2 - math.atan2(centre.y, centre.x)
pointer.matrix_world = grip_world @ Matrix.Rotation(turn, 4, 'Z') @ local
bpy.context.view_layer.update()
print('POINTER_TURN_DEGREES=%.3f' % math.degrees(turn))

# --- Ticks: one per detent, on the ring the old ticks used.
axis = (grip_world.to_3x3() @ Vector((0, 0, 1))).normalized()
tip = sum((pointer.matrix_world @ v.co for v in pointer.data.vertices), Vector()) / len(pointer.data.vertices)
rest = tip - grip_world.translation
rest = (rest - axis * rest.dot(axis)).normalized()
to_frame = frame.matrix_world.inverted()
knob = to_frame @ grip_world.translation
face = max(v.co.z for v in frame.data.vertices)
ticks = []
for degrees in (-DETENT, 0.0, DETENT):
    direction = to_frame.to_3x3() @ (Quaternion(axis, math.radians(degrees)) @ rest)
    direction.z = 0
    direction.normalize()
    start = Vector((knob.x + RING_INNER * direction.x, knob.y + RING_INNER * direction.y, face))
    ticks.append((start, direction * TICK_LENGTH))

group = bpy.data.node_groups['24 | Painted casing and exact graphics on Thick monitor armor frame']
group_input = next(n for n in group.nodes if n.type == 'GROUP_INPUT')


def follower(socket, kind, operation=None):
    for link in socket.links:
        node = link.to_node
        if node.type == kind and (operation is None or node.operation == operation):
            return node
    raise RuntimeError('Stroke chain is not the generated shape: missing ' + kind)


chains = []
for node in group.nodes:
    if node.type != 'VECT_MATH' or node.operation != 'SUBTRACT' or node.inputs[1].is_linked:
        continue
    if not node.inputs[0].is_linked or node.inputs[0].links[0].from_node != group_input:
        continue
    start = Vector(node.inputs[1].default_value)
    if abs((start.xy - knob.xy).length - RING_INNER) > 1e-3 or abs(start.z - face) > 1e-3:
        continue
    dot = follower(node.outputs[0], 'VECT_MATH', 'DOT_PRODUCT')
    clamp = follower(dot.outputs['Value'], 'CLAMP')
    scale = follower(clamp.outputs[0], 'VECT_MATH', 'SCALE')
    if abs(Vector(scale.inputs[0].default_value).length - TICK_LENGTH) > 1e-3:
        continue
    near = follower(scale.outputs[0], 'VECT_MATH', 'SUBTRACT')
    length = follower(near.outputs[0], 'VECT_MATH', 'LENGTH')
    fall = follower(length.outputs['Value'], 'MAP_RANGE')
    merge = follower(fall.outputs[0], 'MATH', 'MAXIMUM')
    chains.append((math.atan2(start.y - knob.y, start.x - knob.x), [node, dot, clamp, scale, near, length, fall, merge]))
chains.sort(key=lambda item: item[0])
if len(chains) != 4:
    raise RuntimeError('Expected the four authored selector ticks, found %d' % len(chains))
for (start, vector), (_, nodes) in zip(ticks, chains):
    nodes[0].inputs[1].default_value = start
    nodes[1].inputs[1].default_value = vector / vector.length_squared
    nodes[3].inputs[0].default_value = vector
spare = chains[3][1]
merge = spare[-1]
kept = merge.inputs[0].links[0].from_socket
for link in list(merge.outputs[0].links):
    group.links.new(kept, link.to_socket)
for node in spare:
    group.nodes.remove(node)
print('TICKS=' + ' '.join('%.1f' % math.degrees(math.atan2(v.y, v.x)) for _, v in ticks))

# --- Chevrons: lit legends like Launch's.
backlight = bpy.data.materials['25 | Launch letter backlight']
for name in ('08 | Previous inlaid chevron', '08 | Next inlaid chevron'):
    D[name].material_slots[0].material = backlight

# --- Launch boot: the cap's own outline inset, straight down into the deck's thickness.
cap = D['08 | Launch broad beveled button']
boot = D['14 | Recessed Launch key boot']
INSET, TOP_INSET, FLOOR = .006, .026, -.31
cap_scale = cap.matrix_world.to_scale()
cap_axis = (cap.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()
boot_height = (boot.matrix_world.translation - cap.matrix_world.translation).dot(cap_axis)
widest = max(v.co.x for v in cap.data.vertices)
ring = [v.co for v in cap.data.vertices if abs(v.co.z - .047) < 1e-4]
if len(ring) != 8 or abs(max(v.x for v in ring) - widest) > 1e-6:
    raise RuntimeError('Launch cap no longer has its eight-sided side ring')
outline = sorted((Vector((v.x * cap_scale.x, v.y * cap_scale.y)) for v in ring), key=lambda p: math.atan2(p.y, p.x))


def inset(points, distance):
    lines = []
    for a, b in zip(points, points[1:] + points[:1]):
        edge = (b - a).normalized()
        normal = Vector((-edge.y, edge.x))
        lines.append((a + normal * distance, edge))
    result = []
    for (p, d), (q, e) in zip(lines[-1:] + lines[:-1], lines):
        t = ((q - p).x * e.y - (q - p).y * e.x) / (d.x * e.y - d.y * e.x)
        result.append(p + d * t)
    return result


boot_scale = boot.matrix_world.to_scale()
levels = [(inset(outline, INSET), FLOOR - boot_height), (inset(outline, INSET), .052),
          (inset(outline, INSET + TOP_INSET), .08)]
vertices = [(p.x / boot_scale.x, p.y / boot_scale.y, z / boot_scale.z) for points, z in levels for p in points]
faces = [tuple(reversed(range(8))), tuple(range(16, 24))]
for level in range(2):
    for i in range(8):
        a, b = level * 8 + i, level * 8 + (i + 1) % 8
        faces.append((a, b, b + 8, a + 8))
old = boot.data
used = {p.material_index for p in old.polygons}
if len(used) != 1:
    raise RuntimeError('Launch boot uses more than one material')
mesh = bpy.data.meshes.new('Straight Launch boot nested in the cap')
mesh.from_pydata(vertices, [], faces)
for material in old.materials:
    mesh.materials.append(material)
for polygon in mesh.polygons:
    polygon.material_index = used.pop() if used else polygon.material_index
index = mesh.polygons[0].material_index
for polygon in mesh.polygons:
    polygon.material_index = index
mesh.update()
boot.data = mesh
bpy.data.meshes.remove(old)
print('BOOT_FLOOR=%.3f CAP_BOTTOM_AT_FULL_PRESS=%.3f' % (FLOOR, -.5 * .35 * cap_scale.z - .35 * .35 * cap_scale.z))
bpy.ops.wm.save_mainfile()
print('FIX_DONE')
