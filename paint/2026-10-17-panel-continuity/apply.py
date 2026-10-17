import bpy,numpy as np,hashlib,json,runpy
from pathlib import Path
s=Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
capture=runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
def snap():return json.loads(json.dumps(capture({o.name:o for o in bpy.context.scene.objects})))
before=snap();original=json.loads((s.parent/'2026-10-17-symmetric-lines'/'after-data.json').read_text())
assert before['objects']==original['objects'],'Geometry or scene changed during cleanup'
assert before['images']['Bulwark BaseRegion']['pixels']=='a592ce991963eb7f39e45dcbfdf35aadbaf26280ea40795a62f1d84ada3cdf81'
im=bpy.data.images['Bulwark BaseRegion'];tmp=bpy.data.images.load(str(s/'BaseRegion.png'),check_existing=False);tmp.colorspace_settings.name='Non-Color'
a=np.empty(len(tmp.pixels),np.float32);tmp.pixels.foreach_get(a);assert len(a)==len(im.pixels);bpy.data.images.remove(tmp)
raw=(s/'BaseRegion.png').read_bytes();Path(bpy.path.abspath(im.filepath)).write_bytes(raw);im.pixels.foreach_set(a);im.update();im.pack(data=raw,data_len=len(raw))
after=snap();assert before['objects']==after['objects'];expected=hashlib.sha256(a.tobytes()).hexdigest();assert after['images']['Bulwark BaseRegion']['pixels']==expected
for k,v in before['images'].items():
 if k!='Bulwark BaseRegion':assert v==after['images'][k],k
v=bpy.context.preferences.filepaths.save_version;bpy.context.preferences.filepaths.save_version=0
try:bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
finally:bpy.context.preferences.filepaths.save_version=v
(s/'after-data.json').write_text(json.dumps(after,indent=2));(s/'verification.json').write_text(json.dumps({'geometry_uv_transforms_modifiers_materials_unchanged':True,'other_images_unchanged':True,'mask_pixels':expected,'mask_size':list(im.size),'owner_checkpoint':'b62e2387'},indent=2))

result={'saved':bpy.data.filepath,'mask_pixels':expected,'geometry_unchanged':True}
