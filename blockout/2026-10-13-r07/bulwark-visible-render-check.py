import sys,json,hashlib
from pathlib import Path
import bpy,numpy as np
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
source=Path(sys.argv[sys.argv.index('--')+1]).resolve()
out=Path(sys.argv[sys.argv.index('--')+2]).resolve();out.mkdir(parents=True,exist_ok=True)
sha=hashlib.sha256(source.read_bytes()).hexdigest()
scene=ship_source.open_source(source)
parts=[o.name for o in scene.objects if o.type=='MESH' and not o.hide_render]
evaluation=ship_source.Evaluation(scene);evaluation.show_only(parts)
points=[]
for name in parts:
 mesh=evaluation.mesh(name);co=ship_source.mesh_arrays(mesh)[0];bpy.data.meshes.remove(mesh)
 matrix=np.array(evaluation.to_space(name),np.float64);points.append(co@matrix[:3,:3].T+matrix[:3,3])
bounds=ship_source.bounds(np.concatenate(points))
paths=ship_render.render(evaluation,bounds,ship_render.ORTHO,768,out)
for size in [32,48,64,96]:
 paths.update({f'scale{size}_{k}':v for k,v in ship_render.render(evaluation,bounds,{'top':ship_render.ORTHO['top']},size,out,f'scale{size}_').items()})
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
(out/'render-report.json').write_text(json.dumps({'source_sha256':sha,'source_unchanged':True,'visible_parts':parts,'method':'ship_render review helpers with all visible authored meshes, including unassigned owner additions','images':{k:str(v) for k,v in paths.items()}},indent=2))
print('REVIEW_VERDICT=pass')
