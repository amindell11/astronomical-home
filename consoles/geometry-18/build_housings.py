"""Load geometry-17 and replace its connector sheets with closed recessed housings. --name writes a new .blend and checks JSON, refusing overwrite."""
import bpy,bmesh,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent;args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output')
source=Path(bpy.data.filepath);s=bpy.context.scene;s.name='18 - Fitted connector housings';O=bpy.data.objects
locked={o.name:([list(r)for r in o.matrix_world],[list(v.co)for v in o.data.vertices]if o.type=='MESH'else None)for o in s.objects}
col=bpy.data.collections.new('18 - Recessed connector housings');s.collection.children.link(col)
parts=[o for o in s.objects if o.name.startswith('17 |')and o.type=='MESH']
for o in parts:o.hide_render=True;o.hide_set(True)
def housing(label,panels,recess,bottom):
 points=[];faces=[];keys={}
 for o in panels:
  face=[]
  for index in o.data.polygons[0].vertices:
   p=o.matrix_world@o.data.vertices[index].co;p.z-=recess;key=tuple(round(x,5)for x in p)
   if key not in keys:keys[key]=len(points);points.append(p)
   face.append(keys[key])
  faces.append(face)
 temp=bpy.data.meshes.new(label+' topology');temp.from_pydata(points,[],faces)
 bm=bmesh.new();bm.from_mesh(temp);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(temp);bm.free()
 faces=[list(p.vertices)for p in temp.polygons];bpy.data.meshes.remove(temp)
 edges={}
 for f in faces:
  for a,b in zip(f,f[1:]+f[:1]):
   key=tuple(sorted((a,b)));edges.setdefault(key,[]).append((a,b))
 boundary=[uses[0]for uses in edges.values()if len(uses)==1];assert all(len(e)<=2 for e in edges.values())
 remaining=dict(boundary);loop=[];cur=boundary[0][0]
 while cur in remaining:loop.append(cur);cur=remaining.pop(cur)
 assert not remaining and cur==loop[0],(label,remaining)
 topcount=len(faces);lower={}
 for i in loop:
  z=bottom(points[i])if callable(bottom)else bottom
  assert z<points[i].z-.02,(label,i,z,points[i].z)
  lower[i]=len(points);points.append(Vector((points[i].x,points[i].y,z)))
 for a,b in boundary:faces.append([b,a,lower[a],lower[b]])
 faces.append([lower[i]for i in reversed(loop)])
 d=bpy.data.meshes.new('18 | '+label);d.from_pydata(points,[],faces)
 for n in ['Armor side walls','Dark structural housing']:d.materials.append(bpy.data.materials['08 | '+n])
 for i,p in enumerate(d.polygons):p.material_index=0 if i<topcount else 1
 bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free();d.update()
 o=bpy.data.objects.new(d.name,d);col.objects.link(o)
 m=o.modifiers.new('Housing edge radius','BEVEL');m.width=.018;m.segments=2;m.limit_method='ANGLE';m.angle_limit=.65
 m=o.modifiers.new('Broad casing normals','WEIGHTED_NORMAL');m.keep_sharp=True
 return o
upper=[o for o in parts if 'Lower join' not in o.name]
lower=[o for o in parts if o.name in ['17 | Lower join flange front','17 | Lower join flange rear']]
tray=O['14 | Forearm fold into Launch and controls'];vs=[tray.matrix_world@v.co for v in tray.data.vertices];n=(vs[7]-vs[6]).cross(vs[8]-vs[6])
def seat_floor(p):return vs[6].z-(n.x*(p.x-vs[6].x)+n.y*(p.y-vs[6].y))/n.z-.30
housing('Projector control and Launch seat',upper,.075,seat_floor)
housing('Lower tray mounting return',lower,.005,-.10)
bpy.context.view_layer.update()
for n,(m,v)in locked.items():
 o=O[n];assert [list(r)for r in o.matrix_world]==m,n
 if v is not None:assert [list(x.co)for x in o.data.vertices]==v,n
s.camera=O['07 Camera - perspective'];bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(out.stem+'-checks.json')).write_text(json.dumps({'source':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'existingObjectsUnmoved':len(locked),'allExistingMeshVerticesUnchanged':True,'replacedSheets':[o.name for o in parts],'newHousings':[o.name for o in col.objects],'visualApproval':'pending'},indent=2))
