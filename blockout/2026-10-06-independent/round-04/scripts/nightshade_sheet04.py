from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path('D:/amind/git/agent-1/results/nightshade-blockout/round-04')
sheet=Image.new('RGB',(1400,1070),(32,37,47))
d=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
d.text((25,18),'NIGHTSHADE / ROUGH SHAPE CORRECTION',font=font,fill='white')
d.text((25,57),'Lower tails • smaller, flatter cockpit • mechanical planes • shallow nose-down pitch',font=small,fill='#bac3d4')
side=Image.open(root/'volume/side.png').convert('RGB').crop((0,460,1100,630))
side=side.resize((1350,209),Image.Resampling.LANCZOS)
sheet.paste(side,(25,102))
iso=Image.open(root/'volume/iso_front.png').convert('RGB').crop((20,250,1080,885))
iso.thumbnail((1350,680),Image.Resampling.LANCZOS)
sheet.paste(iso,((1400-iso.width)//2,350))
d.text((25,1033),'Proportions under review. No detail pass.',font=small,fill='#bac3d4')
sheet.save(root/'nightshade-correction.jpg',quality=94)
