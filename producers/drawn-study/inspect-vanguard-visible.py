import bpy,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-4/art/ships/vanguard/drawn-study/VanguardStudy.blend')
s=bpy.data.scenes['Vanguard - Texture MVP']; trees={o.name:BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[list(p.vertices) for p in o.data.polygons]) for o in s.objects if o.type=='MESH'}
for y in [.3,.2,.1,0,-.1,-.18]:
 for x in [0,.04,.08,.12]:
  hits=[]
  for name,t in trees.items():
   hit,normal,idx,dist=t.ray_cast(Vector((x,y,2)),Vector((0,0,-1)))
   if hit is not None: hits.append((round(hit.z,4),name,idx))
  print('SURFACE',x,y,sorted(hits,reverse=True)[:2])
