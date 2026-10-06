from pathlib import Path
import sys, json
import bpy, numpy as np

root=Path('D:/amind/git/agent-1');sys.path.insert(0,str(root/'art/tools/ship'))
import ship_render,ship_source
out=root/'results/nightshade-straight-fork/round-01'
base=ship_source.apply_review_preset
def preset(scene,size):
    base(scene,size);s=scene.display.shading
    s.light='STUDIO';s.studiolight_rotate_z=.25;s.color_type='MATERIAL'
    s.single_color=(.38,.42,.49);s.show_cavity=True;s.cavity_type='WORLD'
    s.cavity_ridge_factor=.25;s.cavity_valley_factor=.65
    s.show_shadows=False;s.show_specular_highlight=False
    scene.world.color=(.47,.48,.50)
ship_source.apply_review_preset=preset
evaluation,bounds=ship_render.prepare(root/'art/ships/nightshade/Nightshade.blend')
target=out/'material';target.mkdir(parents=True,exist_ok=True)
ship_render.render(evaluation,bounds,{
    'side_reference_angle':((-1,0,.16),(0,0,1)),
    'side_orthographic':((-1,0,0),(0,0,1)),
    'iso_front':((-1.5,1.3,2.3),(0,0,1)),
    'iso_rear':((1.25,-1.7,1.35),(0,0,1)),
    'top':((0,0,1),(0,1,0))},1400,target)
target=out/'fork-only';target.mkdir(parents=True,exist_ok=True)
evaluation.show_only(['Rear swept wing pair'])
framing=(np.array([-.36,-.99,-.02]),np.array([.36,.20,.16]))
ship_render.render(evaluation,framing,{'top':((0,0,1),(0,1,0)),
    'side':((-1,0,0),(0,0,1))},1000,target)

args=['--ship',str(root/'art/ships/nightshade/ship.json')]
ship_render.main(args+['--out',str(out/'ortho'),'--set','ortho','--size','650'])
ship_render.main(args+['--out',str(out/'scale'),'--set','scale'])
print('FORK_REVIEW_RENDERED',flush=True)
