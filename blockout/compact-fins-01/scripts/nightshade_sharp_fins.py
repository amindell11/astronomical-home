from pathlib import Path
import bpy,json,sys,hashlib
import numpy as np
from mathutils import Vector

root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
base=root/'results/nightshade-sharp-fins';out=base/'round-01';out.mkdir(parents=True,exist_ok=True)
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((base/'owner-baseline.json').read_text())
before=ship_source.fingerprint(bpy.context.scene)
assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
names=['Upper small swept fins','Lower small swept fins','Upper fin armor joint','Lower fin armor joint']
original={n:{'xyz':np.array([v.co[:] for v in bpy.data.objects[n].data.vertices]),
             'faces':[tuple(p.vertices) for p in bpy.data.objects[n].data.polygons],
             'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
             'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers]} for n in names}
inside=np.array([.199,.184,.080,0,0,0,0,.020,.105,.175,.228,.256,.294,.330,.375,.419])
outside=np.array([.216,.273,.320,.326,.335,.344,.353,.363,.372,.376,.387,.399,.410,.421,.429,.433])
dy=np.array([.008,.006,.004,.003,.002,.001,0,0,0,0,-.003,-.006,-.012,-.018,-.026,-.042])
audit={}
for prefix in ('Upper','Lower'):
    name=prefix+' small swept fins';ob=bpy.data.objects[name]
    old=original[name]['xyz'].reshape(16,14,3);new=old.copy()
    lo=old[:,:,0].min(axis=1);hi=old[:,:,0].max(axis=1)
    ymean=old[:,:,1].mean(axis=1)
    for j,ring in enumerate(new):
        u=(old[j,:,0]-lo[j])/(hi[j]-lo[j])
        ring[:,0]=inside[j]+u*(outside[j]-inside[j])
        ring[:,1]+=dy[j]
        if j==15:ring[:,1]+=.003*(1-2*u)
    assert np.array_equal(old[:,:,2],new[:,:,2])
    for v,p in zip(ob.data.vertices,new.reshape(-1,3)):v.co=p
    ob.data.update()
    joint=bpy.data.objects[prefix+' fin armor joint']
    to_fin=ob.matrix_world.inverted()@joint.matrix_world
    to_joint=joint.matrix_world.inverted()@ob.matrix_world
    for v in joint.data.vertices:
        p=to_fin@v.co
        a=float(np.interp(p.y,ymean[::-1],lo[::-1]));b=float(np.interp(p.y,ymean[::-1],hi[::-1]))
        u=(p.x-a)/(b-a)
        new_a=float(np.interp(p.y,ymean[::-1],inside[::-1]))
        new_b=float(np.interp(p.y,ymean[::-1],outside[::-1]))
        delta_y=float(np.interp(p.y,ymean[::-1],dy[::-1]))
        p.x=new_a+u*(new_b-new_a);p.y+=delta_y
        v.co=to_joint@p
    joint.data.update()
for name,old in original.items():
    ob=bpy.data.objects[name]
    assert old['faces']==[tuple(p.vertices) for p in ob.data.polygons]
    assert old['matrix']==[list(r) for r in ob.matrix_basis]
    assert old['mods']==[(m.name,m.type) for m in ob.modifiers]
    audit[name]={'topology_retained':True,'object_transform_retained':True,'live_modifiers_retained':True}
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names),changed
assert set(before['parts'])==set(after['parts'])
bpy.context.scene['review']='Sharper upper and lower swept fins: defined shoulders, straighter sweep and clipped blade tips'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'parts':audit,'owner_checkpoint':'e77f6110','modifiers_applied':False,
    'existing_root_thickness_retained':True,'local_fin_z_unchanged':True,
    'owner_lower_fin_transform_retained':True,'all_unclaimed_parts_unchanged':True},indent=2))
result={'saved':str(source),'changed':changed,'topology_retained':True}
