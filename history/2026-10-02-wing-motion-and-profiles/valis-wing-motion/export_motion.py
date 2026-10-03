import json
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

output = Path(sys.argv[sys.argv.index('--') + 1])
parts = []
forward = json.loads((Path(__file__).parent / 'v01/forward-reference.json').read_text())['parts']
moving = json.loads((Path(__file__).parent / 'v01/manifest.json').read_text())['pose_models']
deps = bpy.context.evaluated_depsgraph_get()
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH':
        continue
    evaluated = obj.evaluated_get(deps)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers['ValisPaintUV'].data
    vertices, uv, posed = [], [], []
    delta = Matrix(forward[obj.name]['matrix_world']) @ obj.matrix_world.inverted()
    for triangle in mesh.loop_triangles:
        for vertex, loop in zip(triangle.vertices, triangle.loops):
            position = obj.matrix_world @ mesh.vertices[vertex].co
            vertices.append(dict(x=position.x, y=-position.y, z=-position.z))
            if obj.name in moving:
                side = 1 if position.x >= 0 else -1
                transformed = delta @ Vector((side * position.x, position.y, position.z))
                posed.append(dict(x=side * transformed.x, y=-transformed.y, z=-transformed.z))
            else:
                posed.append(vertices[-1])
            coord = uv_layer[loop].uv
            uv.append(dict(x=coord.x, y=coord.y))
    parts.append(dict(name=obj.name, vertices=vertices, uv=uv, forward=posed))
    evaluated.to_mesh_clear()
models = json.loads((Path(__file__).parent / 'v01/manifest.json').read_text())['pose_models']
for name, model in models.items():
    obj = bpy.data.objects[name]
    frame = obj.matrix_world @ obj.matrix_basis.inverted()
    model['frame_scale'] = dict(zip(('x', 'y', 'z'), frame.to_scale()))
    angle = __import__('math').radians(model['angle_degrees']) / 2
    sine = __import__('math').sin(angle)
    model['rotation'] = dict(zip(('x', 'y', 'z', 'w'),
                                [a * sine for a in model['axis']] + [__import__('math').cos(angle)]))
    model['pivot'] = dict(zip(('x', 'y', 'z'), model['effective_pivot']))
    model['translation'] = dict(zip(('x', 'y', 'z'), model['axis_translation']))
output.write_text(json.dumps(dict(parts=parts, models=models), separators=(',', ':')))
print('EXPORTED_WING_RIG', str(output), flush=True)
