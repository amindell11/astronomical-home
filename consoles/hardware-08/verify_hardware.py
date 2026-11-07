"""Load hardware.blend in a fresh Blender process; write verification.json without saving."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
manifest=json.loads((root/'hardware-manifest.json').read_text())
s=bpy.context.scene
assert s.name=='08 - Hardware forms with locked camera'
assert s.camera.name=='07 Camera - perspective'
def same(matrix,rows):return max(abs(matrix[i][j]-rows[i][j])for i in range(4)for j in range(4))<1e-6
state=manifest['lockedState'];bpy.context.view_layer.update()
assert same(s.camera.matrix_world,state['camera']) and s.camera.data.lens==manifest['cameraLens']
for name,matrix in state['screens'].items():assert same(bpy.data.objects[name].matrix_world,matrix)
assert same(bpy.data.objects['07 | Hologram assembly'].matrix_world,state['ship'])
for name,light in state['lights'].items():
    o=bpy.data.objects[name];assert same(o.matrix_world,light['matrix']) and o.data.energy==light['energy'] and list(o.data.color)==light['color']
assert hashlib.sha256(Path(manifest['input']).read_bytes()).hexdigest()==manifest['inputSha256']
owner=root.parent/'refinement-04/inputs/owner-edited.blend'
assert hashlib.sha256(owner.read_bytes()).hexdigest()=='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
assert len(bpy.data.scenes['SOURCE - owner saved scene'].objects)==772
assert all(bpy.data.objects[name].hide_render for name in manifest['retiredObjects'])
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file];assert not missing
route=bpy.data.objects['08 | Complete hose loom'];hose=bpy.data.objects['08 | Heavy supply hose']
assert hose.type=='CURVE';before=hose.matrix_world.translation.copy();old=route.location.copy()
route.location.x+=.1;bpy.context.view_layer.update();assert (hose.matrix_world.translation-before-Vector((.1,0,0))).length<1e-5
route.location=old;bpy.context.view_layer.update()
projector=bpy.data.objects['08 | Projector housing'];housing=bpy.data.objects['08 | Faceted projector armored housing']
assert housing.parent==projector
assert any(m.type=='BOOLEAN' and m.object is not None for m in housing.modifiers)
report={'result':'pass','cameraScreensShipAndLightsUnchanged':True,'sourceFilesUnchanged':True,'ownerSceneObjects':772,'missingImages':missing,'hoseAssemblyMovement':'pass','projectorHousingGrouped':True,'blendSha256':hashlib.sha256((root/'hardware.blend').read_bytes()).hexdigest(),'visualApproval':'pending'}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
