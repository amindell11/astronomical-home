"""Polish loaded painted-console-24; --name writes a new packed scene and manifest, refusing existing output."""
import bpy,sys,json,hashlib,math
from pathlib import Path
from mathutils import Matrix
root=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='25 - Backlit Launch and etched viewport'
for ls in s.view_layers[0].freestyle_settings.linesets:
 outer='Outer' in ls.name;st=ls.linestyle
 for mod in st.thickness_modifiers:
  if mod.type=='NOISE':mod.amplitude=.55 if outer else .34
 for mod in st.geometry_modifiers:
  if mod.name=='Uneven drawn edge':mod.amplitude=.21 if outer else .14;mod.octaves=1
active={slot.material for o in s.objects for slot in o.material_slots if slot.material}
for m in active:
 if not m.use_nodes:continue
 n=m.node_tree.nodes
 if 'Painted color independent of smooth reflections' in n:
  p=n['Painted color independent of smooth reflections'];p.inputs['Strength'].default_value*=1.12
 if m.name=='19 | Distant nebula':next(a for a in n if a.type=='EMISSION').inputs['Strength'].default_value=.078
 if 'Launch amber resin' in m.name:
  n['Principled BSDF'].inputs['Emission Strength'].default_value=.08
  for e in n['Restrained pigment variation'].color_ramp.elements:e.color=tuple(c*.88 for c in e.color[:3])+(1,)
for o in s.objects:
 if o.type=='LIGHT' and o.name in {'19 | Projection spill','19 | Screen bounce'}:o.data.energy*=1.20
 if o.name=='19 | Soft ambient readability':o.data.energy=55
s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.045
g=bpy.data.node_groups['24 | Painted casing and exact graphics on Raised brow cap']
tex=g.nodes['Shared Rajdhani stencil']
for link in list(tex.outputs['Alpha'].links):
 target=link.to_socket;g.links.remove(link);target.default_value=0
g.name='25 | Unlabelled brow paint and wear'
legend=bpy.data.objects['19 | Launch legend'];m=legend.data.materials[0].copy();m.name='25 | Launch letter backlight';legend.data.materials[0]=m
p=m.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(.92,.65,.32,1);p.inputs['Emission Color'].default_value=(1,.73,.40,1);p.inputs['Emission Strength'].default_value=3;p.inputs['Specular IOR Level'].default_value=0

# A transparent glass surface sits just behind the existing viewport lip and hologram.
depth=21.60;cam=s.camera;frame=cam.data.view_frame(scene=s);verts=[cam.matrix_world@(p*(depth/-p.z))for p in frame]
mesh=bpy.data.meshes.new('25 | Viewport glass surface');mesh.from_pydata(verts,[],[(0,1,2,3)]);uv=mesh.uv_layers.new(name='GlassUV')
for loop,xy in zip(uv.data,[(1,1),(1,0),(0,0),(0,1)]):loop.uv=xy
col=bpy.data.collections.new('25 - Viewport surface');s.collection.children.link(col)
glass=bpy.data.objects.new('25 | Viewport glass',mesh);col.objects.link(glass);glass.parent=bpy.data.objects['11 | Beveled viewport header'];glass.matrix_world=Matrix.Identity(4)
m=bpy.data.materials.new('25 | Etched glass perimeter');m.use_nodes=True;mesh.materials.append(m);n=m.node_tree.nodes;L=m.node_tree.links;n.clear()
def node(kind,label):
 a=n.new(kind);a.name=a.label=label;return a
def mathnode(op,a,b=None,label=''):
 t=node('ShaderNodeMath',label or op);t.operation=op
 for i,x in enumerate([a,b]):
  if x is None:continue
  if isinstance(x,(float,int)):t.inputs[i].default_value=x
  else:L.new(x,t.inputs[i])
 return t.outputs[0]
coord=node('ShaderNodeUVMap','Glass coordinates');coord.uv_map='GlassUV';sep=node('ShaderNodeSeparateXYZ','Window coordinates');L.new(coord.outputs[0],sep.inputs[0])
x=mathnode('MULTIPLY',sep.outputs['X'],1672);y=mathnode('MULTIPLY',mathnode('SUBTRACT',1,sep.outputs['Y']),941)
point=node('ShaderNodeCombineXYZ','Etching surface coordinates');L.new(x,point.inputs['X']);L.new(y,point.inputs['Y'])
paths=[
 [(140,60),(246,60),(267,67),(295,67),(314,53),(487,53),(515,58),(973,58),(992,72),(1163,72)],
 [(1430,57),(1540,57),(1562,74),(1616,74)],[(1515,66),(1562,66),(1569,71),(1612,71)],
 [(19,146),(19,205),(25,199),(25,151)],[(18,253),(27,266),(27,368),(18,382),(18,424)],
 [(1655,142),(1655,179)],[(1655,302),(1655,381),(1645,397),(1645,487),(1655,501),(1655,525)],
 [(550,646),(569,666),(666,666),(694,650),(760,650)],[(694,655),(766,655)],
 [(1180,666),(1208,651),(1534,651),(1558,664),(1616,664)],[(1458,646),(1532,646)],
 [(22,397),(40,397)],[(1192,72),(1247,72),(1260,77),(1287,77)] ]
for cx,cy,r,indices in [(41,86,17,range(7)),(73,79,17,range(7)),(105,81,17,range(6)),(28,115,17,range(5)),(1631,79,17,range(7)),(1645,110,17,range(5)),(560,622,16,range(6)),(579,649,16,range(5)),(1635,641,16,range(7)),(1607,649,16,range(5)),(1646,612,16,range(5))]:
 paths.append([(cx+r*math.cos(math.radians(30+60*i)),cy+r*math.sin(math.radians(30+60*i)))for i in indices])
mask=None
for path in paths:
 for a,b in zip(path,path[1:]):
  ax,ay=a;bx,by=b;vx,vy=bx-ax,by-ay;length2=vx*vx+vy*vy
  sub=node('ShaderNodeVectorMath','Line origin');sub.operation='SUBTRACT';L.new(point.outputs[0],sub.inputs[0]);sub.inputs[1].default_value=(ax,ay,0)
  dot=node('ShaderNodeVectorMath','Line position');dot.operation='DOT_PRODUCT';L.new(sub.outputs[0],dot.inputs[0]);dot.inputs[1].default_value=(vx/length2,vy/length2,0)
  clamp=node('ShaderNodeClamp','Segment ends');L.new(dot.outputs['Value'],clamp.inputs[0])
  scale=node('ShaderNodeVectorMath','Nearest point');scale.operation='SCALE';scale.inputs[0].default_value=(vx,vy,0);L.new(clamp.outputs[0],scale.inputs['Scale'])
  dist=node('ShaderNodeVectorMath','Distance to etch');dist.operation='DISTANCE';L.new(sub.outputs[0],dist.inputs[0]);L.new(scale.outputs[0],dist.inputs[1])
  fall=node('ShaderNodeMapRange','Fine etched stroke');fall.clamp=True;fall.inputs['From Min'].default_value=.15;fall.inputs['From Max'].default_value=.75;fall.inputs['To Min'].default_value=1;fall.inputs['To Max'].default_value=0;L.new(dist.outputs['Value'],fall.inputs[0])
  mask=mathnode('MAXIMUM',mask,fall.outputs[0])if mask else fall.outputs[0]
etch=mathnode('MULTIPLY',mask,.075,'Etching visibility')
distances=[mathnode('SUBTRACT',x,12),mathnode('SUBTRACT',1660,x),mathnode('SUBTRACT',y,44),mathnode('SUBTRACT',690,y)]
edge=distances[0]
for d in distances[1:]:edge=mathnode('MINIMUM',edge,d)
outerfade=node('ShaderNodeMapRange','Clouding fades at outer edge');outerfade.clamp=True;outerfade.interpolation_type='SMOOTHSTEP';outerfade.inputs['From Min'].default_value=0;outerfade.inputs['From Max'].default_value=24;outerfade.inputs['To Min'].default_value=0;outerfade.inputs['To Max'].default_value=1;L.new(edge,outerfade.inputs[0]);inside=outerfade.outputs[0]
edgefade=node('ShaderNodeMapRange','Clouding fades inward');edgefade.clamp=True;edgefade.interpolation_type='SMOOTHSTEP';edgefade.inputs['From Min'].default_value=0;edgefade.inputs['From Max'].default_value=90;edgefade.inputs['To Min'].default_value=1;edgefade.inputs['To Max'].default_value=0;L.new(edge,edgefade.inputs[0])
noise=node('ShaderNodeTexNoise','Subtle edge clouding');noise.inputs['Scale'].default_value=13;noise.inputs['Detail'].default_value=3;L.new(coord.outputs[0],noise.inputs[0])
cloud=mathnode('MULTIPLY',mathnode('MULTIPLY',edgefade.outputs[0],noise.outputs['Fac']),inside);cloud=mathnode('MULTIPLY',cloud,.020,'Clouding visibility')
opacity=mathnode('ADD',etch,cloud)
transparent=node('ShaderNodeBsdfTransparent','Clear central glass');em=node('ShaderNodeEmission','Etch catches faint window light');em.inputs['Color'].default_value=(.36,.40,.42,1);em.inputs['Strength'].default_value=1
mix=node('ShaderNodeMixShader','Only the perimeter catches light');L.new(opacity,mix.inputs[0]);L.new(transparent.outputs[0],mix.inputs[1]);L.new(em.outputs[0],mix.inputs[2]);output=node('ShaderNodeOutputMaterial','Material Output');L.new(mix.outputs[0],output.inputs['Surface'])
s.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'existingGeometry':'unchanged','camera':'unchanged','addedSurface':glass.name,'glassDepth':depth,'etchPaths':paths,'changes':['Smoother ink','Backlit Launch letters','Removed Display label','Modest warm light lift','Brighter nebula','Restored viewport perimeter glass marks'],'approval':'pending user review'},indent=2))
