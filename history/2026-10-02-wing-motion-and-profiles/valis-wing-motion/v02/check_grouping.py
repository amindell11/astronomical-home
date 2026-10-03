import bpy
import json
import numpy as np
from pathlib import Path

out = Path(__file__).parent
def points(name):
    bpy.context.view_layer.update()
    obj = bpy.data.objects[name]
    deps = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(deps), preserve_all_data_layers=True, depsgraph=deps)
    p = np.array([list(obj.matrix_world @ v.co) for v in mesh.vertices])
    result = {'bounds':[[float(min(p[:,i])),float(max(p[:,i]))] for i in range(3)],'matrix':[list(row) for row in obj.matrix_world], 'keyvalues':[(k.name,k.value) for k in obj.data.shape_keys.key_blocks]}
    return p, result
bpy.ops.wm.open_mainfile(filepath=str(out / 'Valis-before-grouping.blend'))
before, a = points('50 aft split prongs')
bpy.ops.wm.open_mainfile(filepath=str(out / 'Valis-profile-assemblies.blend'))
after, b = points('Aft prongs')
nearest = np.sqrt(((before[:,None,:]-after[None,:,:])**2).sum(axis=2)).min(axis=1)
report={'before':a,'after':b,'max_error':float(nearest.max())}
out.joinpath('grouping-diagnosis.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
