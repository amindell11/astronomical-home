import json
from pathlib import Path
from PIL import Image
import numpy as np
root=Path(__file__).parent
report={}
for locale in ['bright','copper']:
    scene=np.array(Image.open(root/f'canopy-owner-{locale}-scene.png').convert('RGB'),dtype=np.int16)
    for mode in ['on','off']:
        hud=np.array(Image.open(root/f'canopy-owner-{locale}-{mode}.png').convert('RGB'),dtype=np.int16)
        delta=hud-scene
        report[f'{locale}-{mode}']={'minimum_channel_delta':int(delta.min()),'channels_darker':int((delta<0).sum()),'channels_darker_by_more_than_one':int((delta < -1).sum()),'pixels_brighter':int(np.any(delta>1,axis=2).sum()),'pixels':int(scene.shape[0]*scene.shape[1])}
(root/'owner-pixel-check.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))

