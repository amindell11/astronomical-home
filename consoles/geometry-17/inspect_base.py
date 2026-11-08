import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
s=bpy.context.scene;c=bpy.data.objects['07 Camera - construction'];o=bpy.data.objects['07 | Continuous left console body'];ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
for i,v in enumerate(m.vertices):
 p=o.matrix_world@v.co
 if 3<p.x<4.6 and -4.1<p.y<-2.4:
  q=world_to_camera_view(s,c,p);print(i,[round(x,4)for x in p],[round(q.x*1672,1),round((1-q.y)*941,1)])
