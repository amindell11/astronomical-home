import bpy,sys,json,hashlib,shutil
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
sys.path.insert(0,str(Path(__file__).parent))
from paint_region_rules_r04 import region_for
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend');folder=source.parent
review=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r04')
src=ship_source.open_source(source);before=ship_source.fingerprint(src)
assert hashlib.sha256(source.read_bytes()).hexdigest()=='34aef2be48e17fc4e842826396981046bcbc101548af3c2289f7ce7fc2a9c78d'
selection=[o.name for o in bpy.context.selected_objects];active=bpy.context.view_layer.objects.active
palette={'Plum':'#66505C','Bone':'#D8CDB4','Structure':'#302E34','Glass':'#718591','Flap':'#66505C'}
def rgba(h):
 vals=[int(h[i:i+2],16)/255 for i in (1,3,5)]
 return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in vals)+(1,)
images={};(folder/'layers').mkdir(exist_ok=True)
for name in ('Shadow','Light','Ink','BaseRegion'):
 shutil.copyfile(review/'layers'/(name+'.png'),folder/'layers'/(name+'.png'))
 im=bpy.data.images.load(str(folder/'layers'/(name+'.png')),check_existing=False);im.name='Bulwark '+name;im.colorspace_settings.name='Non-Color';im.pack();im.filepath='//layers/'+name+'.png';images[name]=im
materials=[]
for key,h in palette.items():
 mat=bpy.data.materials['Bulwark '+key];mat.use_nodes=True;mat.diffuse_color=rgba(h)
 nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
 uv=nodes.new('ShaderNodeUVMap');uv.uv_map='PaintUV';uv.location=(-900,100)
 tex={}
 for i,(name,im) in enumerate(images.items()):
  n=nodes.new('ShaderNodeTexImage');n.name=name;n.label=name+' mask';n.image=im;n.location=(-690,420-i*260);links.new(uv.outputs['UV'],n.inputs['Vector']);tex[name]=n
 def math(op,a,b,x,y):
  n=nodes.new('ShaderNodeMath');n.operation=op;n.location=(x,y)
  for i,v in enumerate((a,b)):
   if isinstance(v,(int,float)):n.inputs[i].default_value=v
   else:links.new(v,n.inputs[i])
  return n.outputs[0]
 shadow=math('MULTIPLY',tex['Shadow'].outputs['Color'],.9,-430,420)
 remaining=math('SUBTRACT',1,shadow,-250,420)
 base=nodes.new('ShaderNodeMixRGB');base.blend_type='MULTIPLY';base.inputs[0].default_value=1;base.inputs[1].default_value=rgba(palette['Plum'] if key=='Bone' else h);links.new(remaining,base.inputs[2]);base.location=(-50,420);base.name='Base coat';base.label='Recolourable plum substrate' if key=='Bone' else 'Recolourable '+key
 result=base.outputs[0]
 for name,strength,color,y in [('Light',.7,'#E3D8CB',160),('Ink',1.05,'#29222F',-100)]:
  factor=math('MULTIPLY',tex[name].outputs['Color'],strength,-430,y)
  mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.location=(160,y);mix.inputs[2].default_value=rgba(color);links.new(factor,mix.inputs[0]);links.new(result,mix.inputs[1]);result=mix.outputs[0]
 if key in ('Bone','Flap'):
  mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.location=(360,20);mix.inputs[2].default_value=rgba(palette['Bone']);links.new(tex['BaseRegion'].outputs['Color'],mix.inputs[0]);links.new(result,mix.inputs[1]);result=mix.outputs[0];mix.name='Alternate base coat';mix.label='Bone ram coat / plum chevron' if key=='Bone' else 'Diagonal bone outer fin coat'
 bsdf=nodes.new('ShaderNodeBsdfPrincipled');bsdf.location=(590,220);bsdf.inputs['Roughness'].default_value=.82;bsdf.inputs['Metallic'].default_value=0;links.new(result,bsdf.inputs['Base Color'])
 output=nodes.new('ShaderNodeOutputMaterial');output.location=(900,220);links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
 materials.append(mat)
ev=ship_source.Evaluation(src);matrices={n:ev.to_space(n).copy() for n in ev.copies}
for o in list(ev.scene.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.data.scenes.remove(ev.scene);bpy.context.window.scene=src
for obj in src.objects:
 if obj.type!='MESH' or obj.hide_render:continue
 obj.data.materials.clear()
 for mat in materials:obj.data.materials.append(mat)
 matrix=matrices[obj.name];normal_matrix=matrix.to_3x3().inverted().transposed()
 for face in obj.data.polygons:
  normal=(normal_matrix@face.normal).normalized()
  face.material_index=region_for(obj.name,normal,matrix@face.center)
after=ship_source.fingerprint(src)
assert before['components']['geometry']==after['components']['geometry']
assert before['components']['uv']==after['components']['uv']
for name in ('Upper flap deployment','Lower flap deployment'):assert src.objects[name].rotation_euler.x==0
for o in src.objects:o.select_set(o.name in selection)
bpy.context.view_layer.objects.active=active
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
sha=hashlib.sha256(source.read_bytes()).hexdigest()
(folder/'palettes.json').write_text(json.dumps({'schema_version':1,'default':'plum-bone','palettes':{'plum-bone':palette},'alternate_base_regions':{'Bone':'Bone nose coat crosses onto forward armor with angled rear boundary and plum chevron','Flap':'Diagonal bone outer fin panels over plum substrate'}},indent=2))
(folder/'paint-settings.json').write_text(json.dumps({'texture_size':2048,'uv_layer':'PaintUV','shadow':.9,'light':.7,'ink':1.05,'light_color':'#E3D8CB','ink_color':'#29222F','base_region_mask':'layers/BaseRegion.png','base_region_meaning':'Shaded bone coat opacity over plum substrate; retains edge chips and painted planes','unity_shader':'Astronomical/Drawn/Surface','base_region_shader_binding':'_EmissionMap with _PaintEmissionBlend=1 and emission strength 0; alternate bone coat supplied as linear color','shader_shadow_color':[.78,.74,.70],'glass_shader_shadow_color':[.5,.6,.69],'shader_shadow_threshold':.22,'shader_shadow_softness':.16,'status':'mask-review-pending'},indent=2))
audit={'source_sha256':sha,'baseline_uv_source_sha256':'2790e4dfce08191ccf35443e46b2bacfecf70b630b2babdf6ca7c44cbbfe9e54','previous_paint_source_sha256':'34aef2be48e17fc4e842826396981046bcbc101548af3c2289f7ce7fc2a9c78d','baseline_commit':'b4dfd2a3451506be84136aedcc0eb458785aeb26','geometry_unchanged':True,'uvs_unchanged':True,'geometry_fingerprint':after['components']['geometry'],'uv_fingerprint':after['components']['uv'],'materials_changed':True,'packed_grayscale_images':list(images),'separate_editable_base_colours':palette,'approval':'UV approved; palette approved; revised masks pending review'}
(review/'paint-source-audit.json').write_text(json.dumps(audit,indent=2));print(json.dumps(audit))
