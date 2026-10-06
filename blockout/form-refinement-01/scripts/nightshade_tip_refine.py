from pathlib import Path
import bpy, json, sys
import numpy as np

root=Path('D:/amind/git/agent-1');out=root/'results/nightshade-form-refinement/round-02'
out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source
bpy.ops.wm.open_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
before=ship_source.fingerprint(bpy.context.scene)
assert before==json.loads((out.parent/'round-01/check/ship_check.json').read_text())['fingerprint']
ob=bpy.data.objects['Rebuilt main wing blades']
faces=[tuple(p.vertices) for p in ob.data.polygons]
co=np.array([v.co[:] for v in ob.data.vertices]).reshape(28,14,3)
rows={24:(.760,.821),25:(.752,.844),26:(.750,.866),27:(.789,.849)}
for j,(lo,hi) in rows.items():
    x0,x1=co[j,:,0].min(),co[j,:,0].max()
    for k in range(14):
        x=lo+(co[j,k,0]-x0)/(x1-x0)*(hi-lo)
        co[j,k,2]+=-.145*(x-co[j,k,0])
        co[j,k,0]=x
for v,p in zip(ob.data.vertices,co.reshape(-1,3)):v.co=p
ob.data.update()
assert faces==[tuple(p.vertices) for p in ob.data.polygons]
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert changed==[ob.name]
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
(out/'tip-edit-audit.json').write_text(json.dumps({'changed_parts':changed,'topology_unchanged':True,
    'construction':'Narrower tip neck with outward flared, angular capped end; same subtle vertical bend',
    'parts_added':[],'parts_removed':[],'modifiers_applied':False},indent=2))
