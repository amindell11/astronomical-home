from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path('D:/amind/git/agent-1/results/nightshade-blockout/round-03')
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
bg=(32,37,47)

def panel(sheet,path,box,crop=None):
    im=Image.open(path).convert('RGB')
    if crop: im=im.crop(crop)
    im.thumbnail((box[2],box[3]),Image.Resampling.LANCZOS)
    sheet.paste(im,(box[0]+(box[2]-im.width)//2,box[1]+(box[3]-im.height)//2))

sheet=Image.new('RGB',(1500,1050),bg)
draw=ImageDraw.Draw(sheet)
draw.text((28,20),'NIGHTSHADE / INDEPENDENT BLOCKOUT',font=font,fill='white')
draw.text((28,58),'Side and isometric priority • proportions review • editable mirrored source',font=small,fill='#bac3d4')
panel(sheet,root/'volume/iso_front.png',(20,100,920,600),(20,270,1080,880))
panel(sheet,root/'volume/iso_rear.png',(960,100,520,600),(20,265,1080,875))
draw.text((32,708),'SIDE / late upward tail sweep',font=small,fill='white')
panel(sheet,root/'volume/side.png',(20,744,1460,235),(0,430,1100,685))
draw.text((28,1004),'Blockout only: flat regions identify major forms; panels, paint and integration await approval.',font=small,fill='#bac3d4')
sheet.save(root/'nightshade-review.jpg',quality=94)

sheet=Image.new('RGB',(1500,1140),bg)
draw=ImageDraw.Draw(sheet)
draw.text((28,20),'NIGHTSHADE / ORTHOGRAPHIC + GAME SCALE',font=font,fill='white')
panel(sheet,root/'ortho/top.png',(20,70,710,710))
panel(sheet,root/'ortho/front.png',(750,80,720,210),(0,390,1100,720))
panel(sheet,root/'ortho/back.png',(750,350,720,210),(0,390,1100,720))
draw.text((770,65),'FRONT (supporting view)',font=small,fill='white')
draw.text((770,330),'REAR',font=small,fill='white')
draw.text((30,800),'32 / 48 / 64 / 96 px — nearest-neighbour enlargement from ship_render',font=small,fill='white')
im=Image.open(root/'scale/scale.png').convert('RGB').resize((1440,288),Image.Resampling.NEAREST)
sheet.paste(im,(30,839))
sheet.save(root/'nightshade-ortho-scale.jpg',quality=94)
