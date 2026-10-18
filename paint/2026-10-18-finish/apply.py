import bpy,json,runpy,shutil
from pathlib import Path
stage=Path(__file__).parent
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
bpy.ops.wm.open_mainfile(filepath=str(source))
capture=runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
def snapshot():return json.loads(json.dumps(capture({o.name:o for o in bpy.context.scene.objects})))
before=snapshot();(stage/'before-data.json').write_text(json.dumps(before,indent=2))
for name in ['FinishShadow','FinishLight']:shutil.copyfile(stage/(name+'.png'),source.parent/'layers'/(name+'.png'))
finish_image_root=source.parent/'layers'
exec(compile((stage/'finish_materials.py').read_text(),str(stage/'finish_materials.py'),'exec'),globals())
for im in bpy.data.images:
 im.use_fake_user=True
 if im.name in ['Bulwark FinishShadow','Bulwark FinishLight']:
  im.pack();im.filepath='//layers/'+im.name.removeprefix('Bulwark ')+'.png'
after=snapshot()
assert before['objects']==after['objects']
assert all(after['images'][k]==v for k,v in before['images'].items())
(stage/'after-data.json').write_text(json.dumps(after,indent=2))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
bpy.ops.wm.open_mainfile(filepath=str(source))
reloaded=snapshot()
assert reloaded==after
(stage/'verification.json').write_text(json.dumps({'objects_geometry_uv_assignments_unchanged':True,'all_original_image_pixels_and_packed_bytes_unchanged':True,'fresh_reload_matches':True,'base_region':reloaded['images']['Bulwark BaseRegion'],'new_images':[k for k in after['images'] if k not in before['images']],'deferred':['flight geometry lock','UV density','in-game review']},indent=2))
