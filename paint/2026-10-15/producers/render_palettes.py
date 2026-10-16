import bpy,json,sys
from pathlib import Path
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_render,ship_contract
source=Path('D:/amind/git/agent-2/art/ships/vanguard/Vanguard.blend')
source_hash=ship_contract.sha256_file(source)
palettes=json.loads((source.parent/'palettes.json').read_text())['palettes']
out=Path('D:/amind/git/agent-2/results/vanguard-redo/paint/concept')
views={'top':((0,0,1),(0,1,0)),'three-quarter':((.65,-1,1.4),(0,0,1))}
rendered={};scales=[]
for name,regions in palettes.items():
    evaluation,framing=ship_render.prepare(source)
    for region,color in regions.items():
        if bpy.data.materials.get(region) is None:raise ValueError('Missing palette material: '+region)
        bpy.data.materials[region].diffuse_color=color
    rendered[name]=ship_render.render(evaluation,framing,views,1024,out,name+'_')
    row=[]
    for size in (32,48,64,96):
        image=ship_render.render(evaluation,framing,{'top':views['top']},size,out,name+f'_scale{size}_')['top']
        tile=ship_render.load_pixels(image)
        row.append(tile.repeat(192//size,axis=0).repeat(192//size,axis=1))
    scales.append(np.concatenate(row,axis=1))
ship_render.save_pixels(out/'palettes-flat.png',ship_render.grid([ship_render.load_pixels(rendered[n][v]) for v in views for n in palettes],2))
ship_render.save_pixels(out/'palettes-scale.png',np.concatenate(list(reversed(scales)),axis=0))
assert ship_contract.sha256_file(source)==source_hash,'Review render changed source'
(out/'palette-render.json').write_text(json.dumps({'source_sha256':source_hash,'palettes':list(palettes),'views':list(views),'source_unchanged':True,'mask_generation':False},indent=2)+'\n')
