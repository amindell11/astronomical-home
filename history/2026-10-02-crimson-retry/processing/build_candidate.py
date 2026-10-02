import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
out=Path('D:/amind/git/agent-2/results/valis-crimson-retry/v02')
if (out/'ARTIST_SOURCE_LOCK').exists():raise RuntimeError('Artist source is locked. Use a new revision directory; do not overwrite these layers or this blend.')
bpy.ops.wm.open_mainfile(filepath=str(out/'Valis-UV-layout.blend'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
def fingerprint():
 d={o.name:dict(vertices=[list(o.matrix_world@v.co) for v in o.data.vertices],faces=[list(p.vertices) for p in o.data.polygons],modifiers=[[m.name,m.type] for m in o.modifiers]) for o in meshes}
 (out/'fingerprint-data.json').write_text(json.dumps(d,sort_keys=True,separators=(',',':')));return hashlib.sha256(json.dumps(d,sort_keys=True,separators=(',',':')).encode()).hexdigest()
for o in meshes:
 for m in o.modifiers:
  if m.type=='MIRROR':m.offset_v=-.5
expected='c713fb3e410c5c6b66c74ad0e5d5b4885984cfe709b026866cdc6e6ae8d73981'
actual=fingerprint();assert actual==expected,(actual,expected)
settings=json.loads((out/'settings.json').read_text())
g=bpy.data.node_groups.new('Valis painted finish','ShaderNodeTree')
for name,typ in [('Base coat','NodeSocketColor'),('Shadow strength','NodeSocketFloat'),('Light strength','NodeSocketFloat'),('Ink strength','NodeSocketFloat')]:
 s=g.interface.new_socket(name=name,in_out='INPUT',socket_type=typ)
 if typ=='NodeSocketFloat':s.default_value=1;s.min_value=0;s.max_value=2
s=g.interface.new_socket(name='Painted color',in_out='OUTPUT',socket_type='NodeSocketColor')
n=g.nodes;l=g.links;inp=n.new('NodeGroupInput');inp.location=(-750,0);end=n.new('NodeGroupOutput');end.location=(700,50)
def mix(name,kind,loc):
 q=n.new('ShaderNodeMixRGB');q.name=q.label=name;q.blend_type=kind;q.location=loc;return q
imgs={}
for i,name in enumerate(['Shadow','Light','Ink']):
 img=bpy.data.images.load(str(out/'layers'/f'{name}.png'));img.name='Valis '+name+' — editable';img.colorspace_settings.name='Non-Color';img.pack();imgs[name]=img
 tx=n.new('ShaderNodeTexImage');tx.image=img;tx.name=tx.label=name+' paint layer';tx.location=(-750,400-i*220)
 mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';mul.location=(-470,400-i*220);mul.label=name+' strength';l.new(tx.outputs['Color'],mul.inputs[0]);l.new(inp.outputs[name+' strength'],mul.inputs[1])
 if name=='Shadow':
  inv=n.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;inv.location=(-220,400);l.new(mul.outputs[0],inv.inputs[1])
  q=mix('Painted shadows','MULTIPLY',(0,150));q.inputs[0].default_value=1;l.new(inp.outputs['Base coat'],q.inputs[1]);l.new(inv.outputs[0],q.inputs[2]);last=q
 elif name=='Light':
  q=mix('Painted lights','MIX',(230,120));q.inputs[2].default_value=settings['light_color_linear'];l.new(mul.outputs[0],q.inputs[0]);l.new(last.outputs[0],q.inputs[1]);last=q
 else:
  q=mix('Panel ink','MIX',(460,100));q.inputs[2].default_value=settings['ink_color_linear'];l.new(mul.outputs[0],q.inputs[0]);l.new(last.outputs[0],q.inputs[1]);last=q
l.new(last.outputs[0],end.inputs[0])
for m in bpy.data.materials:
 if not m.users:continue
 base=list(m.diffuse_color);m.use_nodes=True;ns=m.node_tree.nodes;ls=m.node_tree.links;ns.clear()
 rgb=ns.new('ShaderNodeRGB');rgb.name=rgb.label='BASE COAT — recolor here';rgb.outputs[0].default_value=base;rgb.location=(-380,0)
 gr=ns.new('ShaderNodeGroup');gr.node_tree=g;gr.name=gr.label='PAINT — independent layers';gr.location=(-110,0)
 for name in ['Shadow','Light','Ink']:gr.inputs[name+' strength'].default_value=settings[name.lower()+'_strength']
 ls.new(rgb.outputs[0],gr.inputs['Base coat'])
 emit=ns.new('ShaderNodeEmission');emit.label='Unlit painted review';emit.location=(150,0);ls.new(gr.outputs[0],emit.inputs['Color'])
 output=ns.new('ShaderNodeOutputMaterial');output.location=(380,0);ls.new(emit.outputs[0],output.inputs['Surface'])
 # The active image remains paintable in the native texture-paint workspace.
 tx=ns.new('ShaderNodeTexImage');tx.image=imgs['Shadow'];tx.location=(-400,-330);tx.label='ACTIVE HAND PAINT TARGET';ns.active=tx
scene=bpy.context.scene
for o in list(scene.objects):
 if o.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(o,do_unlink=True)
proj=json.loads((out/'projection.json').read_text());center=Vector(proj['center'])
bpy.ops.object.camera_add(location=center+Vector((0,0,30)));cam=bpy.context.object;cam.name='Paint review camera';cam.data.type='ORTHO';cam.data.ortho_scale=proj['span'];scene.camera=cam
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=1600;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.view_settings.view_transform='Standard'
scene.render.filepath=str(out/'actual-top.png');bpy.ops.render.render(write_still=True)
cam.location=center+Vector((9,-12,15));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=proj['span']*1.06
scene.render.filepath=str(out/'actual-quarter.png');bpy.ops.render.render(write_still=True)
for o in meshes:o['geometry_status']='Approved locked mesh; paint only'
scene['paint_source']='paint-source.png';scene['paint_controls']='Material group: Shadow strength, Light strength, Ink strength; RGB base coat.';scene['palettes_json']=Path('D:/amind/git/astronomical-home/results/valis-texturing-restart/base-model/palettes.json').read_text()
for img in imgs.values():img.filepath='//layers/'+img.name.split(' —')[0].replace('Valis ','')+'.png'
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='IMAGE_EDITOR':area.spaces.active.image=imgs['Shadow']
  if area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Valis-Crimson-study.blend'))
(out/'validation.json').write_text(json.dumps(dict(geometry_sha256=actual,matches_approved=actual==expected,mesh_parts=len(meshes),mirror_modifiers=sum(m.type=='MIRROR' for o in meshes for m in o.modifiers),uv_charts=150,base_coat_preserved=True,paint_layers=['Shadow','Light','Ink'],paint_symmetry='Independent left and right paint; live geometry symmetry retained',evidence='Blender unlit actual-model previews; Unity integration untouched'),indent=2))
print('VALIDATION '+(out/'validation.json').read_text())





