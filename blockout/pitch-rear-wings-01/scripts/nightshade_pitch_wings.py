from pathlib import Path
import bpy, bmesh, json, math, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1')
source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-pitch-rear-wings/round-01'
assert Path(bpy.data.filepath).resolve()==source.resolve()
assert bpy.context.mode=='OBJECT'
assert not bpy.data.is_dirty, 'Owner must save before this edit'
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source
before_fp=ship_source.fingerprint(bpy.context.scene)
assert before_fp==json.loads((out.parent/'precheck/ship_check.json').read_text())['fingerprint']
assert 'Sculpted compact tail pair' not in bpy.data.objects
assert 'Rear swept wing pair' not in bpy.data.objects
before={o.name:{'xyz':np.array([v.co[:] for v in o.data.vertices]),
                'faces':[tuple(p.vertices) for p in o.data.polygons],
                'mods':[(m.name,m.type) for m in o.modifiers]}
        for o in bpy.context.scene.objects if o.type=='MESH'}
out.mkdir(parents=True,exist_ok=True)
claimed={}

def ease(t):
    t=min(1,max(0,t));return t*t*(3-2*t)

body=bpy.data.objects['Rebuilt pitched hull']
native=before[body.name]['xyz'][:442].reshape(26,17,3)
ys=native[:,0,1][::-1]
high=native[:,0,2][::-1];low=native[:,16,2][::-1]

def pitch(p):
    x,y,z=p
    hi=float(np.interp(y,ys,high));lo=float(np.interp(y,ys,low))
    height=max(0,min(1,(z-lo)/(hi-lo)))
    weight=(.45+.55*height)*ease((y+.48)/.36)
    a=math.radians(-5)*weight
    dy,dz=y-.15,z-.05
    return (x,.15+dy*math.cos(a)-dz*math.sin(a),.05+dy*math.sin(a)+dz*math.cos(a))

parts=['Rebuilt pitched hull','Aft spine armor','Dorsal central blade','Dorsal shield cap','Dorsal shield inset',
       'Ventral central blade','Ventral shield cap','Ventral shield inset']
parts += [prefix+' canopy '+suffix for prefix in ('Upper','Lower')
          for suffix in ('frame','glazing','metal bezel','inner seal')]
for name in parts:
    ob=bpy.data.objects[name]
    for v in ob.data.vertices:v.co=pitch(v.co)
    ob.data.update();claimed[name]='Upper body pitched forward five degrees, blended through underside; attachments follow hull'

root_bounds={2:.08,3:0,4:0,5:0,6:0,7:.02,8:.12,9:.15,10:.19,11:.22}
for lower in (False,True):
    name=('Lower' if lower else 'Upper')+' small swept fins'
    ob=bpy.data.objects[name]
    co=np.array([v.co[:] for v in ob.data.vertices]).reshape(16,14,3)
    for j,ring in enumerate(co):
        lo,hi=ring[:,0].min(),ring[:,0].max()
        if j in root_bounds:
            for k in range(14):
                ring[k,0]=root_bounds[j]+(ring[k,0]-lo)/(hi-lo)*(hi-root_bounds[j])
        for k in range(14):
            root_weight=ease((.275-ring[k,0])/.20)
            ring[k,1]=.24+(ring[k,1]-.24)*(1+.20*root_weight)
            amount=(.003 if k<7 else -.024)*root_weight
            ring[k,2]+= -amount if lower else amount
            ring[k]=pitch(ring[k])
    for v,p in zip(ob.data.vertices,co.reshape(-1,3)):v.co=p
    ob.data.update();claimed[name]='Broader center-root planform and thicker root section tapering into unchanged thin outer blade'

for lower in (False,True):
    name=('Lower' if lower else 'Upper')+' fin armor joint'
    ob=bpy.data.objects[name]
    for v in ob.data.vertices:v.co=pitch(v.co)
    mesh=bpy.data.objects[('Lower' if lower else 'Upper')+' small swept fins'].data
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons])
    for v in ob.data.vertices:
        hit,_,_,_=tree.ray_cast(Vector((v.co.x,v.co.y,-.6 if lower else .6)),Vector((0,0,1 if lower else -1)),1.2)
        assert hit is not None,(name,v.index)
        v.co.z=hit.z+(-.0008 if lower else .0008)
    ob.data.update();claimed[name]='Existing seam follows pitched root plate'

# Broad planar sections form wings; no rounded swept tube or imported legacy topology.
rows=[(.14,.105,.228,.041,.030),(.02,.116,.235,.042,.031),
      (-.15,.125,.250,.047,.032),(-.30,.165,.302,.052,.031),
      (-.43,.188,.352,.063,.029),(-.53,.179,.366,.079,.027),
      (-.61,.153,.353,.096,.026),(-.69,.126,.321,.116,.024),
      (-.755,.126,.310,.129,.022),(-.815,.115,.285,.140,.020),
      (-.91,.098,.224,.153,.017),(-.995,.083,.128,.158,.009),
      (-1.025,.080,.093,.158,.003)]
us=[0,.045,.18,.52,.94,1]
verts=[];n=12
for y,inside,outside,z,depth in rows:
    top=[];bottom=[]
    for k,u in enumerate(us):
        x=inside+(outside-inside)*u
        face=z-.085*(x-(inside+outside)/2)
        chamfer=min(.005,depth*.25) if k in (0,5) else 0
        top.append((x,y,face+depth/2-chamfer))
        bottom.append((x,y,face-depth/2+chamfer*.5))
    verts.extend(top+list(reversed(bottom)))
faces=[(j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k)
       for j in range(len(rows)-1) for k in range(n)]
for j in (0,len(rows)-1):
    for k in range(1,n-1):faces.append((j*n,j*n+k,j*n+k+1))
mesh=bpy.data.meshes.new('Rear swept wing pair cage');mesh.from_pydata(verts,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
ob=bpy.data.objects.new('Rear swept wing pair',mesh)
bpy.data.collections['role.hull'].objects.link(ob);ob.parent=bpy.data.objects['SymmetryOrigin']
for name in ('Clean armor slate','Clean armor face','Clean armor bevel','Clean mechanical recess'):
    mesh.materials.append(bpy.data.materials[name])
for p in mesh.polygons:
    k=p.index%n
    p.material_index=1 if k==1 else 2 if k in (0,4,5,10,11) else 0
    p.use_smooth=False
mirror=ob.modifiers.new('Live X symmetry','MIRROR');mirror.mirror_object=ob.parent
mirror.use_clip=True;mirror.merge_threshold=.00001
ob['construction']='Broad angular rear wing pair; native legacy silhouette and approved Nightshade three-quarter concept guide; no imported faces or UVs'

audit={}
for name,old in before.items():
    existing=bpy.data.objects[name];xyz=np.array([v.co[:] for v in existing.data.vertices])
    assert old['faces']==[tuple(p.vertices) for p in existing.data.polygons],name
    assert old['mods']==[(m.name,m.type) for m in existing.modifiers],name
    changed=not np.array_equal(xyz,old['xyz'])
    if changed:assert name in claimed,name
    audit[name]={'topology_retained':True,'positions_changed':changed,
                 'max_displacement':float(np.linalg.norm(xyz-old['xyz'],axis=1).max())}
after_fp=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before_fp['parts'] if before_fp['parts'][n]!=after_fp['parts'][n]]
assert not set(changed)-set(claimed)
assert set(after_fp['parts'])-set(before_fp['parts'])=={'Rear swept wing pair'}
assert set(before_fp['parts'])<=set(after_fp['parts'])
bpy.context.scene['review']='Nose taper and forward upper-body pitch; substantial swept-fin roots; broad angular rear wings'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'claimed_edits':claimed,'changed_parts':changed,
    'added_parts':['Rear swept wing pair'],'removed_parts':[],'parts':audit,
    'owner_deleted_tail_remains_absent':True,'modifiers_applied':False,'upper_pitch_degrees':5},indent=2))
result={'saved':str(source),'changed':changed,'added':['Rear swept wing pair']}
