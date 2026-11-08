"""Render the loaded scene without saving it; --prefix names camera, angle and inspection PNGs."""
import bpy,sys
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];prefix=args[args.index('--prefix')+1]
s=bpy.context.scene;s.cycles.samples=32
def render(label):
    s.render.filepath=str(root/(prefix+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
render('camera')
bpy.data.objects['11 | Neutral projector frustum'].hide_render=True
s.camera=bpy.data.objects['07 Camera - construction'];render('angle')
for o in s.objects:
    if o.name.startswith(('07 reuse |','11 | Long chamfered','11 | Left beveled','11 | Right beveled','11 | Inset viewport')):o.hide_render=True
s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1400;s.render.resolution_y=900
s.display.shading.light='STUDIO';s.display.shading.color_type='MATERIAL';s.display.shading.show_shadows=True
s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH'
s.display.shading.background_type='WORLD';s.world.color=(.13,.13,.13)
s.display.shading.show_specular_highlight=False
data=bpy.data.cameras.new('Inspection camera');camera=bpy.data.objects.new('Inspection camera',data);s.collection.objects.link(camera)
s.camera=camera;data.type='ORTHO';data.ortho_scale=21
target=Vector((0,-.6,1.3))
for label,loc in [('right',(12,-15,11)),('left',(-12,-13,8)),('rear',(9,10,8))]:
    camera.location=loc;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();render(label)
