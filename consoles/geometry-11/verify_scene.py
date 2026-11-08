"""Fresh-load console.blend; write verification.json without saving changes."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
root=Path(__file__).resolve().parent
s=bpy.context.scene;bpy.context.view_layer.update()
prior=json.loads((root.parent/'hardware-08/hardware-manifest.json').read_text())
manifest=json.loads((root/'console-manifest.json').read_text())
def same(m,rows):return max(abs(m[i][j]-rows[i][j])for i in range(4)for j in range(4))<1e-6
assert s.name=='11 - Joined console and viewport'
assert s.camera.name=='07 Camera - perspective'
assert same(s.camera.matrix_world,prior['lockedState']['camera']) and s.camera.data.lens==prior['cameraLens']
assert same(bpy.data.objects['07 | Monitor assembly'].matrix_world,manifest['monitorMatrix'])
assert same(bpy.data.objects['07 | Hologram assembly'].matrix_world,prior['lockedState']['ship'])
for label,state in prior['lockedState']['lights'].items():
    o=bpy.data.objects[label];assert same(o.matrix_world,state['matrix']) and o.data.energy==state['energy'] and list(o.data.color)==state['color']
assert hashlib.sha256(Path(manifest['input']).read_bytes()).hexdigest()==manifest['inputSha256']
owner=root.parent/'refinement-04/inputs/owner-edited.blend'
assert hashlib.sha256(owner.read_bytes()).hexdigest()=='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
assert len(bpy.data.scenes['SOURCE - owner saved scene'].objects)==772
post_bounds={}
for name in manifest['sidePostNames']:
    o=bpy.data.objects[name];points=[world_to_camera_view(s,s.camera,o.matrix_world@Vector(p))for p in o.bound_box]
    lo=min(p.x for p in points)*1672;hi=max(p.x for p in points)*1672
    assert hi<0 or lo>1672;post_bounds[name]=[lo,hi]
body=bpy.data.objects['11 | Continuous faceted console body'];bm=bmesh.new();bm.from_mesh(body.data)
assert all(e.is_manifold for e in bm.edges)
seen=set();pending=[next(iter(bm.verts))]
while pending:
    v=pending.pop()
    if v in seen:continue
    seen.add(v);pending.extend(e.other_vert(v)for e in v.link_edges)
assert len(seen)==len(bm.verts);bm.free()
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not missing
cone=bpy.data.objects['11 | Neutral projector frustum'];assert not cone.hide_render
apron=bpy.data.objects['11 | Projector wraparound armor'];assert any(m.type=='BOOLEAN' and m.object for m in apron.modifiers)
report={'result':'pass','cameraMonitorShipAndLightObjectsUnchanged':True,'sourceFilesUnchanged':True,'ownerSceneObjects':772,'sidePostPixelBounds':post_bounds,'consoleBodyClosedAndConnected':True,'missingImages':missing,'projectionCueEnabledInSavedScene':True,'projectorBoolean':'pass','visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
