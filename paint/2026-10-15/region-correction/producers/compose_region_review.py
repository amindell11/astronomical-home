import sys,json,bpy
from pathlib import Path
import numpy as np
ROOT=Path('D:/amind/git/agent-2')
HERE=Path(__file__).parent
sys.path.insert(0,str(ROOT/'art/tools/ship'))
import ship_render
out=ROOT/'results/vanguard-redo/paint/region-correction'
ship_render.save_pixels(out/'old-versus-recovered.png',ship_render.grid([ship_render.load_pixels(p) for p in [HERE/'vivid-top.png',out/'vanguard_top.png',HERE/'vivid-hero.png',out/'vanguard_hero.png']],2))
tiles=[ship_render.load_pixels(p).repeat(4,axis=0).repeat(4,axis=1) for p in [HERE/'vivid-small.png',out/'vanguard_scale96_top.png',out/'mint_plum_scale96_top.png']]
ship_render.save_pixels(out/'old-and-recovered-scale.png',ship_render.grid(tiles,3))
palettes=json.loads((ROOT/'art/ships/vanguard/palettes.json').read_text())
for name,colors in palettes['palettes'].items():
    values=(.39,.46,.54) if name=='vanguard' else (.31,.47,.48)
    colors['HullInset']=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in values]+[1.]
(out/'proposed-palettes.json').write_text(json.dumps(palettes,indent=2)+'\n')
print('REGION_COMPARISON='+str(out/'old-versus-recovered.png'))
