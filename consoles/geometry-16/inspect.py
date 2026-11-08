import bpy,json
from pathlib import Path
from bpy_extras.object_utils import world_to_camera_view
s=bpy.context.scene;c=bpy.data.objects['07 Camera - construction'];out={}
for o in s.objects:
 if o.type=='MESH' and not o.hide_render and (o.name.startswith(('14 |','15 |')) or o.name=='07 | Continuous left console body'):
  vs=[o.matrix_world@v.co for v in o.data.vertices]
  out[o.name]={'verts':[list(v) for v in vs],'pixels':[[round(p.x*1672,1),round((1-p.y)*941,1)] for v in vs for p in [world_to_camera_view(s,c,v)]],'faces':[list(p.vertices)for p in o.data.polygons]}
Path(__file__).with_name('inspection.json').write_text(json.dumps(out,indent=1))
