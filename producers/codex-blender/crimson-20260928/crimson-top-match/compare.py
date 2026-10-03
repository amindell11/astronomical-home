from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
from scipy.ndimage import binary_erosion, binary_dilation, distance_transform_edt
import json

root=Path('D:/amind/git/agent-7')
out=root/'results/crimson-top-match'
ref=Image.open(root/'art/ships/crimson/mesh-study/top-match/reference/original-top.png').convert('RGBA')
model=Image.open(out/'top-alpha.png').convert('RGBA')
a=np.array(ref)[:,:,3]>127
b=np.array(model)[:,:,3]>127
ae=a & ~binary_erosion(a)
be=b & ~binary_erosion(b)
dist=distance_transform_edt(~ae)[be]
stats={'silhouette_intersection_over_union':float((a&b).sum()/(a|b).sum()),
       'mesh_outline_distance_pixels_mean':float(dist.mean()),
       'mesh_outline_distance_pixels_p95':float(np.percentile(dist,95)),
       'resolution':[1024,1024]}
(out/'projection-check.json').write_text(json.dumps(stats,indent=2))
bg=Image.new('RGBA',(1024,1024),(33,38,47,255))
left=Image.alpha_composite(bg,ref).convert('RGB')
right=Image.alpha_composite(bg,model).convert('RGB')
overlay=np.array(right)
overlay[binary_dilation(ae,iterations=1)]=(255,111,45)
overlay=Image.fromarray(overlay)
canvas=Image.new('RGB',(2048,1100),(22,26,34))
canvas.paste(left,(0,65));canvas.paste(overlay,(1024,65))
draw=ImageDraw.Draw(canvas)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',28)
draw.text((35,15),'ORIGINAL TOP REFERENCE',font=font,fill='white')
draw.text((1060,15),'REVISED MESH / orange = reference silhouette',font=font,fill='white')
canvas.save(out/'top-comparison.png')
print(json.dumps(stats))
