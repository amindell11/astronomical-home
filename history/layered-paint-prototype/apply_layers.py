import bpy,json
from pathlib import Path
out=Path('D:/amind/git/agent-2/results/valis-paint-v2')
bpy.ops.wm.open_mainfile(filepath=str(out/'Valis-UV.blend'))
baseline=json.loads(Path('D:/amind/git/agent-2/results/valis-integration/approved-geometry.json').read_text())
geometry={o.name:{'vertices':[list(o.matrix_world@v.co) for v in o.data.vertices],'faces':[list(p.vertices) for p in o.data.polygons],'modifiers':[[m.name,m.type] for m in o.modifiers]} for o in bpy.context.scene.objects if o.type=='MESH'}
assert geometry==baseline,'Approved mesh changed'
(out/'geometry-verification.json').write_text(json.dumps({'matches_approved_geometry':True,'mesh_parts':len(geometry),'uv_map':'ValisPaintUV','layers':['Paint_Shadow.png','Paint_Light.png','Paint_Ink.png']},indent=2))
images={key:bpy.data.images.load(str(out/('Paint_'+key+'.png')),check_existing=True) for key in ('Shadow','Light','Ink')}
for img in images.values():img.colorspace_settings.name='Non-Color';img.pack()
for mat in bpy.data.materials:
 base=tuple(mat.diffuse_color);mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear()
 output=nt.nodes.new('ShaderNodeOutputMaterial');output.location=(1000,0)
 emit=nt.nodes.new('ShaderNodeEmission');emit.location=(800,0);nt.links.new(emit.outputs[0],output.inputs['Surface'])
 rgb=nt.nodes.new('ShaderNodeRGB');rgb.name='Base coat - recolor here';rgb.label=rgb.name;rgb.outputs[0].default_value=base;rgb.location=(-600,120)
 last=rgb.outputs[0]
 for index,(key,strength,target) in enumerate([('Shadow',.55,(0,0,0,1)),('Light',.22,(1,1,1,1)),('Ink',.9,(.003,.008,.012,1))]):
  tex=nt.nodes.new('ShaderNodeTexImage');tex.image=images[key];tex.name='Editable '+key+' paint';tex.label=tex.name;tex.location=(-600,-200-index*260)
  amount=nt.nodes.new('ShaderNodeMath');amount.operation='MULTIPLY';amount.name=key+' strength';amount.label=amount.name;amount.inputs[1].default_value=strength;amount.location=(-300,-120-index*180);nt.links.new(tex.outputs['Color'],amount.inputs[0])
  mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.location=(index*220,0);mix.inputs[2].default_value=target;nt.links.new(last,mix.inputs[1]);nt.links.new(amount.outputs[0],mix.inputs[0]);last=mix.outputs[0]
  if key=='Light':nt.nodes.active=tex
 nt.links.new(last,emit.inputs['Color'])
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.cycles.use_denoising=False
s.view_settings.view_transform='Standard';s.view_settings.look='None';s.view_settings.exposure=0;s.view_settings.gamma=1
s.world.use_nodes=True;s.world.node_tree.nodes.get('Background').inputs[0].default_value=(.047,.057,.08,1)
s.render.resolution_x=1100;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
c=bpy.data.cameras.new('Paint review');o=bpy.data.objects.new('Paint review',c);s.collection.objects.link(o);s.camera=o;c.type='ORTHO';c.ortho_scale=12.8
for obj in s.objects:
 if obj.type=='MESH':obj.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Valis-painted-v2.blend'))
for view,pos in [('quarter',(13,-18,17)),('top',(0,0,25)),('game-scale',(13,-18,17))]:
 o.location=pos;o.rotation_euler=(0,0,0) if view=='top' else (-o.location).to_track_quat('-Z','Y').to_euler()
 if view=='game-scale':s.render.resolution_x=165;s.render.resolution_y=150
 s.render.filepath=str(out/(view+'.png'));bpy.ops.render.render(write_still=True)
print('Actual-model paint preview saved')


palettes=json.loads(Path('D:/amind/git/agent-2/art/ships/valis/palettes.json').read_text())['palettes']
s.render.resolution_x=1100;s.render.resolution_y=1000
for palette in ('ivory-lavender','jade-gray'):
 for mat in bpy.data.materials:
  if mat.name in palettes[palette]:mat.node_tree.nodes['Base coat - recolor here'].outputs[0].default_value=palettes[palette][mat.name]
 s.render.filepath=str(out/(palette+'.png'));bpy.ops.render.render(write_still=True)
