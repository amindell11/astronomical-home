import numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageFont
rng=np.random.default_rng
W,H=96,240   # frame px (supersampled x3 later)
SS=3
def profile(y,L,round_top,belly):
    # y in [0,1] from nozzle down; width 0..1
    t=np.clip(y/L,0,1)
    top=np.clip(y/round_top,0,1)**0.5
    tail=(1-t)**belly
    return top*tail*(y<=L)
def zig(n,seed,amp,count):
    r=rng(seed); ph=r.random(); k=count*r.uniform(.8,1.25); jit=r.uniform(.5,1,64)
    def f(y):
        u=y*k+ph; i=np.floor(u).astype(int)%64
        return -amp*jit[i]*(u-np.floor(u))**1.5
    return f
def band_mask(v,frame,scale,L,rt,belly,jag,count,seed,side_off=0):
    h,w=H*SS,W*SS
    yy,xx=np.mgrid[0:h,0:w]
    y=yy/h; x=(xx/w-0.5)*2
    zl=zig(0,seed*97+frame*13+1,jag,count); zr=zig(0,seed*97+frame*13+2,jag,count)
    p=profile(y,L,rt,belly)*scale
    return ((x>-(p+zl(y)*p)) & (x<(p+zr(y)*p)) & (p>0)).astype(float)
def render(v,frame,col,throttle=1.0):
    L=v['L']*throttle
    rim=band_mask(v,frame,v['w'],L,v['rt'],v['belly'],v['jag'],v['n'],1)
    mid=band_mask(v,frame,v['w']*v['mid'],L*v['midL'],v['rt']*0.8,v['belly'],v['jag'],v['n'],2)
    core=band_mask(v,frame,v['w']*v['core'],L*v['coreL'],v['rt']*0.6,v['belly'],v['jag']*0.7,v['n'],3)
    base=np.array(col,float)/255
    c_rim=base*v['rimk']; c_mid=np.minimum(1,base*1.05+0.12); c_core=np.minimum(1,0.75+base*0.3)
    img=np.zeros((H*SS,W*SS,4))
    for m,c,a in ((rim,c_rim,v['rima']),(mid,c_mid,v['mida']),(core,c_core,1.0)):
        img[...,:3]=img[...,:3]*(1-m[...,None])+c*m[...,None]; img[...,3]=np.maximum(img[...,3],m*a)
    im=Image.fromarray((img*255).astype(np.uint8),'RGBA').resize((W,H),Image.LANCZOS)
    if v['soft']>0:
        a=im.split()[3].filter(ImageFilter.GaussianBlur(v['soft'])); im.putalpha(a)
    # glow
    g=Image.fromarray((rim*255).astype(np.uint8)).resize((W,H)).filter(ImageFilter.GaussianBlur(v['glowr']))
    ga=np.array(g,float)/255*v['glow']
    out=np.zeros((H,W,3)); out+=ga[...,None]*base
    fa=np.array(im,float)/255
    out=out*(1-fa[...,3:])+fa[...,:3]*fa[...,3:]
    return np.clip(out,0,1)
V={
 'A  Ram, toned down':dict(L=.85,w=.5,rt=.10,belly=1.4,jag=.45,n=6,mid=.72,midL=.8,core=.42,coreL=.45,rimk=.55,rima=.85,mida=.9,soft=0,glow=.35,glowr=10),
 'B  Soft-edged bands':dict(L=.75,w=.55,rt=.14,belly=1.6,jag=.25,n=4,mid=.7,midL=.7,core=.38,coreL=.38,rimk=.5,rima=.7,mida=.85,soft=1.2,glow=.45,glowr=12),
 'C  Glow-led, small hard core':dict(L=.6,w=.42,rt=.12,belly=1.5,jag=.4,n=5,mid=.68,midL=.75,core=.4,coreL=.45,rimk=.6,rima=.8,mida=.9,soft=0,glow=.9,glowr=18),
 'D  Stubby teardrop':dict(L=.5,w=.62,rt=.18,belly=1.1,jag=.35,n=3,mid=.68,midL=.72,core=.4,coreL=.42,rimk=.5,rima=.85,mida=.9,soft=.6,glow=.5,glowr=12),
}
COLS={'Shoal':(254,134,26),'Bastion':(43,210,247),'Veil':(168,246,59),'Player':(126,194,236)}
BG=np.array((14,16,26))/255
def tile(arr): 
    return arr
font=ImageFont.truetype('/mnt/project-files/menu-rework/source/a/Rajdhani-Medium.ttf',26)
small=ImageFont.truetype('/mnt/project-files/menu-rework/source/a/Rajdhani-Medium.ttf',18)
rowH=H+70; sheetW=1500
sheet=Image.new('RGB',(sheetW,rowH*len(V)+60),tuple((BG*255).astype(int))); d=ImageDraw.Draw(sheet)
d.text((20,15),'Main exhaust sketches: 3 stepped frames, then throttle 50%, pilot flicker, and game scale (~1/5)',fill=(200,200,210),font=small)
for ri,(name,v) in enumerate(V.items()):
    y0=60+ri*rowH; d.text((20,y0),name,fill=(230,230,235),font=font)
    x=20
    for ci,(cn,col) in enumerate(COLS.items()):
        if ci>1: break
        for f,thr in ((0,1),(1,1),(2,1),(0,.55),(3,.18)):
            a=render(v,f,col,thr); a=a+(1-np.clip(a.sum(-1,keepdims=True)*3,0,1))*BG
            im=Image.fromarray((np.clip(a,0,1)*255).astype(np.uint8))
            sheet.paste(im,(x,y0+40)); x+=W+4
        a=render(v,1,col); a=a+(1-np.clip(a.sum(-1,keepdims=True)*3,0,1))*BG
        gs=Image.fromarray((np.clip(a,0,1)*255).astype(np.uint8)).resize((W//5,H//5),Image.LANCZOS)
        sheet.paste(gs,(x+10,y0+90)); x+=W//5+60
sheet.save('/mnt/project-files/engine-look/sketch-r1-main-exhaust.png')
