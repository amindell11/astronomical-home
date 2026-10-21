import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
out=Path('D:/amind/git/agent-8/scratch/bulwark-glass')
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
def sheet(left,right,size=512):
    result=Image.new('RGB',(size*2,size+48),(17,20,25))
    draw=ImageDraw.Draw(result)
    draw.text((18,10),'Before: fixed painted highlights',font=font,fill='white')
    draw.text((size+18,10),'After: shared moving reflections',font=font,fill='white')
    for x,path in ((0,left),(size,right)):
        result.paste(Image.open(path).convert('RGB').resize((size,size),Image.Resampling.LANCZOS),(x,48))
    return result
sheet(out/'before-ship.png',out/'after-ship.png',640).save(out/'comparison.png')
sheet(out/'before/009.png',out/'after/009.png',640).save(out/'bank-comparison.png')
frames=[sheet(out/'before'/f'{i:03}.png',out/'after'/f'{i:03}.png') for i in range(36)]
frames[0].save(out/'motion.gif',save_all=True,append_images=frames[1:],duration=90,loop=0,optimize=False)
before=np.array(Image.open(out/'before-ship.png')).astype(int)
after=np.array(Image.open(out/'after-ship.png')).astype(int)
changed=np.max(np.abs(before-after),axis=2)>1
y,x=np.where(changed)
report={'changed_pixel_count':int(changed.sum()),'changed_pixel_bounds_xy':[int(x.min()),int(y.min()),int(x.max()),int(y.max())]}
(out/'image-diff.json').write_text(json.dumps(report,indent=2))
print(report)
