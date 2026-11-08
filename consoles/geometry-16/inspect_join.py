import bpy
from pathlib import Path
from mathutils import Vector
s=bpy.context.scene
for o in s.objects:
    o.color=(.12,.12,.12,1)
    if o.name.startswith(('07 reuse |','11 | Long chamfered','11 | Left beveled','11 | Right beveled','11 | Inset viewport','11 | Neutral')):o.hide_render=True
colors={'07 | Continuous left console body':(.25,.55,.9,1),'14 | Forearm fold into Launch and controls':(.8,.3,.2,1),'14 | Launch casing folded returns':(.2,.8,.35,1),'14 | Dark support beneath apron fold':(.8,.7,.2,1)}
for n,c in colors.items():bpy.data.objects[n].color=c
s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1200;s.render.resolution_y=800
s.display.shading.light='STUDIO';s.display.shading.color_type='OBJECT';s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH'
data=bpy.data.cameras.new('Join inspection');camera=bpy.data.objects.new('Join inspection',data);s.collection.objects.link(camera);s.camera=camera
camera.location=(12,-15,11);target=Vector((4,-2,1));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=9
s.render.filepath=str(Path(__file__).resolve().parent/'draft-join.png');bpy.ops.render.render(write_still=True)
