"""Run in the pass-28 Blender scene. --output names a new source file; input is never overwritten."""
import argparse
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


args = argparse.ArgumentParser()
args.add_argument('--output', required=True)
args.add_argument('--preview')
options = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
output = Path(options.output).resolve()
if output == Path(bpy.data.filepath).resolve():
    raise RuntimeError('Repair must write a separate source file.')

scene = bpy.context.scene
assembly = bpy.data.objects['28 | Tube entry assembly']
frame = assembly.matrix_world.copy()
glands = [bpy.data.objects[f'15 | Seated tube gland {i}'] for i in range(2)]
centers = [g.matrix_world.translation.copy() for g in glands]
middle = (centers[0] + centers[1]) * .5
direction = (centers[1] - centers[0]).normalized()
spacing = .43

for i, gland in enumerate(glands):
    target = middle + direction * (i - .5) * spacing
    offset = target - centers[i]
    gland.matrix_world.translation = target
    seal = bpy.data.objects[f'15 | Fascia tube seal {i}']
    seal.hide_render = True
    seal.hide_set(True)
    collar = bpy.data.objects[f'28 | Cable gland collar {i}']
    collar.matrix_world.translation += offset
    for vertex in collar.data.vertices:
        vertex.co.x *= .18 / .205
        vertex.co.y *= .18 / .205
    hose = bpy.data.objects[f'14 | Front entry hose {i}']
    inverse = hose.matrix_world.inverted().to_3x3()
    local_offset = inverse @ offset
    points = hose.data.splines[0].bezier_points
    for index, weight in ((0, 1), (1, 1), (2, .35)):
        points[index].co += local_offset * weight
        points[index].handle_left += local_offset * weight
        points[index].handle_right += local_offset * weight

plate = bpy.data.objects['28 | Recessed twin feedthrough plate']
old = plate.data
corners = [(-.29, -.33), (.29, -.33), (.39, -.23), (.39, .23),
           (.29, .33), (-.29, .33), (-.39, .23), (-.39, -.23)]
vertices = [(x, y, z) for z in (-.02, .025) for x, y in corners]
faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
faces += [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)]
mesh = bpy.data.meshes.new('Twin discrete feedthrough bores')
mesh.from_pydata(vertices, [], faces)
mesh.materials.append(bpy.data.materials['21 | Graphite chassis'])
plate.data = mesh
for i, gland in enumerate(glands):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=.137, depth=.5)
    cutter = bpy.context.object
    cutter.matrix_world = gland.matrix_world.copy()
    bpy.context.view_layer.objects.active = plate
    modifier = plate.modifiers.new(f'Discrete feedthrough bore {i}', 'BOOLEAN')
    modifier.operation = 'DIFFERENCE'
    modifier.solver = 'EXACT'
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)

bpy.context.view_layer.update()
actual = (glands[1].matrix_world.translation - glands[0].matrix_world.translation).length
assert actual > .36 + .05, actual
bm = bmesh.new()
bm.from_mesh(plate.data)
assert all(e.is_manifold for e in bm.edges)
bm.free()
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output))
if options.preview:
    scene.cycles.samples = 24
    scene.render.filepath = options.preview
    bpy.ops.render.render(write_still=True)
print(f'SOCKET_SPACING={actual:.6f} COLLAR_DIAMETER=0.36 WEB_MINIMUM={actual - .36:.6f}')
