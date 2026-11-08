"""Restyle loaded wear-materials-22. --name saves a new packed scene and manifest; refuses overwrite."""
import bpy,sys,json,hashlib,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='23 - Warm electronic light and drawn hardware'
paint=['Warm limestone paint','Paint edge','Warm grey casing','Secondary cover paint','Taupe keys']
active={slot.material for o in s.objects for slot in o.material_slots if slot.material}
changed=[]
for m in active:
 if not m.use_nodes:continue
 n=m.node_tree.nodes;L=m.node_tree.links
 if 'Three brush-mixed paint values' in n:
  label=m.name.split(' / ')[-1].split(' | ')[-1];ispaint=label in paint
  factor=1.15 if ispaint else 1.10
  if label=='Secondary cover paint':factor=1.5
  for e in n['Three brush-mixed paint values'].color_ramp.elements:
   e.color=tuple(e.color[i]*factor*(1.05,1.01,.99)[i] for i in range(3))+(1,)
  tones=n['Painted shadow middle and light'].color_ramp.elements
  for e,col in zip(tones,[(.48,.40,.35),(.82,.73,.64),(1.22,1.08,.93)]):e.color=col+(1,)
  n['Painted color independent of smooth reflections'].inputs['Strength'].default_value=.82 if ispaint else .9
  n['Mostly painted with restrained light response'].inputs[0].default_value=.50 if ispaint else .85
  n['Large painted value shapes'].inputs['Scale'].default_value=1.7
  n['Large painted value shapes'].inputs['Detail'].default_value=.6
  n['Broad directional brush patches'].inputs['Scale'].default_value=(2.5,.8,1.0)
  graphite={'Graphite cooling fins':(.080,.078,.086),'Cooling fin edges':(.034,.034,.040),'Satin rubber':(.027,.026,.030),'Graphite chassis':(.045,.044,.050)}
  if label in graphite:
   for e,f in zip(n['Three brush-mixed paint values'].color_ramp.elements,[.84,.97,1.10]):e.color=tuple(c*f for c in graphite[label])+(1,)
   for e,col in zip(tones,[(.48,.48,.51),(.73,.74,.78),(1.02,1.02,1.05)]):e.color=col+(1,)
   n['Mostly painted with restrained light response'].inputs[0].default_value=.94
  changed.append(m.name)
for o in s.objects:
 if o.type!='LIGHT':continue
 if o.name=='19 | Projection spill':o.data.energy=1700;o.data.color=(1,.69,.38);o.data.size=3
 if o.name=='19 | Screen bounce':o.data.energy=850;o.data.color=(1,.81,.58);o.data.size=3
 if o.name=='19 | Faint starlight':o.data.energy=65;o.data.color=(.78,.80,1)
 if o.name=='19 | Soft ambient readability':o.data.energy=200;o.data.color=(1,.90,.78)
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.20,.17,.15,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value=.18
for m in active:
 if not m.use_nodes:continue
 n=m.node_tree.nodes;L=m.node_tree.links
 if m.name in ['19 | Main rectangular display','19 | Detail rectangular display']:
  p=n['Principled BSDF'];color=p.inputs['Emission Color'].links[0].from_socket
  tint=n.new('ShaderNodeMixRGB');tint.name='Warm phosphor tint';tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1,.78,.54,1);L.new(color,tint.inputs[1])
  L.new(tint.outputs[0],p.inputs['Base Color']);L.new(tint.outputs[0],p.inputs['Emission Color'])
  p.inputs['Emission Strength'].default_value=2.8;p.inputs['Roughness'].default_value=.9;p.inputs['Specular IOR Level'].default_value=0
 if m.name=='19 | Distant nebula':
  next(n for n in n if n.type=='EMISSION').inputs['Strength'].default_value=.105
 if m.name in ['19 | Projector optical emitter','19 | Amber status glass']:
  p=n.get('Principled BSDF')
  if p:p.inputs['Emission Color'].default_value=(1,.31,.035,1);p.inputs['Emission Strength'].default_value=5
  if 'Projector' in m.name:
   coord=n.new('ShaderNodeTexCoord');dist=n.new('ShaderNodeVectorMath');dist.operation='DISTANCE';dist.inputs[1].default_value=(.5,.5,1);L.new(coord.outputs['Generated'],dist.inputs[0])
   ramp=n.new('ShaderNodeValToRGB');ramp.name='Cream hot center to amber lens edges';ramp.color_ramp.elements[0].position=.10;ramp.color_ramp.elements[0].color=(1,.72,.32,1);ramp.color_ramp.elements[1].position=.52;ramp.color_ramp.elements[1].color=(1,.16,.012,1);L.new(dist.outputs['Value'],ramp.inputs[0]);L.new(ramp.outputs[0],p.inputs['Emission Color'])
 if m.name=='19 | Warm projection beam':
  v=n['Principled Volume'];v.inputs['Color'].default_value=(1,.66,.37,1);v.inputs['Emission Color'].default_value=(1,.38,.09,1)
  n['Map Range'].inputs['From Min'].default_value=1.67;n['Map Range'].inputs['From Max'].default_value=5.05
  for link in list(n['Math.001'].inputs[0].links):L.remove(link)
  for link in list(n['Math.001'].inputs[1].links):L.remove(link)
  power=n.new('ShaderNodeMath');power.operation='POWER';power.inputs[1].default_value=2.4;L.new(n['Map Range'].outputs[0],power.inputs[0]);L.new(power.outputs[0],n['Math.001'].inputs[0]);n['Math.001'].inputs[1].default_value=.95
  for link in list(v.inputs['Density'].links):L.remove(link)
  v.inputs['Density'].default_value=.001
 if m.name=='19 | Projected hull outlines':
  p=n.get('Principled BSDF')
  if p:p.inputs['Emission Color'].default_value=(1,.65,.28,1);p.inputs['Emission Strength'].default_value=4
 if m.name.startswith('19 | HOLO'):
  p=n.get('Muted painted projection')
  if p:p.inputs['Emission Strength'].default_value=1.0
for ls in s.view_layers[0].freestyle_settings.linesets:
 st=ls.linestyle;st.thickness=2.3 if 'Outer' in ls.name else 1.05
 st.color=(.012,.007,.005)
for n in s.compositing_node_group.nodes:
 if n.type=='GLARE':n.inputs['Threshold'].default_value=1.1;n.inputs['Strength'].default_value=.32;n.inputs['Size'].default_value=.35

dg=bpy.context.evaluated_depsgraph_get();cam=s.camera;width=s.render.resolution_x;height=s.render.resolution_y
invp=cam.calc_matrix_camera(dg,x=width,y=height).inverted()
def ray(pixel):
 p=invp@Vector((2*pixel[0]/width-1,1-2*pixel[1]/height,-1,1));p=p.xyz/p.w
 return cam.matrix_world.translation,(cam.matrix_world.to_3x3()@p).normalized()
def hit(o,pixel):
 origin,direction=ray(pixel);inv=o.matrix_world.inverted()
 ok,p,n,face=o.ray_cast(inv@origin,(inv.to_3x3()@direction).normalized(),depsgraph=dg)
 if not ok:raise RuntimeError('Drawing misses '+o.name+' at '+str(pixel))
 return p
marks={}
def stroke(obj,kind,points,w=1.3):marks.setdefault(obj,[]).append((kind,points,w))
frame='08 | Thick monitor armor frame';control='14 | Thin fitted control face';brow='08 | Raised brow cap'
projector='14 | Projector formed armor';forearm='14 | Forearm fold into Launch and controls'
# Paired light and dark strokes describe existing edges, without inventing folds.
for obj,points,w in [
 (frame,[(69,530),(66,561),(62,590)],2),(frame,[(53,722),(48,763)],2),
 (frame,[(49,807),(45,843)],1.8),(frame,[(155,934),(215,929),(260,925)],2),
 (frame,[(535,530),(541,570)],1.8),(frame,[(550,708),(553,750)],1.6),
 (brow,[(178,448),(230,447),(278,447)],2),(brow,[(301,447),(355,446)],1.4),
 (projector,[(893,707),(920,700),(945,701)],1.8),(projector,[(853,818),(889,831),(922,831)],2),
 (forearm,[(1046,842),(1084,841),(1112,841)],1.4),
 (control,[(1182,718),(1183,746),(1204,761)],1.8),(control,[(1462,778),(1496,777),(1526,759)],1.6)]:stroke(obj,'light',points,w)
for obj,points,w in [
 ('07 | Continuous inner gasket',[(183,490),(203,490),(235,490)],1.5),
 ('07 | Continuous inner gasket',[(255,490),(278,490)],1.1),
 (frame,[(168,539),(167,555),(166,571)],1.7),
 (frame,[(164,606),(163,619),(162,631)],1.4),
 (frame,[(155,768),(154,782),(154,791)],1.2)]:stroke(obj,'light',points,w)
for x,y in [(163,510),(162,515),(161,521)]:stroke(frame,'dark',[(x,y),(x-4,y+2)],1.1)
# Knob indexing marks and a service plate are printed onto the existing shell.
for a in [-135,-110,-85,-60,-35,-10]:
 rad=math.radians(a);cx,cy=97,654
 stroke(frame,'dark',[(cx+47*math.cos(rad),cy+39*math.sin(rad)),(cx+53*math.cos(rad),cy+45*math.sin(rad))],1.7)
for a in [-160,-130,-100,-70,-40,-10]:
 rad=math.radians(a);cx,cy=1389,731
 stroke(control,'dark',[(cx+51*math.cos(rad),cy+29*math.sin(rad)),(cx+57*math.cos(rad),cy+34*math.sin(rad))],1.4)
stroke(frame,'dark',[(64,813),(112,810),(110,834),(62,837),(64,813)],1.2)
for x,ln in [(69,20),(69,31),(69,15)]:
 y=818+len(marks[frame])%3*5;stroke(frame,'dark',[(x,y),(x+ln,y-1)],1)
for x,y in [(1122,817),(1141,812),(1160,807)]:stroke(forearm,'dark',[(x,y),(x+5,y-7)],1.4)
for x,y in [(897,801),(902,804),(908,806)]:stroke(projector,'dark',[(x,y),(x+5,y-7)],1.2)
for x,y in [(1289,775),(1295,775),(1301,775)]:stroke(control,'dark',[(x,y),(x+2,y-6)],1.1)
# Stencils use the shared Rajdhani font masks, packed with the Blender scene.
labels={brow:('display-stencil.png',263,458,10),control:('projection-stencil.png',1285,694,10)}
stroke(control,'dark',[(1206,695),(1233,695)],1.1);stroke(control,'dark',[(1332,695),(1351,695)],1.1)
for obj,pts,w in [(brow,[(200,460),(217,461),(239,460)],11),(frame,[(47,831),(53,814),(56,791)],13),(frame,[(539,552),(542,568),(541,580)],9),(forearm,[(1087,805),(1107,804),(1129,806)],15),(control,[(1197,737),(1201,749),(1219,757)],10),(projector,[(869,807),(881,812),(902,812)],14)]:stroke(obj,'brush',pts,w)

prepared={};misses=[]
for objname,strokes in marks.items():
 o=bpy.data.objects[objname];local=[]
 for kind,pixels,w in strokes:
  try:
   points=[hit(o,p)for p in pixels];center=points[0]
   origin,direction=ray((pixels[0][0]+w/2,pixels[0][1]));worldcenter=o.matrix_world@center;viewnormal=cam.matrix_world.to_3x3()@Vector((0,0,-1))
   distance=(worldcenter-origin).dot(viewnormal)/direction.dot(viewnormal);radius=(o.matrix_world.inverted()@(origin+distance*direction)-center).length
   local.append({'kind':kind,'points':[list(v)for v in points],'radius':radius,'pixels':pixels})
  except RuntimeError as e:misses.append(str(e))
 prepared[objname]=local
if misses:
 (root/(name+'-misses.json')).write_text(json.dumps(misses,indent=2));raise RuntimeError('\n'.join(misses))
for o in s.objects:
 if o.name.startswith('08 | Large round captive bolt'):
  z=max(v.co.z for v in o.data.vertices)
  prepared[o.name]=[{'kind':'dark','points':[[-.078,-.022,z],[.078,.022,z]],'radius':.010,'pixels':None},{'kind':'light','points':[[-.063,-.037,z],[.064,-.001,z]],'radius':.005,'pixels':None}]
def group_for(o,strokes):
 g=bpy.data.node_groups.new('23 | Surface drawing on '+o.name.split(' | ')[-1],'ShaderNodeTree');g.interface.new_socket(name='Local position',in_out='INPUT',socket_type='NodeSocketVector')
 for label in ['dark','light','brush']:g.interface.new_socket(name=label,in_out='OUTPUT',socket_type='NodeSocketFloat')
 n=g.nodes;L=g.links;inp=n.new('NodeGroupInput');output=n.new('NodeGroupOutput');totals={}
 for j,st in enumerate(strokes):
  for aa,bb in zip(st['points'],st['points'][1:]):
   a=Vector(aa);v=Vector(bb)-a
   sub=n.new('ShaderNodeVectorMath');sub.operation='SUBTRACT';L.new(inp.outputs[0],sub.inputs[0]);sub.inputs[1].default_value=a
   dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';L.new(sub.outputs[0],dot.inputs[0]);dot.inputs[1].default_value=v/v.length_squared
   t=n.new('ShaderNodeClamp');L.new(dot.outputs['Value'],t.inputs[0])
   scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[0].default_value=v;L.new(t.outputs[0],scale.inputs['Scale'])
   near=n.new('ShaderNodeVectorMath');near.operation='SUBTRACT';L.new(sub.outputs[0],near.inputs[0]);L.new(scale.outputs[0],near.inputs[1])
   length=n.new('ShaderNodeVectorMath');length.operation='LENGTH';L.new(near.outputs[0],length.inputs[0])
   fall=n.new('ShaderNodeMapRange');fall.clamp=True;fall.interpolation_type='SMOOTHSTEP';L.new(length.outputs['Value'],fall.inputs[0]);fall.inputs['From Min'].default_value=st['radius']*.60;fall.inputs['From Max'].default_value=st['radius'];fall.inputs['To Min'].default_value=1;fall.inputs['To Max'].default_value=0
   if st['kind'] in totals:
    mx=n.new('ShaderNodeMath');mx.operation='MAXIMUM';L.new(totals[st['kind']],mx.inputs[0]);L.new(fall.outputs[0],mx.inputs[1]);totals[st['kind']]=mx.outputs[0]
   else:totals[st['kind']]=fall.outputs[0]
 if o.name in labels:
  filename,cx,cy,h=labels[o.name];im=bpy.data.images.load(str(root/filename));im.pack();w=h*im.size[0]/im.size[1]
  anchor=hit(o,(cx-w/2,cy+h/2));du=hit(o,(cx+w/2,cy+h/2))-anchor;dv=hit(o,(cx-w/2,cy-h/2))-anchor
  aa=du.dot(du);bb=dv.dot(dv);ab=du.dot(dv);den=aa*bb-ab*ab
  u=(du*bb-dv*ab)/den;v=(dv*aa-du*ab)/den
  delta=n.new('ShaderNodeVectorMath');delta.operation='SUBTRACT';L.new(inp.outputs[0],delta.inputs[0]);delta.inputs[1].default_value=anchor
  combine=n.new('ShaderNodeCombineXYZ')
  for j,axis in enumerate([u,v]):
   dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=axis;L.new(delta.outputs[0],dot.inputs[0]);L.new(dot.outputs['Value'],combine.inputs[j])
  tex=n.new('ShaderNodeTexImage');tex.name='Shared Rajdhani stencil';tex.image=im;tex.extension='CLIP';L.new(combine.outputs[0],tex.inputs[0])
  depth=n.new('ShaderNodeVectorMath');depth.operation='DOT_PRODUCT';depth.inputs[1].default_value=du.cross(dv).normalized();L.new(delta.outputs[0],depth.inputs[0])
  absolute=n.new('ShaderNodeMath');absolute.operation='ABSOLUTE';L.new(depth.outputs['Value'],absolute.inputs[0])
  plane=n.new('ShaderNodeMath');plane.operation='LESS_THAN';plane.inputs[1].default_value=.01;L.new(absolute.outputs[0],plane.inputs[0])
  mask=n.new('ShaderNodeMath');mask.operation='MULTIPLY';L.new(tex.outputs['Alpha'],mask.inputs[0]);L.new(plane.outputs[0],mask.inputs[1])
  if 'dark' in totals:
   mx=n.new('ShaderNodeMath');mx.operation='MAXIMUM';L.new(totals['dark'],mx.inputs[0]);L.new(mask.outputs[0],mx.inputs[1]);totals['dark']=mx.outputs[0]
  else:totals['dark']=mask.outputs[0]
 for k,v in totals.items():L.new(v,output.inputs[k])
 return g
for objname,strokes in prepared.items():
 o=bpy.data.objects[objname];g=group_for(o,strokes)
 for i in {p.material_index for p in o.data.polygons}:
  old=o.material_slots[i].material
  if not old or 'Small natural shadow contribution' not in old.node_tree.nodes:continue
  m=old.copy();m.name='23 | '+objname.split(' | ')[-1]+' / '+old.name.split(' / ')[-1].split(' | ')[-1];o.material_slots[i].link='OBJECT';o.material_slots[i].material=m
  n=m.node_tree.nodes;L=m.node_tree.links;targets=[n['Small natural shadow contribution'].inputs['Color'],n['Painted color independent of smooth reflections'].inputs['Color']];color=targets[0].links[0].from_socket
  coord=n.new('ShaderNodeTexCoord');group=n.new('ShaderNodeGroup');group.node_tree=g;L.new(coord.outputs['Object'],group.inputs[0])
  for kind,col,strength in [('brush',(.16,.105,.07,1),.13),('dark',(.055,.034,.024,1),.83),('light',(.95,.72,.47,1),.40)]:
   amount=n.new('ShaderNodeMath');amount.operation='MULTIPLY';amount.name='Drawn '+kind+' intensity';amount.inputs[1].default_value=strength;L.new(group.outputs[kind],amount.inputs[0])
   mix=n.new('ShaderNodeMixRGB');mix.name='Drawn '+kind+' surface strokes';L.new(amount.outputs[0],mix.inputs[0]);L.new(color,mix.inputs[1]);mix.inputs[2].default_value=col;color=mix.outputs[0]
  for target in targets:L.new(color,target)
s.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'materials':changed,'surfaceDrawing':prepared,'geometry':'unchanged','camera':'unchanged','lighting':'Warmer and stronger electronics, weaker starlight, warm painted midtones','approval':'pending user review'},indent=2))
