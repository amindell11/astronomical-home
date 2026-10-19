import bpy,sys,json,math,hashlib,bmesh
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
stage=Path('D:/amind/git/agent-3/results/bulwark/gameplay/2026-10-19')
scene=bpy.context.scene

before=ship_source.fingerprint(scene)
roles=ship_source.Roles(scene)
visual=set().union(*(roles.members.get(k,set()) for k in ('hull','canopy','cores','ink')))
ev=ship_source.Evaluation(scene)
points=[]
for n in visual:
 m=ev.mesh(n);mat=ev.to_space(n);points.extend(tuple(mat@v.co) for v in m.vertices);bpy.data.meshes.remove(m)
bpy.context.window.scene=scene
bpy.data.scenes.remove(ev.scene)
# Select silhouette support points; retain bilateral collision symmetry.
xy=[(abs(p[0]),p[1]) for p in points];xy += [(-x,y) for x,y in xy]
support=sorted(set(max(xy,key=lambda p:p[0]*math.cos(a)+p[1]*math.sin(a)) for a in [i*math.tau/24 for i in range(24)]))
def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
lo=[];hi=[]
for p in support:
 while len(lo)>1 and cross(lo[-2],lo[-1],p)<=0:lo.pop()
 lo.append(p)
for p in reversed(support):
 while len(hi)>1 and cross(hi[-2],hi[-1],p)<=0:hi.pop()
 hi.append(p)
poly=lo[:-1]+hi[:-1]
zmin=-.9;zmax=.9
verts=[(x,y,z) for z in (zmin,zmax) for x,y in poly];n=len(poly)
faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
mesh=bpy.data.meshes.new('Bulwark collision hull');mesh.from_pydata(verts,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
def coll(name):
 c=bpy.data.collections.get(name)
 if c is None:c=bpy.data.collections.new(name);scene.collection.children.link(c)
 return c
origin=bpy.data.objects['SymmetryOrigin']
o=bpy.data.objects.new('Physics - Collision hull',mesh);coll('role.collider').objects.link(o);o.parent=origin;o.display_type='WIRE';o.hide_render=True;o.hide_viewport=True
sockets={'Primary':(0,5.4,1.7),'Secondary':(0,3.6,3.1),'Exhaust.L':(-1.15,-3.35,2.6),'Exhaust.R':(1.15,-3.35,2.6)}
for name,pos in sockets.items():
 o=bpy.data.objects.new(name,None);coll('role.sockets').objects.link(o);o.parent=origin;o.location=pos;o.empty_display_size=.18;o.hide_render=True;o.hide_viewport=True
for name in visual:
 if name.startswith('Cockpit'):groups=['Cockpit']
 elif name.startswith('Engine'):groups=['Engine.L','Engine.R']
 elif name.startswith('Armor') or name.startswith('Canisters'):groups=['Armor.L','Armor.R']
 elif 'flaps' in name:groups=['Tail.L','Tail.R']
 else:groups=['Central_hull']
 for g in groups:coll('role.debris.'+g).objects.link(bpy.data.objects[name])
after=ship_source.fingerprint(scene)
assert all(after['parts'][k]==v for k,v in before['parts'].items())
(stage/'source-additions.json').write_text(json.dumps({'before':before,'after':after,'collider_polygon':poly,'collider_z':[zmin,zmax],'sockets':sockets,'all_original_part_fingerprints_unchanged':True},indent=2))
baseline=Path(bpy.data.filepath).parent/'layers/baseline/baseline.json'
data=json.loads(baseline.read_text());assert data['geometry']==before['components']['geometry'];data['geometry']=after['components']['geometry'];data['uv']=after['components']['uv'];baseline.write_text(json.dumps(data,indent=2)+'\n')
settings={'schema_version':1,'name':'Bulwark','lifetime':1.4,'fade_duration':.25,'soot':0,'burst_seconds':.12,'burst_fraction':.62,'drift_start':.34,'drift_fraction':.78,'pieces':[]}
for g in ['Central_hull','Cockpit','Engine.L','Engine.R','Armor.L','Armor.R','Tail.L','Tail.R']:
 sign=-1 if g.endswith('.L') else 1
 offset=[0,0,0];spin=[0,0,0];life=.32
 if g=='Cockpit':offset=[0,.5,.2];spin=[15,10,18];life=.6
 if g.startswith('Armor'):offset=[sign*1.8,.2,.25];spin=[20,sign*25,sign*40];life=1.4
 if g.startswith('Engine'):offset=[sign*.8,-2,.15];spin=[30,sign*20,sign*35];life=1.4
 if g.startswith('Tail'):offset=[sign*1.3,-2.3,.35];spin=[25,sign*30,sign*55];life=1.2
 settings['pieces'].append({'id':g,'offset':offset,'spin':spin,'lifetime':life,'delay':0})
(Path(bpy.data.filepath).parent/'breakup.json').write_text(json.dumps(settings,indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result={'original_parts_preserved':True,'collider_triangles':len(mesh.polygons),'sockets':sockets}

