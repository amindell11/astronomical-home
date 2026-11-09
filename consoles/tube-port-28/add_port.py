"""Fit an editable tube entry around the saved hose crossings; write console.blend and a manifest, refusing overwrite."""
import bpy,bmesh,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(__file__).resolve().parent;out=root/'console.blend'
if out.exists():raise RuntimeError('Output already exists')
source=Path(bpy.data.filepath);s=bpy.context.scene;s.name='28 - Fitted tube entry'
col=bpy.data.collections.new('28 - Fitted tube entry');s.collection.children.link(col)
ink=bpy.data.collections['21 - Console hardware ink']
deck=bpy.data.objects['07 | Deck assembly']
fascia=bpy.data.objects['13 | Cooling deck folded beneath monitor']
vv=[fascia.matrix_world@v.co for v in fascia.data.vertices]
x=(vv[2]-vv[3]).normalized();z=(vv[3]-vv[2]).cross(vv[4]-vv[2]).normalized()
if z.y>0:z=-z
y=z.cross(x).normalized();x=y.cross(z).normalized()
glands=[bpy.data.objects['15 | Seated tube gland '+str(i)] for i in range(2)]
center=sum((g.matrix_world.translation for g in glands),Vector())/2
frame=Matrix(((x.x,y.x,z.x,center.x),(x.y,y.y,z.y,center.y),(x.z,y.z,z.z,center.z),(0,0,0,1)))
assembly=bpy.data.objects.new('28 | Tube entry assembly',None);col.objects.link(assembly);assembly.parent=deck;assembly.matrix_world=frame
created=[]

def mesh(name,verts,faces,mat,world=frame,bevel=.008):
 data=bpy.data.meshes.new('28 | '+name);data.from_pydata(verts,[],faces);data.update()
 bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
 o=bpy.data.objects.new('28 | '+name,data);col.objects.link(o);ink.objects.link(o);o.parent=assembly;o.matrix_world=world
 data.materials.append(bpy.data.materials[mat])
 if bevel:
  m=o.modifiers.new('Small fitted edge bevel','BEVEL');m.width=bevel;m.segments=1
 created.append(o.name);return o

def octagon(w,h,c):
 return [(-w/2+c,-h/2),(w/2-c,-h/2),(w/2,-h/2+c),(w/2,h/2-c),(w/2-c,h/2),(-w/2+c,h/2),(-w/2,h/2-c),(-w/2,-h/2+c)]

def ring(name,outer,inner,back,front,mat,world=frame):
 n=len(outer);v=[(a,b,d)for d,loop in [(back,outer),(front,outer),(front,inner),(back,inner)]for a,b in loop]
 f=[]
 for j in range(4):
  for i in range(n):f.append((j*n+i,j*n+(i+1)%n,((j+1)%4)*n+(i+1)%n,((j+1)%4)*n+i))
 return mesh(name,v,f,mat,world)

ring('Chamfered port rim',octagon(.98,.86,.14),octagon(.77,.65,.10),-.025,.075,'21 | Warm grey casing')
loop=octagon(.78,.66,.10);n=len(loop)
v=[(a,b,d)for d in [-.02,.025]for a,b in loop]
f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
insert=mesh('Recessed twin feedthrough plate',v,f,'21 | Graphite chassis')
for i,g in enumerate(glands):
 pose=g.matrix_world.copy()
 outer=[(.205*math.cos(a*math.tau/12),.205*math.sin(a*math.tau/12))for a in range(12)]
 inner=[(.165*math.cos(a*math.tau/12),.165*math.sin(a*math.tau/12))for a in range(12)]
 ring('Cable gland collar '+str(i),outer,inner,.008,.045,'21 | Brushed warm nickel',pose)
 # The inserts are cut at the actual hose axes, leaving the saved fascia untouched.
 bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.145,depth=.5)
 cutter=bpy.context.object;cutter.matrix_world=pose
 bpy.context.view_layer.objects.active=insert;insert.select_set(True)
 m=insert.modifiers.new('Feedthrough bore '+str(i),'BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=cutter
 bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(cutter,do_unlink=True)

for i,(a,b) in enumerate([(-.34,.29),(.34,-.29)]):
 pts=[(.045*math.cos(k*math.tau/6)+a,.045*math.sin(k*math.tau/6)+b)for k in range(6)]
 verts=[(u,v,d)for d in [.076,.096]for u,v in pts]
 faces=[tuple(reversed(range(6))),tuple(range(6,12))]+[(k,(k+1)%6,(k+1)%6+6,k+6)for k in range(6)]
 mesh('Captive port fastener '+str(i),verts,faces,'21 | Brushed warm nickel',bevel=.003)

bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/'console-manifest.json').write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'addedObjects':created+[assembly.name],'placement':'Fitted to the two existing hose/fascia crossings. No existing objects changed.','newMaterials':[]},indent=2))
