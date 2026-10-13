import bpy,bmesh,json,hashlib,sys,math
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/interfaces')
source=root/'owner/Bulwark.blend';out=root/'refined';out.mkdir(exist_ok=True)
sha=hashlib.sha256(source.read_bytes()).hexdigest()
assert sha=='e218ca1f4c0e13ce4ef7b3e22de0c24d45c4f89a519d725363ea40a41f7fd505'
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
scene=bpy.context.scene;before=ship_source.fingerprint(scene)
audit={'source_sha256':sha,'owner_checkpoint':'9d17e34f53414637ac43c3f96d69cf3fac5d9458','changes':{}}
def begin(name):
 o=bpy.data.objects[name]
 assert o.type=='MESH' and o.data.users==1
 bm=bmesh.new();bm.from_mesh(o.data);bm.transform(o.matrix_world);bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table()
 return o,bm
def finish(o,bm):
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bad=[f for f in bm.faces if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-5 for v in f.verts)]
 if bad:bmesh.ops.triangulate(bm,faces=bad)
 bm.transform(o.matrix_world.inverted());bm.to_mesh(o.data);bm.free();o.data.update()

o,bm=begin('04 Tail booms');old=list(bm.verts)
assert len(old)==26
mid=[old[i] for i in [1,8,9,10,11,2,4]]
tip=bpy.data.objects['05 Tail tips'];tipv=[tip.matrix_world@v.co for v in tip.data.vertices]
target=[tipv[i]+Vector((0,.03,0)) for i in range(7)]
bmesh.ops.delete(bm,geom=[old[i] for i in [0,3,7,12,13,14,15,16,18]],context='VERTS')
bm.faces.new([old[i] for i in [4,2,24,25,1]])
start=[v.co.copy() for v in mid];previous=mid
for t in [.45,.78,1.0]:
 ring=[]
 for i,(a,b) in enumerate(zip(start,target)):
  direction=(b-a)
  final_tangent=(tipv[i+7]-tipv[i]).normalized()*direction.length
  h00=2*t**3-3*t**2+1;h10=t**3-2*t**2+t;h01=-2*t**3+3*t**2;h11=t**3-t**2
  p=h00*a+h10*direction+h01*b+h11*final_tangent
  ring.append(bm.verts.new(p))
 for i in range(7):
  j=(i+1)%7;bm.faces.new([previous[i],previous[j],ring[j],ring[i]])
 previous=ring
bm.faces.new(list(reversed(previous)))
finish(o,bm)
audit['changes'][o.name]='Rear boom section extends to the housing profile, with a shaped transition and 0.03-unit seam. Tip, engine, and owner collar geometry remain unchanged.'
audit['boom_joint_gap']=.03

o,bm=begin('07 Side insets')
front=[bm.verts[i] for i in range(1,21)];back=[bm.verts[i] for i in range(22,42)];inner=[bm.verts[i] for i in [0]+list(range(42,61))]
center=sum((v.co for v in front),Vector())/20
points=np.array([list(v.co-center) for v in front]);_,_,vh=np.linalg.svd(points);normal=Vector(vh[-1]);normal*=1 if normal.x>0 else -1
u=(front[0].co-center);u-=normal*u.dot(normal);u.normalize();v=normal.cross(u).normalized()
coords=np.array([[(q.co-center).dot(u),(q.co-center).dot(v)] for q in front]);area=abs(sum(coords[i,0]*coords[(i+1)%20,1]-coords[(i+1)%20,0]*coords[i,1] for i in range(20)))/2
radius=math.sqrt(area/(10*math.sin(math.pi/10)))
depth=(center-sum((q.co for q in back),Vector())/20).dot(normal)
layers=[(front,0,radius),(inner,-.055,radius*.83),(back,-depth,radius)]
ring_edges=[]
for ring,z,r in layers:
 for i,q in enumerate(ring):q.co=center+normal*z+(u*math.cos(i*math.tau/20)+v*math.sin(i*math.tau/20))*r
 for i in range(20):ring_edges.append(bm.edges.get((ring[i],ring[(i+1)%20])))
bm.verts[21].co=center-normal*depth
assert all(e is not None for e in ring_edges)
bmesh.ops.subdivide_edges(bm,edges=ring_edges,cuts=2,use_grid_fill=True)
errors=[]
for q in bm.verts:
 d=q.co-center;z=d.dot(normal);radial=d-normal*z
 if radial.length<1e-5:continue
 layer=min(layers,key=lambda entry:abs(entry[1]-z))
 assert abs(layer[1]-z)<1e-4
 q.co=center+normal*layer[1]+radial.normalized()*layer[2]
 errors.append(abs((q.co-center-normal*layer[1]).length-layer[2]))
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
for f in bm.faces:f.smooth=abs(f.normal.dot(normal))<.9
finish(o,bm)
for m in o.modifiers:
 if m.type=='BEVEL':m.segments=2;m.harden_normals=True
audit['changes'][o.name]='Three concentric, planar circular rings with 60 segments, correcting the previous oval and irregular back face.'
audit['side_circle']={'radius':radius,'depth':depth,'radial_error_max':max(errors),'normal':list(normal),'segments':60}

bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();verts=[];tris=[]
for name in ['01 Ram nose','02 Upper hull.001']:
 hull=bpy.data.objects[name];mesh=bpy.data.meshes.new_from_object(hull.evaluated_get(dg),depsgraph=dg);mesh.transform(hull.matrix_world);mesh.calc_loop_triangles();offset=len(verts)
 verts.extend([q.co.copy() for q in mesh.vertices])
 for tri in mesh.loop_triangles:
  a,b,c=[mesh.vertices[i].co for i in tri.vertices]
  if (b-a).cross(c-a).normalized().z>.5:tris.append([offset+i for i in tri.vertices])
 bpy.data.meshes.remove(mesh)
bvh=BVHTree.FromPolygons(verts,tris,all_triangles=True)
def support(x,y):
 hit=bvh.ray_cast(Vector((x,y,8)),Vector((0,0,-1)),10)[0]
 if hit is not None:return hit.z
 # Span the existing narrow ram/deck panel seam.
 hits=[bvh.ray_cast(Vector((x,y+d,8)),Vector((0,0,-1)),10)[0] for d in [-.05,.05]]
 assert all(p is not None for p in hits),(x,y)
 return sum(p.z for p in hits)/2
o,bm=begin('06 Cockpit dome')
base=[0,1,8,15,22,29,36,43];deltas=[]
for index in base:
 q=bm.verts[index];dz=support(q.co.x,q.co.y)+.045-q.co.z;deltas.append(dz)
 if index in [0,43]:q.co.z+=dz
 else:
  for j in range(7):bm.verts[index+j].co.z+=dz*(1-.15*math.sin(j*math.pi/12))
finish(o,bm)
audit['changes'][o.name]='Base follows ram and deck heights, keeping the teardrop planform and live subdivision.'
audit['cockpit_base_vertical_adjustments']=deltas

bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();mesh=bpy.data.meshes.new_from_object(o.evaluated_get(dg),depsgraph=dg);mesh.transform(o.matrix_world)
bm=bmesh.new();bm.from_mesh(mesh)
boundary={q for e in bm.edges if e.is_boundary for q in e.verts}
points=sorted([q.co.copy() for q in boundary if q.co.x>=-1e-5],key=lambda p:-p.y)
bm.free();bpy.data.meshes.remove(mesh)
assert len(points)>20 and abs(points[0].x)<1e-5 and abs(points[-1].x)<1e-5
seatverts=[]
for i,p in enumerate(points):
 if i==0:outward=Vector((0,1,0))
 elif i==len(points)-1:outward=Vector((0,-1,0))
 else:
  tangent=points[i+1]-points[i-1];outward=Vector((-tangent.y,tangent.x,0)).normalized()
 outer=p+outward*.055;outer.x=max(0,outer.x)
 innerbottom=Vector((p.x,p.y,support(p.x,p.y)-.008));outerbottom=Vector((outer.x,outer.y,support(outer.x,outer.y)-.008))
 innertop=p+Vector((0,0,.005));outertop=Vector((outer.x,outer.y,support(outer.x,outer.y)+.025))
 seatverts.extend([list(innertop),list(outertop),list(outerbottom),list(innerbottom)])
faces=[]
for i in range(len(points)-1):
 for j in range(4):faces.append([4*i+j,4*i+(j+1)%4,4*(i+1)+(j+1)%4,4*(i+1)+j])
mesh=bpy.data.meshes.new('Cockpit fitted surround');mesh.from_pydata(seatverts,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
seat=bpy.data.objects.new('06 Cockpit surround',mesh);bpy.data.collections['Ship'].objects.link(seat);bpy.data.collections['role.hull'].objects.link(seat);seat.parent=bpy.data.objects['Cockpit'];seat.matrix_world=Matrix.Identity(4)
mesh.materials.append(bpy.data.materials['Bulwark Blockout Gray']);mesh.uv_layers.new(name='PaintUV')
mirror=seat.modifiers.new('Linked left / right','MIRROR');mirror.mirror_object=bpy.data.objects['SymmetryOrigin'];mirror.use_clip=True
bevel=seat.modifiers.new('Surround edge chamfer','BEVEL');bevel.width=.006;bevel.segments=1
seat['part_purpose']='Hull-conforming cockpit seating rim; separately editable.'
audit['changes'][seat.name]='New thin, separate cockpit surround conforming to the hull and evaluated canopy edge.'
after=ship_source.fingerprint(scene)
changed=[n for n,p in before['parts'].items() if p!=after['parts'].get(n)]
assert set(changed)=={'04 Tail booms','06 Cockpit dome','07 Side insets'},changed
audit['changed_existing_parts']=changed;audit['untouched_parts']=[n for n in before['parts'] if n not in changed]
audit['new_parts']=[n for n in after['parts'] if n not in before['parts']]
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True
for obj in scene.objects:obj.select_set(False)
bpy.data.objects['04 Tail booms'].select_set(True);bpy.context.view_layer.objects.active=bpy.data.objects['04 Tail booms']
scene['review_stage']='Boom, circular inset, and cockpit interfaces'
target=out/'Bulwark.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target))
audit['output_sha256']=hashlib.sha256(target.read_bytes()).hexdigest();audit['original_unchanged']=hashlib.sha256(source.read_bytes()).hexdigest()==sha
assert audit['original_unchanged']
(out/'interface-audit.json').write_text(json.dumps(audit,indent=2))
ship=json.loads((root/'owner/ship.json').read_text(encoding='utf-8-sig'));ship['source']=target.name
(out/'ship.json').write_text(json.dumps(ship,indent=2))
print(json.dumps({'file':str(target),'changed':changed,'added':audit['new_parts'],'circle':audit['side_circle']}))
