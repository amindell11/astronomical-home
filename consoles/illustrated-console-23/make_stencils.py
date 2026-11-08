"""Read shared Rajdhani-Medium.ttf; write display-stencil.png and projection-stencil.png beside this script."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parent
font=ImageFont.truetype(str(root.parent/'display-concepts-01/assets/fonts/Rajdhani-Medium.ttf'),100)
for word in ['DISPLAY','PROJECTION']:
    box=font.getbbox(word)
    image=Image.new('RGBA',(box[2]-box[0]+8,box[3]-box[1]+8),(0,0,0,0))
    ImageDraw.Draw(image).text((4-box[0],4-box[1]),word,font=font,fill=(255,255,255,255))
    image.save(root/(word.lower()+'-stencil.png'))
