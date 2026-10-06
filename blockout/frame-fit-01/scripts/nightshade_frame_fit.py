from pathlib import Path
import bpy,bmesh,json,math,sys,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
base=root/'results/nightshade-frame-fit';out=base/'round-01';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
check=json.loads((base/'precheck/ship_check.json').read_text())
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
before=ship_source.fingerprint(bpy.context.scene)
assert before==check['fingerprint'] and hashlib.sha256(source.read_bytes()).hexdigest()==check['source_sha256']
names=['Rebuilt pitched hull','Upper canopy frame','Upper canopy inner seal','Lower canopy metal bezel']
records={n:{'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
    'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers],
    'mesh':bpy.data.objects[n].data.as_pointer(),'visible':not bpy.data.objects[n].hide_get(),
    'vertices':[tuple(v.co) for v in bpy.data.objects[n].data.vertices]} for n in names}
assert 'Upper canopy metal bezel' not in bpy.data.objects
hull=bpy.data.objects['Rebuilt pitched hull'];inv=hull.matrix_world.inverted()
original=[hull.matrix_world@v.co for v in hull.data.vertices];points=[p.copy() for p in original]
assert len(points)==388
indices={'Upper':[34,35,36,37,38,51,60,69,78,87,96,105,118,117,116,115,114],
         'Lower':[50,49,48,47,46,59,68,77,86,95,104,113,126,127,128,129,130]}
angles=[i*.1 for i in range(5)]+[math.acos((y-.589)/.225) for y in (.744,.694,.644,.594,.544,.494,.444)]+[math.pi-.4+i*.1 for i in range(5)]
dep=bpy.context.evaluated_depsgraph_get()
for side,ids in indices.items():
    ob=bpy.data.objects[side+' canopy glazing'];eo=ob.evaluated_get(dep);m=eo.to_mesh()
    bvh=BVHTree.FromPolygons([eo.matrix_world@v.co for v in m.vertices],[list(p.vertices) for p in m.polygons])
    eo.to_mesh_clear()
    for j,idx in enumerate(ids):
        p=original[idx]
        if j<5:
            origin=Vector(([0,.018,.036,.055,.073][j],.90,p.z));direction=Vector((0,-1,0));offset=Vector((0,.0035,0))
        elif j<12:
            origin=Vector((.5,p.y,p.z));direction=Vector((-1,0,0));offset=Vector((.0035,0,0))
        else:
            origin=Vector((p.x,.30,p.z));direction=Vector((0,1,0));offset=Vector((0,-.0035,0))
        hit=bvh.ray_cast(origin,direction,1)[0]
        assert hit is not None,(side,idx)
        points[idx]=hit+offset;points[idx].z=p.z

# Broaden the nose cap and blend its shoulders into the owner's lowered side rails.
mid=(original[0].z+original[16].z)/2
half=(original[0].z-original[16].z)/2
for k in range(17):
    p=original[k];q=points[k]
    q.x=p.x/.012*.029;q.y=.8425+.001*(1-(p.x/.012)**2)
    q.z=mid+(p.z-mid)/half*.010
for k in range(17):
    p=original[17+k];q=points[17+k]
    q.x=p.x/.055*.074
    center=(original[17].z+original[33].z)/2
    q.z=center+(p.z-center)*1.07
points[386]=Vector((0,.844,mid))
points[42].x=.115
for r in range(2,11):
    if r==2: ids=list(range(38,47))
    elif r<10: ids=list(range(51+(r-3)*9,60+(r-3)*9))
    else: ids=list(range(118,127))
    top,middle,bottom=points[ids[0]],points[ids[4]],points[ids[8]]
    for j,xy,z in [(1,.28,.12),(2,.67,.42),(3,.96,.75)]:
        q=top.lerp(middle,xy);q.z=top.z+(middle.z-top.z)*z;points[ids[j]]=q
        q=bottom.lerp(middle,xy);q.z=bottom.z+(middle.z-bottom.z)*z;points[ids[8-j]]=q
for side,ids in indices.items():
    assert all(abs(points[i].z-original[i].z)<1e-8 for i in ids)
for i,(v,p) in enumerate(zip(hull.data.vertices,points)):
    if (p-original[i]).length>1e-8:v.co=inv@p
hull.data.update()

# Add two support loops to the existing nose surfaces, retaining all original vertices.
bm=bmesh.new();bm.from_mesh(hull.data);bm.verts.ensure_lookup_table();oldverts=list(bm.verts)
lookup={v:i for i,v in enumerate(oldverts)};edges=[];midpoints=[]
for e in bm.edges:
    a,b=sorted(lookup[v] for v in e.verts)
    if b-a==17 and 0<=a<34:
        edges.append(e);r,k=divmod(a,17)
        p1,p2=oldverts[a].co.copy(),oldverts[b].co.copy()
        if r==0:
            p0=2*p1-p2;p3=oldverts[b+17].co.copy()
        elif 4<=k<=12:
            p0=oldverts[k].co.copy();p3=oldverts[51+k-4].co.copy()
        else:p0=2*p1-p2;p3=2*p2-p1
        curve=(-p0+9*p1+9*p2-p3)/16
        midpoints.append(((p1+p2)/2,curve))
assert len(edges)==34
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
ordered=list(bm.verts)
assert all((v.co-Vector(p)).length<1e-7 for v,p in zip(ordered[:388],[inv@p for p in points]))
newverts=ordered[388:]
assert len(newverts)==34
for v in newverts:
    a,b=min(midpoints,key=lambda pair:(v.co-pair[0]).length)
    assert (v.co-a).length<1e-5
    v.co=b
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(hull.data);bm.free();hull.data.update()

def edge_at(side,angle):
    ids=indices[side]
    for i in range(len(angles)-1):
        if angles[i]<=angle<=angles[i+1]:
            return points[ids[i]].lerp(points[ids[i+1]],(angle-angles[i])/(angles[i+1]-angles[i]))
    return points[ids[0 if angle<0 else -1]].copy()
def frame_at(side,angle):
    p=edge_at(side,angle);a=edge_at(side,max(0,angle-.01));b=edge_at(side,min(math.pi,angle+.01))
    tangent=b-a;tangent.z=0;tangent.normalize();normal=Vector((-tangent.y,tangent.x,0))
    return p,tangent,normal
def ring_fit(name,side,stride,inset,zoffset,zscale):
    ob=bpy.data.objects[name];to_local=ob.matrix_world.inverted()
    old=[ob.matrix_world@v.co for v in ob.data.vertices]
    centers=[sum(old[s*stride:(s+1)*stride],Vector())/stride for s in range(22)]
    for station in range(22):
        angle=math.pi*station/21;p,tangent,normal=frame_at(side,angle)
        p-=normal*inset;p.z+=zoffset
        ta=centers[max(station-1,0)];tb=centers[min(station+1,21)]
        old_t=tb-ta;old_t.z=0;old_t.normalize();old_n=Vector((-old_t.y,old_t.x,0))
        for k in range(stride):
            d=old[station*stride+k]-centers[station]
            q=p+normal*d.dot(old_n)+tangent*d.dot(old_t)+Vector((0,0,d.z*zscale))
            if station in (0,21):q.x=0
            ob.data.vertices[station*stride+k].co=to_local@q
    ob.data.update()
ring_fit('Upper canopy inner seal','Upper',5,.002,0,1)
ring_fit('Lower canopy metal bezel','Lower',8,-.004,-.001,.72)

ob=bpy.data.objects['Upper canopy frame'];to_local=ob.matrix_world.inverted()
retained=[i for i in range(70) if not(2<=i//7<=7 and i%7<5)]
outer=list(range(7))+[r*7+6 for r in range(1,9)]+list(range(69,62,-1))
inner=list(range(7,13))+[r*7+5 for r in range(2,8)]+list(range(61,55,-1))
assert len(retained)==len(ob.data.vertices)==40
for path,inset,z in [(outer,-.002,-.003),(inner,.009,-.004)]:
    for j,oldidx in enumerate(path):
        p,t,n=frame_at('Upper',math.pi*j/(len(path)-1));p-=n*inset;p.z+=z
        if j in (0,len(path)-1):p.x=0
        ob.data.vertices[retained.index(oldidx)].co=to_local@p
ob.data.update()
for name in names[1:]:
    ob=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names) and set(before['parts'])==set(after['parts'])
for n,old in records.items():
    ob=bpy.data.objects[n]
    assert [list(r) for r in ob.matrix_basis]==old['matrix']
    assert [(m.name,m.type) for m in ob.modifiers]==old['mods']
    assert ob.data.as_pointer()==old['mesh'] and (not ob.hide_get())==old['visible']
for i in list(range(131,386))+[387]:
    assert tuple(hull.data.vertices[i].co)==records[hull.name]['vertices'][i]
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'owner_checkpoint':'c1c0a7bd','changed_parts':changed,
    'parts_added':[],'parts_removed':[],'all_unclaimed_parts_unchanged':True,
    'glass_and_fins_unchanged':True,'owner_aperture_boundary_heights_retained':True,
    'nose_support_loops_added':2,'hull_vertices_added':34,'hull_vertices':len(hull.data.vertices),
    'existing_meshes_transforms_live_modifiers_and_visibility_retained':True,
    'hull_aft_of_cockpit_unchanged':True,'deleted_upper_bezel_not_recreated':True},indent=2))
result={'saved':str(source),'changed_parts':changed,'hull_vertices':len(hull.data.vertices)}
