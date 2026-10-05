from PIL import Image, ImageDraw
from pathlib import Path
out = Path('D:/amind/git/agent-2/results/nightshade-concept/concept/2026-10-05-r06')
out.mkdir(parents=True, exist_ok=True)
im = Image.new('RGB', (1500, 1000), '#d7d7d7')
d = ImageDraw.Draw(im)
d.text((30,25), 'TAIL SHAPE GUIDE ONLY - retain the original whole ship', fill='#222222')
d.line([(200,140),(1300,140)], fill='#707070', width=3)
d.text((510,112), 'existing outer wing span', fill='#444444')
d.line([(750,170),(750,940)], fill='#aaaabb', width=2)
d.polygon([(700,220),(800,220),(820,520),(770,650),(730,650),(680,520)], fill='#a6a8b5')
d.text((690,260), 'body', fill='#444444')
for side in (-1,1):
    finx = 750+side*205
    d.rectangle((finx-20,405,finx+20,440),fill='#b853c8')
d.rectangle((535,420,965,925), outline='#5e7d69', width=3)
d.text((548,930), 'rear pair stays inside this width', fill='#36543b')
def bezier(a,b,c,e,n=30):
    result=[]
    for j in range(n+1):
        t=j/n
        result.append(tuple((1-t)**3*a[k]+3*(1-t)**2*t*b[k]+3*(1-t)*t*t*c[k]+t**3*e[k] for k in (0,1)))
    return result
left = [(670,435),(662,490)]
left += bezier((662,490),(640,570),(555,612),(552,690))
left += bezier((552,690),(548,762),(625,835),(713,902))
left += [(688,834),(653,759),(612,706)]
left += bezier((612,706),(600,650),(682,590),(690,525))
left += [(698,435)]
for side in (-1,1):
    pts=left if side==-1 else [(1500-x,y) for x,y in left]
    d.polygon(pts,fill='#535b77',outline='#262b3c',width=4)
    crease=[(650,595),(623,630),(578,659)]
    if side==1: crease=[(1500-x,y) for x,y in crease]
    d.line(crease,fill='#8990a8',width=4)
im.save(out/'tail-envelope-guide.png')
