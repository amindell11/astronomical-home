from pathlib import Path
import bpy,bmesh,json,sys,math,hashlib
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');scratch=Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba')
source=root/'art/ships/nightshade/Nightshade.blend';out=root/'results/nightshade-detail-01/round-01';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((root/'results/nightshade-detail-01/precheck/ship_check.json').read_text())
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
before=ship_source.fingerprint(bpy.context.scene);assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
origin=bpy.data.objects['SymmetryOrigin'];old_names=set(before['parts'])
old_visibility={o.name:o.hide_get() for o in bpy.context.scene.objects}
hidden=['Upper small swept fins','Upper canopy frame','Upper canopy inner seal','Lower canopy metal bezel']
for n in hidden:
    ob=bpy.data.objects[n];assert ob.hide_get()
    bpy.data.collections['ignore'].objects.link(ob)
    for c in list(ob.users_collection):
        if c.name.startswith('role.'):c.objects.unlink(ob)
bpy.data.collections['role.hull'].objects.link(bpy.data.objects['Upper small swept fins.001'])
editing_origin=bpy.data.objects['SymmetryOrigin.001'];assert editing_origin.matrix_world==Matrix.Identity(4)
editing_origin.parent=origin
bpy.context.view_layer.update()
points={n:[bpy.data.objects[n].matrix_world@v.co for v in bpy.data.objects[n].data.vertices]
        for n in old_names if bpy.data.objects[n].type=='MESH'}
dep=bpy.context.evaluated_depsgraph_get();trees={}
def surface(name,p,sign=1):
    if name not in trees:
        eo=bpy.data.objects[name].evaluated_get(dep);m=eo.to_mesh()
        trees[name]=BVHTree.FromPolygons([eo.matrix_world@v.co for v in m.vertices],[list(f.vertices) for f in m.polygons]);eo.to_mesh_clear()
    hit=trees[name].ray_cast(Vector((p.x,p.y,sign*2)),Vector((0,0,-sign)),4)[0]
    assert hit is not None,(name,tuple(p))
    return hit
def newmat(name,color):
    assert name not in bpy.data.materials
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);return m
armor=newmat('Detail machined armor',(.104,.128,.187))
edge=newmat('Detail edge metal',(.155,.180,.236))
dark=bpy.data.materials['Clean mechanical recess']
slate=bpy.data.materials['Clean armor slate']
created=[]
def meshpart(name,verts,faces,mats,indices=None,role='hull',thickness=.001):
    assert name not in bpy.data.objects
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();m.uv_layers.new(name='PaintUV')
    for mat in mats:m.materials.append(mat)
    if indices:
        for f,i in zip(m.polygons,indices):f.material_index=i
    ob=bpy.data.objects.new(name,m);bpy.data.collections['role.'+role].objects.link(ob);ob.parent=origin
    mirror=ob.modifiers.new('Live X symmetry','MIRROR');mirror.mirror_object=origin;mirror.use_clip=True;mirror.use_mirror_merge=True
    if thickness:
        shell=ob.modifiers.new('Editable detail thickness','SOLIDIFY');shell.thickness=thickness;shell.offset=-1
    created.append(name);return ob
seam_v=[];seam_f=[]
def param(name,stride,cross,r,u):
    a=int(math.floor(r));t=r-a;c=cross[0]+(cross[1]-cross[0])*u;k=int(math.floor(c));s=c-k
    ps=points[name]
    def row(i):return ps[i*stride+k].lerp(ps[i*stride+min(k+1,stride-1)],s)
    p=row(a).lerp(row(a+1),t) if t>1e-8 else row(a)
    return p
def strip(name,target,start,end,low,high,stride=14,cross=(1,5),sign=1,lift=.0045,mat=armor):
    assert end>start+.25
    rows=sorted(set([start,start+.10,end-.10,end]+[float(i) for i in range(math.ceil(start+.11),math.floor(end-.11)+1)]))
    vs=[];fs=[];mi=[]
    for i,r in enumerate(rows):
        for j,u in enumerate([0,.045,.5,.955,1]):
            inset=.055 if i in (0,len(rows)-1) else 0
            v=low+(high-low)*(inset+(1-2*inset)*u)
            p=surface(target,param(target,stride,cross,r,v),sign)
            p.z+=sign*(.0018 if i in (0,len(rows)-1) or j in (0,4) else lift)
            vs.append(tuple(p))
    for i in range(len(rows)-1):
        for j in range(4):
            f=(i*5+j,(i+1)*5+j,(i+1)*5+j+1,i*5+j+1)
            if sign<0:f=tuple(reversed(f))
            fs.append(f);mi.append(1 if j in (0,3) or i in (0,len(rows)-2) else 0)
    meshpart(name,vs,fs,[mat,edge],mi)
    sv=[]
    for i,r in enumerate(rows):
        rr=r+(-.025 if i==0 else (.025 if i==len(rows)-1 else 0))
        for u in (max(0,low-.012),min(1,high+.012)):
            p=surface(target,param(target,stride,cross,rr,u),sign);p.z+=sign*.0008;sv.append(tuple(p))
    off=len(seam_v);seam_v.extend(sv)
    for i in range(len(rows)-1):
        f=(off+i*2,off+(i+1)*2,off+(i+1)*2+1,off+i*2+1)
        seam_f.append(f if sign>0 else tuple(reversed(f)))
wing='Rebuilt main wing blades'
strip('Wing forward armor',wing,2.5,10.4,.12,.65)
strip('Wing shoulder armor',wing,10.58,17.65,.13,.66)
strip('Wing outer armor',wing,5.3,12.2,.73,.92,lift=.0032,mat=slate)
strip('Wing aft blade armor',wing,18.15,25.35,.14,.88)
strip('Wing ventral armor',wing,4.5,16.6,.18,.78,cross=(12,8),sign=-1,mat=slate)
strip('Wing ventral aft armor',wing,18.3,24.8,.18,.84,cross=(12,8),sign=-1,mat=slate)
strip('Upper fin armor','Upper small swept fins.001',1.6,10.7,.22,.77,lift=.003,mat=slate)
strip('Lower fin armor','Lower small swept fins',1.6,11.8,.22,.77,cross=(12,8),sign=-1,lift=.003,mat=slate)
tail='Rear swept wing pair'
strip('Tail root armor',tail,1.8,4.6,.16,.78,stride=12,cross=(1,4),lift=.0035,mat=slate)
strip('Tail fork armor',tail,4.85,8.8,.18,.77,stride=12,cross=(1,4),lift=.0035)
strip('Tail tip armor',tail,9.08,11.65,.20,.76,stride=12,cross=(1,4),lift=.0028,mat=slate)
strip('Tail ventral armor',tail,4.85,8.8,.18,.77,stride=12,cross=(10,7),sign=-1,lift=.003,mat=slate)
spine='Aft spine armor'
strip('Spine shoulder armor',spine,.7,2.4,.10,.86,stride=8,cross=(0,3),lift=.0035)
strip('Spine aft armor',spine,2.57,5.3,.12,.83,stride=8,cross=(0,3),lift=.003)
meshpart('Panel joint beds',seam_v,seam_f,[dark],thickness=.0005)
# Broad cooling slots and short louvers sit inside the outer wing panels.
vent_v=[];vent_f=[];vent_i=[]
for j in range(4):
    r=13.0+j*.8
    patch=[]
    for rr,u in [(r,.73),(r+.48,.74),(r+.48,.925),(r,.91)]:
        p=surface(wing,param(wing,14,(1,5),rr,u));p.z+=.0018;patch.append(p)
    off=len(vent_v);vent_v.extend(tuple(p) for p in patch);vent_f.append(tuple(off+i for i in range(4)));vent_i.append(0)
    for rr in (r+.12,r+.29):
        q=[]
        for t,u,h in [(rr,.755,.005),(rr+.065,.755,.0025),(rr+.065,.90,.0025),(rr,.90,.005)]:
            p=surface(wing,param(wing,14,(1,5),t,u));p.z+=h;q.append(tuple(p))
        off=len(vent_v);vent_v.extend(q);vent_f.append(tuple(off+i for i in range(4)));vent_i.append(1)
meshpart('Wing cooling louvers',vent_v,vent_f,[dark,edge],vent_i,thickness=.0007)
# Angular clamps and service panels follow the existing metal surfaces.
def plate(name,target,xy,sign=1,lift=.004):
    center=sum((Vector((x,y,0)) for x,y in xy),Vector())/len(xy)
    verts=[]
    for scale,height in [(1,.001),(.84,lift)]:
        for x,y in xy:
            p=center+(Vector((x,y,0))-center)*scale;p=surface(target,p,sign);p.z+=sign*height;verts.append(tuple(p))
    p=surface(target,center,sign);p.z+=sign*lift;verts.append(tuple(p));n=len(xy)
    faces=[];idx=[]
    for i in range(n):
        f=(i,(i+1)%n,(i+1)%n+n,i+n);g=(i+n,(i+1)%n+n,2*n)
        faces.extend([f if sign>0 else tuple(reversed(f)),g if sign>0 else tuple(reversed(g))]);idx.extend([1,0])
    return meshpart(name,verts,faces,[armor,edge],idx)
plate('Cockpit shoulder latch','Rebuilt pitched hull',[(.178,.469),(.198,.461),(.215,.480),(.205,.502),(.183,.504)])
plate('Cockpit forward latch','Rebuilt pitched hull',[(.110,.753),(.116,.741),(.126,.744),(.123,.757),(.116,.762)])
plate('Tail mount service panel','Tail root mounting blocks',[(.17,.145),(.257,.163),(.269,.206),(.221,.221),(.18,.195)])
plate('Dorsal bridge service cover','Dorsal central blade',[(.022,.02),(.052,.011),(.068,.055),(.050,.098),(.024,.095)])
# The exhaust insert is separate from the owner-authored collar.
vs=[];fs=[];mi=[];zc=.110
for y,rx,rz in [(-.496,.043,.017),(-.505,.040,.016),(-.5055,.030,.0115),(-.499,.029,.0105)]:
    for k in range(9):
        a=-math.pi/2+math.pi*k/8;vs.append((0 if k in (0,8) else rx*math.cos(a),y,zc+rz*math.sin(a)))
for r in range(3):
    for k in range(8):fs.append((r*9+k,r*9+k+1,(r+1)*9+k+1,(r+1)*9+k));mi.append(0 if r<2 else 1)
meshpart('Main exhaust throat',vs,fs,[edge,dark],mi,thickness=.001)
vs=[(0,-.4995,zc)]+[(0 if k in (0,8) else .029*math.cos(-math.pi/2+math.pi*k/8),-.4995,zc+.0105*math.sin(-math.pi/2+math.pi*k/8)) for k in range(9)]
meshpart('Main exhaust core',vs,[(0,k+1,k+2) for k in range(8)],[dark],role='cores',thickness=0)
socket=bpy.data.objects.new('Engine.Main',None);bpy.data.collections['role.sockets'].objects.link(socket);socket.parent=origin
socket.location=(0,-.507,zc);socket.empty_display_type='ARROWS';socket.empty_display_size=.03;created.append(socket.name)
bpy.context.view_layer.update()
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in old_names if before['parts'][n]!=after['parts'][n]]
assert changed==['SymmetryOrigin.001'],changed
assert set(after['parts'])-old_names==set(created)
assert all(bpy.data.objects[n].hide_get()==v for n,v in old_visibility.items())
audit={'approved_source':'124a71cf4c050b2232d6f19b815233980fa9fa97','changed_existing_parts':changed,
       'new_parts':created,'existing_mesh_geometry_materials_transforms_modifiers_unchanged':True,
       'old_visibility_preserved':True,'role_assignments':{'ignored_hidden_parts':hidden,'hull':['Upper small swept fins.001']},
       'parent_change':'SymmetryOrigin.001 parented to SymmetryOrigin, both identity; world transforms preserved',
       'moving_assemblies':'None in this detail candidate; straight forks remain rigid.'}
(out/'edit-audit.json').write_text(json.dumps(audit,indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
result={'saved':str(source),'new_parts':created,'changed_existing_parts':changed}
