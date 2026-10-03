import bpy,sys
from pathlib import Path
from mathutils import Vector
root=Path('D:/amind/git/agent-2/results/valis-canopy-joins')
src,prefix=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=src)
s=bpy.context.scene
s.render.engine='BLENDER_WORKBENCH'
s.display.shading.light='STUDIO';s.display.shading.studio_light='paint.sl';s.display.shading.color_type='MATERIAL'
s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH'
s.display.shading.curvature_ridge_factor=.35;s.display.shading.curvature_valley_factor=.65
s.display.shading.background_type='WORLD';s.world.color=(.047,.057,.080)
s.render.resolution_x=1100;s.render.resolution_y=1000;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.display.render_aa='16'
c=bpy.data.cameras.new('Review');o=bpy.data.objects.new('Review',c);s.collection.objects.link(o);s.camera=o;c.type='ORTHO'
for name,pos,aim,scale in [('close',(7,-9,13),(0,-1.1,.45),3.7),('top',(0,-1.1,25),(0,-1.1,.45),3.7),('whole',(13,-18,17),(0,0,0),12.8)]:
 o.location=pos;o.rotation_euler=(Vector(aim)-o.location).to_track_quat('-Z','Y').to_euler();c.ortho_scale=scale
 s.render.filepath=str(root/(prefix+'-'+name+'.png'));bpy.ops.render.render(write_still=True)

