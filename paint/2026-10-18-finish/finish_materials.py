import bpy
from pathlib import Path
s=Path(globals().get('finish_image_root',Path(__file__).parent))
images={name:bpy.data.images.load(str(s/(name+'.png')),check_existing=False) for name in ['FinishShadow','FinishLight']}
for name,im in images.items():im.name='Bulwark '+name;im.colorspace_settings.name='Non-Color'
materials={m for o in bpy.context.scene.objects if o.type=='MESH' and o.visible_get() for m in o.data.materials if m}
for m in materials:
 nodes=m.node_tree.nodes;links=m.node_tree.links;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
 if m.name=='Bulwark Glass':
  bs.inputs['Roughness'].default_value=.36;bs.inputs['Coat Weight'].default_value=.25;bs.inputs['Coat Roughness'].default_value=.28;continue
 if nodes.get('Shadow'):
  nodes['Shadow'].image=images['FinishShadow'];nodes['Light'].image=images['FinishLight'];nodes['Math'].inputs[1].default_value=.75;nodes['Math.002'].inputs[1].default_value=.35
  alternate=nodes.get('Alternate base coat')
  if alternate:
   for l in list(alternate.inputs[1].links):links.remove(l)
   alternate.inputs[1].default_value=nodes['Base coat'].inputs[1].default_value
   links.new(alternate.outputs[0],nodes['Base coat'].inputs[1]);links.new(nodes['Mix (Legacy).001'].outputs[0],bs.inputs['Base Color'])
 else:
  color_source=bs.inputs['Base Color'].links[0].from_socket if bs.inputs['Base Color'].is_linked else None
  uv=nodes.new('ShaderNodeUVMap');uv.name='Finish UV';uv.uv_map='PaintUV'
  tex=nodes.new('ShaderNodeTexImage');tex.name='Finish Shadow';tex.image=images['FinishShadow'];links.new(uv.outputs['UV'],tex.inputs['Vector'])
  strength=nodes.new('ShaderNodeMath');strength.operation='MULTIPLY';strength.inputs[1].default_value=.65;links.new(tex.outputs['Color'],strength.inputs[0])
  value=nodes.new('ShaderNodeMath');value.operation='SUBTRACT';value.inputs[0].default_value=1;links.new(strength.outputs[0],value.inputs[1])
  mix=nodes.new('ShaderNodeMixRGB');mix.name='Finish shade';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=bs.inputs['Base Color'].default_value;links.new(value.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],bs.inputs['Base Color'])
  if color_source:links.new(color_source,mix.inputs[1])
  for i,n in enumerate([uv,tex,strength,value,mix]):n.location=(-1000+i*210,-350)
 if m.name=='Bulwark Metal':
  bs.inputs['Metallic'].default_value=.85;bs.inputs['Roughness'].default_value=.38;bs.inputs['Anisotropic'].default_value=.25
 else:
  bs.inputs['Roughness'].default_value=.72 if 'Charcoal' in m.name or 'Structure' in m.name else .66;bs.inputs['Coat Weight'].default_value=.12;bs.inputs['Coat Roughness'].default_value=.32
