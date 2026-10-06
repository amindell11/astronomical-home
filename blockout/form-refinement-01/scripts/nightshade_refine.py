from pathlib import Path
import bpy, bmesh, json, math, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path('D:/amind/git/agent-1')
source = root/'art/ships/nightshade/Nightshade.blend'
out = root/'results/nightshade-form-refinement/round-01'
out.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(root/'art/tools/ship'))
import ship_source
bpy.ops.wm.open_mainfile(filepath=str(source))
baseline = json.loads((out.parent/'precheck/ship_check.json').read_text())['fingerprint']
assert ship_source.fingerprint(bpy.context.scene) == baseline
before = {o.name: {'xyz': np.array([v.co[:] for v in o.data.vertices]),
                     'faces': [tuple(p.vertices) for p in o.data.polygons],
                     'modifiers': [(m.name, m.type) for m in o.modifiers]}
          for o in bpy.context.scene.objects if o.type == 'MESH'}
edits = {}
origin = bpy.data.objects['SymmetryOrigin']

def smooth(t):
    t = min(1, max(0, t))
    return t*t*(3-2*t)

def coords(name, n):
    ob = bpy.data.objects[name]
    assert len(ob.data.vertices) % n == 0
    return ob, np.array([v.co[:] for v in ob.data.vertices]).reshape(-1, n, 3)

def update(ob, co, reason):
    for v, p in zip(ob.data.vertices, co.reshape(-1, 3)): v.co = p
    ob.data.update()
    edits[ob.name] = reason

ob, co = coords('Rebuilt main wing blades', 14)
wing_old = co.copy()
tip_rows = {23: (-.50, .681, .792), 24: (-.60, .720, .824),
            25: (-.66, .735, .845), 26: (-.715, .748, .846),
            27: (-.748, .770, .831)}
for j, (y, inside, outside) in tip_rows.items():
    lo, hi = co[j, :, 0].min(), co[j, :, 0].max()
    for k in range(14):
        x = inside + (co[j,k,0]-lo)/(hi-lo)*(outside-inside)
        co[j,k,2] += -.145*(x-co[j,k,0]) - .014*smooth((-y-.48)/.268)
        co[j,k,0], co[j,k,1] = x, y
update(ob, co, 'Flared angular tip cap; maximum added downward bend 0.014 m; unchanged main wing roots')

ob, co = coords('Sculpted compact tail pair', 10)
tail_old = co.copy()
centers = co.mean(axis=1)
for j, ring in enumerate(co):
    center = centers[j].copy()
    y = center[1]
    root_blend = smooth((y+.37)/.515)
    blade = smooth((-y-.15)/.33)
    tip = smooth((-y-.87)/.14)
    width_scale = 1.30 + .50*root_blend - .15*tip
    height_scale = 1.22 + .14*root_blend - .15*tip
    tangent = centers[min(j+1,len(co)-1)]-centers[max(j-1,0)]
    side = np.array([-tangent[1], tangent[0], 0.0]); side /= np.linalg.norm(side)
    delta = ring-center
    across = delta @ side
    delta += np.outer(across*(width_scale-1), side)
    delta[:,2] *= height_scale
    center[0] -= .057*root_blend
    center[1] += .080*root_blend
    center[2] += .014*root_blend - .004*blade
    co[j] = center+delta
update(ob, co, 'Broader blade and root cross-sections; roots extend into hull shoulders; tail centerline remains low')

fin_rows = {0:(.448,.194,.220), 1:(.40,.192,.282), 2:(.36,.125,.309),
            3:(.32,.044,.319), 4:(.28,.009,.324), 5:(.24,0,.330),
            6:(.20,0,.338), 7:(.16,.087,.345), 8:(.12,.201,.352),
            9:(.10,.224,.358), 10:(.05,.238,.367), 11:(0,.243,.380),
            12:(-.05,.244,.388), 13:(-.115,.251,.389),
            14:(-.174,.282,.383), 15:(-.205,.318,.374)}
for lower in (False, True):
    name = ('Lower' if lower else 'Upper')+' small swept fins'
    ob, co = coords(name,14)
    if lower: co[:,:,2] = -co[:,:,2]-.016
    for j,(y,lo,hi) in fin_rows.items():
        x0,x1 = co[j,:,0].min(),co[j,:,0].max()
        mid = (co[j,1:6,2].mean()+co[j,8:13,2].mean())/2
        for k in range(14):
            x = lo+(co[j,k,0]-x0)/(x1-x0)*(hi-lo)
            co[j,k,2] = mid+(co[j,k,2]-mid)*1.16-.15*(x-co[j,k,0])
            co[j,k,0],co[j,k,1] = x,y
    if lower: co[:,:,2] = -co[:,:,2]-.016
    update(ob,co,'More decisive swept leading edge, broader chisel tip and thicker armor section')

ob = bpy.data.objects['Rebuilt pitched hull']
co = np.array([v.co[:] for v in ob.data.vertices])
for j in range(26):
    ring = co[j*17:(j+1)*17]
    y = ring[0,1]
    amount = .010*smooth((.78-y)/.23)*smooth((y+.42)/.25)
    for k in range(17):
        ring[k,2] += amount*math.cos(math.pi*k/16)
    if .40 < y < .79:
        old = ring.copy()
        for a,b in zip([0,4,7,9,12,16],[4,7,9,12,16]):
            for k in range(a+1,b): ring[k] = old[a]+(old[b]-old[a])*(k-a)/(b-a)
for p in ob.data.polygons:
    y = sum(co[i,1] for i in p.vertices)/len(p.vertices)
    p.use_smooth = bool(y>.79)
update(ob,co,'Stronger angular hull shoulders and modest depth increase; top-view width retained')
body_tree = BVHTree.FromPolygons([v.co for v in ob.data.vertices],[list(p.vertices) for p in ob.data.polygons])

def hull_z(x,y,lower=False):
    hit,_,_,_ = body_tree.ray_cast(Vector((x,y,-.7 if lower else .7)),Vector((0,0,1 if lower else -1)),1.4)
    assert hit is not None,(x,y,lower)
    return hit.z

palette = [bpy.data.materials[n] for n in ('Clean armor slate','Clean armor face','Clean armor bevel','Clean mechanical recess')]
new_parts = []
def new_ring(name, path, profile, lower=False):
    assert name not in bpy.data.objects
    verts=[]; count=len(profile); sign=-1 if lower else 1
    for j, p in enumerate(path):
        prev = path[j-1] if j else Vector((-path[1].x,path[1].y,path[1].z))
        nxt = path[j+1] if j+1<len(path) else Vector((-path[-2].x,path[-2].y,path[-2].z))
        a=Vector((p.x-prev.x,p.y-prev.y));a.normalize()
        b=Vector((nxt.x-p.x,nxt.y-p.y));b.normalize()
        normal=Vector((-(a.y+b.y),a.x+b.x));normal.normalize()
        for width,height in profile:
            x=p.x+normal.x*width;y=p.y+normal.y*width
            if j in (0,len(path)-1):x=0
            verts.append((x,y,p.z+sign*height))
    faces=[(j*count+k,j*count+(k+1)%count,(j+1)*count+(k+1)%count,(j+1)*count+k)
           for j in range(len(path)-1) for k in range(count)]
    mesh=bpy.data.meshes.new(name+' cage');mesh.from_pydata(verts,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
    new=bpy.data.objects.new(name,mesh);bpy.data.collections['role.hull'].objects.link(new);new.parent=origin
    for mat in palette:mesh.materials.append(mat)
    for p in mesh.polygons:
        k=p.index%count
        p.material_index=1 if k in (1,2) else 2 if k in (0,3,4) else 3
        p.use_smooth=False
    mirror=new.modifiers.new('Live X symmetry','MIRROR');mirror.mirror_object=origin;mirror.use_clip=True;mirror.merge_threshold=.00001
    new_parts.append(name)
    return new

for lower in (False,True):
    prefix='Lower' if lower else 'Upper';sign=-1 if lower else 1
    glass,co=coords(prefix+' canopy glazing',7)
    for j,ring in enumerate(co):
        y=ring[0,1]
        foreaft=math.sin(math.pi*j/(len(co)-1))
        for k in range(7):
            ring[k,0]*=.955
            ring[k,2]=hull_z(ring[k,0],y,lower)+sign*(.009+.022*foreaft*(1-(k/6)**2))
    update(glass,co,'Single continuous curved glass piece seated inside a separate metal frame')
    for m in glass.modifiers:
        if m.type=='SOLIDIFY':m.thickness=.008;m.offset=-1
    # The existing underlay stays as the recessed frame seat, preserving its topology.
    frame,seat=coords(prefix+' canopy frame',7)
    for j,ring in enumerate(seat):
        y=ring[0,1]
        for k in range(7):
            ring[k,2]=hull_z(ring[k,0],y,lower)+sign*.003
    update(frame,seat,'Existing frame underlay becomes the recessed canopy seat')
    for m in frame.modifiers:
        if m.type=='SOLIDIFY':m.thickness=.010
    path=[Vector(p) for p in co[0]]+[Vector(co[j,6]) for j in range(1,len(co))]+[Vector(p) for p in co[-1,5::-1]]
    new_ring(prefix+' canopy metal bezel',path,
             [(-.001,.002),(.001,.006),(.006,.008),(.017,.008),(.023,.001),(.023,-.010),(.001,-.010),(-.001,-.003)],lower)
    gasket=new_ring(prefix+' canopy inner seal',path,
             [(-.004,.001),(-.002,.003),(.001,.003),(.002,-.003),(-.004,-.003)],lower)
    for p in gasket.data.polygons:p.material_index=3

def reproject(name,target,lower=False):
    mesh=bpy.data.objects[target].data
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons])
    ob=bpy.data.objects[name]
    for v in ob.data.vertices:
        hit,_,_,_=tree.ray_cast(Vector((v.co.x,v.co.y,-.6 if lower else .6)),Vector((0,0,1 if lower else -1)),1.2)
        assert hit is not None,(name,v.index)
        v.co.z=hit.z+(-.0008 if lower else .0008)
    ob.data.update();edits[name]='Existing seam re-seated on its edited plate'

reproject('Main wing inset channel','Rebuilt main wing blades')
reproject('Upper fin armor joint','Upper small swept fins')
reproject('Lower fin armor joint','Lower small swept fins',True)

audit={}
for name,old in before.items():
    ob=bpy.data.objects[name];xyz=np.array([v.co[:] for v in ob.data.vertices])
    assert old['faces']==[tuple(p.vertices) for p in ob.data.polygons],name
    assert old['modifiers']==[(m.name,m.type) for m in ob.modifiers],name
    changed=not np.array_equal(old['xyz'],xyz)
    if changed:assert name in edits,name
    audit[name]={'positions_changed':changed,'topology_unchanged':True,
                 'max_vertex_displacement':float(np.linalg.norm(xyz-old['xyz'],axis=1).max())}
fp=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in baseline['parts'] if baseline['parts'][n]!=fp['parts'][n]]
assert not set(changed)-set(edits)
assert set(fp['parts'])-set(baseline['parts'])==set(new_parts)
assert set(baseline['parts'])<=set(fp['parts'])
bpy.context.scene['review']='Blockout refinement: flared subtly dipped wingtips, broader integrated tail, defined armor, seated one-piece canopy'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'form-edit-audit.json').write_text(json.dumps({'edits':edits,'new_parts':new_parts,'parts':audit,
    'original_topology_retained':True,'modifiers_applied':False,'changed_parts':changed,
    'wingtip_added_drop_m':.014},indent=2))
print(json.dumps({'changed':changed,'added':new_parts}))
