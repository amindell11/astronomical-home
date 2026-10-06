from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
root=Path('D:/amind/git/agent-1/results/nightshade-clean-guide/guide')
data=json.loads((root/'sections.json').read_text())
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
for axis in (1,2):
    items=[(k,v) for k,v in data.items() if k.startswith(str(axis)+':')]
    w,h=600,(230 if axis==1 else 640)
    im=Image.new('RGB',(w*3,h*((len(items)+2)//3)),(245,245,245));d=ImageDraw.Draw(im)
    for i,(key,segs) in enumerate(items):
        x0=(i%3)*w;y0=(i//3)*h
        bounds=(-.9,.9,-.25,.25) if axis==1 else (-.9,.9,-1.05,.95)
        xmin,xmax,ymin,ymax=bounds
        def pixel(v):return (x0+35+(v[0]-xmin)/(xmax-xmin)*(w-60),y0+35+(ymax-v[1])/(ymax-ymin)*(h-55))
        for x in [-.8,-.6,-.4,-.2,0,.2,.4,.6,.8]:d.line([pixel((x,ymin)),pixel((x,ymax))],fill=(218,218,218))
        for y in ([-.2,-.1,0,.1,.2] if axis==1 else [-1,-.8,-.6,-.4,-.2,0,.2,.4,.6,.8]):d.line([pixel((xmin,y)),pixel((xmax,y))],fill=(218,218,218))
        for a,b in segs:d.line([pixel(a),pixel(b)],fill=(27,36,65),width=2)
        d.text((x0+12,y0+8),('Y' if axis==1 else 'Z')+' = '+key.split(':')[1],font=font,fill=(20,20,20))
    im.save(root/f'sections-{axis}.png')
