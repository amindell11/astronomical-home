import bpy, bmesh, sys, json, hashlib
from pathlib import Path
sys.path.insert(0, 'D:/amind/git/agent-3/art/tools/ship')
import ship_source, ship_render

root = Path('D:/amind/git/agent-3/results/bulwark/straight-tail')
source = root/'refined/Bulwark.blend'
sha = hashlib.sha256(source.read_bytes()).hexdigest()
src = ship_source.open_source(source)
evaluation = ship_source.Evaluation(src)
stats = {}
for name in ['04 Tail booms','05 Tail tips']:
    mesh = evaluation.mesh(name)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    stats[name] = {'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'vertices':len(bm.verts),'faces':len(bm.faces)}
    bm.free()
    bpy.data.meshes.remove(mesh)
assert all(p['nonmanifold_edges']==0 for p in stats.values()), stats
old = json.loads((root/'owner/check/ship_check.json').read_text())
new = json.loads((root/'refined/check/ship_check.json').read_text())
oldrules = {(f['rule'],f['part']) for f in old['findings']}
newrules = {(f['rule'],f['part']) for f in new['findings']}
assert not newrules-oldrules, newrules-oldrules
(root/'refined/validation-delta.json').write_text(json.dumps({'source_sha256':sha,'parts':stats,'findings_before':len(old['findings']),'findings_after':len(new['findings']),'new_findings':list(newrules-oldrules),'removed_findings':list(oldrules-newrules)},indent=2))
for render_set in ['sheet','scale','compare']:
    args = ['--ship',str(root/'refined/ship.json'),'--out',str(root/'refined'/render_set),'--set',render_set,'--size','512']
    if render_set=='compare':
        args.extend(['--before',str(root/'owner/Bulwark.blend')])
    ship_render.main(args)
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print(json.dumps({'source_sha256':sha,'render_sets':['sheet','scale','compare'],'stats':stats}))
