import bpy,json,hashlib
from pathlib import Path
out=Path('D:/amind/git/agent-2/results/valis-crimson-retry/v02')
def snapshot(path):
 bpy.ops.wm.open_mainfile(filepath=str(path))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 geo={o.name:dict(vertices=[list(o.matrix_world@v.co) for v in o.data.vertices],faces=[list(p.vertices) for p in o.data.polygons],modifiers=[[m.name,m.type] for m in o.modifiers]) for o in meshes}
 textures={im.name:hashlib.sha256(bytes(im.packed_file.data)).hexdigest() for im in bpy.data.images if im.packed_file and im.name.startswith('Valis ')}
 palette=json.loads(bpy.context.scene['palettes_json'])['palettes']['jade-iris']
 assert all(max(abs(a-b) for a,b in zip(bpy.data.materials[name].node_tree.nodes['BASE COAT — recolor here'].outputs[0].default_value,color))<1e-6 for name,color in palette.items())
 assert all(m.offset_v==-.5 for o in meshes for m in o.modifiers if m.type=='MIRROR')
 deps=bpy.context.evaluated_depsgraph_get()
 uvs=[tuple(loop.uv) for o in meshes for loop in o.evaluated_get(deps).data.uv_layers['ValisPaintUV'].data]
 assert all(0<=u<=1 and 0<=v<=1 for u,v in uvs)
 return dict(geometry_sha256=hashlib.sha256(json.dumps(geo,sort_keys=True,separators=(',',':')).encode()).hexdigest(),packed_texture_sha256=textures,base_palette_matches=True,independent_mirror_uvs=True,uv_range_valid=True)
a=snapshot(out/'Valis-Crimson-study.blend');b=snapshot(out/'control-check.blend');assert a==b
assert a['geometry_sha256']=='c713fb3e410c5c6b66c74ad0e5d5b4885984cfe709b026866cdc6e6ae8d73981'
a['controls_roundtrip_preserves_geometry_and_paint']=True
(out/'native-roundtrip-validation.json').write_text(json.dumps(a,indent=2))
print('NATIVE_ROUNDTRIP '+json.dumps(a))
