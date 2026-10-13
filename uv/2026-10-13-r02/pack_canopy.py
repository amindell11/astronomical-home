import json
import numpy as np
from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
from scipy.signal import fftconvolve
root=Path('D:/amind/git/agent-3/results/bulwark/uv')
other=json.loads((root/'2026-10-13-r01/uv-triangles.json').read_text())
candidate=json.loads((root/'2026-10-13-r02/canopy-candidate.json').read_text())
size=2048
im=Image.new('L',(size,size));draw=ImageDraw.Draw(im)
for part in other:
 if part['name']=='06 Cockpit dome':continue
 uv=np.array(part['uv'])*size
 for t in part['triangles']:draw.polygon([tuple(uv[j]) for j in t],fill=255)
occupied=np.array(im.filter(ImageFilter.MaxFilter(17)))>0
occupied[:20,:]=occupied[-20:,:]=True;occupied[:,:20]=occupied[:,-20:]=True
uv=np.array(candidate['uv']);tri=candidate['triangles']
for turn in (0,1):
 rotation=np.eye(2) if turn==0 else np.array([[0,-1],[1,0]])
 values=uv@rotation.T*size;lo=np.floor(values.min(axis=0))-2;hi=np.ceil(values.max(axis=0))+2
 w,h=(hi-lo).astype(int)+1
 footprint=Image.new('L',(int(w),int(h)));d=ImageDraw.Draw(footprint)
 for t in tri:d.polygon([tuple(values[j]-lo) for j in t],fill=1)
 mask=np.array(footprint,dtype=np.float32)
 collisions=fftconvolve(occupied.astype(np.float32),mask[::-1,::-1],mode='valid')
 available=np.argwhere(collisions<.1)
 if not len(available):print('No placement',turn,w,h);continue
 # Prefer an interior free region, leaving the atlas edges clear.
 y,x=min(available,key=lambda p:abs(p[1]+w/2-size*.5)+abs(p[0]+h/2-size*.5))
 translation=(np.array([x,y])-lo)/size
 result={'turn':turn,'translation':translation.tolist(),'margin_pixels':8,'other_charts_moved':False,'footprint_pixels':[int(w),int(h)]}
 (root/'2026-10-13-r02/canopy-placement.json').write_text(json.dumps(result,indent=2))
 print(result);break
else:raise RuntimeError('No full-density canopy placement found without moving other charts')
