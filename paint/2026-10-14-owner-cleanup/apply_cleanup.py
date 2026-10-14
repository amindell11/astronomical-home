import bpy
import hashlib
import json
import sys
from pathlib import Path
import numpy as np

root=Path('D:/amind/git/agent-3')
stage=root/'results/bulwark/paint/2026-10-14-owner-cleanup'
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source

assert Path(bpy.data.filepath).resolve()==(root/'art/ships/bulwark/Bulwark.blend').resolve()
snapshot=json.loads((stage/'owner-snapshot.json').read_text())
before=ship_source.fingerprint(bpy.context.scene)['components']
assert before['geometry']=='d3ebf4905127aed18fde3b192691931a4532bd7c91c0e3c374bdab2c17446a7b'
assert before['uv']=='31bd63956362eeb59ece366072ebd5f4e1d917ee99c1253b4fff5c3fc948c580'

def pixel_hash(image):
    values=np.empty(len(image.pixels),dtype=np.float32)
    image.pixels.foreach_get(values)
    return hashlib.sha256(values.tobytes()).hexdigest()

for key,meta in snapshot['images'].items():
    assert pixel_hash(bpy.data.images[meta['name']])==meta['sha256'],key+' changed during cleanup'
selection=sorted(o.name for o in bpy.context.selected_objects)
mode=bpy.context.mode
active=bpy.context.active_object.name
replacements={
    'BaseRegion':stage/'clean-BaseRegion.png',
    'Light':stage.parent/'2026-10-13-owner-setup/layers/Light.png',
    'Ink':stage.parent/'2026-10-13-owner-setup/layers/Ink.png',
}
report={}
for key,path in replacements.items():
    image=bpy.data.images[snapshot['images'][key]['name']]
    temporary=bpy.data.images.load(str(path),check_existing=False)
    temporary.colorspace_settings.name='Non-Color'
    values=np.empty(len(temporary.pixels),dtype=np.float32)
    temporary.pixels.foreach_get(values)
    assert len(values)==len(image.pixels)
    bpy.data.images.remove(temporary)
    raw=path.read_bytes()
    target=root/'art/ships/bulwark/layers'/f'{key}.png'
    target.write_bytes(raw)
    image.pixels.foreach_set(values)
    image.update()
    image.pack(data=raw,data_len=len(raw))
    assert bytes(image.packed_file.data)==raw
    report[key]={'file_sha256':hashlib.sha256(raw).hexdigest(),'pixel_sha256':pixel_hash(image),'packed_size':image.packed_file.size}

after=ship_source.fingerprint(bpy.context.scene)['components']
assert before['geometry']==after['geometry'] and before['uv']==after['uv']
assert before['materials']==after['materials']
assert pixel_hash(bpy.data.images[snapshot['images']['Shadow']['name']])==snapshot['images']['Shadow']['sha256']
assert sorted(o.name for o in bpy.context.selected_objects)==selection
assert bpy.context.mode==mode and bpy.context.active_object.name==active
save_version=bpy.context.preferences.filepaths.save_version
bpy.context.preferences.filepaths.save_version=0
try:
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
finally:
    bpy.context.preferences.filepaths.save_version=save_version
for area in bpy.context.screen.areas:area.tag_redraw()
result={'before':before,'after':after,'shadow_unchanged':True,'images':report,'mode':mode,'active':active,'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()}
(stage/'cleanup-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
