import bpy,json,hashlib,sys
from pathlib import Path
root=Path('D:/amind/git/agent-4');sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source
source=root/'art/ships/vanguard/Vanguard.blend'
before_sha=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
before=ship_source.fingerprint(scene)
detail=bpy.data.collections.new('Uninked detail');scene.collection.children.link(detail)
names=['Pod cooling hatch','Cockpit cooling vent','Wing service hatch']
for name in names:detail.objects.link(scene.objects[name])
for lineset in bpy.context.view_layer.freestyle_settings.linesets:
 lineset.select_by_collection=True
 lineset.collection=detail
 lineset.collection_negation='EXCLUSIVE'
after=ship_source.fingerprint(scene)
assert before==after,'Line selection must not alter geometry, UVs or materials'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
record={'root_cause':'Inherited Freestyle line set selected crease edges on new detail assemblies',
 'correction':'Exclude the three approved detail assemblies from the supplemental Freestyle line pass',
 'geometry_uv_materials_unchanged':True,'geometry':after['components']['geometry'],
 'source_before':before_sha,'source_after':hashlib.sha256(source.read_bytes()).hexdigest(),
 'excluded_parts':names,'owner_reference':'codex-clipboard-97d4058b-9f45-4ca8-8965-8d31cc0a8833.png',
 'reference_matches':'paint/2026-10-18/preservation/review/preserved-brush_top.png',
 'unity_review':'deferred by owner; no Unity launch or runtime binding'}
(root/'results/vanguard-redo/authored-regions/line-selection-audit.json').write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps(record))
