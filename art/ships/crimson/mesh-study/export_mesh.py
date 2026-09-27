"""Blender --background --python export_mesh.py; exports only the editable ship collections to FBX."""
import bpy
import json
import hashlib
from pathlib import Path

HERE=Path(__file__).resolve().parent
source=HERE/'CrimsonMesh.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
objects=[obj for collection in bpy.data.collections if collection.name[:2] in ('01','02','03','04','05')
         for obj in collection.objects if obj.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'parts':[]}
depsgraph=bpy.context.evaluated_depsgraph_get()
for obj in objects:
    evaluated=obj.evaluated_get(depsgraph)
    data=bpy.data.meshes.new_from_object(evaluated)
    data.transform(obj.matrix_world)
    data.materials.clear()
    exported=bpy.data.objects.new(obj.name,data)
    bpy.context.scene.collection.objects.link(exported)
    exported.select_set(True)
    data.calc_loop_triangles()
    report['parts'].append({'name':obj.name,'triangles':len(data.loop_triangles)})
target=HERE/'CrimsonMesh.fbx'
bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
report['fbx_sha256']=hashlib.sha256(target.read_bytes()).hexdigest()
(HERE/'export-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'fbx':str(target),'parts':len(objects),'triangles':sum(p['triangles'] for p in report['parts'])}))
