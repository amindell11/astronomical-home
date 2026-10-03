import bpy
import json
import hashlib
from pathlib import Path

root=Path('D:/amind/git/agent-7')
folder=root/'art/ships/crimson/mesh-study/top-match'
bpy.ops.wm.open_mainfile(filepath=str(folder/'CrimsonTopMatch.blend'))
col=bpy.data.collections['90 References']
assert not col.hide_viewport
refs=list(col.objects)
assert len(refs)==2 and all(o.data.packed_file for o in refs)
for obj in refs:
    obj.hide_set(False)
bpy.context.view_layer.update()
assert all(o.visible_get() for o in refs)
assert len([o for o in bpy.context.scene.objects if o.type=='MESH'])==36
old=root/'art/ships/crimson/mesh-study/CrimsonMesh.blend'
old_hash=hashlib.sha256(old.read_bytes()).hexdigest()
assert old_hash=='f716a0ae26649c9fa50447bd4919fdf6cf962d806156513a01105f7d9b6e0314'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(folder/'CrimsonTopMatch.fbx'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(objects)==36
assert not any(o.data.materials for o in objects)
assert not any('REF' in o.name for o in objects)
triangles=0
for obj in objects:
    obj.data.calc_loop_triangles()
    triangles+=len(obj.data.loop_triangles)
assert triangles==6268
report={'fbx_parts':len(objects),'triangles':triangles,'materials':0,
    'references_packed_and_toggleable':True,'earlier_blend_unchanged':True}
(root/'results/crimson-top-match/verification.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
