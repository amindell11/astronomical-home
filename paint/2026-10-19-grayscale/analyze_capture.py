from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,numpy as np
root=Path('D:/amind/git/agent-5/scratch/crimson-paint')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',30)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
report={}
for pose in ['top','bank','underside','side-light']:
    a=Image.open(root/'baseline'/f'{pose}-1024.png').convert('RGB')
    b=Image.open(root/'converted'/f'{pose}-1024.png').convert('RGB')
    av=np.asarray(a).astype(int);bv=np.asarray(b).astype(int)
    repeat=np.asarray(Image.open(root/'baseline-repeat'/f'{pose}-1024.png')).astype(int)
    assert np.array_equal(av,repeat),pose+' baseline not repeatable'
    ship=(abs(av-av[0,0]).max(axis=2)>4)|(abs(bv-bv[0,0]).max(axis=2)>4)
    delta=abs(av-bv)
    report[pose]={'mean_abs_rgb_255':round(float(delta[ship].mean()),3),'percent_ship_pixels_max_channel_delta_gt16':round(float((delta.max(axis=2)[ship]>16).mean()*100),3),'repeat_baseline_max_delta':0}
    canvas=Image.new('RGB',(2048,1110),(15,17,22));draw=ImageDraw.Draw(canvas)
    draw.text((30,15),'Current atlas  |  '+pose,font=font,fill='white')
    draw.text((1054,15),'Grayscale layers  |  '+pose,font=font,fill='white')
    draw.text((30,55),'Same geometry, camera, lighting and canopy; paint changes only',font=small,fill=(180,185,195))
    canvas.paste(a,(0,86));canvas.paste(b,(1024,86))
    canvas.save(root/f'{pose}-comparison.png')
report['top_glass_samples']={}
a=np.asarray(Image.open(root/'baseline/top-1024.png')).astype(int)
b=np.asarray(Image.open(root/'converted/top-1024.png')).astype(int)
for name,sl in [('upper',(slice(300,335),slice(500,525))),('lower',(slice(360,420),slice(500,525)))]:
    maximum=int(abs(a[sl]-b[sl]).max());assert maximum==0
    report['top_glass_samples'][name]={'max_rgb_delta':maximum}
canvas=Image.new('RGB',(1024,630),(15,17,22));draw=ImageDraw.Draw(canvas)
for i,pose in enumerate(['top','bank','underside','side-light']):
    x=i*256;draw.text((x+12,8),pose,font=small,fill='white')
    canvas.paste(Image.open(root/'baseline'/f'{pose}-256.png'),(x,44))
    canvas.paste(Image.open(root/'converted'/f'{pose}-256.png'),(x,355))
draw.text((12,315),'Above: current atlas     Below: grayscale layers   (256 px captures)',font=small,fill='white')
canvas.save(root/'game-scale-comparison.png')
(root/'comparison-metrics.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
