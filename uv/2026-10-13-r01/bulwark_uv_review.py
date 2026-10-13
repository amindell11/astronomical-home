import bpy,sys,json,hashlib
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/uv/2026-10-13-r01')
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
sha=hashlib.sha256(source.read_bytes()).hexdigest()
src=ship_source.open_source(source)
names=[o.name for o in src.objects if o.type=='MESH' and not o.hide_render]
for obj in src.objects:obj.select_set(False)
for name in names:
 obj=src.objects[name];obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=src.objects[names[0]]
src.tool_settings.use_uv_select_sync=False
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.select_all(action='DESELECT')
bpy.ops.uv.select_overlap()
bpy.ops.object.mode_set(mode='OBJECT')
overlap={n:sum(bool(l.value) for l in src.objects[n].data.attributes['.uv_select_vert'].data) for n in names}
(root/'uv-overlap.json').write_text(json.dumps({'source_sha256':sha,'native_uv_selected_loop_counts':overlap,'overlapping_parts':[n for n,v in overlap.items() if v]},indent=2))
print(json.dumps({'native_uv_overlap':{n:v for n,v in overlap.items() if v}}))
src=ship_source.open_source(source)
ev=ship_source.Evaluation(src);ev.show_only(names);scene=ev.scene
ship_source.apply_review_preset(scene,1000)
image=bpy.data.images.load(str(root/'Checker.png'),check_existing=False)
mat=bpy.data.materials.new('UV review checker only');mat.use_nodes=True
node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
mat.node_tree.nodes.active=node;node.select=True
mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
for name in names:
 obj=ev.copies[name]
 for slot in obj.material_slots:slot.link='OBJECT';slot.material=mat
scene.display.shading.color_type='TEXTURE';scene.display.shading.light='STUDIO'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
scene.display.shading.cavity_type='BOTH';scene.display.shading.show_object_outline=False
scene.display.shading.background_type='WORLD';scene.world.color=(.065,.08,.10)
scene.display.render_aa='32'
cam=bpy.data.objects.new('UV checker camera',bpy.data.cameras.new('UV checker camera'));scene.collection.objects.link(cam);scene.camera=cam
cam.data.type='ORTHO';cam.data.clip_start=.01;cam.data.clip_end=100
for name,direction,up,center,scale,w,h in [
 ('checker-hero',(10,15,8.5),(0,0,1),(0,0,1.5),14,1400,1050),
 ('checker-top',(0,0,1),(1,0,0),(0,0,1.5),11.1,1400,820),
 ('checker-side',(-1,0,0),(0,0,1),(0,0,1.8),11.1,1400,700)]:
 cam.matrix_world=ship_render.camera_matrix(direction,up,center,30);cam.data.ortho_scale=scale
 scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(root/(name+'.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
(root/'checker-report.json').write_text(json.dumps({'source_sha256':sha,'source_unchanged':True,'views':['checker-hero','checker-top','checker-side']},indent=2))
