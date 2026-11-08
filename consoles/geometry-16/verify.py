"""Read final and previous scenes; compare untouched data and repaired mesh closure. Writes verification.json, exits nonzero on failure."""
import bpy,bmesh,json
from pathlib import Path
root=Path(__file__).resolve().parent
changed={'14 | Projector formed armor','14 | Forearm fold into Launch and controls','14 | Launch casing folded returns','14 | Projector to control folded shoulder','15 | Launch underside clearance'}
def capture():
 out={}
 for o in bpy.context.scene.objects:
  item={'matrix':[list(r)for r in o.matrix_world],'hidden':o.hide_render}
  if o.type=='MESH':item['vertices']=[list(v.co)for v in o.data.vertices];item['faces']=[list(p.vertices)for p in o.data.polygons]
  if o.type=='CURVE':item['curves']=[[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points]for sp in o.data.splines]
  out[o.name]=item
 return out
current=capture();dg=bpy.context.evaluated_depsgraph_get();edges={}
for name in changed|{'07 | Continuous left console body'}:
 ev=bpy.data.objects[name].evaluated_get(dg);d=ev.to_mesh();bm=bmesh.new();bm.from_mesh(d);edges[name]=sum(not e.is_manifold for e in bm.edges);bm.free();ev.to_mesh_clear()
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'geometry-15/console.blend'));previous=capture()
errors=[]
for name,old in previous.items():
 new=current[name]
 if old['matrix']!=new['matrix']:errors.append(name+' transform')
 if old['hidden']!=new['hidden']:errors.append(name+' visibility')
 if name not in changed and old!=new:errors.append(name+' data')
if any(edges.values()):errors.append('Nonmanifold repaired mesh')
report={'result':'pass'if not errors else 'fail','existingObjects':len(previous),'unchangedTransforms':not errors,'editedMeshObjects':sorted(changed),'nonManifoldEdges':edges,'errors':errors,'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not errors
