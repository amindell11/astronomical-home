"""Read console.blend and geometry-15, verify locked vertices/faces and planar joins; write verification.json and fail on mismatch."""
import bpy,bmesh,json
from pathlib import Path
root=Path(__file__).resolve().parent
mount='14 | Forearm fold into Launch and controls';shoulder='14 | Projector to control folded shoulder'
removed={mount:{6,7,8},shoulder:{1}}
def capture():
 out={}
 for o in bpy.context.scene.objects:
  d={'matrix':[list(r)for r in o.matrix_world],'hidden':o.hide_render}
  if o.type=='MESH':d['vertices']=[list(v.co)for v in o.data.vertices];d['faces']=[(list(p.vertices),p.material_index)for p in o.data.polygons]
  if o.type=='CURVE':d['curves']=[[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points]for sp in o.data.splines]
  out[o.name]=d
 return out
current=capture();dg=bpy.context.evaluated_depsgraph_get();planarity={};nonmanifold={};errors=[]
for o in bpy.context.scene.objects:
 if not o.name.startswith('17 |'):continue
 p=o.data.polygons[0];vs=[o.data.vertices[i].co for i in p.vertices];n=(vs[1]-vs[0]).cross(vs[2]-vs[0]).normalized();planarity[o.name]=max(abs((v-vs[0]).dot(n))for v in vs)
 ev=o.evaluated_get(dg);me=ev.to_mesh();bm=bmesh.new();bm.from_mesh(me);nonmanifold[o.name]=sum(not e.is_manifold for e in bm.edges);bm.free();ev.to_mesh_clear()
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'geometry-15/console.blend'));source=capture();vertexcount=0
for name,old in source.items():
 new=current[name]
 for key in ['matrix','hidden','vertices','curves']:
  if key in old and old[key]!=new[key]:errors.append(name+' '+key)
 if 'vertices'in old:vertexcount+=len(old['vertices'])
 if 'faces'in old:
  expected=[f for i,f in enumerate(old['faces'])if i not in removed.get(name,set())]
  if expected!=new['faces']:errors.append(name+' faces')
if max(planarity.values())>1e-5:errors.append('Nonplanar new panel')
if any(nonmanifold.values()):errors.append('Open evaluated new panel')
report={'result':'pass'if not errors else 'fail','source':'geometry-15','existingObjectsVerified':len(source),'originalVerticesUnchanged':vertexcount,'armorFacesAndFoldEdgesUnchanged':not errors,'removedConnectionFaces':{n:sorted(v)for n,v in removed.items()},'maxNewPanelPlanarityError':max(planarity.values()),'newPanels':len(planarity),'nonmanifoldEdges':nonmanifold,'errors':errors,'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not errors
