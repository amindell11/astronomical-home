from pathlib import Path
from PIL import Image
import numpy as np,json
s=Path(__file__).parent;g=s/'generation/layers';old=s/'previous-layers';rects=json.loads((s/'armor-uv-layout.json').read_text());changed=np.zeros((2048,2048),bool)
for r in rects:
 x,y,w,h=r['rect']
 for off in [0,1024]:changed[2048-y-h:2048-y,x+off:x+off+w]=True
checks={}
for n in ['Shadow','Light','Ink']:
 new=np.array(Image.open(g/f'{n}.png')).astype(np.int16);prior=np.array(Image.open(old/f'{n}.png')).astype(np.int16);baseline=np.array(Image.open(old/'baseline'/f'{n}.png')).astype(np.int16)
 if n=='Ink':new[~changed]=prior[~changed]
 else:new=np.clip(new+np.where(changed,0,prior-baseline),0,255)
 Image.fromarray(new.astype(np.uint8)).save(g/f'{n}.png')
 checks[n]={'edited_texels':int(np.count_nonzero(prior-baseline)),'changes':int(np.count_nonzero(new-prior))}
(s/'retained-edits.json').write_text(json.dumps(checks,indent=2))
