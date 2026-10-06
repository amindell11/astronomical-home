from pathlib import Path
import bpy,json,sys,math,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-cockpit-frame-01/round-01';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((root/'results/nightshade-cockpit-frame-01/precheck/ship_check.json').read_text())
assert bpy.context.mode=='OBJECT' and Path(bpy.data.filepath).resolve()==source.resolve()
before=ship_source.fingerprint(bpy.context.scene);assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
origin=bpy.data.objects['SymmetryOrigin'];hull=bpy.data.objects['Rebuilt pitched hull']
vertices=[hull.matrix_world@v.co for v in hull.data.vertices]
tree=BVHTree.FromPolygons(vertices,[list(p.vertices) for p in hull.data.polygons])
ids={'Upper':[34,35,36,37,38,51,60,69,78,87,96,105],
     'Lower':[50,49,48,47,46,59,68,77,86,95,104,113]}
paths={side:[vertices[i] for i in items] for side,items in ids.items()}
created=[];armor=bpy.data.materials['Detail machined armor'];edge=bpy.data.materials['Detail edge metal'];dark=bpy.data.materials['Clean mechanical recess'];slate=bpy.data.materials['Clean armor slate']
def part(name,vs,fs,mats,indices,thickness=.0008):
    assert name not in bpy.data.objects
    m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update();m.uv_layers.new(name='PaintUV')
    for mat in mats:m.materials.append(mat)
    for f,i in zip(m.polygons,indices):f.material_index=i
    ob=bpy.data.objects.new(name,m);bpy.data.collections['role.hull'].objects.link(ob);ob.parent=origin
    mod=ob.modifiers.new('Live X symmetry','MIRROR');mod.mirror_object=origin;mod.use_clip=True
    mod=ob.modifiers.new('Editable frame thickness','SOLIDIFY');mod.thickness=thickness;mod.offset=-1
    created.append(name);return ob
def curve(side,t):
    ps=paths[side];i=min(int(t),len(ps)-2);u=t-i
    a=ps[i-1] if i>0 else ps[0]*2-ps[1]
    b=ps[i];c=ps[i+1];d=ps[i+2] if i+2<len(ps) else ps[-1]*2-ps[-2]
    p=(2*b+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)*.5
    if t==0:p.x=0
    return p
def frame(side,t):
    p=curve(side,t);d=curve(side,min(10.85,t+.01))-curve(side,max(0,t-.01));d.z=0;d.normalize()
    return p,Vector((-d.y,d.x,0))
def skin(p,sign):
    hit=tree.ray_cast(Vector((p.x,p.y,sign*2)),Vector((0,0,-sign)),4)[0]
    assert hit is not None,tuple(p)
    return hit
def band(name,side,segments,section,mats,face_materials,project=True,nose_taper=False):
    vs=[];fs=[];indices=[];sign=1 if side=='Upper' else -1
    for start,end in segments:
        count=max(3,math.ceil((end-start)*6));steps=[start+(end-start)*i/count for i in range(count+1)]
        off=len(vs);stride=len(section)
        for t in steps:
            p,n=frame(side,t)
            for distance,height in section:
                if nose_taper:distance*=.68+.32*min(1,max(0,(t-4)/2))
                q=p+n*distance
                if project:q=skin(q,sign)
                q.z+=sign*height
                if t==0:q.x=0
                vs.append(tuple(q))
        for i in range(count):
            for k in range(stride-1):
                f=(off+i*stride+k,off+(i+1)*stride+k,off+(i+1)*stride+k+1,off+i*stride+k+1)
                fs.append(f if sign>0 else tuple(reversed(f)));indices.append(face_materials[k])
    return part(name,vs,fs,mats,indices)
for side in ['Upper','Lower']:
    band('Cockpit '+side.lower()+' glazing gasket',side,[(0,10.70)],[(-.0022,.0008),(.0015,.0008)],[dark],[0],project=False)
    band('Cockpit '+side.lower()+' retaining rail',side,[(0,4.45),(4.56,8.05),(8.16,10.68)],
        [(.0017,.0015),(.0034,.0038),(.008,.0038),(.010,.0012)],[armor,edge],[1,0,1])
# Segmented plates sit on the metal cradle, outside the glass-retaining rail.
band('Cockpit upper cradle plating','Upper',[(.12,3.55),(3.76,6.65),(6.88,9.95)],
    [(.018,.001),(.020,.0032),(.026,.0032),(.029,.001)],[slate,edge],[1,0,1],nose_taper=True)
band('Cockpit lower cradle plating','Lower',[(.12,3.55),(3.76,6.65),(6.88,9.95)],
    [(.017,.001),(.019,.0027),(.023,.0027),(.026,.001)],[slate,edge],[1,0,1],nose_taper=True)
# A stepped collar follows the existing bridge instead of widening the glass pod.
bridge=bpy.data.objects['Dorsal central blade'];ps=[bridge.matrix_world@v.co for v in bridge.data.vertices]
bridge_tree=BVHTree.FromPolygons(ps,[list(f.vertices) for f in bridge.data.polygons])
def bridge_point(r,u,height):
    a=int(r);t=r-a;c=u*3;k=int(c);s=c-k
    def row(i):return ps[i*8+k].lerp(ps[i*8+min(k+1,3)],s)
    p=row(a).lerp(row(a+1),t)
    hit=bridge_tree.ray_cast(Vector((p.x,p.y,2)),Vector((0,0,-1)),4)[0];assert hit is not None
    hit.z+=height;return tuple(hit)
vs=[];fs=[];mi=[];rows=[.10,.22,.60,1.0,1.45,1.75,1.85]
for i,r in enumerate(rows):
    for j,u in enumerate([0,.08,.85,.95]):
        vs.append(bridge_point(r,u,.002 if i in (0,len(rows)-1) or j==3 else .007))
for i in range(len(rows)-1):
    for k in range(3):
        fs.append((i*4+k,(i+1)*4+k,(i+1)*4+k+1,i*4+k+1));mi.append(1 if i in (0,len(rows)-2) or k==2 else 0)
part('Cockpit rear retaining collar',vs,fs,[armor,edge],mi,.0015)
vs=[bridge_point(r,u,.0076) for r,u in [(.55,.28),(.98,.28),(.98,.60),(.55,.60)]]
part('Cockpit collar latch recess',vs,[(0,1,2,3)],[dark],[0],.0004)
vs=[bridge_point(r,u,h) for r,u,h in [(.67,.31,.009),(.76,.31,.0079),(.76,.56,.0079),(.67,.56,.009)]]
part('Cockpit collar latch tongue',vs,[(0,1,2,3)],[edge],[0],.0005)
bpy.context.view_layer.update();after=ship_source.fingerprint(bpy.context.scene)
assert all(before['parts'][n]==after['parts'][n] for n in before['parts'])
assert set(after['parts'])-set(before['parts'])==set(created)
(out/'edit-audit.json').write_text(json.dumps({'checkpoint':'f12664a4d3c6fbc1e599c0df0f904d2aa58aba88',
    'new_parts':created,'existing_parts_changed':[],'all_original_part_fingerprints_unchanged':True,
    'glass_shape_and_side_exposure_preserved':True,'frame_parts_have_live_mirror_and_thickness_modifiers':True},indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
result={'saved':str(source),'new_frame_parts':created}
