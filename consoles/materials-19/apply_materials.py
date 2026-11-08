"""Material/light study on geometry-18. --name writes a new packed .blend and manifest; refuses overwrite. No existing geometry is moved."""
import bpy,json,math,sys,hashlib
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent;args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Output exists')
s=bpy.context.scene;s.name='19 - Neutral materials and electronic light';O=bpy.data.objects;source=Path(bpy.data.filepath)
col=bpy.data.collections.new('19 - Lighting and surface labels');s.collection.children.link(col)
def rgba(h):
 rgb=[int(h[i:i+2],16)/255 for i in (1,3,5)];return tuple(x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in rgb)+(1,)
def material(label,color,metal=0,rough=.5,emission=0):
 m=bpy.data.materials.new('19 | '+label);m.use_nodes=True;m.diffuse_color=rgba(color);p=m.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=rgba(color);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;p.inputs['Emission Color'].default_value=rgba(color);p.inputs['Emission Strength'].default_value=emission;return m
roles={
 'Armor face':('Warm limestone paint','#bba78e',.08,.55),
 'Broad armor chamfers':('Paint edge','#a3937f',.16,.43),
 'Armor side walls':('Warm grey casing','#766c60',.22,.48),
 'Dark structural housing':('Graphite chassis','#343238',.32,.48),
 'Gasket and recess':('Black elastomer','#121417',0,.8),
 'Hose jacket':('Satin rubber','#242225',0,.52),
 'Faceted controls':('Charcoal control grips','#353234',.02,.54),
 'Control top faces':('Control caps','#514a43',.12,.48),
 'Connector alloy':('Brushed warm nickel','#999187',.68,.37),
 'Neutral lens and pointer':('Ivory legends','#e0d4b9',.06,.43),
 'Raised key faces':('Taupe keys','#8a7d6e',.10,.45),
 'Matte cooling fin faces':('Graphite cooling fins','#474146',.48,.56),
 'Matte cooling fin sides':('Cooling fin edges','#2c292d',.40,.53)}
new={key:material(*params)for key,params in roles.items()}
old_to_new={'08 | '+key:mat for key,mat in new.items()};old_to_new.update({'07 | Recess grey':new['Gasket and recess'],'07 | Bezel grey':new['Armor side walls'],'11 | Continuous chassis grey':new['Dark structural housing']})
for o in s.objects:
 if o.name.startswith('07 reuse |')or not hasattr(o.data,'materials'):continue
 for i,m in enumerate(o.data.materials):
  if m and m.name in old_to_new:o.data.materials[i]=old_to_new[m.name]
# Broad painted covers vary in neutral value without adopting a ship livery.
taupe=material('Secondary cover paint','#8a7d6e',.1,.55)
for n in ['08 | Raised brow cap','14 | Forearm fold into Launch and controls','14 | Thin fitted control face']:
 o=O[n]
 for i,m in enumerate(o.data.materials):
  if m==new['Armor face']:o.data.materials[i]=taupe
amber=material('Amber status glass','#ffb956',.05,.28,3.0);projector=material('Projector optical emitter','#ffd397',.05,.3,5)
button=material('Launch amber resin','#e9a044',.0,.48,.6)
for n in ['08 | Brow light lens','08 | Power light lens','10 | Inline status lens']:
 o=O[n];o.data.materials.clear();o.data.materials.append(amber)
for o in s.objects:
 if o.name=='09 | Projector lens' or o.name.startswith('09 | Projector lens rib'):
  o.data.materials.clear();o.data.materials.append(projector)
o=O['08 | Launch broad beveled button'];o.data.materials.clear();o.data.materials.append(button)
# Packed display exports retain the selected fonts, icons, and real values.
def screen(n,filename,w,h):
 o=O[n];uv=o.data.uv_layers.new(name='DisplayUV')
 for loop in o.data.loops:
  v=o.data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(v.x/w+.5,v.y/h+.5)
 m=material(n.split('|')[-1].strip(),'#111419',.03,.27);nt=m.node_tree;p=nt.nodes['Principled BSDF'];tex=nt.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(root.parent/'blender-console-06/inputs/ui'/filename));tex.image.pack()
 coord=nt.nodes.new('ShaderNodeUVMap');coord.uv_map='DisplayUV';nt.links.new(coord.outputs[0],tex.inputs['Vector'])
 nt.links.new(tex.outputs['Color'],p.inputs['Base Color']);nt.links.new(tex.outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=1.6
 o.data.materials.append(m)
 for f in o.data.polygons:
  if f.normal.z>.9:f.material_index=len(o.data.materials)-1
screen('07 | Main rectangular display','main-hud.png',4.45,3.96)
screen('07 | Detail rectangular display','detail-hud.png',4.45,.98)
# Restore the existing real-ship materials and the authored paint UVs.
holo={}
for o in s.objects:
 if not o.name.startswith('07 reuse | Vanguard')or not hasattr(o.data,'materials'):continue
 src=O[o.name.removeprefix('07 reuse |').strip()]
 for i,m in enumerate(src.data.materials):
  if m.name not in holo:
   cp=m.copy();cp.name='19 | '+m.name;holo[m.name]=cp
   if cp.use_nodes:
    ns=cp.node_tree.nodes
    if ns.get('Ship coordinates'):ns['Ship coordinates'].object=O['07 | Hologram assembly']
    if ns.get('Front opacity / back opacity'):ns['Front opacity / back opacity'].inputs['To Min'].default_value=.48
    if ns.get('Muted painted projection'):
     p=ns['Muted painted projection'];p.inputs['Emission Strength'].default_value=.65
    if ns.get('Contour brightness'):
     ns['Contour brightness'].inputs['Color'].default_value=rgba('#ffe0aa');ns['Contour brightness'].inputs['Strength'].default_value=2.5
  o.data.materials[i]=holo[m.name]
# Reuse authored contour curves as surface graphics, aligned to the unchanged ship meshes.
contour=material('Projected hull outlines','#ffe0aa',0,.5,2.5)
for src in list(bpy.data.objects):
 if src.type!='CURVE' or not src.name.endswith(' / bright contours'):continue
 target=O.get('07 reuse | '+src.name.removesuffix(' / bright contours'))
 if target is None:continue
 o=src.copy();o.data=src.data.copy();o.name='19 | '+src.name;col.objects.link(o);o.parent=None;o.matrix_world=target.matrix_world.copy();o.hide_render=False;o.hide_viewport=False;o.hide_set(False);o.data.materials.clear();o.data.materials.append(contour)
for o in s.objects:
 if o.name.startswith('09 | Projector lens rib'):
  o.data.materials.clear();o.data.materials.append(new['Armor side walls'])
vol=O['11 | Neutral projector frustum'].data.materials[0].copy();vol.name='19 | Warm projection beam';O['11 | Neutral projector frustum'].data.materials[0]=vol
v=vol.node_tree.nodes.get('Principled Volume');v.inputs['Color'].default_value=rgba('#d9b98a');v.inputs['Emission Color'].default_value=rgba('#ffb66c')
# Restore space as a distant camera-aligned backdrop.
cam=s.camera;frame=cam.data.view_frame(scene=s);verts=[cam.matrix_world@(p*(65/-p.z))for p in frame]
d=bpy.data.meshes.new('19 | Distant space');d.from_pydata(verts,[],[(0,1,2,3)]);bg=bpy.data.objects.new(d.name,d);col.objects.link(bg);uv=d.uv_layers.new()
# Camera frame order: top-right, bottom-right, bottom-left, top-left.
for l,xy in zip(uv.data,[(1,1),(1,0),(0,0),(0,1)]):l.uv=xy
m=bpy.data.materials.new('19 | Distant nebula');m.use_nodes=True;nt=m.node_tree;nt.nodes.clear();tex=nt.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(root.parent/'reference-scene-03/inputs/nebula-background.png'));tex.image.pack();e=nt.nodes.new('ShaderNodeEmission');e.inputs[1].default_value=.055;nt.links.new(tex.outputs['Color'],e.inputs[0]);outnode=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(e.outputs[0],outnode.inputs['Surface']);d.materials.append(m)
# Practical lighting is separate and editable.
for o in s.objects:
 if o.type=='LIGHT':o.data.energy=0
s.world=s.world.copy();s.world.name='19 | Faint space ambient';s.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.18,.24,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.12
lights=[]
def area(label,loc,target,energy,size,color):
 d=bpy.data.lights.new('19 | '+label,'AREA');d.energy=energy;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(d.name,d);col.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();lights.append(o);return o
area('Projection spill',(2.4,-.2,4.4),(2.5,-1.2,1),360,3.8,(1,.78,.52))
area('Screen bounce',(-5.4,-2.0,3.0),(-5.5,-4.5,2.2),110,3.8,(1,.89,.71))
area('Faint starlight',(-3,-3,11),(0,0,1),220,9,(.62,.72,1))
area('Soft ambient readability',(-4,-8,7),(0,0,1),220,10,(.90,.89,.86))
area('Launch glow',(6.5,-2.3,1.45),(6,-1,1),18,2,(1,.52,.17))
# Label uses the established HUD label font, separate from the button geometry.
font=bpy.data.fonts.load(str(root.parent/'display-concepts-01/assets/fonts/Rajdhani-Medium.ttf'));font.pack()
ink=material('Printed legends','#30251b',0,.75)
buttonobj=O['08 | Launch broad beveled button'];top=max(v.co.z for v in buttonobj.data.vertices)
d=bpy.data.curves.new('19 | Launch legend','FONT');d.body='L A U N C H';d.font=font;d.align_x='CENTER';d.align_y='CENTER';d.size=.34;d.extrude=0
o=bpy.data.objects.new(d.name,d);col.objects.link(o);o.matrix_world=buttonobj.matrix_world.copy();o.location=buttonobj.matrix_world@Vector((0,0,top+.008));d.materials.append(ink)
s.render.engine='CYCLES';s.cycles.samples=40;s.cycles.use_denoising=True;s.cycles.transparent_max_bounces=12;s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=0
# Restrained optical glow, kept editable in the compositor.
nt=bpy.data.node_groups.new('19 | Restrained electronic glow','CompositorNodeTree');s.compositing_node_group=nt
nt.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor');rl=nt.nodes.new('CompositorNodeRLayers');rl.scene=s;g=nt.nodes.new('CompositorNodeGlare');g.inputs['Type'].default_value='Fog Glow';g.inputs['Quality'].default_value='High';g.inputs['Threshold'].default_value=1.5;g.inputs['Size'].default_value=.25;g.inputs['Strength'].default_value=.12;output=nt.nodes.new('NodeGroupOutput');nt.links.new(rl.outputs['Image'],g.inputs['Image']);nt.links.new(g.outputs['Image'],output.inputs['Image'])
bpy.ops.wm.save_as_mainfile(filepath=str(root/(name+'.blend')))
(root/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'palette':roles,'displaySources':['../blender-console-06/inputs/ui/main-hud.png','../blender-console-06/inputs/ui/detail-hud.png'],'lighting':[o.name for o in lights],'mechanicalGeometry':'unchanged','geometryApproval':'provisional','visualApproval':'pending'},indent=2))
