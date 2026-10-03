from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageChops
import imageio.v2 as imageio
import numpy as np
p=Path('D:/amind/git/agent-2/results/valis-geometry')
bg=(59,65,77)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
views=['top','bottom','front','rear','left','right','front-quarter','rear-quarter']
sheet=Image.new('RGB',(1600,900),bg); d=ImageDraw.Draw(sheet)
d.text((20,10),'VALIS / editable geometry review',font=font,fill='white')
for i,name in enumerate(views):
    im=Image.open(p/(name+'.png')).convert('RGB');im.thumbnail((400,350))
    x=(i%4)*400;y=55+(i//4)*420
    sheet.paste(im,(x,y+25));d.text((x+16,y),name.upper().replace('-',' '),font=small,fill='white')
d.text((20,872),'Actual Blender geometry renders / flat color blocks / no production textures or runtime integration',font=small,fill=(203,206,217))
sheet.save(p/'eight-views.png')
scale=Image.new('RGB',(880,310),bg);d=ImageDraw.Draw(scale)
d.text((20,10),'VALIS / approximate on-screen scale',font=font,fill='white')
for j,name in enumerate(['top','front-quarter']):
    im=Image.open(p/(name+'.png')).convert('RGB')
    box=ImageChops.difference(im,Image.new('RGB',im.size,im.getpixel((0,0)))).getbbox()
    im=im.crop(box)
    for i,size in enumerate([60,100,150]):
        thumb=im.copy();thumb.thumbnail((size,size))
        x=160+i*240;y=65+j*120
        scale.paste(thumb,(x-thumb.width//2,y))
        d.text((x-30,y+thumb.height+4),str(size)+' px',font=small,fill='white')
d.text((15,95),'TOP',font=small,fill='white');d.text((15,210),'QUARTER',font=small,fill='white')
scale.save(p/'game-scale.png')
frames=[Image.open(p/('turn-%03d.png'%i)).convert('RGB') for i in range(72)]
frames[0].save(p/'turntable.gif',save_all=True,append_images=frames[1:],duration=83,loop=0)
with imageio.get_writer(str(p/'turntable.mp4'),fps=12,codec='libx264',quality=8,macro_block_size=2) as writer:
    for im in frames:writer.append_data(np.asarray(im))
ref=Image.open('D:/amind/git/astronomical-home/art/ships/valis/concepts/valis-turnaround.png').convert('RGB').crop((12,651,373,847))
ref.thumbnail((900,360))
ref=ref.resize((820,445))
actual=Image.open(p/'left.png').convert('RGB').crop((225,330,1035,625));actual.thumbnail((820,330))
comparison=Image.new('RGB',(900,890),bg);d=ImageDraw.Draw(comparison)
d.text((25,10),'APPROVED CONCEPT / left side',font=font,fill='white');comparison.paste(ref,(40,40))
d.text((25,500),'ACTUAL MODEL / revised forward fuselage slope',font=font,fill='white');comparison.paste(actual,(40,555))
comparison.save(p/'side-comparison.png')
print('Created eight views, game-scale sheet, side comparison and 6-second turntable.')
