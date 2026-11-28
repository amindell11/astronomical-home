exec(open('sketch.py').read().split("font=ImageFont")[0])
from PIL import ImageFont
B=V['B  Soft-edged bands']; D=V['D  Stubby teardrop']
def mix(t): return {k:(B[k]*(1-t)+D[k]*t if isinstance(B[k],float) else round(B[k]*(1-t)+D[k]*t)) for k in B}
V2={'BD1  leans B':mix(.33),'BD2  middle':mix(.5),'BD3  leans D':mix(.67)}
font=ImageFont.truetype('/mnt/project-files/menu-rework/source/a/Rajdhani-Medium.ttf',26)
small=ImageFont.truetype('/mnt/project-files/menu-rework/source/a/Rajdhani-Medium.ttf',18)
rowH=H+70; COLS2={'Shoal':(254,134,26),'Bastion':(43,210,247),'Veil':(168,246,59),'Player':(126,194,236)}
sheet=Image.new('RGB',(1500,rowH*len(V2)+60),tuple((BG*255).astype(int))); d=ImageDraw.Draw(sheet)
d.text((20,15),'B/D blends: 3 stepped frames, 50% throttle, pilot flicker, then the four flame colours',fill=(200,200,210),font=small)
def comp(a): a=a+(1-np.clip(a.sum(-1,keepdims=True)*3,0,1))*BG; return Image.fromarray((np.clip(a,0,1)*255).astype(np.uint8))
for ri,(n,v) in enumerate(V2.items()):
    y0=60+ri*rowH; d.text((20,y0),n,fill=(230,230,235),font=font); x=20
    for f,thr in ((0,1),(1,1),(2,1),(0,.55),(3,.18)):
        sheet.paste(comp(render(v,f,(254,134,26),thr)),(x,y0+40)); x+=W+4
    x+=40
    for cn,col in COLS2.items():
        sheet.paste(comp(render(v,4,col)),(x,y0+40)); x+=W+4
sheet.save('/mnt/project-files/engine-look/sketch-r2-blend.png')
big=comp(render(V2['BD2  middle'],1,(255,255,255)))  # unused
import json; json.dump(V2,open('/mnt/project-files/engine-look/source/blend-params.json','w'),indent=1)
# hi-res single for imagegen ref: Shoal, 3 frames
s=Image.new('RGB',(W*3+40,H+20),tuple((BG*255).astype(int)))
for f in range(3): s.paste(comp(render(V2['BD2  middle'],f,(254,134,26))),(10+f*(W+10),10))
s.resize((s.width*4,s.height*4),Image.LANCZOS).save('/mnt/project-files/engine-look/sketch-r2-pick-ref.png')
