import bpy,json
from pathlib import Path
from mathutils import Vector,Quaternion
out=Path('D:/amind/git/agent-7/results/crimson-fuselage-20260927-204902')
bpy.ops.wm.open_mainfile(filepath=str(out/'Before-fuselage-refinement.blend'))
s=bpy.context.scene
for o in s.objects:
    o.hide_render=o.name not in ('Central hull','Cockpit surround')
s.render.engine='BLENDER_WORKBENCH'
s.render.resolution_x=1400;s.render.resolution_y=850;s.render.resolution_percentage=100
s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE'
s.display.shading.single_color=(.64,.66,.7)
s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH'
s.display.shading.background_type='WORLD';s.world.color=(.07,.08,.10)
s.view_settings.view_transform='Standard'
data=bpy.data.cameras.new('Inspection');cam=bpy.data.objects.new('Inspection',data);s.collection.objects.link(cam);s.camera=cam;data.type='ORTHO';data.ortho_scale=1.85
for name,loc in [('side',(4,.06,.02)),('hero',(2,2,1.35)),('top',(0,.06,4))]:
    center=Vector((0,.06,.015));cam.location=Vector(loc)
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(out/('before-'+name+'.png'));bpy.ops.render.render(write_still=True)
