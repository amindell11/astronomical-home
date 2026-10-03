import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Quaternion

OUT = Path(__file__).parent
def vector(v):
    return dict(x=v.x, y=-v.y, z=-v.z)
parts = []
geometry = []
deps = bpy.context.evaluated_depsgraph_get()
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or obj.hide_render or not obj.visible_get():
        continue
    ancestors = []
    parent = obj.parent
    while parent:
        ancestors.append(parent.name)
        parent = parent.parent
    group = 'Upper wings' if 'Upper wings' in ancestors else 'Lower wings' if 'Lower wings' in ancestors else None
    evaluated = obj.evaluated_get(deps)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=deps)
    mesh.calc_loop_triangles()
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    vertices, normals, uv = [], [], []
    for triangle in mesh.loop_triangles:
        for vertex, loop in zip(triangle.vertices, triangle.loops):
            vertices.append(vector(obj.matrix_world @ mesh.vertices[vertex].co))
            normals.append(vector((normal_matrix @ mesh.corner_normals[loop].vector).normalized()))
            coord = mesh.uv_layers['ValisPaintUV'].data[loop].uv
            uv.append(dict(x=coord.x, y=coord.y))
    parts.append(dict(name=obj.name, material=obj.data.materials[0].name, group=group, vertices=vertices, normals=normals, uv=uv))
    geometry.append(dict(name=obj.name, vertices=vertices))
    evaluated.to_mesh_clear()
assert len(parts) == 30
assert any(p['name'] == '13 charcoal trailing spars' and p['group'] == 'Upper wings' for p in parts)
assert not any(p['name'].startswith(('30 ', '31 ', '72 ', '73 ')) for p in parts)
models = {}
for name in ['Upper wings', 'Lower wings']:
    group = bpy.data.objects[name]
    axis = group.matrix_world.to_3x3().col[2].normalized()
    rotation = Quaternion(axis, math.radians(-20))
    models[name] = dict(pivot=vector(group.matrix_world.translation), rest_rotation=dict(x=rotation.x,y=-rotation.y,z=-rotation.z,w=rotation.w))
payload = dict(parts=parts, models=models)
OUT.joinpath('approved-export.json').write_text(json.dumps(payload, separators=(',', ':')))
approval = dict(status='approved-geometry-locked', geometry_sha256=hashlib.sha256(json.dumps(geometry, separators=(',', ':')).encode()).hexdigest(), mesh_parts=len(parts), scope='Approved reangled, tapered geometry with restored charcoal spars. Nested parent groups preserve separate meshes, live mirrors, paint UVs and profile keys.', fingerprint='SHA256 of ordered evaluated world-space triangle corners converted to Unity coordinates, grouped by object name.')
OUT.joinpath('mesh-approval.json').write_text(json.dumps(approval, indent=2)+'\n')
print(json.dumps(dict(parts=len(parts), triangle_corners=sum(len(p['vertices']) for p in parts), models=models, approval=approval['geometry_sha256'])))
