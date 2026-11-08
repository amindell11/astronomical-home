"""Add local material wear to a loaded ink-materials-21 scene. --name writes a new packed blend and placement manifest; refuses existing output."""
import bpy,sys,json,hashlib,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent;args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='22 - Handled paint and drawn scuffs';dg=bpy.context.evaluated_depsgraph_get();cam=s.camera
width=s.render.resolution_x;height=s.render.resolution_y;invp=cam.calc_matrix_camera(dg,x=width,y=height).inverted()
def ray(pixel):
 p=invp@Vector((2*pixel[0]/width-1,1-2*pixel[1]/height,-1,1));p=p.xyz/p.w
 return cam.matrix_world.translation, (cam.matrix_world.to_3x3()@p).normalized()
def hit(o,pixel):
 origin,direction=ray(pixel);inv=o.matrix_world.inverted();local=inv@origin;direction=(inv.to_3x3()@direction).normalized()
 ok,p,n,face=o.ray_cast(local,direction,depsgraph=dg)
 if not ok:raise RuntimeError('Wear point misses '+o.name+' at '+str(pixel))
 return p,n
# Pixel coordinates locate authored marks once; saved masks use the part's local coordinates.
marks={
 '08 | Thick monitor armor frame':[
  ('dark',[(43,793),(51,789),(56,791)],1.8),('light',[(44,795),(52,792)],.9),
  ('dark',[(57,718),(67,714)],1.3),('dark',[(61,721),(65,719)],1.0),
  ('dark',[(121,692),(128,696),(132,695)],1.5),('light',[(122,694),(129,698)],.8),
  ('dark',[(537,577),(540,587)],1.7),('dark',[(539,590),(541,594)],1.0),
  ('dark',[(163,930),(176,932)],1.8),('light',[(164,932),(173,934)],.9),
  ('dark',[(555,870),(558,862)],1.6)],
 '09 | Left shoulder armor':[
  ('dark',[(72,507),(79,505),(82,507)],1.7),('light',[(73,509),(79,507)],.9),
  ('dark',[(126,460),(131,463)],1.2)],
 '08 | Raised brow cap':[
  ('dark',[(188,470),(204,470)],1.3),('light',[(190,472),(199,472)],.8),
  ('dark',[(366,445),(375,446)],1.5)],
 '14 | Thin fitted control face':[
  ('dark',[(1196,758),(1207,755)],1.5),('dark',[(1201,761),(1205,760)],1.0),
  ('light',[(1198,760),(1203,758)],.8),('dark',[(1364,771),(1378,768)],1.5),
  ('dark',[(1382,770),(1387,768)],1.1),('light',[(1367,773),(1374,771)],.8),
  ('dark',[(1501,692),(1509,696)],1.8)],
 '14 | Projector formed armor':[
  ('dark',[(856,817),(866,819),(871,817)],2.0),('light',[(858,820),(865,822)],.8),
  ('dark',[(919,790),(933,786)],1.1),('dark',[(922,793),(928,791)],.8)],
 '14 | Forearm fold into Launch and controls':[
  ('dark',[(1042,856),(1054,859)],1.7),('light',[(1044,859),(1051,861)],.8),
  ('dark',[(1178,796),(1185,800),(1189,799)],1.8),
  ('dark',[(1219,832),(1227,829)],1.2)],
 '10 | Wraparound retaining strap':[
  ('dark',[(807,888),(816,891)],1.2),('light',[(808,891),(813,893)],.8)],
 '10 | Inline connector jacket':[
  ('light',[(1092,883),(1104,881)],1.4),('light',[(1139,880),(1144,883)],1.1)],
 '08 | Previous chunky keycap':[
  ('light',[(80,754),(86,754)],1.0)],
 '08 | Launch broad beveled button':[
  ('dark',[(1343,816),(1352,813)],1.4),('light',[(1344,819),(1349,816)],.7),
  ('dark',[(1551,870),(1557,865)],1.3)]
}
marks['09 | Left shoulder armor'] += [('dark',[(90,465),(86,469),(88,473)],2.6),('light',[(91,466),(89,470)],1.4),('dark',[(84,477),(85,480)],1.4)]
marks['08 | Thick monitor armor frame'] += [('dark',[(165,930),(170,933),(174,932)],2.6),('light',[(166,932),(170,934)],1.4),('dark',[(44,789),(48,791),(52,789)],2.8),('light',[(45,792),(49,793)],1.5)]
marks['14 | Thin fitted control face'] += [('dark',[(1403,779),(1412,779),(1414,777)],2.5),('light',[(1405,777),(1410,777)],1.4),('dark',[(1420,780),(1423,779)],1.4)]
marks['14 | Forearm fold into Launch and controls'] += [('dark',[(1059,873),(1067,875),(1073,873)],2.6),('light',[(1060,875),(1066,877)],1.4),('dark',[(1078,875),(1081,875)],1.2)]
marks['10 | Wraparound retaining strap'] += [('dark',[(807,881),(812,883),(814,881)],2.2),('light',[(808,883),(812,884)],1.3)]
prepared={};misses=[]
for objname,strokes in marks.items():
 o=bpy.data.objects[objname];local=[]
 for kind,pixels,stroke_width in strokes:
  try:
   points=[hit(o,p)[0] for p in pixels]
   center,norm=hit(o,pixels[0]);origin,direction=ray((pixels[0][0]+stroke_width/2,pixels[0][1]));worldcenter=o.matrix_world@center;viewnormal=cam.matrix_world.to_3x3()@Vector((0,0,-1))
   distance=(worldcenter-origin).dot(viewnormal)/direction.dot(viewnormal);radius=(o.matrix_world.inverted()@(origin+distance*direction)-center).length
   local.append({'kind':kind,'points':[list(v)for v in points],'radius':radius,'pixels':pixels,'widthPixels':stroke_width})
  except RuntimeError as e:misses.append(str(e))
 prepared[objname]=local
if misses:
 (root/(name+'-misses.json')).write_text(json.dumps(misses,indent=2));raise RuntimeError('\n'.join(misses))

def group_for(o,strokes):
 g=bpy.data.node_groups.new('22 | Wear on '+o.name.split(' | ')[-1],'ShaderNodeTree');g.interface.new_socket(name='Local surface position',in_out='INPUT',socket_type='NodeSocketVector')
 for label in ['Dark marks','Pale chipped edges']:g.interface.new_socket(name=label,in_out='OUTPUT',socket_type='NodeSocketFloat')
 n=g.nodes;L=g.links;inp=n.new('NodeGroupInput');outp=n.new('NodeGroupOutput');totals={}
 for j,stroke in enumerate(strokes):
  for k,(aa,bb)in enumerate(zip(stroke['points'],stroke['points'][1:])):
   a=Vector(aa);b=Vector(bb);v=b-a
   sub=n.new('ShaderNodeVectorMath');sub.operation='SUBTRACT';L.new(inp.outputs[0],sub.inputs[0]);sub.inputs[1].default_value=a
   dot=n.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';L.new(sub.outputs[0],dot.inputs[0]);dot.inputs[1].default_value=v/v.length_squared
   t=n.new('ShaderNodeClamp');L.new(dot.outputs['Value'],t.inputs[0])
   scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[0].default_value=v;L.new(t.outputs[0],scale.inputs['Scale'])
   near=n.new('ShaderNodeVectorMath');near.operation='SUBTRACT';L.new(sub.outputs[0],near.inputs[0]);L.new(scale.outputs[0],near.inputs[1])
   length=n.new('ShaderNodeVectorMath');length.operation='LENGTH';L.new(near.outputs[0],length.inputs[0])
   fall=n.new('ShaderNodeMapRange');fall.name=stroke['kind']+' stroke '+str(j+1)+' segment '+str(k+1);fall.clamp=True;fall.interpolation_type='SMOOTHSTEP';L.new(length.outputs['Value'],fall.inputs['Value']);fall.inputs['From Min'].default_value=stroke['radius']*.62;fall.inputs['From Max'].default_value=stroke['radius'];fall.inputs['To Min'].default_value=1;fall.inputs['To Max'].default_value=0
   if stroke['kind'] in totals:
    mx=n.new('ShaderNodeMath');mx.operation='MAXIMUM';L.new(totals[stroke['kind']],mx.inputs[0]);L.new(fall.outputs[0],mx.inputs[1]);totals[stroke['kind']]=mx.outputs[0]
   else:totals[stroke['kind']]=fall.outputs[0]
 for kind,label in [('dark','Dark marks'),('light','Pale chipped edges')]:
  if kind in totals:L.new(totals[kind],outp.inputs[label])
 return g
changed=[]
for objname,strokes in prepared.items():
 o=bpy.data.objects[objname];g=group_for(o,strokes)
 used={p.material_index for p in o.data.polygons}
 for i in used:
  old=o.material_slots[i].material
  if not old or not old.name.startswith(('21 |','20 | Launch')):continue
  m=old.copy();m.name='22 | '+objname.split(' | ')[-1]+' / '+old.name.split(' | ')[-1];o.material_slots[i].link='OBJECT';o.material_slots[i].material=m;changed.append(m.name)
  nt=m.node_tree;n=nt.nodes;L=nt.links
  painted='Small natural shadow contribution' in n
  targets=[n['Small natural shadow contribution'].inputs['Color'],n['Painted color independent of smooth reflections'].inputs['Color']] if painted else [n['Principled BSDF'].inputs['Base Color']]
  base=targets[0].links[0].from_socket
  if painted:
   r=n['Three brush-mixed paint values'].color_ramp.elements;palette=tuple((r[0].color[c]+r[-1].color[c])/1.94 for c in range(3))
  else:palette=tuple(n['Principled BSDF'].inputs['Base Color'].default_value[:3])
  coord=n.new('ShaderNodeTexCoord');coord.name='Wear follows this part';group=n.new('ShaderNodeGroup');group.node_tree=g;L.new(coord.outputs['Object'],group.inputs[0])
  for kind,label,factor,mult,add in [('Dark marks','Handled dark scuffs',.76,.20,0),('Pale chipped edges','Exposed paint edges',.68,1.30,.045)]:
   amount=n.new('ShaderNodeMath');amount.operation='MULTIPLY';amount.name=label+' strength';L.new(group.outputs[kind],amount.inputs[0]);amount.inputs[1].default_value=factor
   mix=n.new('ShaderNodeMixRGB');mix.name=label;L.new(amount.outputs[0],mix.inputs[0]);L.new(base,mix.inputs[1]);mix.inputs[2].default_value=tuple(min(1,c*mult+add)for c in palette)+(1,);base=mix.outputs[0]
  for socket in targets:L.new(base,socket)
s.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'materials':changed,'marks':prepared,'geometry':'unchanged','cameraAndLighting':'unchanged','approval':'pending user review'},indent=2))
