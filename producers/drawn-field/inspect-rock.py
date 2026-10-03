import bpy
import json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='D:/amind/git/agent-4/src/Asteroids3D/Assets/Visuals/Environment/Asteroids/HD_Asteroids/Models/Asteroid1.fbx')
print(json.dumps([{'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'size':list(o.dimensions),'scale':list(o.scale),'rotation':list(o.rotation_euler)} for o in bpy.data.objects if o.type=='MESH']))
