"""Compare saved console with geometry-17 and geometry-15 armor, inspect closed housing meshes. Writes verification.json; fails on mismatch."""
import bpy,bmesh,json
from pathlib import Path
root=Path(__file__).resolve().parent
removed={'14 | Forearm fold into Launch and controls':{6,7,8},'14 | Projector to control folded shoulder':{1}}
def capture():
 out={}
 for o in bpy.context.scene.objects:
  d={'matrix':[list(r)for r in o.matrix_world],'hidden':o.hide_render}
  if o.type=='MESH':d['vertices']=[list(v.co)for v in o.data.vertices];d['faces']=[(list(p.vertices),p.material_index)for p in o.data.polygons]
  if o.type=='CURVE':d['curves']=[[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points]for sp in o.data.splines]
  out[o.name]=d
 return out
current=capture();dg=bpy.context.evaluated_depsgraph_get();housings={};errors=[]
for o in bpy.context.scene.objects:
 if not o.name.startswith('18 |'):continue
 ev=o.evaluated_get(dg);me=ev.to_mesh();bm=bmesh.new();bm.from_mesh(me)
 housings[o.name]={'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'volume':bm.calc_volume(signed=True),'faces':len(bm.faces)};bm.free();ev.to_mesh_clear()
 if housings[o.name]['nonManifoldEdges']or housings[o.name]['volume']<=0:errors.append(o.name+' housing closure')
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'geometry-17/console.blend'));previous=capture()
for name,old in previous.items():
 new=current[name]
 for key in ['matrix','vertices','faces','curves']:
  if key in old and old[key]!=new[key]:errors.append(name+' '+key)
 if not name.startswith('17 |')and old['hidden']!=new['hidden']:errors.append(name+' visibility')
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'geometry-15/console.blend'));source=capture();count=0
for name,old in source.items():
 new=current[name]
 if 'vertices'in old:
  count+=len(old['vertices'])
  if old['vertices']!=new['vertices']:errors.append(name+' original armor vertices')
  if [f for i,f in enumerate(old['faces'])if i not in removed.get(name,set())]!=new['faces']:errors.append(name+' original armor faces')
report={'result':'pass'if not errors else 'fail','geometry17ObjectsPreserved':len(previous),'geometry15VerticesUnchanged':count,'armorFacesAndFoldEdgesUnchanged':not errors,'newClosedHousings':housings,'errors':errors,'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not errors
