import bpy,bmesh,json,hashlib,sys,math
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
out=Path('D:/amind/git/agent-3/results/bulwark/fitted-parts')
out.mkdir(parents=True,exist_ok=True)
sha=hashlib.sha256(source.read_bytes()).hexdigest()
assert sha=='9a3ccb8dfcd23f6a628826bf51336ea526ac8fed67169c46c280fb5257df948e'
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
scene=bpy.context.scene
original_names={o.name for o in scene.objects if o.type=='MESH'}
audit={'source_sha256':sha,'basis':'6695210794adb7e8e1f78ddfd92a84af3fa3f607','changes':{},'unmerged':True}
def edit(name,fn,why):
 o=bpy.data.objects[name]
 if o.data.users>1:o.data=o.data.copy()
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table()
 bm.transform(o.matrix_world)
 fn(bm)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bad=[f for f in bm.faces if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-5 for v in f.verts)]
 if bad:bmesh.ops.triangulate(bm,faces=bad)
 bm.transform(o.matrix_world.inverted())
 bm.to_mesh(o.data);bm.free();o.data.update()
 audit['changes'][name]=why
def cut(bm,point,normal,positive=False):
 p=Vector(point);n=Vector(normal).normalized()
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=p,plane_no=n,clear_inner=positive,clear_outer=not positive)
 edges=[e for e in bm.edges if e.is_boundary and all(abs((v.co-p).dot(n))<1e-5 for v in e.verts)]
 if edges:
  degree={v:sum(v in e.verts for e in edges) for e in edges for v in e.verts}
  ends=[v for v,d in degree.items() if d==1]
  if ends:
   assert len(ends)==2 and all(abs(v.co.x)<1e-5 for v in ends),[(tuple(v.co)) for v in ends]
   edges.append(bm.edges.new(ends))
  bmesh.ops.holes_fill(bm,edges=edges,sides=0)
def close_guard(bm,lower=False):
 bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index>=21],context='VERTS')
 bm.verts.ensure_lookup_table()
 bm.faces.new([bm.verts[i] for i in range(14,21)])
 # A flat inner mounting face replaces the reused nozzle cavity.
 cut(bm,(2.08 if lower else 2.15,0,0),(1,-.24,0),True)
 bmesh.ops.dissolve_limit(bm,angle_limit=math.radians(.2),verts=list(bm.verts),edges=list(bm.edges))
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 face=max([f for f in bm.faces if f.normal.x>.5],key=lambda f:f.calc_area())
 bmesh.ops.inset_individual(bm,faces=[face],thickness=.065,depth=-.025,use_even_offset=True)
edit('05 Tail tips.002',lambda bm:close_guard(bm),'Solid upper shoulder guard with a planar inner seat and closed rear end.')
edit('05 Tail tips.003',lambda bm:close_guard(bm,True),'Solid lower armor skid with a planar inner seat and closed rear end.')
edit('09 Side armor',lambda bm:cut(bm,(0,.61,0),(0,1,-.20),True),'Side belt terminates at the shoulder guard rather than projecting through it.')
edit('09 Side armor.001',lambda bm:cut(bm,(0,-.09,0),(0,1,0)),'Spine has a clean leading seat behind the main deck.')
edit('09 Side armor.002',lambda bm:(cut(bm,(0,-1.04,0),(0,1,0)),cut(bm,(0,-2.91,0),(0,1,-.20),True)),'Engine-root collars have clean front and rear seating faces; long buried tails removed.')
edit('01 Ram nose',lambda bm:cut(bm,(0,2.62,0),(0,1,-.08),True),'Ram rear mating face aligned to the deck seam.')
edit('02 Upper hull.001',lambda bm:cut(bm,(0,2.59,0),(0,1,-.08)),'Main deck front trimmed to a narrow separation behind the ram.')
def keel(bm):
 for idx,z in [(12,1.225),(13,1.225),(14,1.40),(15,1.67),(16,1.70),(17,1.70)]:bm.verts[idx].co.z=z
 cut(bm,(0,2.59,0),(0,1,-.08))
edit('03 Lower hull',keel,'Keel upper seating surface lowered beneath the deck and bridge; exterior bottom contour retained.')
edit('02 Upper hull',lambda bm:(cut(bm,(0,-.06,0),(0,1,0)),cut(bm,(.895,0,0),(1,-.155,0))),'Aft bridge begins behind deck and meets the two booms along sloping seams.')
edit('04 Tail booms',lambda bm:(cut(bm,(0,-.055,0),(0,1,0)),cut(bm,(.925,0,0),(1,-.155,0),True),cut(bm,(0,-2.91,0),(0,1,-.20),True)),'Booms have defined deck, bridge, and shroud interfaces.')

def flank_rim(bm):
 import numpy as np
 ring=[bm.verts[i] for i in range(1,21)]
 center=sum((v.co for v in ring),Vector())/len(ring)
 yz=np.array([[v.co.y-center.y,v.co.z-center.z,1] for v in ring]);xs=np.array([v.co.x for v in ring])
 a,b,c=np.linalg.lstsq(yz,xs,rcond=None)[0]
 for v in ring:v.co.x=a*(v.co.y-center.y)+b*(v.co.z-center.z)+c
 center=sum((v.co for v in ring),Vector())/len(ring)
 normal=Vector((1,-a,-b)).normalized()
 bmesh.ops.delete(bm,geom=[bm.verts[0]],context='VERTS')
 inner=[bm.verts.new(center+(v.co-center)*.83-normal*.055) for v in ring]
 for i in range(len(ring)):
  j=(i+1)%len(ring);bm.faces.new([ring[i],ring[j],inner[j],inner[i]])
 bm.faces.new(inner)
edit('07 Side insets',flank_rim,'Round flank module receives a planar inset center and thick protective bezel; original rim outline retained.')

meanings={'01 Ram nose':'Reinforced ram','02 Upper hull.001':'Main deck','02 Upper hull':'Aft bridge','03 Lower hull':'Armored keel','04 Tail booms':'Engine shoulders','05 Tail tips':'Engine shrouds','05 Tail tips.002':'Upper shoulder guards','05 Tail tips.003':'Lower armor skids','09 Side armor':'Side armor belt','09 Side armor.001':'Dorsal spine','09 Side armor.002':'Engine root collars','06 Cockpit dome':'Teardrop canopy','07 Side insets':'Flank modules','08 Nested engines':'Nested engine nozzles'}
for name,meaning in meanings.items():
 o=bpy.data.objects[name];o['part_purpose']=meaning
 if o.hide_render:continue
 if name not in ['06 Cockpit dome','08 Nested engines']:
  bevel=o.modifiers.new('Small armor edge chamfer','BEVEL');bevel.width=(.012 if name=='07 Side insets' else .024)/max(o.scale);bevel.segments=1;bevel.angle_limit=math.radians(25);bevel.use_clamp_overlap=True
  o['edit_note']='Original object, transform, parent, and mirror retained. Fitted independently; never joined.'
 if not any(c.name.startswith('role.') for c in o.users_collection):bpy.data.collections['role.hull'].objects.link(o)
 if name in ['05 Tail tips.002','05 Tail tips.003','09 Side armor.001','09 Side armor.002']:
  collection=bpy.data.collections.new(meaning);scene.collection.children.link(collection);collection.objects.link(o)

assert original_names=={o.name for o in scene.objects if o.type=='MESH'}
assert all(not any(m.type=='BOOLEAN' for m in bpy.data.objects[n].modifiers) for n in meanings)
scene['review_stage']='Separate-part fitting and light form refinement'
scene['review_note']='Each original kitbash part remains independent. Owner original preserved in source control.'
for o in scene.objects:o.select_set(False)
active=bpy.data.objects['05 Tail tips.002'];active.select_set(True);bpy.context.view_layer.objects.active=active
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   s=area.spaces.active;s.overlay.show_overlays=False;s.shading.type='SOLID';s.shading.light='STUDIO';s.shading.color_type='MATERIAL';s.shading.show_cavity=True
   s.region_3d.view_rotation=ship_render.camera_matrix((10,15,8.5),(0,0,1),(0,0,1.5),20).to_quaternion();s.region_3d.view_location=(0,0,1.5);s.region_3d.view_distance=14;s.region_3d.view_perspective='ORTHO'
target=out/'Bulwark-fitted-parts.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(target))
audit['mesh_objects']=sorted(original_names);audit['visible_parts']=meanings
audit['output_sha256']=hashlib.sha256(target.read_bytes()).hexdigest()
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
audit['original_unchanged']=True
(out/'fit-audit.json').write_text(json.dumps(audit,indent=2))
ship=json.loads((source.parent/'ship.json').read_text());ship['source']=target.name
(out/'ship.json').write_text(json.dumps(ship,indent=2))
print(json.dumps({'saved':str(target),'separate_visible_parts':len(meanings),'source_unchanged':True}))
