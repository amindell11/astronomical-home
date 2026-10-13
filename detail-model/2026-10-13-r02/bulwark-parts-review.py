import bpy,sys,json,hashlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/fitted-parts')
source=root/'Bulwark-fitted-parts.blend';sha=hashlib.sha256(source.read_bytes()).hexdigest()
src=ship_source.open_source(source)
names=[o.name for o in src.objects if o.type=='MESH' and not o.hide_render]
evaluation=ship_source.Evaluation(src)
display=bpy.data.scenes.new('Separate parts review');parts={}
for name in names:
 mesh=evaluation.mesh(name);mesh.transform(evaluation.world(evaluation.copies[name]));o=bpy.data.objects.new(name+' display',mesh);display.collection.objects.link(o);parts[name]=o
bpy.context.window.scene=display
ship_source.apply_review_preset(display,1300)
s=display.display.shading;s.light='STUDIO';s.show_shadows=True;s.show_cavity=True;s.cavity_type='BOTH';s.show_object_outline=False;s.background_type='WORLD'
display.world.color=(.11,.12,.14);display.display.render_aa='32'
camera=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));display.collection.objects.link(camera);display.camera=camera;camera.data.type='ORTHO';camera.data.clip_start=.01;camera.data.clip_end=100
display.render.resolution_x=1500;display.render.resolution_y=1100
camera.matrix_world=ship_render.camera_matrix((10,-15,-9),(0,0,1),(0,0,1.5),30);camera.data.ortho_scale=14
display.render.filepath=str(root/'studio/underside.png');bpy.ops.render.render(write_still=True)
offsets={'01 Ram nose':(0,1.1,0),'02 Upper hull.001':(0,.25,1.7),'02 Upper hull':(0,-.1,.5),'03 Lower hull':(0,0,-1.35),'04 Tail booms':(.35,-.1,0),'05 Tail tips':(.4,-.9,0),'05 Tail tips.002':(1.1,0,.6),'05 Tail tips.003':(1.1,0,-.5),'09 Side armor':(.8,0,0),'09 Side armor.001':(0,-.1,1.3),'09 Side armor.002':(.35,-.1,1.0),'06 Cockpit dome':(0,.25,2.4),'07 Side insets':(1.25,0,0),'08 Nested engines':(.4,-1.65,0)}
for name,o in parts.items():
 dx,dy,dz=offsets[name]
 for v in o.data.vertices:v.co+=Vector((dx if v.co.x>=0 else -dx,dy,dz))
camera.matrix_world=ship_render.camera_matrix((10,15,9),(0,0,1),(0,0,1.6),30);camera.data.ortho_scale=18
display.render.filepath=str(root/'studio/exploded.png');bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
(root/'studio/parts-review.json').write_text(json.dumps({'source_sha256':sha,'source_unchanged':True,'visible_parts':names,'exploded_is_render_only':True},indent=2))
