import bpy,sys
from pathlib import Path
from mathutils import Vector
root=Path('D:/amind/git/agent-2/results/valis-profile')
src,prefix=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.open_mainfile(filepath=src)
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH'
s.display.shading.light='STUDIO';s.display.shading.studio_light='paint.sl';s.display.shading.color_type='MATERIAL'
s.display.shading.background_type='WORLD';s.world.color=(.047,.057,.080)
s.render.image_settings.file_format='PNG';s.render.resolution_percentage=100;s.display.render_aa='16'
c=bpy.data.cameras.new('Profile review');o=bpy.data.objects.new('Profile review',c);s.collection.objects.link(o);s.camera=o;c.type='ORTHO'
for name,pos,aim,scale,iso in [('isolated',(-25,0,.2),(0,0,.2),8.2,True),('assembled',(-25,0,.2),(0,0,.2),12,False),('quarter',(13,-18,17),(0,0,0),12.8,False)]:
 for obj in s.objects:
  if obj.type=='MESH':obj.hide_render=iso and obj.name!='01 central fuselage'
 o.location=pos;o.rotation_euler=(Vector(aim)-o.location).to_track_quat('-Z','Y').to_euler();c.ortho_scale=scale
 s.render.resolution_x=1400;s.render.resolution_y=450 if name!='quarter' else 1000
 s.render.filepath=str(root/(prefix+'-'+name+'.png'));bpy.ops.render.render(write_still=True)
