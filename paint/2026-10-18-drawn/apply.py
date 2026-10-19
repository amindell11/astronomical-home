import bpy,json,runpy,shutil
from pathlib import Path
s=Path(__file__).parent;root=Path('D:/amind/git/agent-3/art/ships/bulwark');source=root/'Bulwark.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
capture=runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
def snapshot():return json.loads(json.dumps(capture({o.name:o for o in bpy.context.scene.objects})))
before=snapshot();(s/'before-data.json').write_text(json.dumps(before,indent=2))
for n in ['Shadow','Light','Ink']:shutil.copyfile(s/'generation/layers'/f'{n}.png',root/'layers'/f'{n}.png')
shutil.copytree(s/'generation/layers/baseline',root/'layers/baseline',dirs_exist_ok=True)
for n in ['paint.json','palettes.json']:shutil.copyfile(s/'generation'/n,root/n)
(root/'textures').mkdir(exist_ok=True);shutil.copyfile(s/'generation/textures/painted-brush-source.png',root/'textures/painted-brush-source.png')
drawn_image_root=root/'layers'
exec(compile((s/'drawn_materials.py').read_text(),str(s/'drawn_materials.py'),'exec'),globals())
for im in bpy.data.images:
 im.use_fake_user=True
 if im.name.startswith('Bulwark Drawn '):
  im.pack();im.filepath='//layers/'+im.name.removeprefix('Bulwark Drawn ')+'.png'
after=snapshot();expected=json.loads(json.dumps(before['objects']))
for o in expected.values():
 if 'materials' in o:o['materials']=[renames.get(n,n) for n in o['materials']]
assert expected==after['objects']
assert all(after['images'][k]==v for k,v in before['images'].items())
(s/'after-data.json').write_text(json.dumps(after,indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
bpy.ops.wm.open_mainfile(filepath=str(source));reloaded=snapshot();assert reloaded==after
(s/'verification.json').write_text(json.dumps({'geometry_uv_transforms_modifiers_face_assignments_unchanged':True,'material_name_changes_only':renames,'all_original_image_pixels_and_packed_bytes_unchanged':True,'base_region':reloaded['images']['Bulwark BaseRegion'],'fresh_reload_matches':True,'flight_approval_pending':True,'uv_density_pending':True},indent=2))
