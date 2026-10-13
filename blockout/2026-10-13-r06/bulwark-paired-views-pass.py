import bpy,bmesh,sys,json,hashlib,math
from pathlib import Path
from mathutils import Vector,Matrix
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
baseline=json.load(open('D:/amind/git/agent-3/results/bulwark/planform-2026-10-12/check/ship_check.json'))['fingerprint']
assert ship_source.fingerprint(bpy.context.scene)==baseline
assert bpy.context.mode=='OBJECT'
out=Path('D:/amind/git/agent-3/results/bulwark/paired-views-2026-10-13')
out.mkdir(parents=True,exist_ok=True)
def put(o,i,x=None,y=None,z=None):
 v=o.data.vertices[i];p=o.matrix_world@v.co
 if x is not None:p.x=x
 if y is not None:p.y=y
 if z is not None:p.z=z
 v.co=o.matrix_world.inverted()@p
upper=bpy.data.objects['02 Upper hull']
for i,x,z in [(0,None,2.18),(1,2.10,2.78),(2,None,2.78),(3,None,2.22),(4,1.88,2.90),(5,None,2.90),(6,None,2.23),(7,.86,2.90),(8,None,2.90),(9,None,2.24),(10,.70,2.90),(11,None,2.90)]:
 put(upper,i,x=x,z=z)
lower=bpy.data.objects['03 Lower hull']
for i,z in [(0,.14),(1,.23),(2,.36),(3,.12),(4,.08),(5,.32),(6,.24),(7,.18),(8,.38),(9,.96),(10,.92),(11,1.10)]:
 put(lower,i,z=z)
nose=bpy.data.objects['01 Ram nose']
for i,x,z in [(0,None,.14),(1,None,.23),(2,None,1.68),(3,None,1.68),(4,None,1.24),(5,None,.99),(6,None,.68),(7,None,.76),(8,None,.36),(9,None,2.18),(10,2.10,2.78),(11,None,2.78)]:
 put(nose,i,x=x,z=z)
boom=bpy.data.objects['04 Tail booms']
for i,x,z in [(7,None,.28),(6,None,.24),(5,None,.43),(4,None,2.24),(3,2.001,2.34),(2,.67,2.90),(1,None,2.80),(0,None,1.01),(8,None,.97),(9,None,1.10),(10,None,2.77),(11,None,2.94),(12,None,2.96),(13,None,2.85),(14,None,1.72),(15,None,1.67),(16,None,1.82)]:
 put(boom,i,x=x,z=z)
tip=bpy.data.objects['05 Tail tips']
for i in range(7):
 p=boom.matrix_world@boom.data.vertices[14+i].co
 tip.data.vertices[i].co=tip.matrix_world.inverted()@p
canopy=bpy.data.objects['06 Cockpit dome']
def roof(y):
 if y>2.875:return 2.78+(y-2.875)/(5-2.875)*(1.68-2.78)
 if y>1.27:return 2.90+(y-1.27)/(2.875-1.27)*(2.78-2.90)
 return 2.90
put(canopy,0,x=0,y=4.0,z=roof(4.0)-.02)
for start,y,r,h in [(1,3.90,.25,.17),(8,3.61,.49,.32),(15,3.13,.57,.36),(22,2.70,.49,.28),(29,1.80,.29,.14),(36,.96,.12,.045)]:
 for j in range(7):
  a=j*math.pi/12
  put(canopy,start+j,x=r*math.cos(a),y=y,z=roof(y)-.02+h*math.sin(a))
put(canopy,43,x=0,y=.40,z=roof(.40)-.02)
insets=bpy.data.objects['07 Side insets']
for v in insets.data.vertices:
 p=insets.matrix_world@v.co
 p.x+=.28;p.z+=.17
 v.co=insets.matrix_world.inverted()@p
verts=[];faces=[]
stations=[(3.22,2.49,.61,2.28),(2.72,2.82,.16,2.65),(1.25,2.82,.12,2.66),(.22,2.48,.28,2.45),(-.55,2.18,.73,2.31)]
for y,x,low,high in stations:
 for px,pz in [(x-.23,low+.06),(x-.02,low),(x+.035,low+.12),(x+.035,high-.12),(x-.015,high),(x-.23,high-.08)]:
  verts.append((px,y,pz))
for s in range(len(stations)-1):
 for i in range(6):
  j=(i+1)%6
  faces.append((s*6+i,s*6+j,(s+1)*6+j,(s+1)*6+i))
faces+=[tuple(reversed(range(6))),tuple(range(24,30))]
mesh=bpy.data.meshes.new('Broad side armor blockout')
mesh.from_pydata(verts,[],faces)
mesh.materials.append(bpy.data.materials['Bulwark Blockout Gray'])
mesh.uv_layers.new(name='PaintUV')
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
group=bpy.data.objects.new('Side armor',None)
bpy.data.collections['Ship'].objects.link(group)
group.parent=bpy.data.objects['Body']
group.matrix_world=Matrix.Identity(4)
group.empty_display_type='PLAIN_AXES';group.empty_display_size=.3
armor=bpy.data.objects.new('09 Side armor',mesh)
bpy.data.collections['Ship'].objects.link(armor)
bpy.data.collections['role.hull'].objects.link(armor)
armor.parent=group
armor['editing_group']='Side armor'
mirror=armor.modifiers.new('Linked left / right','MIRROR')
mirror.mirror_object=bpy.data.objects['SymmetryOrigin'];mirror.use_clip=True
for o in [upper,lower,nose,boom,tip,canopy,insets,armor]:
 o.data.update()
 bm=bmesh.new();bm.from_mesh(o.data)
 bad=[]
 for f in bm.faces:
  if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-4 for v in f.verts):bad.append(f)
 if bad:bmesh.ops.triangulate(bm,faces=bad)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bm.to_mesh(o.data);bm.free();o.data.update()
bpy.context.view_layer.update()
bpy.ops.ed.undo_push(message='Bulwark paired top and side blockout')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result={'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'mesh_parts':sum(o.type=='MESH' for o in bpy.context.scene.objects),'mirrors':sum(m.type=='MIRROR' for o in bpy.context.scene.objects for m in o.modifiers),'material':'neutral gray'}
(out/'edit-audit.json').write_text(json.dumps(result,indent=2))

