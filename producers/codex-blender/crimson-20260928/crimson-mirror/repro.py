import bpy,json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-7/art/ships/crimson/mesh-study/top-match/CrimsonTopMatch.blend')
o=bpy.data.objects['Upper inner shoulder armor']
o.location.x += .1
bpy.context.view_layer.update()
e=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
v=[(e.matrix_world @ p.co).x for p in e.data.vertices]
error=abs(min(v)+max(v))
print(json.dumps({'move_x':.1,'center_symmetry_error':error,'passes':error<1e-6}))
