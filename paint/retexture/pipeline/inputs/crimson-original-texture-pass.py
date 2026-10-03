import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('D:/amind/git/agent-7/art/ships/crimson')
WORK=Path('C:/Users/amind/.codex/artifact-archives/crimson-texturing-20260928-010254')
bpy.ops.wm.open_mainfile(filepath=str(WORK/'Before-texturing.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
brush=bpy.data.images.load(str(BASE/'textures/painted-brush-source.png'));brush.colorspace_settings.name='Non-Color';brush.pack()
PALETTE={'silver':(.94,.92,.86),'red':(.92,.038,.023),'charcoal':(.14,.15,.17),'ink':(.019,.023,.029),'glass':(.028,.034,.041),'green':(.10,.66,.13),'rim':(.57,.58,.55)}
def linear(c):return tuple(v/12.92 if v<.04045 else ((v+.055)/1.055)**2.4 for v in c)
def role(name):
 if name=='Canopy':return 'glass'
 if name in ('Canopy perimeter rim','Canopy transverse frame','Dorsal service spine','Service pod cap','Engine housing') or 'wing foundation' in name or 'fin' in name.lower():return 'red'
 if any(w in name for w in ('armor','prow','Cockpit surround','Rear cockpit yoke','Engine neck','Engine support frame','Wing root beam')):return 'silver'
 if name=='Engine forward cowling':return 'rim'
 return 'charcoal'
def create_material(o):
 name=o.name;r=role(name);mat=bpy.data.materials.new('Paint source - '+name);mat.use_nodes=True;mat.diffuse_color=(*PALETTE[r],1)
 nt=mat.node_tree;nt.nodes.clear();N=nt.nodes;L=nt.links
 def node(t):return N.new(t)
 def link(a,b):L.new(a,b)
 def mathn(op,a,b=None):
  n=node('ShaderNodeMath');n.operation=op
  for v,i in [(a,0),(b,1)]:
   if v is not None:
    if isinstance(v,(float,int)):n.inputs[i].default_value=v
    else:link(v,n.inputs[i])
  return n.outputs[0]
 def vec(op,a,b=None):
  n=node('ShaderNodeVectorMath');n.operation=op
  for v,i in [(a,0),(b,1)]:
   if v is not None:
    if isinstance(v,(tuple,list,Vector)):n.inputs[i].default_value=v
    else:link(v,n.inputs[i])
  return n.outputs['Value' if op in ('DOT_PRODUCT','LENGTH','DISTANCE') else 'Vector']
 def mix(f,a,b):
  n=node('ShaderNodeMixRGB');n.blend_type='MIX'
  for v,i in [(f,0),(a,1),(b,2)]:
   if isinstance(v,(tuple,list)):n.inputs[i].default_value=(*v[:3],1)
   elif isinstance(v,(int,float)):n.inputs[i].default_value=v
   else:link(v,n.inputs[i])
  return n.outputs[0]
 def tint(a,b):
  n=node('ShaderNodeMixRGB');n.blend_type='MULTIPLY';n.inputs[0].default_value=1
  for v,i in [(a,1),(b,2)]:
   if isinstance(v,tuple):n.inputs[i].default_value=(*v,1)
   else:link(v,n.inputs[i])
  return n.outputs[0]
 geom=node('ShaderNodeNewGeometry');sep=node('ShaderNodeSeparateXYZ');link(geom.outputs['Position'],sep.inputs[0])
 folded=node('ShaderNodeCombineXYZ');link(mathn('ABSOLUTE',sep.outputs['X']),folded.inputs['X']);link(sep.outputs['Y'],folded.inputs['Y']);link(sep.outputs['Z'],folded.inputs['Z'])
 tex=node('ShaderNodeTexImage');tex.image=brush;tex.projection='BOX';tex.projection_blend=.25;tex.extension='REPEAT'
 link(vec('MULTIPLY',folded.outputs[0],(1.3,1.3,1.3)),tex.inputs['Vector'])
 factor=mathn('ADD',mathn('MULTIPLY',tex.outputs['Color'],3.2),-1.0)
 col=tint(linear(PALETTE[r]),factor)
 # Three broad painted tones based on surface orientation, baked into color.
 normal=node('ShaderNodeSeparateXYZ');link(geom.outputs['Normal'],normal.inputs[0])
 ramp=node('ShaderNodeValToRGB');ramp.color_ramp.interpolation='EASE';e=ramp.color_ramp.elements;e[0].position=.10;e[0].color=(.35,.38,.45,1);e[1].position=.92;e[1].color=(1,1,1,1);mid=e.new(.55);mid.color=(.63,.64,.69,1)
 link(mathn('ABSOLUTE',normal.outputs['Z']),ramp.inputs[0]);col=tint(col,ramp.outputs[0])
 # Ink outline and an inset painted highlight on the broad shell cap.
 polys=[p for p in o.data.polygons if len(p.vertices)>4]
 if (any(w in name for w in ('armor','prow','wing foundation','fin')) or 'swept fin' in name or name=='Wing root beam') and polys:
  cap=max(polys,key=lambda p:sum(abs(o.data.vertices[i].co.z) for i in p.vertices)/len(p.vertices))
  coords=[o.matrix_world@o.data.vertices[i].co for i in cap.vertices];points=[Vector((abs(v.x),v.y,0)) for v in coords]
  if r=='silver':
   minx,maxx=min(p.x for p in points),max(p.x for p in points);miny,maxy=min(p.y for p in points),max(p.y for p in points)
   sx=mathn('DIVIDE',mathn('SUBTRACT',mathn('ABSOLUTE',sep.outputs['X']),minx),max(maxx-minx,.001))
   sy=mathn('DIVIDE',mathn('SUBTRACT',sep.outputs['Y'],miny),max(maxy-miny,.001))
   sweep=mathn('ADD',mathn('MULTIPLY',sx,.65),mathn('MULTIPLY',sy,.35))
   sweep=mathn('ADD',sweep,mathn('MULTIPLY',mathn('SUBTRACT',tex.outputs['Color'],.65),.5))
   dark=mathn('LESS_THAN',sweep,.33);light=mathn('GREATER_THAN',sweep,.66)
   col=mix(dark,col,tint(col,(.58,.61,.69)));col=mix(light,col,tint(col,(1.22,1.21,1.16)))
  flat=node('ShaderNodeCombineXYZ');link(mathn('ABSOLUTE',sep.outputs['X']),flat.inputs[0]);link(sep.outputs['Y'],flat.inputs[1]);distance=None
  for a,b in zip(points,points[1:]+points[:1]):
   edge=b-a;length=edge.length
   if length<1e-6:continue
   unit=edge/length;p=vec('SUBTRACT',flat.outputs[0],a);d=mathn('MINIMUM',mathn('MAXIMUM',vec('DOT_PRODUCT',p,unit),0),length)
   scaled=node('ShaderNodeVectorMath');scaled.operation='SCALE';scaled.inputs[0].default_value=unit;link(d,scaled.inputs['Scale'])
   dis=vec('LENGTH',vec('SUBTRACT',p,scaled.outputs[0]));distance=dis if distance is None else mathn('MINIMUM',distance,dis)
  if distance is not None:
   edgeink=mathn('LESS_THAN',distance,.0030);highlight=mathn('LESS_THAN',distance,.0065)
   edgecol=linear((.94,.92,.83) if r=='silver' else (.93,.18,.095))
   col=mix(highlight,col,edgecol);col=mix(edgeink,col,linear(PALETTE['ink']))
 if 'fin' in name.lower():
  # Existing bevel strips provide the dark rim; no added geometry.
  mask=mathn('LESS_THAN',mathn('ABSOLUTE',normal.outputs['Z']),.65);col=mix(mask,col,linear(PALETTE['ink']))
 if name=='Canopy':
  # A deliberate narrow, softly edged painted reflection follows the canopy length.
  x=mathn('ABSOLUTE',sep.outputs['X']);band=mathn('MULTIPLY',mathn('GREATER_THAN',x,.025),mathn('LESS_THAN',x,.050))
  col=mix(band,col,tint(linear((.36,.38,.39)),factor))
 if name=='Service channel floor':
  band=mathn('GREATER_THAN',mathn('ABSOLUTE',sep.outputs['X']),.068);col=mix(band,col,tint(linear(PALETTE['green']),factor))
 if name in ('Canopy perimeter rim','Canopy transverse frame'):
  col=tint(linear((.85,.045,.025)),factor)
 if name=='Rear cockpit yoke':
  xband=mathn('MULTIPLY',mathn('GREATER_THAN',mathn('ABSOLUTE',sep.outputs['X']),.115),mathn('LESS_THAN',mathn('ABSOLUTE',sep.outputs['X']),.142))
  yband=mathn('MULTIPLY',mathn('GREATER_THAN',sep.outputs['Y'],-.203),mathn('LESS_THAN',sep.outputs['Y'],-.178))
  col=mix(mathn('MULTIPLY',xband,yband),col,tint(linear((.18,.72,.12)),factor))
 if name=='Engine nozzle':
  ys=[(o.matrix_world@v.co).y for v in o.data.vertices];lo,hi=min(ys),max(ys)
  # Two small painted rings on the existing exhaust barrel.
  band1=mathn('LESS_THAN',mathn('ABSOLUTE',mathn('SUBTRACT',sep.outputs['Y'],lo+.020)),.004)
  band2=mathn('LESS_THAN',mathn('ABSOLUTE',mathn('SUBTRACT',sep.outputs['Y'],hi-.025)),.004)
  col=mix(mathn('MAXIMUM',band1,band2),col,tint(linear((.38,.39,.38)),factor))
 out=node('ShaderNodeOutputMaterial');em=node('ShaderNodeEmission');link(col,em.inputs['Color']);link(em.outputs[0],out.inputs['Surface'])
 o.data.materials.clear();o.data.materials.append(mat)
 for p in o.data.polygons:p.material_index=0
 return mat
materials=[create_material(o) for o in objects]
# One shared, editable UV atlas, with mirrored parts intentionally sharing islands.
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(70),island_margin=.006,area_weight=.25,correct_aspect=True,scale_to_bounds=True)
bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin=.003)
bpy.ops.object.mode_set(mode='OBJECT')
for o in objects:o.data.uv_layers.active.name='CrimsonPaintUV'
image=bpy.data.images.new('Crimson painted base color',width=4096,height=4096,alpha=False);image.generated_color=(.03,.03,.035,1);image.colorspace_settings.name='sRGB'
for mat in materials:
 n=mat.node_tree.nodes.new('ShaderNodeTexImage');n.name='BAKE TARGET';n.image=image;mat.node_tree.nodes.active=n
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=1;s.cycles.use_denoising=False;s.render.bake.margin=12;s.render.bake.use_clear=True
# Evaluate only temporary copies into a single bake surface; original parts remain editable.
bpy.ops.object.select_all(action='DESELECT')
copies=[];dg=bpy.context.evaluated_depsgraph_get()
for o in objects:
 mesh=bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
 mesh.transform(o.matrix_world)
 copy=bpy.data.objects.new('BAKE COPY - '+o.name,mesh);s.collection.objects.link(copy);copy.select_set(True);copies.append(copy)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();baker=bpy.context.object
print('START SINGLE-SURFACE BAKE',flush=True)
bpy.ops.object.bake(type='EMIT',use_clear=True,margin=12)
print('BAKE COMPLETE',flush=True)
bpy.data.objects.remove(baker,do_unlink=True)
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
image.filepath_raw=str(BASE/'textures/Crimson_BaseColor.png');image.file_format='PNG';image.save();image.pack()
# Keep procedural sources in the candidate for traceability, but use only the baked paint in final materials.
for mat in materials:mat.use_fake_user=True
final=bpy.data.materials.new('Crimson - hand-painted base color');final.use_nodes=True;nt=final.node_tree;bsdf=nt.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.88;bsdf.inputs['Metallic'].default_value=0;bsdf.inputs['Specular IOR Level'].default_value=.15
tex=nt.nodes.new('ShaderNodeTexImage');tex.name='Paintable base color';tex.image=image;nt.links.new(tex.outputs['Color'],bsdf.inputs['Base Color']);nt.links.new(tex.outputs['Color'],bsdf.inputs['Emission Color']);bsdf.inputs['Emission Strength'].default_value=.35;nt.nodes.active=tex
for o in objects:o.data.materials.clear();o.data.materials.append(final)
# Record only UV changes and materials for transfer into the still-open live file.
uvs={o.name:[list(d.uv) for d in o.data.uv_layers.active.data] for o in objects}
(WORK/'uv-data.json').write_text(json.dumps(uvs))
bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'Textured-candidate.blend'))
# Workbench texture previews are consistent with the neutral modeling captures.
s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1400;s.render.resolution_y=1400;s.render.resolution_percentage=100
sh=s.display.shading;sh.light='FLAT';sh.color_type='TEXTURE';sh.show_shadows=True;sh.show_cavity=True;sh.cavity_type='BOTH';sh.show_object_outline=True;sh.object_outline_color=(.008,.01,.016);sh.background_type='WORLD';s.world.color=(.045,.055,.08);s.view_settings.view_transform='Standard'
points=[o.matrix_world@Vector(p) for o in objects for p in o.bound_box];center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
d=bpy.data.cameras.new('Paint QA');cam=bpy.data.objects.new('Paint QA',d);s.collection.objects.link(cam);s.camera=cam;d.type='ORTHO'
for name,axis,up,size in [('top',(0,0,1),(0,1,0),1400),('hero',(1,1,1.5),(0,0,1),1400),('side',(1,0,0),(0,0,1),1400),('front',(0,1,0),(0,0,1),1400),('game-scale',(0,0,1),(0,1,0),320)]:
 z=Vector(axis).normalized();x=Vector(up).cross(z).normalized();y=z.cross(x);cam.rotation_euler=Matrix((x,y,z)).transposed().to_euler();cam.location=center+z*8
 span=max(max((p-center).dot(v) for p in points)-min((p-center).dot(v) for p in points) for v in [x,y]);d.ortho_scale=span*1.1;s.render.resolution_x=size;s.render.resolution_y=size
 s.render.filepath=str(BASE/'previews'/('textured-'+name+'.png'));bpy.ops.render.render(write_still=True)
print('COMPLETE',flush=True)



