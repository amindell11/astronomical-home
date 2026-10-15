import bpy
import bmesh
import hashlib
import json
import sys
from pathlib import Path
import numpy as np

stage=Path(__file__).parent
sys.path.insert(0,'D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-front-plate')
import snapshot

def edit_geometry():
    state={}
    for o in bpy.context.scene.objects:
        if o.type!='MESH' or o.mode!='EDIT':continue
        bm=bmesh.from_edit_mesh(o.data)
        bm.verts.ensure_lookup_table();bm.verts.index_update()
        uv=bm.loops.layers.uv.get('PaintUV')
        data={'vertices':[list(v.co) for v in bm.verts],
              'faces':[[v.index for v in f.verts] for f in bm.faces],
              'uv':[[list(loop[uv].uv) for loop in f.loops] for f in bm.faces] if uv else []}
        state[o.name]=hashlib.sha256(json.dumps(data).encode()).hexdigest()
    return state

before=snapshot.capture();edit_before=edit_geometry()
old=bpy.data.images['Bulwark BaseRegion.001']
values=np.empty(len(old.pixels),dtype=np.float32);old.pixels.foreach_get(values)
assert hashlib.sha256(values.tobytes()).hexdigest()=='1a03ae8310fdc746f7635c00bc00c0f75054814354ab95eed087897ad7247965'
path=Path('D:/amind/git/agent-3/art/ships/bulwark/layers/ArmorBaseRegion.png')
image=bpy.data.images.load(str(path),check_existing=False)
image.colorspace_settings.name='Non-Color'
image.name='Bulwark Armor BaseRegion'
image.filepath='//layers/ArmorBaseRegion.png'
image.pack()
actual=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(actual)
assert np.array_equal(actual,values), 'Saved armor paint differs from original buffer'
for material in bpy.data.materials:
    if material.use_nodes:
        for node in material.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image==old:node.image=image

source=bpy.data.materials['Bulwark Structure']
source_shader=source.node_tree.nodes['Principled BSDF']
color=list(source.node_tree.nodes['Base coat'].inputs[1].default_value)
assert bpy.data.materials.get('Bulwark Armor Charcoal') is None
material=bpy.data.materials.new('Bulwark Armor Charcoal')
material.use_nodes=True;material.diffuse_color=color
shader=material.node_tree.nodes['Principled BSDF']
shader.inputs['Base Color'].default_value=color
shader.inputs['Roughness'].default_value=source_shader.inputs['Roughness'].default_value
shader.inputs['Metallic'].default_value=source_shader.inputs['Metallic'].default_value
material.roughness=source_shader.inputs['Roughness'].default_value
targets=['09 Side armor.002','15 Front armor']
for name in targets:
    o=bpy.data.objects[name]
    o.data.materials.append(material);slot=len(o.data.materials)-1
    if o.mode=='EDIT':
        bm=bmesh.from_edit_mesh(o.data)
        for face in bm.faces:face.material_index=slot
        bmesh.update_edit_mesh(o.data,loop_triangles=False,destructive=False)
    else:
        for face in o.data.polygons:face.material_index=slot
    o.active_material_index=slot

bpy.context.view_layer.update()
after=snapshot.capture();edit_after=edit_geometry()
assert edit_before==edit_after
assert before['fingerprint']['components']['geometry']==after['fingerprint']['components']['geometry']
assert before['fingerprint']['components']['uv']==after['fingerprint']['components']['uv']
assert all(before['images'][k]['pixels']==after['images'][k]['pixels'] for k in before['images'])
for name,fp in before['fingerprint']['parts'].items():
    if name not in targets:assert fp==after['fingerprint']['parts'][name],name
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL';area.tag_redraw()
report={'before':before,'after':after,'edit_geometry_unchanged':edit_before==edit_after,
        'geometry_unchanged':True,'uv_unchanged':True,'paint_pixels_preserved':True,
        'targets':targets,'material':material.name,'color':'#302E34','saved_blend':False}
(stage/'live-applied.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
