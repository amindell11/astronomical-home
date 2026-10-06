from pathlib import Path
import bpy,bmesh,json,sys,math,hashlib
from mathutils import Vector,Matrix
root=Path('D:/amind/git/agent-1');base=root/'results/nightshade-glass-pod';out=base/'round-02'
out.mkdir(parents=True,exist_ok=True);source=root/'art/ships/nightshade/Nightshade.blend'
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
check=json.loads((base/'pre-taper/ship_check.json').read_text())
before=ship_source.fingerprint(bpy.context.scene)
assert bpy.context.mode=='OBJECT' and before==check['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==check['source_sha256']
old=json.loads(Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba/pod-owner-geometry.json').read_text())
names=list(old);original={n:{'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
    'faces':[tuple(p.vertices) for p in bpy.data.objects[n].data.polygons],
    'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers]} for n in names}
def worldverts(name):
    m=Matrix(old[name]['matrix']);return [m@Vector(v) for v in old[name]['vertices']]
up=worldverts('Upper canopy glazing');lo=worldverts('Lower canopy glazing')
cy=.589
def oldzc(y):return -.028-.15*(y-cy)
def lerp_samples(samples,t):
    for i in range(len(samples)-1):
        a,p=samples[i];b,q=samples[i+1]
        if a<=t<=b:return p.lerp(q,(t-a)/(b-a))
    return samples[0][1].copy() if t<samples[0][0] else samples[-1][1].copy()
rows=[]
for i in range(10):
    u,l=up[i*7],lo[i*7];ue,le=up[i*7+6],lo[i*7+6]
    rows.append((ue.y,Vector((ue.x,u.z,l.z,(ue.z+le.z)/2))))
rows += [(.823,Vector((0,-.069,-.069,-.069))),(.395,Vector((.099,.090,-.126,-.019))),
         (.367,Vector((.039,.030,-.074,-.022))),(.352,Vector((0,-.023,-.023,-.023)))]
rows.sort(key=lambda a:a[0])
for name in ['Upper canopy glazing','Lower canopy glazing']:
    ob=bpy.data.objects[name];inv=ob.matrix_world.inverted();sign=1 if name.startswith('Upper') else -1
    for v in ob.data.vertices:
        p=ob.matrix_world@v.co
        qy=max(-1,min(1,(p.y-cy)/.229));rad=math.sqrt(max(0,1-qy*qy))
        phi=math.atan2(abs(p.x)/.134,abs(p.z-oldzc(p.y))/.112)
        y=.352+(p.y-(cy-.229))/.458*(.823-.352)
        width,top,bottom,center=lerp_samples(rows,y)
        x=1.07*width*math.sin(phi)
        h=max(0,math.cos(phi))**.45
        z=center+(top-center)*h if sign>0 else center+(bottom-center)*h
        v.co=inv@Vector((x,y,z))
    ob.data.update()

boundary=list(range(7))+[r*7+6 for r in range(1,9)]+list(range(69,62,-1))
def path_samples(vs):
    return [(math.atan2(p.x/.140,(p.y-cy)/.219),p) for p in [vs[i] for i in boundary]]
paths={'Upper':path_samples(up),'Lower':path_samples(lo)}
def path(side,t):return lerp_samples(paths[side],max(0,min(math.pi,t)))
def at(side,t,offset,height):
    p=path(side,t);a=path(side,t-.005);b=path(side,t+.005)
    tangent=b-a;normal=Vector((-tangent.y,tangent.x,0)).normalized()
    p+=normal*offset;p.z+=height
    if t<1e-7 or t>math.pi-1e-7:p.x=0
    return p
for side,sign in [('Upper',1),('Lower',-1)]:
    ob=bpy.data.objects[side+' canopy metal bezel'];inv=ob.matrix_world.inverted()
    profile=[(.004,0),(.008,.010),(.023,.010),(.029,.002),(.029,-.009),(.023,-.014),(.008,-.014),(.004,-.007)]
    for i,v in enumerate(ob.data.vertices):
        station,j=divmod(i,8);offset,height=profile[j]
        v.co=inv@at(side,math.pi*station/21,offset,sign*height)
    ob.data.update()
ob=bpy.data.objects['Upper canopy inner seal'];inv=ob.matrix_world.inverted()
for i,v in enumerate(ob.data.vertices):
    station,j=divmod(i,5);offset,height=[(-.003,-.008),(-.002,-.005),(.002,-.005),(.004,-.013),(-.003,-.013)][j]
    v.co=inv@at('Upper',math.pi*station/21,offset,height)
ob.data.update()
ob=bpy.data.objects['Upper canopy frame'];inv=ob.matrix_world.inverted()
for v in ob.data.vertices:
    p=ob.matrix_world@v.co
    outer=abs((p.x/.148)**2+((p.y-cy)/.225)**2-1)<.001
    ax,ay=(.148,.225) if outer else (.129,.208)
    angle=math.atan2(p.x/ax,(p.y-cy)/ay)
    v.co=inv@at('Upper',angle,.018 if outer else .004,-.014 if outer else -.020)
ob.data.update()
ob=bpy.data.objects['Rebuilt pitched hull'];inv=ob.matrix_world.inverted();moved=[]
for v in ob.data.vertices:
    p=ob.matrix_world@v.co
    on_ring=abs((p.x/.148)**2+((p.y-cy)/.225)**2-1)<.0001
    offset=p.z-oldzc(p.y)
    if on_ring and abs(abs(offset)-.036)<1e-5:
        side='Upper' if offset>0 else 'Lower';sign=1 if offset>0 else -1
        angle=math.atan2(p.x/.148,(p.y-cy)/.225)
        v.co=inv@at(side,angle,.021,-sign*.010);moved.append(v.index)
assert len(moved)==34
ob.data.update()
for name in names:
    ob=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
    assert [list(r) for r in ob.matrix_basis]==original[name]['matrix']
    assert sorted(tuple(sorted(p.vertices)) for p in ob.data.polygons)==sorted(tuple(sorted(f)) for f in original[name]['faces'])
    assert [(m.name,m.type) for m in ob.modifiers]==original[name]['mods']
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'all_unclaimed_parts_unchanged':True,
    'topology_transforms_and_modifiers_retained':True,'hull_boundary_vertices_moved':moved,
    'direction':'Restore the owner checkpoint tapered canopy profile, retaining pod volume and open cradle',
    'legacy_mesh_used':False},indent=2))
result={'saved':str(source),'taper_restored':True,'changed_parts':changed}
