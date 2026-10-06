from pathlib import Path
import bpy,json,sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-aft-taper/round-02';out.mkdir(parents=True,exist_ok=True)
assert Path(bpy.data.filepath).resolve()==source.resolve()
assert bpy.context.mode=='OBJECT'
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source
before=ship_source.fingerprint(bpy.context.scene)
baseline=json.loads((out.parent/'round-01/check/ship_check.json').read_text())
assert before==baseline['fingerprint'], 'Live source changed since the owner checkpoint'
names=['Rebuilt pitched hull','Aft spine armor','Aft engine collar']
original={n:{'xyz':np.array([v.co[:] for v in bpy.data.objects[n].data.vertices]),
             'faces':[tuple(p.vertices) for p in bpy.data.objects[n].data.polygons],
             'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers]} for n in names}
body=bpy.data.objects[names[0]];co=original[body.name]['xyz'].copy()
assert co.shape==(444,3)
old_tree=BVHTree.FromPolygons([Vector(v) for v in co],original[body.name]['faces'])
targets={14:(.147,-.155),15:(.143,-.141),16:(.139,-.122),17:(.135,-.102),
         18:(.131,-.081),19:(.128,-.044),20:(.127,-.007),21:(.129,.031),
         22:(.132,.066),23:(.134,.084),24:(.134,.098),25:(.132,.102)}
measurements=[]
for j,(hi,lo) in targets.items():
    ring=co[j*17:(j+1)*17]
    old_hi,old_lo=float(ring[0,2]),float(ring[16,2])
    ring[:,2]=lo+(ring[:,2]-old_lo)/(old_hi-old_lo)*(hi-lo)
    measurements.append({'row':j,'y':float(ring[:,1].mean()),'old_top':old_hi,'old_bottom':old_lo,
                         'new_top':hi,'new_bottom':lo})
co[443,2]=(targets[25][0]+targets[25][1])/2
assert np.array_equal(co[:14*17],original[body.name]['xyz'][:14*17])
assert np.array_equal(co[:,:2],original[body.name]['xyz'][:,:2])
for v,p in zip(body.data.vertices,co):v.co=p
body.data.update()
new_tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],original[body.name]['faces'])

armor=bpy.data.objects['Aft spine armor']
for v in armor.data.vertices:
    ray=Vector((v.co.x,v.co.y,.6));direction=Vector((0,0,-1))
    old_hit,_,_,_=old_tree.ray_cast(ray,direction,1.2)
    new_hit,_,_,_=new_tree.ray_cast(ray,direction,1.2)
    assert old_hit is not None and new_hit is not None,v.index
    v.co.z+=new_hit.z-old_hit.z
armor.data.update()

old_rows=original[body.name]['xyz'][:442].reshape(26,17,3)
new_rows=co[:442].reshape(26,17,3)
axis_y=old_rows[:,:,1].mean(axis=1)[::-1]
old_depth=(old_rows[:,0,2]-old_rows[:,16,2])[::-1]
new_depth=(new_rows[:,0,2]-new_rows[:,16,2])[::-1]
new_center=((new_rows[:,0,2]+new_rows[:,16,2])/2)[::-1]
collar=bpy.data.objects['Aft engine collar']
collar_co=original[collar.name]['xyz'].reshape(4,9,3)
for row in collar_co:
    center_z=(row[:,2].min()+row[:,2].max())/2
    y=float(row[:,1].mean())
    factor=float(np.interp(y,axis_y,new_depth)/np.interp(y,axis_y,old_depth))
    center=float(np.interp(y,axis_y,new_center))
    row[:,2]=center+(row[:,2]-center_z)*factor
for v,p in zip(collar.data.vertices,collar_co.reshape(-1,3)):v.co=p
collar.data.update()

audit={}
for name,old in original.items():
    ob=bpy.data.objects[name];xyz=np.array([v.co[:] for v in ob.data.vertices])
    assert old['faces']==[tuple(p.vertices) for p in ob.data.polygons],name
    assert old['mods']==[(m.name,m.type) for m in ob.modifiers],name
    audit[name]={'topology_retained':True,'xy_positions_unchanged':bool(np.array_equal(old['xyz'][:,:2],xyz[:,:2])),
                 'max_displacement':float(np.linalg.norm(xyz-old['xyz'],axis=1).max())}
after=ship_source.fingerprint(bpy.context.scene)
assert set(before['parts'])==set(after['parts'])
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert set(changed)==set(names),changed
bpy.context.scene['review']='Central fuselage aft taper corrected: continuous helicopter-like aft taper with a gradual rise; rear wings and owner canopy edits retained'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],'parts_removed':[],
    'parts':audit,'sections':measurements,'modifiers_applied':False,'baseline_findings':baseline['findings'],
    'construction':'Continuous aft hull taper from the body shoulder with volume carried through the middle; attached armor and collar follow',
    'owner_canopy_and_wing_edits_unchanged':True},indent=2))
result={'saved':str(source),'changed_parts':changed,'aft_depth_at_y_minus_01':.172,'tip_depth':.030}

