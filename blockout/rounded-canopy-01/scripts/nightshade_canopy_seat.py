from pathlib import Path
import bpy,json,sys,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
scratch=Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba')
out=root/'results/nightshade-rounded-canopy/round-02';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
assert bpy.context.mode=='OBJECT' and Path(bpy.data.filepath).resolve()==source.resolve() and not bpy.data.is_dirty
before=ship_source.fingerprint(bpy.context.scene)
names=['Rebuilt pitched hull','Upper canopy frame','Upper canopy inner seal','Lower canopy metal bezel']
records={n:{'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
    'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers],
    'visible':not bpy.data.objects[n].hide_get(),
    'vertices':[tuple(v.co) for v in bpy.data.objects[n].data.vertices],
    'faces':[tuple(p.vertices) for p in bpy.data.objects[n].data.polygons]} for n in names}
hull=bpy.data.objects['Rebuilt pitched hull'];inv=hull.matrix_world.inverted()
old=[hull.matrix_world@v.co for v in hull.data.vertices];delta={}
indices={'Upper':[34,35,36,37,38,51,60], 'Lower':[50,49,48,47,46,59,68]}
angles=[0,.1,.2,.3,.4,math.acos((.744-.589)/.225),math.acos((.694-.589)/.225)]
dep=bpy.context.evaluated_depsgraph_get()
for side,ids in indices.items():
    eo=bpy.data.objects[side+' canopy glazing'].evaluated_get(dep);m=eo.to_mesh()
    tree=BVHTree.FromPolygons([eo.matrix_world@v.co for v in m.vertices],[list(p.vertices) for p in m.polygons]);eo.to_mesh_clear()
    for j,idx in enumerate(ids[:-1]):
        p=old[idx]
        if j<5:
            hit=tree.ray_cast(Vector((p.x,.9,p.z)),Vector((0,-1,0)),1)[0]
            assert hit is not None,(side,idx)
            q=hit+Vector((0,.0015,0))
        else:
            hit=tree.ray_cast(Vector((.5,p.y,p.z)),Vector((-1,0,0)),1)[0]
            assert hit is not None,(side,idx)
            q=hit+Vector((.0015,0,0))
        q.z=p.z;delta[idx]=q-p
    delta[ids[-1]]=Vector()
for v in hull.data.vertices:
    if v.index in delta:v.co=inv@(old[v.index]+delta[v.index])
neighbors={i:[] for i in range(len(old))}
for e in hull.data.edges:
    a,b=e.vertices;neighbors[a].append(b);neighbors[b].append(a)
for idx in range(388,422):
    ends=[i for i in neighbors[idx] if i<388]
    if len(ends)==2 and any(i in delta for i in ends):
        d=sum((delta.get(i,Vector()) for i in ends),Vector())*.5
        hull.data.vertices[idx].co=inv@(old[idx]+d)
hull.data.update()
def shift(side,angle):
    for j in range(len(angles)-1):
        if angles[j]<=angle<=angles[j+1]:
            return delta[indices[side][j]].lerp(delta[indices[side][j+1]],(angle-angles[j])/(angles[j+1]-angles[j]))
    return Vector()
for name,side,stride in [('Upper canopy inner seal','Upper',5),('Lower canopy metal bezel','Lower',8)]:
    ob=bpy.data.objects[name];iv=ob.matrix_world.inverted()
    for s in range(22):
        d=shift(side,math.pi*s/21)
        if d.length<1e-8:continue
        for k in range(stride):
            v=ob.data.vertices[s*stride+k];v.co=iv@(ob.matrix_world@v.co+d)
    ob.data.update()
ob=bpy.data.objects['Upper canopy frame'];iv=ob.matrix_world.inverted()
retained=[i for i in range(70) if not(2<=i//7<=7 and i%7<5)]
paths=[list(range(7))+[r*7+6 for r in range(1,9)]+list(range(69,62,-1)),
       list(range(7,13))+[r*7+5 for r in range(2,8)]+list(range(61,55,-1))]
for path in paths:
    for j,idx in enumerate(path):
        d=shift('Upper',math.pi*j/(len(path)-1))
        if d.length<1e-8:continue
        v=ob.data.vertices[retained.index(idx)];v.co=iv@(ob.matrix_world@v.co+d)
ob.data.update()
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert set(changed)==set(names) and set(before['parts'])==set(after['parts'])
audit={}
for n,record in records.items():
    ob=bpy.data.objects[n]
    assert [list(r) for r in ob.matrix_basis]==record['matrix']
    assert [(m.name,m.type) for m in ob.modifiers]==record['mods'] and (not ob.hide_get())==record['visible']
    assert [tuple(p.vertices) for p in ob.data.polygons]==record['faces']
    audit[n]=[v.index for v in ob.data.vertices if tuple(v.co)!=record['vertices'][v.index]]
assert all(tuple(hull.data.vertices[i].co)==records[hull.name]['vertices'][i] for i in range(34))
assert all(tuple(hull.data.vertices[i].co)==records[hull.name]['vertices'][i] for i in range(131,388))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'changed_vertices':audit,
    'outer_nose_vertices_unchanged':True,'topology_transforms_live_modifiers_visibility_retained':True,
    'hull_aperture_heights_retained':True},indent=2))
result={'saved':str(source),'changed_parts':changed,'changed_vertices':audit}
