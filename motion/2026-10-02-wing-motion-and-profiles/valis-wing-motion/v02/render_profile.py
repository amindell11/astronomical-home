import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector

out = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/v02')
label = sys.argv[sys.argv.index('--') + 1]
scene = bpy.context.scene
for obj in scene.objects:
    if obj.hide_get() or obj.hide_viewport:
        obj.hide_render = True
camera = scene.camera
scene.render.engine = 'BLENDER_EEVEE'
scene.eevee.taa_render_samples = 16
scene.render.resolution_x = 800
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.render.use_compositing = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
camera.data.type = 'ORTHO'
center = Vector((0, .30, .35))
for view, location in [('top', center + Vector((0, 0, 25))), ('side',center + Vector((20,0,0))), ('low',center + Vector((12,-10,4.2)))]:
    camera.location = location
    if view == 'top':
        camera.rotation_euler = (0, 0, math.pi)
        camera.data.ortho_scale = 12.3
    else:
        camera.rotation_euler = (center-location).to_track_quat('-Z','Y').to_euler()
        camera.data.ortho_scale = 12.3
    scene.render.filepath = str(out / (label + '-' + view + '.png'))
    bpy.ops.render.render(write_still=True)
