from pathlib import Path
import numpy as np
from PIL import Image
import json
root=Path.cwd(); out=root/'scratch/canopy-reflections'
record={}
for ship in ('Crimson','Nightshade','Valis'):
    p=np.load(out/(ship+'-projection.npz')); coverage=p['coverage']
    if ship=='Crimson':
        weights=np.load(out/(ship+'-Color.npy'))
    else:
        weights=np.stack([np.load(out/(ship+'-'+name+'.npy'))[...,0] for name in ('Shadow','Light','Ink')],axis=-1)
    rgba=np.concatenate((weights,coverage[...,None]),axis=-1)
    rgba[~coverage]=0
    pixels=np.concatenate((rgba[0],rgba[1]),axis=1)[::-1]
    for dest in (root/'art/ships'/ship.lower()/'canopy',root/'src/Asteroids3D/Assets/Visuals/Ships'/ship/'Canopy'):
        dest.mkdir(parents=True,exist_ok=True)
        Image.fromarray(np.uint8(np.rint(np.clip(pixels,0,1)*255))).save(dest/(ship+' reflections.png'))
    low,high=p['low'],p['high']; center=(low+high)/2; scale=1/(high-low)
    record[ship]=[float(center[0]),float(center[1]),float(scale[0]),float(scale[1])]
(out/'reflection-bounds.json').write_text(json.dumps(record,indent=2))
print(record)
