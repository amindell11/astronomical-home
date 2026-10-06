from pathlib import Path
import sys,bpy,numpy as np
root=Path('D:/amind/git/agent-1');sys.path.insert(0,str(root/'art/tools/ship'))
import ship_render,ship_source
phase=sys.argv[sys.argv.index('--')+1]
out=root/'results/nightshade-sharp-fins'/phase
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
    'underside':((-1.5,1.3,-2.3),(0,0,-1)),
    'top':((0,0,1),(0,1,0))},1400,target)
target=out/'fin-only';target.mkdir(parents=True,exist_ok=True)
evaluation.show_only(['Upper small swept fins','Upper fin armor joint'])
framing=(np.array([-.46,-.26,.04]),np.array([.46,.50,.21]))
ship_render.render(evaluation,framing,{'upper_top':((0,0,1),(0,1,0))},1100,target)
if phase.startswith('round-'):
    args=['--ship',str(root/'art/ships/nightshade/ship.json')]
    ship_render.main(args+['--out',str(out/'ortho'),'--set','ortho','--size','650'])
    ship_render.main(args+['--out',str(out/'scale'),'--set','scale'])
print('FIN_REVIEW_RENDERED',phase,flush=True)
