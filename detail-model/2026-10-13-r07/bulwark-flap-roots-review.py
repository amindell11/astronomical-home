import bpy,bmesh,sys,json,hashlib
from pathlib import Path
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/flaps')
source=root/'refined/Bulwark.blend'
sha=hashlib.sha256(source.read_bytes()).hexdigest()
src=ship_source.open_source(source)
ev=ship_source.Evaluation(src)
stats={}
for name in ['11 Upper flap mounts','12 Lower flap mounts']:
    mesh=ev.mesh(name)
    bm=bmesh.new();bm.from_mesh(mesh)
    stats[name]={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'zero_area_faces':sum(f.calc_area()<1e-9 for f in bm.faces),'vertices':len(bm.verts),'faces':len(bm.faces),'volume':bm.calc_volume(signed=True)}
    bm.free();bpy.data.meshes.remove(mesh)
    assert stats[name]['nonmanifold_edges']==0 and stats[name]['zero_area_faces']==0 and stats[name]['volume']>0,stats[name]
old=json.loads((root/'owner/check/ship_check.json').read_text())
new=json.loads((root/'refined/check/ship_check.json').read_text())
oldrules={(f['rule'],f['part']) for f in old['findings']}
newrules={(f['rule'],f['part']) for f in new['findings']}
assert not newrules-oldrules,newrules-oldrules
assert oldrules-newrules=={('visual_role','10 Tail fins.001')}
assert all(new['fingerprint']['parts'][n]==p for n,p in old['fingerprint']['parts'].items())
(root/'refined/validation-delta.json').write_text(json.dumps({'source_sha256':sha,'mount_meshes':stats,'findings_before':len(old['findings']),'findings_after':len(new['findings']),'new_findings':[],'resolved_findings':list(oldrules-newrules),'all_existing_parts_unchanged':True},indent=2))

# Include the owner's unassigned lower flaps in the baseline comparison.
original_prepare=ship_render.prepare
def prepare_all(path):
    evaluation,bounds=original_prepare(path)
    names=[o.name for o in bpy.data.scenes[0].objects if o.type=='MESH' and not o.hide_render]
    evaluation.show_only(names)
    points=[]
    for name in names:
        mesh=evaluation.mesh(name)
        co=ship_source.mesh_arrays(mesh)[0]
        bpy.data.meshes.remove(mesh)
        matrix=np.array(evaluation.to_space(name),np.float64)
        points.append(co@matrix[:3,:3].T+matrix[:3,3])
    return evaluation,ship_source.bounds(np.concatenate(points))
ship_render.prepare=prepare_all
for kind in ['sheet','scale','compare']:
    args=['--ship',str(root/'refined/ship.json'),'--out',str(root/'refined'/kind),'--set',kind,'--size','512']
    if kind=='compare':args+=['--before',str(root/'owner/Bulwark.blend')]
    ship_render.main(args)
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print(json.dumps({'source_sha256':sha,'sets':['sheet','scale','compare'],'mount_meshes':stats}))
