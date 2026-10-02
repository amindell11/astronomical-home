import argparse, hashlib, json, sys
from pathlib import Path
import bpy

p = argparse.ArgumentParser(description='Export evaluated Valis surfaces and authored UVs for the Unity paint updater.')
p.add_argument('--source', required=True, type=Path)
p.add_argument('--output', required=True, type=Path)
a = p.parse_args(sys.argv[sys.argv.index('--') + 1:])
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
geometry = {o.name: dict(vertices=[list(o.matrix_world @ v.co) for v in o.data.vertices], faces=[list(f.vertices) for f in o.data.polygons], modifiers=[[m.name, m.type] for m in o.modifiers]) for o in objects}
fingerprint = hashlib.sha256(json.dumps(geometry, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
expected = json.loads((a.source.parent / 'mesh-approval.json').read_text())
assert fingerprint == 'c713fb3e410c5c6b66c74ad0e5d5b4885984cfe709b026866cdc6e6ae8d73981'
parts = []
deps = bpy.context.evaluated_depsgraph_get()
for obj in objects:
    evaluated = obj.evaluated_get(deps)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers['ValisPaintUV'].data
    vertices, uv = [], []
    for triangle in mesh.loop_triangles:
        for vertex, loop in zip(triangle.vertices, triangle.loops):
            position = obj.matrix_world @ mesh.vertices[vertex].co
            vertices.append(dict(x=position.x, y=-position.y, z=-position.z))
            coord = uv_layer[loop].uv
            uv.append(dict(x=coord.x, y=coord.y))
    parts.append(dict(name=obj.name, material=obj.data.materials[0].name, vertices=vertices, uv=uv))
    evaluated.to_mesh_clear()
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(dict(parts=parts, geometry_sha256=fingerprint), separators=(',', ':')))
print('EXPORTED_VALIS_PAINT ' + str(a.output.resolve()))
