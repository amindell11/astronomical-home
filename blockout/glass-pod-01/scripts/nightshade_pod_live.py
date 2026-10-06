from pathlib import Path
import bpy,bmesh,json,sys,hashlib,math
from mathutils import Vector
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
base=root/'results/nightshade-glass-pod';out=base/'round-01';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
check=json.loads((base/'precheck/ship_check.json').read_text())
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
before=ship_source.fingerprint(bpy.context.scene)
assert before==check['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==check['source_sha256']
names=['Rebuilt pitched hull','Upper canopy glazing','Lower canopy glazing','Upper canopy frame',
       'Upper canopy metal bezel','Lower canopy metal bezel','Upper canopy inner seal']
original={n:{'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
    'mesh':bpy.data.objects[n].data.as_pointer(),
    'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers],
    'vertices':[tuple(v.co) for v in bpy.data.objects[n].data.vertices]} for n in names}
cy=.589;ry=.229;rx=.134;rz=.112
def zc(y):return -.028-.15*(y-cy)
def local(ob,co):return ob.matrix_world.inverted()@Vector(co)
def finish(ob,bm):
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(ob.data);bm.free();ob.data.update()

for side,sign in [('Upper',1),('Lower',-1)]:
    ob=bpy.data.objects[side+' canopy glazing'];assert len(ob.data.vertices)==70
    for i,v in enumerate(ob.data.vertices):
        row,col=divmod(i,7);theta=math.pi*row/9;phi=math.pi*.5*col/6
        y=cy+ry*math.cos(theta);x=rx*math.sin(theta)*math.sin(phi)
        z=zc(y)+sign*rz*math.sin(theta)*math.cos(phi)
        v.co=local(ob,(x,y,z))
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
    bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
    for v in bm.verts:
        p=ob.matrix_world@v.co
        q=Vector((p.x/rx,(p.y-cy)/ry,(p.z-zc(p.y))/rz)).normalized()
        y=cy+ry*q.y
        v.co=local(ob,(rx*q.x,y,zc(y)+rz*q.z))
    for f in bm.faces:f.smooth=True
    finish(ob,bm)

# Keep the original nose and outer hull; open only the two cockpit surface patches.
ob=bpy.data.objects['Rebuilt pitched hull'];bm=bmesh.new();bm.from_mesh(ob.data)
bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();verts=list(bm.verts)
assert len(verts)==444 and len(bm.faces)==432
top_ids=list(range(34,39))+[r*17+4 for r in range(3,10)]+[170+k for k in range(4,-1,-1)]
bottom_ids=[(i//17)*17+16-(i%17) for i in top_ids]
angles=[i*.1 for i in range(5)]
angles += [math.acos((y-cy)/.225) for y in (.744,.694,.644,.594,.544,.494,.444)]
angles += [math.pi-.4+i*.1 for i in range(5)]
assert len(top_ids)==len(bottom_ids)==len(angles)==17
for ids,sign in [(top_ids,1),(bottom_ids,-1)]:
    for idx,angle in zip(ids,angles):
        x=.148*math.sin(angle);y=cy+.225*math.cos(angle)
        verts[idx].co=local(ob,(x,y,zc(y)+sign*.036))
remove=[f for f in bm.faces if f.index<416 and 2<=f.index//16<10 and (f.index%16<4 or f.index%16>=12)]
assert len(remove)==64
bmesh.ops.delete(bm,geom=remove,context='FACES_ONLY')
for i in range(16):
    f=bm.faces.new((verts[top_ids[i]],verts[top_ids[i+1]],verts[bottom_ids[i+1]],verts[bottom_ids[i]]))
    f.material_index=3
loose=[v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm,geom=loose,context='VERTS')
finish(ob,bm)

# Reuse the dark frame's border as a recessed seat flange, removing its filled center.
ob=bpy.data.objects['Upper canopy frame'];bm=bmesh.new();bm.from_mesh(ob.data)
bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();verts=list(bm.verts)
outer=list(range(7))+[r*7+6 for r in range(1,9)]+list(range(69,62,-1))
inner=list(range(7,13))+[r*7+5 for r in range(2,8)]+list(range(61,55,-1))
assert len(outer)==22 and len(inner)==18
for indices,ax,ay in [(outer,.148,.225),(inner,.129,.208)]:
    for i,idx in enumerate(indices):
        angle=math.pi*i/(len(indices)-1);y=cy+ay*math.cos(angle)
        verts[idx].co=local(ob,(ax*math.sin(angle),y,zc(y)+.023))
remove=[f for f in bm.faces if 1<=f.index//6<=7 and f.index%6<5]
bmesh.ops.delete(bm,geom=remove,context='FACES_ONLY')
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
finish(ob,bm)

# Chisel the existing upper/lower rim extrusions around the pod's equatorial cradle.
profile=[(-.011,.001),(-.007,.011),(.010,.011),(.017,.002),(.017,-.009),(.010,-.014),(-.007,-.014),(-.011,-.007)]
for side,sign in [('Upper',1),('Lower',-1)]:
    ob=bpy.data.objects[side+' canopy metal bezel'];assert len(ob.data.vertices)==176
    for i,v in enumerate(ob.data.vertices):
        station,j=divmod(i,8);angle=math.pi*station/21
        width,height=profile[j];y=cy+(.219+width)*math.cos(angle)
        x=(.140+width)*math.sin(angle)
        v.co=local(ob,(x,y,zc(y)+sign*(.036+height)))
    bm=bmesh.new();bm.from_mesh(ob.data);finish(ob,bm)

ob=bpy.data.objects['Upper canopy inner seal'];assert len(ob.data.vertices)==110
profile=[(-.002,.001),(-.001,.004),(.003,.004),(.004,-.005),(-.002,-.005)]
for i,v in enumerate(ob.data.vertices):
    station,j=divmod(i,5);angle=math.pi*station/21
    width,height=profile[j];y=cy+(.211+width)*math.cos(angle)
    v.co=local(ob,((.130+width)*math.sin(angle),y,zc(y)+.030+height))
bm=bmesh.new();bm.from_mesh(ob.data);finish(ob,bm)
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names) and set(before['parts'])==set(after['parts'])
for n,old in original.items():
    ob=bpy.data.objects[n]
    assert [list(r) for r in ob.matrix_basis]==old['matrix']
    assert ob.data.as_pointer()==old['mesh']
    assert [(m.name,m.type) for m in ob.modifiers]==old['mods']
unchanged_hull=set(original['Rebuilt pitched hull']['vertices'])-set(original['Rebuilt pitched hull']['vertices'][i] for i in set(top_ids+bottom_ids))
new_hull=set(tuple(v.co) for v in bpy.data.objects['Rebuilt pitched hull'].data.vertices)
removed_interior=len(unchanged_hull-new_hull)
assert removed_interior==56
bpy.context.scene['review']='Concept-led glass pod seated through angular metal cradle; canopy approval pending'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'owner_checkpoint':'6610e439','changed_parts':changed,
    'parts_added':[],'parts_removed':[],'existing_objects_and_mesh_datablocks_retained':True,
    'object_transforms_and_modifier_stacks_retained':True,'modifiers_applied':False,
    'all_unclaimed_parts_unchanged':True,'hull_faces_removed_for_opening':64,
    'hull_aperture_wall_faces_added':16,'hull_boundary_vertices_moved':34,
    'unused_hull_interior_vertices_removed':removed_interior,
    'pod_halves_share_world_equator':True,'legacy_mesh_used':False},indent=2))
result={'saved':str(source),'changed_parts':changed,'hull_opening':True}
