"""Fresh-load console.blend; write verification.json without saving changes."""
import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
s=bpy.context.scene;bpy.context.view_layer.update()
prior=json.loads((root.parent/'hardware-08/hardware-manifest.json').read_text())
manifest=json.loads((root/'console-manifest.json').read_text())
def same(m,rows):return max(abs(m[i][j]-rows[i][j])for i in range(4)for j in range(4))<1e-6
assert s.name=='10 - Raked monitor and formed panels'
assert s.camera.name=='07 Camera - perspective'
assert same(s.camera.matrix_world,prior['lockedState']['camera']) and s.camera.data.lens==prior['cameraLens']
assert same(bpy.data.objects['07 | Hologram assembly'].matrix_world,prior['lockedState']['ship'])
for label,state in prior['lockedState']['lights'].items():
    o=bpy.data.objects[label];assert same(o.matrix_world,state['matrix']) and o.data.energy==state['energy'] and list(o.data.color)==state['color']
assert hashlib.sha256(Path(manifest['input']).read_bytes()).hexdigest()==manifest['inputSha256']
owner=root.parent/'refinement-04/inputs/owner-edited.blend'
assert hashlib.sha256(owner.read_bytes()).hexdigest()=='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
assert len(bpy.data.scenes['SOURCE - owner saved scene'].objects)==772
rectangles={}
for name in ['07 | Main rectangular display','07 | Detail rectangular display']:
    o=bpy.data.objects[name];v=[o.matrix_world@p.co for p in o.data.vertices[8:16]]
    dirs=[(v[b]-v[a]).normalized() for a,b in [(0,1),(2,3),(4,5),(6,7)]]
    error=max(math.degrees(math.asin(min(1,dirs[a].cross(dirs[b]).length)))for a,b in [(0,2),(1,3)])
    assert error<.001;rectangles[name]=error
main=bpy.data.objects['07 | Monitor assembly'];normal=main.matrix_world.to_quaternion()@Vector((0,0,1))
rake=math.degrees(math.asin(normal.z));assert abs(rake-34)<.05
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not missing
hose=bpy.data.objects['10 | Lower curved tube'];assert hose.type=='CURVE'
route=bpy.data.objects['10 | Complete tube assembly'];old=route.location.copy();before=hose.matrix_world.translation.copy()
route.location.x+=.1;bpy.context.view_layer.update();assert (hose.matrix_world.translation-before-Vector((.1,0,0))).length<1e-5
route.location=old;bpy.context.view_layer.update()
assert bpy.data.objects['10 | Forearm bent armor shell'].parent.name=='10 | Folded forearm assembly'
report={'result':'pass','cameraShipLightsUnchanged':True,'sourceFilesUnchanged':True,'ownerSceneObjects':772,'monitorBackRakeDegrees':rake,'displayParallelEdgeErrorDegrees':rectangles,'missingImages':missing,'editableTubeAssembly':'pass','foldedForearmGrouped':True,'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
