"""Load perspective.blend in a fresh Blender process; write verification.json without saving."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
s=bpy.context.scene
assert s.name=='07 - Perspective and form review'
assert s.camera.name=='07 Camera - perspective' and s.camera.data.type=='PERSP'
assert (s.render.resolution_x,s.render.resolution_y,s.render.resolution_percentage)==(1672,941,100)
original=bpy.data.scenes['SOURCE - owner saved scene']
assert len(original.objects)==772
expected='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
for path in [root.parent/'blender-console-06/inputs/owner-source.blend',root.parent/'refinement-04/inputs/owner-edited.blend']:
    assert hashlib.sha256(path.read_bytes()).hexdigest()==expected
missing=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file]
assert not missing,missing
assembly=bpy.data.objects['07 | Monitor assembly'];child=bpy.data.objects['07 | Main rectangular display']
bpy.context.view_layer.update();before=child.matrix_world.translation.copy();old=assembly.location.copy()
assembly.location.x+=.1;bpy.context.view_layer.update()
assert (child.matrix_world.translation-before-Vector((.1,0,0))).length<1e-4
assembly.location=old;bpy.context.view_layer.update()
report={'result':'pass','camera':s.camera.name,'perspectiveLens':s.camera.data.lens,'sourceSceneObjects':len(original.objects),'sourceFilesUnchanged':True,'missingImages':missing,'assemblyMovement':'pass','visualApproval':'pending','fileSha256':hashlib.sha256((root/'perspective.blend').read_bytes()).hexdigest()}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
