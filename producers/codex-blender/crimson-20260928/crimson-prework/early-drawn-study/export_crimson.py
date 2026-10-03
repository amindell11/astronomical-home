import bpy, json, hashlib
from pathlib import Path

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
ASSETS = ROOT/'src/Asteroids3D/Assets/Visuals/Ships/Crimson/DrawnStudy'
ASSETS.mkdir(parents=True,exist_ok=True)
source=OUT/'CrimsonStudy.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
parts=bpy.data.collections['02 Clean separate ship parts']
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'objects':[]}
bpy.ops.object.select_all(action='DESELECT')
depsgraph=bpy.context.evaluated_depsgraph_get()
for obj in list(parts.objects):
    evaluated=obj.evaluated_get(depsgraph)
    data=bpy.data.meshes.new_from_object(evaluated)
    data.calc_loop_triangles()
    report['objects'].append({'name':obj.name,'vertices':len(data.vertices),'triangles':len(data.loop_triangles),
                              'materials':[m.name for m in data.materials]})
    export=bpy.data.objects.new(obj.name+' export',data)
    bpy.context.scene.collection.objects.link(export)
    export.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ASSETS/'CrimsonStudy.fbx'),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
(OUT/'export-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'fbx':str(ASSETS/'CrimsonStudy.fbx'),'parts':len(report['objects']),
                  'triangles':sum(o['triangles'] for o in report['objects'])}))
