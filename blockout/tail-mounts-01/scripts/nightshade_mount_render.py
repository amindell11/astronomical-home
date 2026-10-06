from pathlib import Path
import sys
import bpy,numpy as np
root=Path('D:/amind/git/agent-1');sys.path.insert(0,str(root/'art/tools/ship'))
import ship_render,ship_source
out=root/'results/nightshade-tail-mounts/round-01'
base=ship_source.apply_review_preset
def preset(scene,size):
    base(scene,size);s=scene.display.shading
    s.light='STUDIO';s.studiolight_rotate_z=.25;s.color_type='MATERIAL'
    s.show_cavity=True;s.cavity_type='WORLD';s.cavity_ridge_factor=.25;s.cavity_valley_factor=.65
    s.show_shadows=False;s.show_specular_highlight=False;scene.world.color=(.47,.48,.50)
ship_source.apply_review_preset=preset
evaluation,bounds=ship_render.prepare(root/'art/ships/nightshade/Nightshade.blend')
target=out/'material';target.mkdir(parents=True,exist_ok=True)
ship_render.render(evaluation,bounds,{'side':((-1,0,.16),(0,0,1)),
    'iso_front':((-1.5,1.3,2.3),(0,0,1)),
    'iso_rear':((1.25,-1.7,1.35),(0,0,1)),
    'top':((0,0,1),(0,1,0))},1400,target)
target=out/'attachment';target.mkdir(parents=True,exist_ok=True)
evaluation.show_only(['Rebuilt pitched hull','Rear swept wing pair','Tail root mounting blocks'])
framing=(np.array([-.36,-.22,-.17]),np.array([.36,.30,.17]))
ship_render.render(evaluation,framing,{'closeup':((1.4,1.7,2.3),(0,0,1)),
    'top':((0,0,1),(0,1,0))},1400,target)
args=['--ship',str(root/'art/ships/nightshade/ship.json')]
ship_render.main(args+['--out',str(out/'ortho'),'--set','ortho','--size','650'])
ship_render.main(args+['--out',str(out/'scale'),'--set','scale'])
print('MOUNT_REVIEW_RENDERED',flush=True)
