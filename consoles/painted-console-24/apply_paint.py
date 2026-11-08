"""Restyle loaded illustrated-console-23; --name saves a new packed blend and manifest without overwriting."""
import bpy,sys,json,hashlib,math
from pathlib import Path
from mathutils import Vector,geometry
root=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='24 - Warm shadows and hand-painted casing'
active={slot.material for o in s.objects for slot in o.material_slots if slot.material}
paint=['Warm limestone paint','Paint edge','Warm grey casing','Secondary cover paint','Taupe keys']
for m in active:
 if not m.use_nodes:continue
 n=m.node_tree.nodes;L=m.node_tree.links;label=m.name.split(' / ')[-1].split(' | ')[-1]
 if 'Three brush-mixed paint values' in n:
  ispaint=label in paint
  n['Painted color independent of smooth reflections'].inputs['Strength'].default_value=.36 if ispaint else .52
  n['Mostly painted with restrained light response'].inputs[0].default_value=.64 if ispaint else .90
  for e,col in zip(n['Painted shadow middle and light'].color_ramp.elements,[(.32,.24,.20),(.66,.54,.44),(1.05,.86,.67)]):e.color=col+(1,)
  if ispaint:
   r=n['Three brush-mixed paint values'].color_ramp
   center=tuple(r.elements[1].color[:3])
   for e,f in zip(r.elements,[.76,1.0,1.16]):e.color=tuple(c*f for c in center)+(1,)
   n['Large painted value shapes'].inputs['Scale'].default_value=2.4
   n['Broad directional brush patches'].inputs['Scale'].default_value=(1.5,1.2,1.0)
 if m.name in ['19 | Main rectangular display','19 | Detail rectangular display']:
  n['Warm phosphor tint'].inputs[2].default_value=(1,.67,.37,1)
  n['Principled BSDF'].inputs['Emission Strength'].default_value=1.7
 if m.name=='19 | Distant nebula':next(a for a in n if a.type=='EMISSION').inputs['Strength'].default_value=.045
 if m.name=='19 | Projector optical emitter':
  n['Principled BSDF'].inputs['Emission Strength'].default_value=2.8
  r=n['Cream hot center to amber lens edges'].color_ramp;r.elements[0].color=(1,.69,.34,1);r.elements[1].color=(1,.34,.09,1)
 if m.name=='19 | Amber status glass':n['Principled BSDF'].inputs['Emission Strength'].default_value=3.5
 if m.name=='19 | Warm projection beam':
  n['Principled Volume'].inputs['Emission Color'].default_value=(1,.63,.30,1);n['Math.001'].inputs[1].default_value=.38
 if m.name=='19 | Projected hull outlines':
  p=n['Principled BSDF'];p.inputs['Emission Color'].default_value=(1,.66,.33,1);p.inputs['Emission Strength'].default_value=2.2
 if m.name.startswith('19 | HOLO'):
  p=n.get('Muted painted projection')
  if p:
   p.inputs['Emission Strength'].default_value=.65
   for socket in ['Base Color','Emission Color']:
    if not p.inputs[socket].is_linked:continue
    original=p.inputs[socket].links[0].from_socket
    tint=n.new('ShaderNodeMixRGB');tint.name='Warm projected pigment '+socket;tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1,.76,.54,1);L.new(original,tint.inputs[1]);L.new(tint.outputs[0],p.inputs[socket])
 if 'Launch amber resin' in m.name:n['Principled BSDF'].inputs['Emission Strength'].default_value=.26
for o in s.objects:
 if o.type!='LIGHT':continue
 settings={'19 | Projection spill':(600,(1,.62,.31),2.3),'19 | Screen bounce':(300,(1,.72,.43),2.3),'19 | Faint starlight':(15,(.9,.78,.63),9),'19 | Soft ambient readability':(35,(1,.70,.43),10)}
 if o.name in settings:o.data.energy,o.data.color,o.data.size=settings[o.name]
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.09,.045,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.035
for ls in s.view_layers[0].freestyle_settings.linesets:
 outer='Outer' in ls.name;st=ls.linestyle;st.thickness=2.2 if outer else 1.15;st.color=(.009,.004,.002)
 for mod in st.thickness_modifiers:
  if mod.type=='NOISE':mod.amplitude=.95 if outer else .62;mod.period=13;mod.seed=24
 wobble=st.geometry_modifiers.new('Uneven drawn edge','SPATIAL_NOISE');wobble.amplitude=.48 if outer else .38;wobble.scale=15;wobble.octaves=2;wobble.smooth=True;wobble.use_pure_random=False
for n in s.compositing_node_group.nodes:
 if n.type=='GLARE':n.inputs['Strength'].default_value=.22;n.inputs['Threshold'].default_value=1.2

dg=bpy.context.evaluated_depsgraph_get();cam=s.camera;width=s.render.resolution_x;height=s.render.resolution_y
invp=cam.calc_matrix_camera(dg,x=width,y=height).inverted()
def ray(pixel):
 p=invp@Vector((2*pixel[0]/width-1,1-2*pixel[1]/height,-1,1));p=p.xyz/p.w
 return cam.matrix_world.translation,(cam.matrix_world.to_3x3()@p).normalized()
def hit(o,pixel):
 origin,direction=ray(pixel);inv=o.matrix_world.inverted();ok,p,n,face=o.ray_cast(inv@origin,(inv.to_3x3()@direction).normalized(),depsgraph=dg)
 if not ok:raise RuntimeError('Drawing misses '+o.name+' at '+str(pixel))
 return p
frame='08 | Thick monitor armor frame';control='14 | Thin fitted control face';brow='08 | Raised brow cap';projector='14 | Projector formed armor';forearm='14 | Forearm fold into Launch and controls'
prepared=json.loads((root.parent/'illustrated-console-23/console-manifest.json').read_text())['surfaceDrawing']
oldticks={frame:[(97+47*math.cos(math.radians(a)),654+39*math.sin(math.radians(a)))for a in [-135,-110,-85,-60,-35,-10]],control:[(1389+51*math.cos(math.radians(a)),731+29*math.sin(math.radians(a)))for a in [-160,-130,-100,-70,-40,-10]]}
for obj,strokes in prepared.items():
 prepared[obj]=[st for st in strokes if st['kind']!='brush' and not any(st['pixels'] and math.dist(st['pixels'][0],p)<.001 for p in oldticks.get(obj,[]))]
decorations={(1122,817),(1141,812),(1160,807),(897,801),(902,804),(908,806),(1289,775),(1295,775),(1301,775)}
for obj,strokes in prepared.items():prepared[obj]=[st for st in strokes if not(st['pixels'] and tuple(st['pixels'][0])in decorations)]
calibrations=[]
for obj,knob,pointer,angles in [(frame,'08 | Selector faceted grip','08 | Selector pointer',[45,75,105,135]),(control,'08 | Projection knob faceted grip','08 | Projection knob pointer',[0,30,60,90,120,150,180])]:
 o=bpy.data.objects[obj];center=o.matrix_world.inverted()@bpy.data.objects[knob].matrix_world.translation;z=max(v.co.z for v in o.data.vertices)
 for a in angles:
  rad=math.radians(a);points=[[center.x+r*math.cos(rad),center.y+r*math.sin(rad),z]for r in [.62,.72]]
  prepared[obj].append({'kind':'dark','points':points,'radius':.011,'pixels':None})
 calibrations.append({'panel':obj,'knob':knob,'center':[center.x,center.y,z],'anglesDegrees':angles,'innerRadius':.62,'outerRadius':.72,'width':.022})
labels={brow:('display-stencil.png',263,458,45),control:('projection-stencil.png',1285,694,82)}
polygons={
 frame:[('shadow',[(138,604),(147,604),(143,651),(138,677),(136,713),(127,721),(131,684),(132,651)]),('shadow',[(544,627),(548,629),(552,680),(551,692),(546,689),(544,654)]),('chip',[(48,788),(57,783),(60,785),(55,789),(55,795),(49,794)]),('chip',[(164,932),(175,930),(181,931),(179,934),(172,935),(167,936)])],
 brow:[('shadow',[(179,463),(210,463),(217,467),(241,467),(237,473),(182,473)])],
 '09 | Left shoulder armor':[('chip',[(85,468),(89,469),(94,470),(92,477),(87,478),(84,475)])],
 control:[('shadow',[(1187,748),(1209,759),(1226,762),(1221,770),(1203,767),(1186,755)]),('chip',[(1488,693),(1499,695),(1504,700),(1495,701)])],
 forearm:[('shadow',[(1048,843),(1093,843),(1132,843),(1184,843),(1181,855),(1160,860),(1128,853),(1098,861),(1065,854)]),('chip',[(1060,870),(1068,870),(1071,873),(1079,872),(1081,876),(1069,879),(1063,876)])],
 projector:[('shadow',[(844,828),(854,834),(880,835),(910,834),(931,837),(931,840),(859,840),(844,832)])]}
polydata={}
for obj,patches in polygons.items():
 o=bpy.data.objects[obj];polydata[obj]=[]
 for kind,pixels in patches:polydata[obj].append({'kind':kind,'points':[list(hit(o,p))for p in pixels],'pixels':pixels})
for obj,paths in {frame:[[(70,535),(73,546),(71,557)],[(140,691),(137,700),(139,707)],[(541,653),(542,661)],[(51,792),(48,802)]]}.items():
 o=bpy.data.objects[obj]
 for j,pixels in enumerate(paths):
  points=[hit(o,p)for p in pixels];origin,direction=ray((pixels[0][0]+.8,pixels[0][1]));wc=o.matrix_world@points[0];vn=cam.matrix_world.to_3x3()@Vector((0,0,-1));distance=(wc-origin).dot(vn)/direction.dot(vn);r=(o.matrix_world.inverted()@(origin+distance*direction)-points[0]).length
  prepared[obj].append({'kind':'dark','points':[list(p)for p in points],'radius':r,'pixels':pixels})
def group_for(o,strokes):
 g=bpy.data.node_groups.new('24 | Painted casing and exact graphics on '+o.name.split(' | ')[-1],'ShaderNodeTree');g.interface.new_socket(name='Local position',in_out='INPUT',socket_type='NodeSocketVector')
 for label in ['dark','light','brush','shadow','chip']:g.interface.new_socket(name=label,in_out='OUTPUT',socket_type='NodeSocketFloat')
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
 for patch in polydata.get(o.name,[]):
  patch_mask=None
  for tri in geometry.tessellate_polygon([[Vector(p)for p in patch['points']]]):
   a,b,c=[Vector(patch['points'][i])for i in tri];du=b-a;dv=c-a;aa=du.dot(du);bb=dv.dot(dv);ab=du.dot(dv);den=aa*bb-ab*ab
   if abs(den)<1e-14:raise RuntimeError('Degenerate paint triangle on '+o.name)
   u=(du*bb-dv*ab)/den;v=(dv*aa-du*ab)/den
   delta=n.new('ShaderNodeVectorMath');delta.operation='SUBTRACT';delta.inputs[1].default_value=a;L.new(inp.outputs[0],delta.inputs[0])
   bary=[]
   for axis in [u,v]:
    dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.inputs[1].default_value=axis;L.new(delta.outputs[0],dot.inputs[0]);bary.append(dot.outputs['Value'])
   positive=[]
   for socket in bary:
    test=n.new('ShaderNodeMath');test.operation='GREATER_THAN';test.inputs[1].default_value=-.0001;L.new(socket,test.inputs[0]);positive.append(test.outputs[0])
   total=n.new('ShaderNodeMath');total.operation='ADD';L.new(bary[0],total.inputs[0]);L.new(bary[1],total.inputs[1])
   test=n.new('ShaderNodeMath');test.operation='LESS_THAN';test.inputs[1].default_value=1.0001;L.new(total.outputs[0],test.inputs[0]);positive.append(test.outputs[0])
   depth=n.new('ShaderNodeVectorMath');depth.operation='DOT_PRODUCT';depth.inputs[1].default_value=du.cross(dv).normalized();L.new(delta.outputs[0],depth.inputs[0])
   absolute=n.new('ShaderNodeMath');absolute.operation='ABSOLUTE';L.new(depth.outputs['Value'],absolute.inputs[0])
   test=n.new('ShaderNodeMath');test.operation='LESS_THAN';test.inputs[1].default_value=.018;L.new(absolute.outputs[0],test.inputs[0]);positive.append(test.outputs[0])
   value=positive[0]
   for socket in positive[1:]:
    mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';L.new(value,mul.inputs[0]);L.new(socket,mul.inputs[1]);value=mul.outputs[0]
   if patch_mask is not None:
    mx=n.new('ShaderNodeMath');mx.operation='MAXIMUM';L.new(patch_mask,mx.inputs[0]);L.new(value,mx.inputs[1]);patch_mask=mx.outputs[0]
   else:patch_mask=value
  if patch['kind']=='shadow':
   nearest=None;pts=[Vector(p)for p in patch['points']]
   for a,b in zip(pts,pts[1:]+pts[:1]):
    v=b-a;sub=n.new('ShaderNodeVectorMath');sub.operation='SUBTRACT';L.new(inp.outputs[0],sub.inputs[0]);sub.inputs[1].default_value=a
    dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';L.new(sub.outputs[0],dot.inputs[0]);dot.inputs[1].default_value=v/v.length_squared
    clamp=n.new('ShaderNodeClamp');L.new(dot.outputs['Value'],clamp.inputs[0])
    scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[0].default_value=v;L.new(clamp.outputs[0],scale.inputs['Scale'])
    distance=n.new('ShaderNodeVectorMath');distance.operation='DISTANCE';L.new(sub.outputs[0],distance.inputs[0]);L.new(scale.outputs[0],distance.inputs[1])
    if nearest is not None:
     mn=n.new('ShaderNodeMath');mn.operation='MINIMUM';L.new(nearest,mn.inputs[0]);L.new(distance.outputs['Value'],mn.inputs[1]);nearest=mn.outputs[0]
    else:nearest=distance.outputs['Value']
   noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=65;noise.inputs['Detail'].default_value=1;L.new(inp.outputs[0],noise.inputs[0])
   width=n.new('ShaderNodeMath');width.operation='MULTIPLY_ADD';width.inputs[1].default_value=.035;width.inputs[2].default_value=.015;L.new(noise.outputs['Fac'],width.inputs[0])
   feather=n.new('ShaderNodeMath');feather.operation='DIVIDE';feather.use_clamp=True;L.new(nearest,feather.inputs[0]);L.new(width.outputs[0],feather.inputs[1])
   mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';L.new(patch_mask,mul.inputs[0]);L.new(feather.outputs[0],mul.inputs[1]);patch_mask=mul.outputs[0]
  if patch['kind'] in totals:
   mx=n.new('ShaderNodeMath');mx.operation='MAXIMUM';L.new(totals[patch['kind']],mx.inputs[0]);L.new(patch_mask,mx.inputs[1]);totals[patch['kind']]=mx.outputs[0]
  else:totals[patch['kind']]=patch_mask
 if o.name in labels:
  filename,cx,cy,w=labels[o.name];im=bpy.data.images.load(str(root/filename));im.pack()
  center=hit(o,(cx,cy));physical_w=abs((hit(o,(cx+w/2,cy))-hit(o,(cx-w/2,cy))).x);axes=o.matrix_world.to_3x3();physical_h=physical_w*axes.col[0].length/axes.col[1].length*im.size[1]/im.size[0]
  anchor=center-Vector((physical_w/2,physical_h/2,0));du=Vector((physical_w,0,0));dv=Vector((0,physical_h,0));u=Vector((1/physical_w,0,0));v=Vector((0,1/physical_h,0))
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
for objname in set(prepared)|set(polydata):
 o=bpy.data.objects[objname];g=group_for(o,prepared.get(objname,[]))
 for i in {p.material_index for p in o.data.polygons}:
  old=o.material_slots[i].material
  if not old or not old.use_nodes or 'Small natural shadow contribution' not in old.node_tree.nodes:continue
  m=old.copy();m.name='24 | '+objname.split(' | ')[-1]+' / '+old.name.split(' / ')[-1].split(' | ')[-1];o.material_slots[i].link='OBJECT';o.material_slots[i].material=m
  n=m.node_tree.nodes;L=m.node_tree.links
  groups=[a for a in n if a.type=='GROUP' and a.node_tree and a.node_tree.name.startswith('23 | Surface drawing')]
  if groups:
   assert len(groups)==1
   group=groups[0];group.node_tree=g
   target=n['Drawn brush surface strokes'].inputs[1];color=target.links[0].from_socket
  else:
   coord=n.new('ShaderNodeTexCoord');group=n.new('ShaderNodeGroup');group.node_tree=g;L.new(coord.outputs['Object'],group.inputs[0]);target=n['Small natural shadow contribution'].inputs['Color'];color=target.links[0].from_socket
  for kind,col,strength in [('shadow',(.075,.044,.031,1),.28),('chip',(.05,.027,.02,1),.86)]:
   amount=n.new('ShaderNodeMath');amount.operation='MULTIPLY';amount.name='Painted '+kind+' amount';amount.inputs[1].default_value=strength;L.new(group.outputs[kind],amount.inputs[0])
   mix=n.new('ShaderNodeMixRGB');mix.name='Hand-painted '+kind;L.new(amount.outputs[0],mix.inputs[0]);L.new(color,mix.inputs[1]);mix.inputs[2].default_value=col;color=mix.outputs[0]
  L.new(color,target)
  if not groups:L.new(color,n['Painted color independent of smooth reflections'].inputs['Color'])
s.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'surfaceDrawing':prepared,'paintedPatches':polydata,'calibrations':calibrations,'geometry':'unchanged','camera':'unchanged','lighting':'Dim warm hardware with localized electronic light','approval':'pending user review'},indent=2))
