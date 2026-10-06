from pathlib import Path
import sys,bpy,numpy as np
root=Path('D:/amind/git/agent-1');sys.path.insert(0,str(root/'art/tools/ship'))
import ship_render,ship_source
phase=sys.argv[sys.argv.index('--')+1]
out=root/'results/nightshade-rounded-canopy'/phase
base=ship_source.apply_review_preset
def preset(scene,size):
    base(scene,size);s=scene.display.shading
    s.light='STUDIO';s.studiolight_rotate_z=.25;s.color_type='MATERIAL'
    s.show_cavity=True;s.cavity_type='WORLD';s.cavity_ridge_factor=.25;s.cavity_valley_factor=.65
    s.show_shadows=False;s.show_specular_highlight=True;scene.world.color=(.47,.48,.50)
ship_source.apply_review_preset=preset
standard_prepare=ship_render.prepare
def owner_review_prepare(source):
    owner_scene=ship_source.open_source(source)
    owner_visible={o.name for o in ship_source.ship_objects(owner_scene)
                   if not o.hide_get(view_layer=owner_scene.view_layers[0]) and not o.hide_render}
    evaluation,bounds=standard_prepare(source)
    visible=[n for n,o in evaluation.copies.items() if not o.hide_render and n in owner_visible]
    assert 'Upper small swept fins.001' in evaluation.copies
    assert 'Upper small swept fins.001' in owner_visible
    evaluation.show_only(visible+['Upper small swept fins.001'])
    return evaluation,bounds
ship_render.prepare=owner_review_prepare
evaluation,bounds=ship_render.prepare(root/'art/ships/nightshade/Nightshade.blend')
target=out/'material';target.mkdir(parents=True,exist_ok=True)
ship_render.render(evaluation,bounds,{'side':((-1,0,0),(0,0,1)),
    'iso_front':((-1.5,1.3,2.3),(0,0,1)),
    'underside':((-1.5,1.3,-2.3),(0,0,-1))},1300,target)
target=out/'cockpit';target.mkdir(parents=True,exist_ok=True)
framing=(np.array([-.24,.34,-.16]),np.array([.24,.86,.15]))
ship_render.render(evaluation,framing,{'iso':((-1.5,1.3,1.6),(0,0,1)),
    'side':((-1,0,0),(0,0,1)),'top':((0,0,1),(0,1,0))},1200,target)
if phase.startswith('round-'):
    visible=[n for n,o in evaluation.copies.items() if not o.hide_render]
    evaluation.show_only(visible+['Upper canopy frame','Upper canopy inner seal','Lower canopy metal bezel'])
    ship_render.render(evaluation,framing,{'rim_fit_iso':((-1.5,1.3,1.6),(0,0,1)),
        'rim_fit_side':((-1,0,0),(0,0,1))},1200,target)
    evaluation.show_only([n for n in visible if 'canopy glazing' not in n])
    ship_render.render(evaluation,framing,{'seat_without_glass':((-1.5,1.3,1.6),(0,0,1))},1200,target)
    args=['--ship',str(root/'art/ships/nightshade/ship.json')]
    ship_render.main(args+['--out',str(out/'ortho'),'--set','ortho','--size','650'])
    ship_render.main(args+['--out',str(out/'scale'),'--set','scale'])
print('FRAME_REVIEW_RENDERED',phase,flush=True)

