import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-4')
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_paint,ship_source
scene=ship_source.open_source(root/'art/ships/vanguard/Vanguard.blend')
evaluation=ship_source.Evaluation(scene)
paint=json.loads((root/'art/ships/vanguard/paint.json').read_text())
part=ship_paint.Part(evaluation,'MVP Cockpit.001',paint)
print('BOUNDS',part.co.min(axis=0),part.co.max(axis=0))
for i,cap in enumerate(part.caps):
 print('CAP',i,'indices',cap['indices'],'bounds',cap['bounds'],'projection',cap.get('sweep_projection'))
for i in range(len(part.faces)):
 co=part.co[list(part.faces[i])]
 if co[:,1].max() > 1.1:
  print('FACE',i,'normal',part.normals[i].tolist(),'coords',co.tolist())
points=[obj.matrix_world@Vector(p) for obj in scene.objects if obj.type=='MESH' and not obj.hide_render for p in obj.bound_box]
center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
span=max(max(p[i] for p in points)-min(p[i] for p in points) for i in [0,1])*1.15
tree=BVHTree.FromPolygons([Vector(co) for co in part.co],part.faces)
for px,py in [(498,105),(515,110),(530,112),(495,116),(510,87)]:
 world=Vector((center.x+(px/1024-.5)*span,center.y+(.5-py/1024)*span,10))
 hit=tree.ray_cast(world,Vector((0,0,-1)))
 print('PIXEL',px,py,'HIT',hit)
