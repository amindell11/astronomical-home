"""Restyle a loaded ink-materials-20 scene. --name writes a new packed blend and manifest; refuses existing output."""
import bpy,sys,math,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent;args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='21 - Painted planes and ink'
paint=['Warm limestone paint','Paint edge','Warm grey casing','Secondary cover paint','Taupe keys']
other=['Graphite chassis','Charcoal control grips','Control caps','Graphite cooling fins','Cooling fin edges','Brushed warm nickel','Satin rubber','Black elastomer']
changed=[]
for label in paint+other:
 old=bpy.data.materials['20 | '+label];m=old.copy();m.name='21 | '+label;changed.append(m.name)
 oldr=old.node_tree.nodes['Restrained pigment variation'].color_ramp.elements
 base=tuple((oldr[0].color[i]+oldr[1].color[i])/1.91 for i in range(3))
 if label=='Paint edge':base=tuple(x*1.22 for x in base)
 for o in s.objects:
  if hasattr(o.data,'materials'):
   for i,mat in enumerate(o.data.materials):
    if mat==old:o.data.materials[i]=m
 nt=m.node_tree;n=nt.nodes;L=nt.links;n.clear()
 def node(typ,label):
  a=n.new(typ);a.name=a.label=label;return a
 def ramp(label,entries,interpolation='EASE'):
  a=node('ShaderNodeValToRGB',label);r=a.color_ramp;r.interpolation=interpolation
  r.elements.remove(r.elements[1])
  for j,(pos,col)in enumerate(entries):
   e=r.elements[0]if j==0 else r.elements.new(pos);e.position=pos;e.color=tuple(col)+(1,)
  return a
 coords=node('ShaderNodeTexCoord','Local paint coordinates');geom=node('ShaderNodeNewGeometry','Existing surface normals')
 mapping=node('ShaderNodeMapping','Broad directional brush patches');mapping.inputs['Scale'].default_value=(1.4,2.0,1.1);mapping.inputs['Rotation'].default_value[2]=.25;L.new(coords.outputs['Generated'],mapping.inputs[0])
 noise=node('ShaderNodeTexNoise','Large painted value shapes');noise.inputs['Scale'].default_value=2.2;noise.inputs['Detail'].default_value=1.3;noise.inputs['Roughness'].default_value=.6;L.new(mapping.outputs[0],noise.inputs[0])
 pigment=ramp('Three brush-mixed paint values',[(.26,tuple(x*.84 for x in base)),(.48,tuple(x*.97 for x in base)),(.67,tuple(x*1.10 for x in base))]);L.new(noise.outputs['Fac'],pigment.inputs[0])
 dot=node('ShaderNodeVectorMath','Drawn light direction');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=(-.25,-.55,.797);L.new(geom.outputs['Normal'],dot.inputs[0])
 tones=ramp('Painted shadow middle and light',[(0,(.35,.37,.41)),(.34,(.64,.63,.61)),(.73,(1.07,1.00,.89))],'CONSTANT');L.new(dot.outputs['Value'],tones.inputs[0])
 mult=node('ShaderNodeMixRGB','Painted planes');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1;L.new(pigment.outputs[0],mult.inputs[1]);L.new(tones.outputs[0],mult.inputs[2]);color=mult.outputs[0]
 if label in paint:
  scratchmap=node('ShaderNodeMapping','Short drawn marks');scratchmap.inputs['Scale'].default_value=(.6,8,2.5);scratchmap.inputs['Rotation'].default_value[2]=.3;L.new(coords.outputs['Generated'],scratchmap.inputs[0])
  scratches=node('ShaderNodeTexNoise','Sparse ink ticks');scratches.inputs['Scale'].default_value=12;scratches.inputs['Detail'].default_value=1;L.new(scratchmap.outputs[0],scratches.inputs[0])
  mask=ramp('Isolated marks',[(.735,(0,0,0)),(.79,(.72,.72,.72))]);L.new(scratches.outputs['Fac'],mask.inputs[0])
  marks=node('ShaderNodeMixRGB','A few drawn surface marks');L.new(mask.outputs[0],marks.inputs[0]);L.new(color,marks.inputs[1]);marks.inputs[2].default_value=tuple(x*.20 for x in base)+(1,);color=marks.outputs[0]
 diff=node('ShaderNodeBsdfDiffuse','Small natural shadow contribution');diff.inputs['Roughness'].default_value=.85;L.new(color,diff.inputs['Color'])
 em=node('ShaderNodeEmission','Painted color independent of smooth reflections');em.inputs['Strength'].default_value=.48 if label in paint else .70;L.new(color,em.inputs['Color'])
 mix=node('ShaderNodeMixShader','Mostly painted with restrained light response');mix.inputs[0].default_value=.72;L.new(diff.outputs[0],mix.inputs[1]);L.new(em.outputs[0],mix.inputs[2])
 output=node('ShaderNodeOutputMaterial','Material Output');L.new(mix.outputs[0],output.inputs['Surface'])
# Line weights vary in screen space; original surfaces and edges stay intact.
fs=s.view_layers[0].freestyle_settings
inkcol=bpy.data.collections.new('21 - Console hardware ink');s.collection.children.link(inkcol)
for o in bpy.data.collections['20 - Console ink selection'].objects:
 if o.name in ['07 | Main rectangular display','07 | Detail rectangular display','10 | Cooling shoulder structural backing','14 | Hose socket inset','14 | Socket perimeter','09 | Projector optical well'] or o.name.startswith(('09 | Projector lens','14 | Flush hose gland','15 | Fascia tube seal')) or any(m and ('status glass' in m.name or 'optical emitter' in m.name) for m in o.data.materials):continue
 inkcol.objects.link(o)
for ls in list(fs.linesets):fs.linesets.remove(ls)
for label,width,outer in [('Outer ink contour',3.25,True),('Internal ink folds',1.65,False)]:
 ls=fs.linesets.new(label);ls.select_by_collection=True;ls.collection=inkcol;ls.select_by_edge_types=True;ls.select_silhouette=outer;ls.select_border=False;ls.select_crease=not outer;ls.select_contour=False;ls.select_external_contour=False;ls.select_material_boundary=False
 st=ls.linestyle;st.name='21 | '+label;st.color=(.006,.004,.003);st.alpha=1;st.thickness=width;st.caps='ROUND';st.use_chaining=True
 noise=st.thickness_modifiers.new('Slight pen pressure','NOISE');noise.amplitude=.35 if outer else .20;noise.period=9;noise.seed=21
fs.use_smoothness=False;fs.crease_angle=math.radians(134);s.render.line_thickness=1;s.cycles.samples=40
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'materials':changed,'geometry':'unchanged','lighting':'unchanged','ink':'Separate Freestyle outer and internal weights, selective hardware contours and slight pen-pressure variation','approval':'pending user review'},indent=2))
