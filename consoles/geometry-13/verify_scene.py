"""Fresh-load console.blend; write verification.json without saving scene changes."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
root=Path(__file__).resolve().parent
s=bpy.context.scene;bpy.context.view_layer.update()
manifest=json.loads((root/'console-manifest.json').read_text())
prior=json.loads((root.parent/'hardware-08/hardware-manifest.json').read_text())
def same(m,rows):return max(abs(m[i][j]-rows[i][j])for i in range(4)for j in range(4))<1e-6
assert s.name=='13 - Folded console skins'
assert s.camera.name=='07 Camera - perspective'
assert same(s.camera.matrix_world,manifest['cameraMatrix'])
assert same(bpy.data.objects['07 | Monitor assembly'].matrix_world,manifest['monitorMatrix'])
assert same(bpy.data.objects['07 | Hologram assembly'].matrix_world,prior['lockedState']['ship'])
assert hashlib.sha256(Path(manifest['input']).read_bytes()).hexdigest()==manifest['inputSha256']
owner=root.parent/'refinement-04/inputs/owner-edited.blend'
assert hashlib.sha256(owner.read_bytes()).hexdigest()=='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
assert len(bpy.data.scenes['SOURCE - owner saved scene'].objects)==772
for o in s.objects:
    if o.name.startswith('07 | Viewport side post'):
        pts=[world_to_camera_view(s,s.camera,o.matrix_world@Vector(p))for p in o.bound_box]
        assert max(p.x for p in pts)<0 or min(p.x for p in pts)>1
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not missing
assert not bpy.data.objects['11 | Neutral projector frustum'].hide_render
meshes={}
for o in bpy.data.collections['13 - Editable folded skins'].objects:
    if o.type!='MESH':continue
    ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh();bm=bmesh.new();bm.from_mesh(me)
    count=sum(not e.is_manifold for e in bm.edges)
    assert count==0,(o.name,count)
    meshes[o.name]={'vertices':len(me.vertices),'faces':len(me.polygons),'nonManifoldEdges':count}
    bm.free();ev.to_mesh_clear()
report={'result':'pass','cameraMonitorShipUnchanged':True,'sourceFilesUnchanged':True,'ownerSceneObjects':772,'sidePostsOutsideCamera':True,'missingImages':missing,'evaluatedNewMeshes':meshes,'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
