"""Embed display thumbnails; canonical renders and texture assets remain untouched."""
import base64
import io
from pathlib import Path

import numpy as np
from PIL import Image

scratch=Path(__file__).parent
fragment=Path('C:/Users/amind/.codex/visualizations/2026/10/15/01a141ed-a58e-73b2-bc04-d5f3d5e6b1b0/vanguard-asymmetry.html')
paths={
 'OLD_IMAGE':scratch/'asymmetry-consult/vanguard-current-top.png',
 'NEW_IMAGE':scratch/'final-review/asymmetric-nose-top.png',
 'VALIS_IMAGE':scratch/'asymmetry-consult/valis-tuned-top.png',
}
text=fragment.read_text(encoding='utf-8')
for token,path in paths.items():
    picture=Image.open(path).convert('RGBA')
    pixels=np.array(picture)
    if token=='VALIS_IMAGE':
        mask=pixels[:,:,3]>20
    else:
        mask=np.max(np.abs(pixels[:,:,:3].astype(int)-pixels[0,0,:3]),axis=2)>8
    y,x=np.where(mask)
    bounds=(int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1))
    picture=picture.crop(bounds)
    picture.thumbnail((720,720),Image.Resampling.LANCZOS)
    encoded=io.BytesIO()
    picture.save(encoded,format='WEBP',quality=92,method=6)
    text=text.replace(token,'data:image/webp;base64,'+base64.b64encode(encoded.getvalue()).decode())
    print(token,bounds,picture.size,len(encoded.getvalue()))
assert len(text.encode('utf-8'))<1_000_000
assert 'OLD_IMAGE' not in text and 'NEW_IMAGE' not in text and 'VALIS_IMAGE' not in text
fragment.write_text(text,encoding='utf-8')
print(fragment.stat().st_size)
