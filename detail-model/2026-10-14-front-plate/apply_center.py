import bpy
import json
import sys
from pathlib import Path
from mathutils import Matrix

stage=Path(__file__).parent
sys.path.insert(0,str(stage))
import snapshot
before=snapshot.capture()
expected=json.loads((stage/'applied.json').read_text())['after']
assert before['fingerprint']==expected['fingerprint'], 'Owner geometry changed after the last save'
assert all(before['images'][k]['pixels']==expected['images'][k]['pixels'] for k in expected['images']), 'Owner paint changed after the last save'
if bpy.context.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
target=bpy.data.objects['15 Front armor']
payload=json.loads((stage/'plate-correction.json').read_text())
mesh=bpy.data.meshes.new('Bulwark front armor joined center')
mesh.from_pydata(payload['vertices'],[],payload['faces']);mesh.update()
for material in target.data.materials:mesh.materials.append(material)
for polygon,index in zip(mesh.polygons,payload['materials']):polygon.material_index=index
for name,values in payload['uv'].items():
    uv=mesh.uv_layers.new(name=name)
    for data,value in zip(uv.data,values):data.uv=value
target.data=mesh
target.matrix_parent_inverse=Matrix(payload['matrix_parent_inverse'])
target.matrix_basis=Matrix(payload['matrix_basis'])
target.modifiers.clear()
for spec in payload['modifiers']:
    modifier=target.modifiers.new(spec['name'],spec['type'])
    for prop,value in spec['properties'].items():
        if value is None:continue
        if isinstance(value,dict):value=bpy.data.objects[value['object']]
        setattr(modifier,prop,value)
bpy.context.view_layer.update()
after=snapshot.capture()
for name,fp in before['fingerprint']['parts'].items():
    if name!='15 Front armor':assert after['fingerprint']['parts'][name]==fp, name+' changed unexpectedly'
assert all(before['images'][k]['pixels']==after['images'][k]['pixels'] for k in before['images'])
original=json.loads((stage/'before.json').read_text())
for name,fp in original['fingerprint']['parts'].items():
    if name!='09 Side armor':assert after['fingerprint']['parts'][name]==fp, name+' changed from owner checkpoint'
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
report={'before':original,'after':after,'topology':payload['topology'],
        'changed_parts':['09 Side armor -> 15 Front armor'],
        'scope':'Retain original overhang, profile, placement and chamfer; trim source half at symmetry plane and weld mirrored center.'}
(stage/'applied.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
