import bpy, json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-4/art/ships/vanguard/drawn-study/VanguardStudy.blend')
s=bpy.data.scenes['Vanguard - Texture MVP']
for name in ['MVP Fuselage','MVP Cockpit.001','MVP Power Nacelle Housing','MVP Power Nacelle Core']:
 o=s.objects[name]
 print(name)
 if 'Nacelle' in name:
  print([[round(x,4) for x in o.matrix_world@Vector(p)] for p in o.bound_box])
 else:
  for f in o.data.polygons:
   n=o.matrix_world.to_3x3()@f.normal
   if n.z>.4 and f.area>.012:
    print(json.dumps({'area':round(f.area,4),'points':[[round(x,4) for x in o.matrix_world@o.data.vertices[v].co] for v in f.vertices]}))
