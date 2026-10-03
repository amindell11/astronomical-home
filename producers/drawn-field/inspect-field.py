import bpy,json
from pathlib import Path
p=Path('D:/amind/git/agent-4/src/Asteroids3D/Assets/Visuals/Environment/Asteroids/HD_Asteroids/Models')
for i in range(1,11):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(p/f'Asteroid{i}.fbx'))
 print('SHAPE',i,json.dumps([{'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'uv':len(o.data.uv_layers),'dims':list(o.dimensions),'scale':list(o.scale),'rot':list(o.rotation_euler)} for o in bpy.context.scene.objects if o.type=='MESH']))
