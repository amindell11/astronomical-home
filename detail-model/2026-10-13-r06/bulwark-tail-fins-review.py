import bpy,bmesh,sys,json,hashlib
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/tail-fins')
source=root/'refined/Bulwark.blend'
sha=hashlib.sha256(source.read_bytes()).hexdigest()
src=ship_source.open_source(source)
ev=ship_source.Evaluation(src)
mesh=ev.mesh('10 Tail fins')
bm=bmesh.new();bm.from_mesh(mesh)
stats={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'vertices':len(bm.verts),'faces':len(bm.faces)}
bm.free();bpy.data.meshes.remove(mesh)
assert stats['nonmanifold_edges']==0,stats
old=json.loads((root/'owner/check/ship_check.json').read_text())
new=json.loads((root/'refined/check/ship_check.json').read_text())
oldrules={(f['rule'],f['part']) for f in old['findings']}
newrules={(f['rule'],f['part']) for f in new['findings']}
assert oldrules==newrules
assert all(new['fingerprint']['parts'][n]==p for n,p in old['fingerprint']['parts'].items())
(root/'refined/validation-delta.json').write_text(json.dumps({'source_sha256':sha,'fin_mesh':stats,'findings_before':len(old['findings']),'findings_after':len(new['findings']),'new_findings':[],'all_existing_parts_unchanged':True},indent=2))
for kind in ['sheet','scale','compare']:
    args=['--ship',str(root/'refined/ship.json'),'--out',str(root/'refined'/kind),'--set',kind,'--size','512']
    if kind=='compare':args+=['--before',str(root/'owner/Bulwark.blend')]
    ship_render.main(args)
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print(json.dumps({'source_sha256':sha,'sets':['sheet','scale','compare'],'fin_mesh':stats}))
