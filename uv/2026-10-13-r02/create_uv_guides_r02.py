import json,colorsys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np
root=Path('D:/amind/git/agent-3/results/bulwark/uv/2026-10-13-r02')
a=json.loads((root/'uv-triangles.json').read_text())
size=2048
im=Image.new('RGB',(size,size),(28,32,40));d=ImageDraw.Draw(im)
for i,part in enumerate(a):
 color=tuple(round(c*255) for c in colorsys.hsv_to_rgb(i/len(a),.47,.75))
 uv=np.array(part['uv']);xy=np.column_stack((uv[:,0]*(size-1),(1-uv[:,1])*(size-1)))
 for t in part['triangles']:
  points=[tuple(xy[j]) for j in t]
  d.polygon(points,fill=color,outline=(225,229,235),width=1)
d.line((1024,0,1024,size),fill=(245,234,200),width=3)
im.save(root/'UV-guide.png')
checker=Image.new('RGB',(size,size));draw=ImageDraw.Draw(checker)
for y in range(64):
 for x in range(64):
  color=(214,219,222) if (x+y)%2==0 else (67,91,105)
  draw.rectangle((x*32,y*32,(x+1)*32-1,(y+1)*32-1),fill=color)
checker.save(root/'Checker.png')
print('UV guide and checker created')

