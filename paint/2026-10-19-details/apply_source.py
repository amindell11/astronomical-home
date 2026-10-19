import bpy, sys, json, hashlib, shutil
import numpy as np
from pathlib import Path
from types import SimpleNamespace
stage=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-19-details')
root=Path('D:/amind/git/agent-3/art/ships/bulwark')
assert Path(bpy.data.filepath).resolve()==(root/'Bulwark.blend').resolve()
assert not bpy.data.is_dirty
assert not [i.name for i in bpy.data.images if i.is_dirty]
sys.path.insert(0,str(stage.parent/'2026-10-18-asymmetric/tool/art/tools/ship'))
import ship_source,ship_regions,ship_paint

def image_hash(im):
    a=np.empty(len(im.pixels),np.float32);im.pixels.foreach_get(a)
    return hashlib.sha256(a.tobytes()).hexdigest()

def protected_images():
    return {im.name:image_hash(im) for im in bpy.data.images if 'BaseRegion' in im.name or im.name=='Bulwark Drawn Shadow'}

def baseline_hashes():
    return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'layers/baseline').glob('*')}

before=ship_source.fingerprint(bpy.context.scene);protected=protected_images();baseline=baseline_hashes()
size=2048;canopy=np.zeros((size,size),bool);deps=bpy.context.evaluated_depsgraph_get()
for name in ('Cockpit - Canopy','Cockpit - Seating rim'):
    ev=bpy.data.objects[name].evaluated_get(deps);mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    part=SimpleNamespace(mesh=mesh,co=np.array([list(ev.matrix_world@v.co) for v in mesh.vertices]))
    for tri,xs,ys,p in ship_regions.surface_samples(part,size,'PaintUV'):
        canopy[ys,xs]=True
    ev.to_mesh_clear()
for name in ('Ink','Light'):
    old=ship_paint.read_gray(root/'layers'/f'{name}.png');new=ship_paint.read_gray(stage/'masks'/f'{name}.png')
    assert np.array_equal(old[canopy],new[canopy]),name+' cockpit changed'

mask_hashes={}
for name in ('Ink','Light','DetailEmission'):
    dst=root/'layers'/f'{name}.png';shutil.copyfile(stage/'masks'/f'{name}.png',dst)
    tmp=bpy.data.images.load(str(dst),check_existing=False);tmp.colorspace_settings.name='Non-Color'
    if name in ('Ink','Light'):
        old=bpy.data.images['Bulwark Drawn '+name];old_name=old.name
        old.user_remap(tmp);bpy.data.images.remove(old);tmp.name=old_name
    else:tmp.name='Bulwark Detail Emission'
    tmp.pack();tmp.filepath='//layers/'+name+'.png';tmp.use_fake_user=True
    mask_hashes[tmp.name]=image_hash(tmp)

def linear(c):return ((c+.055)/1.055)**2.4 if c>.04045 else c/12.92
color=tuple(linear(x/255) for x in (226,185,250))+(1.,)
for mat in bpy.data.materials:
    if not mat.use_nodes or 'Drawn preview' not in mat.node_tree.nodes:continue
    tree=mat.node_tree;n=tree.nodes;preview=n['Drawn preview']
    tex=n.new('ShaderNodeTexImage');tex.name='Detail Emission mask';tex.label='Editable lilac light coverage';tex.image=bpy.data.images['Bulwark Detail Emission'];tex.location=(800,-700)
    tree.links.new(n['Drawn UV'].outputs['UV'],tex.inputs['Vector'])
    mix=n.new('ShaderNodeMixRGB');mix.name='Detail emissive color';mix.label='Lilac lights - recolor here';mix.inputs[2].default_value=color;mix.location=(1100,-300)
    tree.links.new(preview.inputs['Color'].links[0].from_socket,mix.inputs[1]);tree.links.new(tex.outputs['Color'],mix.inputs[0]);tree.links.new(mix.outputs[0],preview.inputs['Color'])
    strength=n.new('ShaderNodeMath');strength.name='Detail emission energy';strength.operation='MULTIPLY_ADD';strength.inputs[1].default_value=.15;strength.inputs[2].default_value=1;strength.location=(1100,-550)
    tree.links.new(tex.outputs['Color'],strength.inputs[0]);tree.links.new(strength.outputs[0],preview.inputs['Strength'])

assert before==ship_source.fingerprint(bpy.context.scene)
assert protected==protected_images()
assert baseline==baseline_hashes()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Bulwark.blend'),check_existing=False)
record={'geometry_uv_assignments_unchanged':True,'cockpit_mask_pixels_unchanged':True,'protected_image_hashes':protected,'baseline_file_hashes':baseline,'mask_hashes':mask_hashes,'fingerprint':before}
(stage/'verification-before-reload.json').write_text(json.dumps(record,indent=2))
result={'saved':str(root/'Bulwark.blend'),'geometry_uv_assignments_unchanged':True,'base_coat_shadow_cockpit_preserved':True,'baseline_unchanged':True}
