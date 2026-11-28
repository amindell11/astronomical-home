import sys; sys.argv=['x']
exec(open('sketch.py').read().split("font=ImageFont")[0])
from PIL import Image,ImageDraw,ImageFont
font=ImageFont.truetype('/mnt/project-files/menu-rework/source/a/Rajdhani-Medium.ttf',22)
ram=Image.open('/home/user/astronomical-home/src/Asteroids3D/Assets/Visuals/Vfx/Keystones/BulwarkRam/BulwarkRam_Flares.png').convert('RGBA')
fw=ram.width//32; fh=ram.height//2
frames=[]
names=list(V)
for f in range(12):
    s=Image.new('RGB',(5*130+20,H+60),tuple((BG*255).astype(int))); d=ImageDraw.Draw(s)
    rf=ram.crop(((f*2+8)%32*fw,0,((f*2+8)%32+1)*fw,fh)); rf=rf.resize((int(fw*H/fh*0.9),int(H*0.9)))
    s.paste(rf,(20+(110-rf.width)//2,40),rf); d.text((20,8),'Ram (ref)',fill=(200,200,210),font=font)
    for i,n in enumerate(names):
        a=render(V[n],f,(254,134,26)); a=a+(1-np.clip(a.sum(-1,keepdims=True)*3,0,1))*BG
        s.paste(Image.fromarray((np.clip(a,0,1)*255).astype(np.uint8)),(150+i*130,40)); d.text((150+i*130+30,8),n[0],fill=(200,200,210),font=font)
    frames.append(s)
frames[0].save('/mnt/project-files/engine-look/sketch-r1-flicker.gif',save_all=True,append_images=frames[1:],duration=83,loop=0)
