import bpy,numpy as np,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
s=Path(__file__).parent;bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.visible_get()]
vv=[];ff=[];g=bpy.context.evaluated_depsgraph_get()
for o in objects:
 e=o.evaluated_get(g);m=e.to_mesh();m.calc_loop_triangles();start=len(vv);vv.extend([e.matrix_world@v.co for v in m.vertices]);ff.extend([tuple(start+i for i in t.vertices) for t in m.loop_triangles]);e.to_mesh_clear()
bvh=BVHTree.FromPolygons(vv,ff,all_triangles=True)
N=2048;shadow=np.zeros((N,N),np.float32);light=np.zeros_like(shadow);covered=np.zeros_like(shadow,dtype=bool)
def occlusion(p,n):
 tangent=n.cross(Vector((0,0,1)) if abs(n.z)<.9 else Vector((1,0,0))).normalized();bit=n.cross(tangent);hits=0
 for k in range(24):
  z=math.sqrt((k+.5)/24);r=math.sqrt(1-z*z);a=k*2.399963229728653;d=tangent*(r*math.cos(a))+bit*(r*math.sin(a))+n*z
  h=bvh.ray_cast(p+n*.012,d,.42)
  if h[0] is not None:hits+=1-h[3]/.42
 return hits/24
for o in objects:
 if o.name=='Cockpit - Canopy':continue
 m=o.data;m.calc_loop_triangles();uv=m.uv_layers['PaintUV'];M=o.matrix_world;normalM=M.to_3x3().inverted().transposed()
 for t in m.loop_triangles:
  points=[M@m.vertices[i].co for i in t.vertices];center=sum(points,Vector())/3;n=(normalM@t.normal).normalized();strength=np.array([.035+.12*(1-max(0,n.z))+.28*occlusion(p*.93+center*.07,n) for p in points]);highlight=.025*max(0,n.z)
  coords=np.array([uv.data[i].uv[:] for i in t.loops]);coords[:,0]*=N;coords[:,1]=(1-coords[:,1])*N
  A=np.vstack([coords.T,np.ones(3)])
  if abs(np.linalg.det(A))<1e-9:continue
  lo=np.maximum(0,np.floor(coords.min(0)).astype(int));hi=np.minimum(N,np.ceil(coords.max(0)).astype(int))
  if np.any(hi<=lo):continue
  Y,X=np.mgrid[lo[1]:hi[1],lo[0]:hi[0]];w=np.stack([X+.5,Y+.5,np.ones_like(X)],-1)@np.linalg.inv(A).T;inside=w.min(-1)>=-1e-7
  sh=shadow[lo[1]:hi[1],lo[0]:hi[0]];li=light[lo[1]:hi[1],lo[0]:hi[0]];co=covered[lo[1]:hi[1],lo[0]:hi[0]];sh[inside]=(w@strength)[inside];li[inside]=highlight;co[inside]=True
np.savez(s/'finish-fields.npz',shadow=shadow,light=light,covered=covered)
print('FINISH_FIELDS_READY')
