from pathlib import Path
import bpy,sys,numpy as np,subprocess,tempfile,os
root=Path('D:/amind/git/agent-1');sys.path.insert(0,str(root/'art/tools/ship'))
import ship_render,ship_source
phase=sys.argv[sys.argv.index('--')+1];out=root/'results/nightshade-detail-01'/phase
base=ship_source.apply_review_preset
def preset(scene,size):
    base(scene,size);s=scene.display.shading
    s.light='STUDIO';s.studiolight_rotate_z=.25;s.color_type='MATERIAL'
    s.show_cavity=True;s.cavity_type='WORLD';s.cavity_ridge_factor=.25;s.cavity_valley_factor=.65
    s.show_shadows=False;s.show_specular_highlight=True;scene.world.color=(.47,.48,.50)
ship_source.apply_review_preset=preset
standard=ship_render.prepare
def visible_prepare(source):
    scene=ship_source.open_source(source)
    visible={o.name for o in ship_source.ship_objects(scene) if not o.hide_get(view_layer=scene.view_layers[0]) and not o.hide_render}
    evaluation,bounds=standard(source)
    names=[n for n,o in evaluation.copies.items() if not o.hide_render and n in visible]
    if 'Upper small swept fins.001' in visible and 'Upper small swept fins.001' not in names:names.append('Upper small swept fins.001')
    evaluation.show_only(names);return evaluation,bounds
ship_render.prepare=visible_prepare
source=root/'art/ships/nightshade/Nightshade.blend'
ev,bounds=ship_render.prepare(source)
views={'iso_front':((-1.5,1.3,2.3),(0,0,1)),'iso_rear':((1.5,-1.3,1.5),(0,0,1)),
       'underside':((-1.5,1.3,-2.3),(0,0,-1)),'top':((0,0,1),(0,1,0)),'side':((-1,0,0),(0,0,1))}
folder=out/'material';folder.mkdir(parents=True,exist_ok=True)
ship_render.render(ev,bounds,views,1500,folder)
folder=out/'closeups';folder.mkdir(parents=True,exist_ok=True)
for name,box,direction in [('cockpit',([-.24,.34,-.16],[.24,.86,.15]),(-1.5,1.3,1.6)),
    ('wing',([.22,-.25,-.10],[.66,.59,.14]),(.5,1,2.5)),
    ('tail',([-.34,-.83,.0],[.34,.12,.23]),(1.2,-1.3,2.5))]:
    ship_render.render(ev,tuple(np.array(p) for p in box),{name:(direction,(0,0,1))},1000,folder)
args=['--ship',str(root/'art/ships/nightshade/ship.json')]
for kind,size in [('sheet','400'),('turnaround','650'),('ortho','800'),('scale','512')]:
    ship_render.main(args+['--out',str(out/kind),'--set',kind,'--size',size])
fd,tmp=tempfile.mkstemp(prefix='nightshade-approved-',suffix='.blend');os.close(fd)
try:
    with open(tmp,'wb') as f:subprocess.run(['git','cat-file','--filters','124a71cf4c050b2232d6f19b815233980fa9fa97:art/ships/nightshade/Nightshade.blend'],cwd=root,stdout=f,check=True)
    ship_render.main(args+['--out',str(out/'compare'),'--set','compare','--before',tmp,'--size','750'])
finally:
    target=Path(tmp).resolve();assert target.parent==Path(tempfile.gettempdir()).resolve() and target.name.startswith('nightshade-approved-');target.unlink()
print('DETAIL_REVIEW_RENDERED',phase,flush=True)
