import bpy,json
from pathlib import Path
s=Path(__file__).parent;image_root=Path(globals().get('drawn_image_root',s/'generation/layers'))
renames=json.loads((s/'material-renames.json').read_text())
for old,new in renames.items():
 if bpy.data.materials.get(old):bpy.data.materials[old].name=new
palette=json.loads((s/'generation/palettes.json').read_text())['palettes']['plum-bone']
settings=json.loads((s/'generation/paint.json').read_text())['material']
images={name:bpy.data.images.load(str(image_root/(name+'.png')),check_existing=False) for name in ['Shadow','Light','Ink']}
for name,im in images.items():im.name='Bulwark Drawn '+name;im.colorspace_settings.name='Non-Color';im.use_fake_user=True
for name,basecolor in palette.items():
 m=bpy.data.materials[name];nodes=m.node_tree.nodes;links=m.node_tree.links
 alternate=nodes.get('Alternate base coat');base=alternate.outputs[0] if alternate else None
 def node(kind,label,x,y):
  n=nodes.new(kind);n.name=label;n.label=label;n.location=(x,y);return n
 def feed(value,socket):
  if isinstance(value,bpy.types.NodeSocket):links.new(value,socket)
  else:socket.default_value=value
 def mix(label,a,b,factor,x,y):
  n=node('ShaderNodeMixRGB',label,x,y);feed(factor,n.inputs[0]);feed(a,n.inputs[1]);feed(b,n.inputs[2]);return n.outputs[0]
 if alternate:
  for l in list(alternate.inputs[1].links):links.remove(l)
  alternate.inputs[1].default_value=basecolor
 else:base=basecolor
 uv=node('ShaderNodeUVMap','Drawn UV',-1100,650);uv.uv_map='PaintUV';tex={}
 for i,k in enumerate(images):
  n=node('ShaderNodeTexImage','Drawn '+k,-900,650-i*240);n.image=images[k];links.new(uv.outputs[0],n.inputs['Vector']);tex[k]=n
 shadow=node('ShaderNodeMath','Drawn shadow strength',-650,650);shadow.operation='MULTIPLY';links.new(tex['Shadow'].outputs['Color'],shadow.inputs[0]);shadow.inputs[1].default_value=settings['shadow_strength']
 shaded=mix('Drawn shadow',base,(0,0,0,1),shadow.outputs[0],-400,600)
 lift=mix('Drawn light color',base,(1,1,1,1),settings['light_lift'],-650,280)
 light=node('ShaderNodeMath','Drawn light strength',-400,280);light.operation='MULTIPLY';links.new(tex['Light'].outputs['Color'],light.inputs[0]);light.inputs[1].default_value=settings['light_strength']
 lit=mix('Drawn highlight',shaded,lift,light.outputs[0],-150,600)
 ink=node('ShaderNodeMath','Drawn ink strength',-150,170);ink.operation='MULTIPLY';links.new(tex['Ink'].outputs['Color'],ink.inputs[0]);ink.inputs[1].default_value=settings['ink_strength']
 final=mix('Drawn ink',lit,(*settings['ink_color'],1),ink.outputs[0],100,600)
 emission=node('ShaderNodeEmission','Drawn preview',350,600);links.new(final,emission.inputs['Color']);emission.inputs['Strength'].default_value=1
 output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');links.new(emission.outputs[0],output.inputs['Surface']);output.location=(580,600)
 for n in nodes:n.select=False
 active=next((n for n in nodes if n.type=='TEX_IMAGE' and n.image and n.image.name=='Bulwark BaseRegion'),tex['Light'])
 nodes.active=active;active.select=True
