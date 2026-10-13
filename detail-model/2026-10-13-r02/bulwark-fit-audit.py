import bpy,bmesh,sys,json
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
root=Path('D:/amind/git/agent-3/results/bulwark/fitted-parts')
bpy.ops.wm.open_mainfile(filepath=str(root/'Bulwark-fitted-parts.blend'),load_ui=False,use_scripts=False)
report={}
dg=bpy.context.evaluated_depsgraph_get()
for o in bpy.context.scene.objects:
 if o.type!='MESH' or o.hide_render:continue
 mesh=bpy.data.meshes.new_from_object(o.evaluated_get(dg),depsgraph=dg)
 bm=bmesh.new();bm.from_mesh(mesh)
 report[o.name]={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'verts':len(bm.verts),'faces':len(bm.faces)}
 bm.free();bpy.data.meshes.remove(mesh)
original=json.loads(Path('D:/amind/git/agent-3/results/bulwark/coherent-cleanup/owner-check/ship_check.json').read_text())
current=json.loads((root/'check/ship_check.json').read_text())
before={(f['rule'],f['part']) for f in original['findings']};after={(f['rule'],f['part']) for f in current['findings']}
fp=ship_source.fingerprint(bpy.context.scene)
unchanged=[n for n,p in original['fingerprint']['parts'].items() if fp['parts'].get(n,{}).get('geometry')==p['geometry']]
result={'parts':report,'findings_before':len(original['findings']),'findings_after':len(current['findings']),'new_findings':sorted(after-before),'removed_findings':sorted(before-after),'unchanged_parts':unchanged,'fingerprint':fp}
(root/'validation-delta.json').write_text(json.dumps(result,indent=2))
print(json.dumps({'parts':report,'new_findings':sorted(after-before),'unchanged_parts':unchanged}))
