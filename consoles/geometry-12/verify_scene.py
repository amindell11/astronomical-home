"""Fresh-load console.blend; write verification.json without saving changes."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
root=Path(__file__).resolve().parent;s=bpy.context.scene;bpy.context.view_layer.update()
manifest=json.loads((root/'console-manifest.json').read_text())
prior=json.loads((root.parent/'hardware-08/hardware-manifest.json').read_text())
def same(m,rows):return max(abs(m[i][j]-rows[i][j])for i in range(4)for j in range(4))<1e-6
assert s.name=='12 - Wrapped console housing'
assert same(s.camera.matrix_world,manifest['cameraMatrix'])
assert same(bpy.data.objects['07 | Monitor assembly'].matrix_world,manifest['monitorMatrix'])
assert same(bpy.data.objects['07 | Hologram assembly'].matrix_world,prior['lockedState']['ship'])
assert hashlib.sha256(Path(manifest['input']).read_bytes()).hexdigest()==manifest['inputSha256']
owner=root.parent/'refinement-04/inputs/owner-edited.blend'
assert hashlib.sha256(owner.read_bytes()).hexdigest()=='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
assert len(bpy.data.scenes['SOURCE - owner saved scene'].objects)==772
body=bpy.data.objects['12 | Complete console housing'];evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());m=evaluated.to_mesh();bm=bmesh.new();bm.from_mesh(m)
assert all(e.is_manifold for e in bm.edges)
seen=set();pending=[next(iter(bm.verts))]
while pending:
    v=pending.pop()
    if v in seen:continue
    seen.add(v);pending.extend(e.other_vert(v)for e in v.link_edges)
assert len(seen)==len(bm.verts)
geometry={'vertices':len(bm.verts),'faces':len(bm.faces),'connectedComponents':1,'nonManifoldEdges':0};bm.free();evaluated.to_mesh_clear()
for name in manifest['housingOperands']:
    assert bpy.data.objects[name].hide_render
    assert any(mod.type=='BOOLEAN' and mod.operation=='UNION' and mod.object.name==name for mod in body.modifiers)
for name in manifest['pockets']:assert any(mod.type=='BOOLEAN' and mod.operation=='DIFFERENCE' and mod.object.name==name for mod in body.modifiers)
post_bounds={}
for o in s.objects:
    if not o.name.startswith('07 | Viewport side post'):continue
    points=[world_to_camera_view(s,s.camera,o.matrix_world@Vector(p))for p in o.bound_box]
    lo=min(p.x for p in points)*1672;hi=max(p.x for p in points)*1672;assert hi<0 or lo>1672;post_bounds[o.name]=[lo,hi]
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not missing
report={'result':'pass','cameraMonitorShipUnchanged':True,'sourceFilesUnchanged':True,'ownerSceneObjects':772,'evaluatedHousing':geometry,'liveHousingOperands':len(manifest['housingOperands']),'recesses':len(manifest['pockets']),'sidePostsOutsideCamera':True,'missingImages':missing,'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
