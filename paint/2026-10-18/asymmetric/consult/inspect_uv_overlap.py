import sys,json
from pathlib import Path
import bpy,numpy as np
root=Path('D:/amind/git/agent-4')
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_paint,ship_source,ship_regions,ship_contract
paint=json.loads((root/'art/ships/vanguard/paint.json').read_text())
scene=ship_source.open_source(root/'art/ships/vanguard/Vanguard.blend')
roles=ship_source.Roles(scene); evaluation=ship_source.Evaluation(scene)
names=sorted(name for role in ship_contract.VISUAL_ROLES for name in roles.members.get(role,()) if scene.objects[name].type=='MESH')
owner=np.full((4096,4096),-1,np.int32); position=np.zeros((4096,4096,3),np.float32)
labels=[]
for name in names:
    part=ship_paint.Part(evaluation,name,paint)
    for tri,xs,ys,points in ship_regions.surface_samples(part,4096,'PaintUV',tolerance=1e-6):
        overlap=owner[ys,xs]>=0
        if overlap.any():
            old=position[ys[overlap],xs[overlap]]; new=points[overlap]
            hit={'new':[name,tri.polygon_index,tri.vertices[:]],'old':[labels[i] for i in np.unique(owner[ys[overlap],xs[overlap]])],
                 'count':int(overlap.sum()),'distance_min':float(np.linalg.norm(new-old,axis=1).min()),'distance_max':float(np.linalg.norm(new-old,axis=1).max()),
                 'old_points':old[:8].tolist(),'new_points':new[:8].tolist(),'pixels':np.column_stack((xs[overlap],ys[overlap]))[:8].tolist()}
            print(json.dumps(hit,indent=2)); (Path(__file__).parent/'overlap.json').write_text(json.dumps(hit,indent=2)); break
        labels.append((name,tri.polygon_index,tri.vertices[:]))
        owner[ys,xs]=len(labels)-1; position[ys,xs]=points
    else:continue
    break
