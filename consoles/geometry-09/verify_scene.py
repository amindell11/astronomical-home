"""Fresh-load console.blend; inspect saved geometry and write verification.json. Does not save the scene."""
import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
s=bpy.context.scene;bpy.context.view_layer.update()
prior=json.loads((root.parent/'hardware-08/hardware-manifest.json').read_text())
fit=json.loads((root/'console-fit.json').read_text())
def same(m,rows):return max(abs(m[i][j]-rows[i][j])for i in range(4)for j in range(4))<1e-6
assert s.name=='09 - Reference geometry fit'
assert s.camera.name=='07 Camera - perspective'
assert same(s.camera.matrix_world,prior['lockedState']['camera'])
assert s.camera.data.lens==prior['cameraLens']
assert same(bpy.data.objects['07 | Hologram assembly'].matrix_world,prior['lockedState']['ship'])
for label,state in prior['lockedState']['lights'].items():
    o=bpy.data.objects[label];assert same(o.matrix_world,state['matrix']) and o.data.energy==state['energy'] and list(o.data.color)==state['color']
assert hashlib.sha256(Path(fit['input']).read_bytes()).hexdigest()==fit['inputSha256']
owner=root.parent/'refinement-04/inputs/owner-edited.blend'
assert hashlib.sha256(owner.read_bytes()).hexdigest()=='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
assert len(bpy.data.scenes['SOURCE - owner saved scene'].objects)==772
rectangles={}
for name in ['07 | Main rectangular display','07 | Detail rectangular display']:
    o=bpy.data.objects[name];v=[o.matrix_world@p.co for p in o.data.vertices[8:16]]
    dirs=[(v[b]-v[a]).normalized() for a,b in [(0,1),(2,3),(4,5),(6,7)]]
    error=max(math.degrees(math.asin(min(1,dirs[a].cross(dirs[b]).length)))for a,b in [(0,2),(1,3)])
    assert error<.001;rectangles[name]=error
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not missing
hose=bpy.data.objects['08 | Heavy supply hose'];assert hose.type=='CURVE'
route=bpy.data.objects['08 | Complete hose loom'];old=route.location.copy();before=hose.matrix_world.translation.copy()
route.location.x+=.1;bpy.context.view_layer.update();assert (hose.matrix_world.translation-before-Vector((.1,0,0))).length<1e-5
route.location=old;bpy.context.view_layer.update()
apron=bpy.data.objects['09 | Broad low projector ramp'];assert any(m.type=='BOOLEAN' and m.object for m in apron.modifiers)
report={'result':'pass','cameraShipLightsUnchanged':True,'sourceFilesUnchanged':True,'ownerSceneObjects':772,'displayParallelEdgeErrorDegrees':rectangles,'missingImages':missing,'editableHoseAssembly':'pass','projectorBoolean':'pass','cornerFitRmsPixels':{k:{'before':v['beforeRmsPixels'],'after':v['afterRmsPixels']}for k,v in fit['fits'].items()},'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
