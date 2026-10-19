import bpy,sys,json,hashlib,shutil,numpy as np
from pathlib import Path
s=Path(__file__).parent;sys.path.insert(0,str(s/'tool/art/tools/ship'));import ship_source
root=Path('D:/amind/git/agent-3/art/ships/bulwark');source=root/'Bulwark.blend';bpy.ops.wm.open_mainfile(filepath=str(source))
def pixels(im):
 a=np.empty(len(im.pixels),np.float32);im.pixels.foreach_get(a);return hashlib.sha256(a.tobytes()).hexdigest()
def coats():return {i.name:pixels(i) for i in bpy.data.images if 'BaseRegion' in i.name}
before=ship_source.fingerprint(bpy.context.scene);base=coats();uvs=json.loads((s/'armor-uvs.json').read_text())
for name,coords in uvs.items():
 layer=bpy.data.objects[name].data.uv_layers['PaintUV'];assert len(layer.data)==len(coords)
 for l,co in zip(layer.data,coords):l.uv=co
for name in ['Shadow','Light','Ink']:
 dst=root/'layers'/f'{name}.png';shutil.copyfile(s/'generation/layers'/f'{name}.png',dst)
 im=bpy.data.images['Bulwark Drawn '+name];tmp=bpy.data.images.load(str(dst),check_existing=False);tmp.colorspace_settings.name='Non-Color';a=np.empty(len(tmp.pixels),np.float32);tmp.pixels.foreach_get(a);im.pixels.foreach_set(a);raw=dst.read_bytes();im.pack(data=raw,data_len=len(raw));im.filepath='//layers/'+name+'.png';bpy.data.images.remove(tmp)
shutil.copytree(s/'generation/layers/baseline',root/'layers/baseline',dirs_exist_ok=True);shutil.copyfile(s/'generation/paint.json',root/'paint.json')
after=ship_source.fingerprint(bpy.context.scene)
assert before['components']['geometry']==after['components']['geometry'];assert before['components']['materials']==after['components']['materials']
changed=[n for n in before['parts'] if before['parts'][n]['uv']!=after['parts'][n]['uv']];assert sorted(changed)==sorted(uvs);assert base==coats()
maskhash={n:pixels(bpy.data.images['Bulwark Drawn '+n]) for n in ['Shadow','Light','Ink']}
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
bpy.ops.wm.open_mainfile(filepath=str(source));assert ship_source.fingerprint(bpy.context.scene)==after;assert base==coats();assert maskhash=={n:pixels(bpy.data.images['Bulwark Drawn '+n]) for n in maskhash}
(s/'verification.json').write_text(json.dumps({'geometry_unchanged':True,'material_assignments_unchanged':True,'changed_uv_parts':changed,'all_base_coat_pixels_unchanged':True,'base_hashes':base,'mask_hashes':maskhash,'fresh_reload_matches':True,'geometry_fingerprint':after['components']['geometry']},indent=2))
