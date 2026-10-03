import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('D:/amind/git/agent-7/results/crimson-canopy-20260928-001334')
bpy.ops.wm.open_mainfile(filepath=str(out/'Before-canopy-and-web.blend'))
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH'
s.render.resolution_x=1200;s.render.resolution_y=1000;s.render.resolution_percentage=100
sh=s.display.shading;sh.light='STUDIO';sh.color_type='SINGLE';sh.single_color=(.63,.66,.70)
sh.show_cavity=True;sh.cavity_type='BOTH';sh.background_type='WORLD';s.world.color=(.07,.08,.1)
s.view_settings.view_transform='Standard'
d=bpy.data.cameras.new('QA');cam=bpy.data.objects.new('QA',d);s.collection.objects.link(cam);s.camera=cam;d.type='ORTHO'
for name,names,center,loc,scale in [
 ('cockpit',['Canopy','Cockpit surround','Central hull'],(0,.35,.02),(1,1.3,.75),1.15),
 ('cockpit-side',['Canopy','Cockpit surround','Central hull'],(0,.34,.02),(4,.34,.02),1.2),
 ('web',['Structural center web'],(0,0,0),(0,0,5),2.3),
 ('web-hero',['Structural center web'],(0,0,0),(2,2,1.5),2.6)]:
 for o in s.objects:o.hide_render=o.name not in names
 d.ortho_scale=scale;cam.location=loc;cam.rotation_euler=(Vector(center)-cam.location).to_track_quat('-Z','Y').to_euler()
 s.render.filepath=str(out/('before-'+name+'.png'));bpy.ops.render.render(write_still=True)
