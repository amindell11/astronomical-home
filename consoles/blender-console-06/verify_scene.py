"""Read console.blend in a fresh Blender process; write verification.json without saving the scene."""
import bpy, hashlib, json
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent
manifest=json.loads((root/'scene-manifest.json').read_text())
scene=bpy.context.scene
assert Path(bpy.data.filepath).resolve()==(root/'console.blend').resolve()
assert scene.camera.name=='Hero camera'
assert (scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage)==(1672,941,100)
expected='3cb1105ee199dd5f2476fa839a9b43623ef1713eb1f6c0ab823359e54489c372'
for path in [root/'inputs/owner-source.blend',root.parent/'refinement-04/inputs/owner-edited.blend']:
    assert hashlib.sha256(path.read_bytes()).hexdigest()==expected, str(path)
unpacked=[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file]
assert not unpacked, unpacked
for name in manifest['assemblyRoots']:
    assert bpy.data.objects[name].children, name
assert bpy.data.collections['99 Source archive (hidden)'].hide_render
assert not bpy.data.objects['Viewport | distant nebula backdrop'].hide_render
assert not bpy.data.objects['Projector | editable light volume'].hide_render
assert tuple(bpy.data.images['main-hud.png'].size)==(370,319)
assert tuple(bpy.data.images['detail-hud.png'].size)==(364,70)
cables=[o for o in scene.objects if o.name.startswith('Deck cable |')]
assert len(cables)==2 and all(o.type=='CURVE' for o in cables)
bpy.context.view_layer.update()
assembly=bpy.data.objects['Main console']
child=bpy.data.objects['Main display | approved B graphics']
before=child.matrix_world.translation.copy()
old=assembly.location.copy()
assembly.location.x+=.1
bpy.context.view_layer.update()
assert (child.matrix_world.translation-before-Vector((.1,0,0))).length<1e-4
assembly.location=old
bpy.context.view_layer.update()
selector=bpy.data.objects['Selector knob']
pointer=bpy.data.objects['Selector | pointer']
before=pointer.matrix_world.translation.copy()
old=selector.rotation_euler.copy()
selector.rotation_euler.rotate_axis('Z',.2)
bpy.context.view_layer.update()
assert (pointer.matrix_world.translation-before).length>1e-4
selector.rotation_euler=old
bpy.context.view_layer.update()
report={'result':'pass','blenderVersion':bpy.app.version_string,'camera':scene.camera.name,'resolution':[1672,941], 'sourceSha256':expected,'sourceUnchanged':True,'packedImages':len(manifest['packedImages']),'missingImages':unpacked,'mainScreen':[370,319],'detailScreen':[364,70],'assemblyTranslation':'pass','selectorLocalRotation':'pass','editableCableCurves':len(cables),'fileSha256':hashlib.sha256((root/'console.blend').read_bytes()).hexdigest()}
(root/'verification.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
