import bpy,sys,json,hashlib,math
import numpy as np
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_export
sys.path.insert(0,str(Path(__file__).parent))
from paint_region_rules_r05 import region_for
root=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r05');root.mkdir(parents=True,exist_ok=True)
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend');sha=hashlib.sha256(source.read_bytes()).hexdigest()
src=ship_source.open_source(source);fp=ship_source.fingerprint(src);ev=ship_source.Evaluation(src)
parts=[];edges=[]
names=sorted(o.name for o in src.objects if o.type=='MESH' and not o.hide_render)
for name in names:
 p=ship_export.Part(ev,name,['PaintUV']);co,n=p.placed(p.normals);_,cn=p.placed(p.contour_normals())
 v=co[p.corners].copy();n=n.copy();cn=cn.copy();centers=v.reshape(-1,3,3).mean(axis=1);normals=n.reshape(-1,3,3).mean(axis=1)
 mesh=ev.mesh(name);_,_,_,tri=ship_source.mesh_arrays(mesh)
 uv=p.uvs['PaintUV']
 assert len(uv)==len(v)
 edge_refs={}
 for face in mesh.polygons:
  verts=list(face.vertices)
  for a,b in zip(verts,verts[1:]+verts[:1]):edge_refs.setdefault(tuple(sorted((a,b))),[]).append(face)
 for (a,b),faces in edge_refs.items():
  if name=='06 Cockpit dome':continue
  boundary=len(faces)==1
  angle=faces[0].normal.angle(faces[1].normal) if len(faces)==2 else 0
  if not boundary and angle<math.radians(28):continue
  pa=ev.to_space(name)@mesh.vertices[a].co;pb=ev.to_space(name)@mesh.vertices[b].co
  if abs(pa.x)<1e-4 and abs(pb.x)<1e-4:continue
  if (pa-pb).length<.14:continue
  edges.append({'part':name,'a':list(pa),'b':list(pb)})
 bpy.data.meshes.remove(mesh)
 regions=([region_for(name,norm,c) for c,norm in zip(centers,normals)]
          if '--draft' in sys.argv else p.material_index.tolist())
 parts.append({'name':name,'worldVertices':v.ravel().tolist(),'normalsWorld':n.ravel().tolist(),'uv':uv.ravel().tolist(),
               'contourNormalsWorld':cn.ravel().tolist(),'triangles':np.arange(len(v)).reshape(-1,3)[:,[0,2,1]].ravel().tolist(),'regions':regions})
assert ship_source.fingerprint(src)==fp and hashlib.sha256(source.read_bytes()).hexdigest()==sha
(root/'paint-mesh.json').write_text(json.dumps({'sourceSha256':sha,'parts':parts,'edges':edges},separators=(',',':')))
for p in parts:
 v=np.array(p['worldVertices']).reshape(-1,3);print(p['name'],v.min(axis=0).round(3),v.max(axis=0).round(3))
print('TRIANGLES',sum(len(p['triangles'])//3 for p in parts),'EDGES',len(edges))
