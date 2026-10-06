from pathlib import Path
import sys
import bpy

root=Path('D:/amind/git/agent-1')
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_render
import ship_source

out=root/'results/nightshade-blockout/round-03/volume'
out.mkdir(parents=True,exist_ok=True)
evaluation,bounds=ship_render.prepare(root/'art/ships/nightshade/Nightshade.blend')
flat=ship_source.apply_review_preset
def volume_preset(scene,size=1024):
    flat(scene,size)
    scene.display.shading.light='STUDIO'
    scene.display.shading.studiolight_rotate_z=.4
    scene.display.shading.show_cavity=True
    scene.display.shading.cavity_type='BOTH'
    scene.display.shading.curvature_ridge_factor=.6
    scene.display.shading.curvature_valley_factor=.6
    scene.display.shading.show_shadows=True
ship_source.apply_review_preset=volume_preset
ship_render.render(evaluation,bounds,{
    'iso_front':((-1.0,1.6,1.45),(0,0,1)),
    'iso_rear':((1.25,-1.7,1.20),(0,0,1)),
    'side':((-1,0,0),(0,0,1)),
    'top':((0,0,1),(0,1,0))},1100,out)
