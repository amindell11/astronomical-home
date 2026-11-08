"""Apply ink surface treatment to materials-19 without changing geometry. --name writes a new packed Blender file; existing output fails."""
import bpy,sys,json,hashlib,math
from pathlib import Path
root=Path(__file__).resolve().parent;args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose another output')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='20 - Ink and painted surfaces'
paint=['Warm limestone paint','Paint edge','Warm grey casing','Secondary cover paint','Taupe keys']
other=['Graphite chassis','Charcoal control grips','Control caps','Graphite cooling fins','Cooling fin edges','Brushed warm nickel','Satin rubber','Black elastomer']
changed=[]
for label in paint+other+['Launch amber resin']:
 old=bpy.data.materials['19 | '+label];m=old.copy();m.name='20 | '+label;changed.append(m.name)
 for o in s.objects:
  if hasattr(o.data,'materials'):
   for i,mat in enumerate(o.data.materials):
    if mat==old:o.data.materials[i]=m
 nt=m.node_tree;n=nt.nodes;L=nt.links;p=n['Principled BSDF'];base=p.inputs['Base Color'].default_value[:]
 lift={'Graphite chassis':1.45,'Graphite cooling fins':1.45,'Cooling fin edges':1.5,'Satin rubber':1.8,'Black elastomer':1.35}.get(label,1)
 base=tuple(x*lift for x in base[:3])+(1,)
 p.inputs['Roughness'].default_value=.82 if label in paint else .7;p.inputs['Specular IOR Level'].default_value=.12;p.inputs['Metallic'].default_value=.0 if label in paint else min(p.inputs['Metallic'].default_value,.35)
 coords=n.new('ShaderNodeTexCoord');coords.name='Surface coordinates'
 noise=n.new('ShaderNodeTexNoise');noise.name='Broad painted variation';noise.inputs['Scale'].default_value=3.4;noise.inputs['Detail'].default_value=1.2;L.new(coords.outputs['Generated'],noise.inputs['Vector'])
 ramp=n.new('ShaderNodeValToRGB');ramp.name='Restrained pigment variation';ramp.color_ramp.elements[0].position=.15;ramp.color_ramp.elements[0].color=tuple(x*.83 for x in base[:3])+(1,);ramp.color_ramp.elements[1].position=.85;ramp.color_ramp.elements[1].color=tuple(x*1.08 for x in base[:3])+(1,);L.new(noise.outputs['Fac'],ramp.inputs[0]);color=ramp.outputs['Color']
 if label in paint:
  # Sparse elongated marks sit in the pigment, without bump or displacement.
  mapping=n.new('ShaderNodeMapping');mapping.name='Direction of drawn scuffs';mapping.inputs['Scale'].default_value=(.7,9,3);mapping.inputs['Rotation'].default_value[2]=.35;L.new(coords.outputs['Generated'],mapping.inputs['Vector'])
  marks=n.new('ShaderNodeTexNoise');marks.name='Sparse drawn surface marks';marks.inputs['Scale'].default_value=18;marks.inputs['Detail'].default_value=1;marks.inputs['Roughness'].default_value=.7;L.new(mapping.outputs[0],marks.inputs['Vector'])
  mask=n.new('ShaderNodeValToRGB');mask.name='Isolated ink scratches';mask.color_ramp.elements[0].position=.715;mask.color_ramp.elements[0].color=(0,0,0,1);mask.color_ramp.elements[1].position=.76;mask.color_ramp.elements[1].color=(1,1,1,1);L.new(marks.outputs['Fac'],mask.inputs[0])
  mix=n.new('ShaderNodeMixRGB');mix.name='Drawn wear over paint';L.new(mask.outputs[0],mix.inputs[0]);L.new(color,mix.inputs[1]);mix.inputs[2].default_value=tuple(x*.35 for x in base[:3])+(1,);color=mix.outputs[0]
  # A few broad value steps give the flat painted faces graphic separation.
  geom=n.new('ShaderNodeNewGeometry');dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=(-.25,-.65,.72);L.new(geom.outputs['Normal'],dot.inputs[0])
  bands=n.new('ShaderNodeValToRGB');bands.name='Three broad painted tones';bands.color_ramp.interpolation='CONSTANT';bands.color_ramp.elements[0].position=0;bands.color_ramp.elements[0].color=(.63,.63,.63,1);bands.color_ramp.elements[1].position=.58;bands.color_ramp.elements[1].color=(1,1,1,1);mid=bands.color_ramp.elements.new(.28);mid.color=(.83,.83,.83,1);L.new(dot.outputs['Value'],bands.inputs[0])
  mult=n.new('ShaderNodeMixRGB');mult.name='Graphic face tones';mult.blend_type='MULTIPLY';mult.inputs[0].default_value=.6;L.new(color,mult.inputs[1]);L.new(bands.outputs[0],mult.inputs[2]);color=mult.outputs[0]
 L.new(color,p.inputs['Base Color'])
 if label in paint:
  diff=n.new('ShaderNodeBsdfDiffuse');diff.name='Matte illustrated paint';diff.inputs['Roughness'].default_value=.65;L.new(color,diff.inputs['Color']);L.new(diff.outputs[0],n['Material Output'].inputs['Surface'])
# Ink is a render treatment; it adds no curves or mesh shells.
inkcol=bpy.data.collections.new('20 - Console ink selection');s.collection.children.link(inkcol)
for o in list(s.objects):
 if o.type not in {'MESH','CURVE'}or o.hide_render or o.name.startswith(('07 reuse | Vanguard','19 | Vanguard'))or o.name in ['19 | Distant space','11 | Neutral projector frustum']:continue
 inkcol.objects.link(o)
s.render.use_freestyle=True;s.render.line_thickness=1.0
fs=s.view_layers[0].freestyle_settings
for ls in list(fs.linesets):fs.linesets.remove(ls)
ls=fs.linesets.new('Visible console contours');ls.select_by_collection=True;ls.collection=inkcol;ls.select_by_edge_types=True;ls.select_silhouette=True;ls.select_border=True;ls.select_crease=True;ls.select_contour=False;ls.select_external_contour=False;ls.select_material_boundary=False
fs.crease_angle=math.radians(110);fs.use_culling=True
style=ls.linestyle;style.name='20 | Warm black drawing ink';style.color=(.018,.012,.009);style.alpha=.9;style.thickness=1.35;style.caps='ROUND'
# The inherited fill light cuts a hard boundary across the Launch face.
bpy.data.objects['19 | Launch glow'].data.energy=0
s.cycles.samples=40
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'materials':changed,'geometry':'unchanged','ink':'Freestyle visible silhouettes and creases','approval':'pending user review','lightingChange':'Disabled extra Launch fill light; retained button emission and all object transforms.'},indent=2))
