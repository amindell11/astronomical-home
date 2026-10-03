import bpy,json
from pathlib import Path
root=Path('D:/amind/git/agent-7')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'art/ships/crimson/mesh-study/CrimsonMesh.fbx'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
count=0
for obj in objects:
 obj.data.calc_loop_triangles();count+=len(obj.data.loop_triangles)
assert len(objects)==22,(len(objects),'parts')
assert count==3252,(count,'triangles')
assert not any(o.data.materials for o in objects),'unexpected materials'
assert not any('REF' in o.name for o in objects),'reference leaked into export'
receipt={'parts':len(objects),'triangles':count,'materials':0,'reference_objects':0,'fbx_roundtrip':'passed'}
(root/'results/crimson-mesh/verification.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps(receipt))
