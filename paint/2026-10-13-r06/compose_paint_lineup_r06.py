from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops
import json,hashlib
root=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r06')
folder=root/'lineup'
names=['Crimson','Vanguard','Nightshade','Valis','Bulwark-concept']
labels=['CRIMSON','VANGUARD','NIGHTSHADE','VALIS','BULWARK']
font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',30)
head=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',40)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
bg=(14,18,24); fg=(222,225,229); muted=(155,167,181)
for view in ['top','quarter']:
 board=Image.new('RGB',(2100,780),bg);draw=ImageDraw.Draw(board)
 draw.text((40,22),'BULWARK / PLUM PAINT MASK REVIEW',font=head,fill=fg)
 draw.text((40,78),'Current Unity ship materials • shared camera and lighting • equal display size',font=small,fill=muted)
 for i,(name,label) in enumerate(zip(names,labels)):
  image=Image.open(folder/(name+'-'+view+'.png')).convert('RGB')
  image=image.resize((400,400),Image.Resampling.LANCZOS)
  x=40+i*410; y=175
  board.paste(image,(x,y))
  box=draw.textbbox((0,0),label,font=font);draw.text((x+(400-box[2])/2,590),label,font=font,fill=(216,205,180) if i==4 else fg)
  sub='Separate paint masks' if i==4 else 'Current game asset'
  box=draw.textbbox((0,0),sub,font=small);draw.text((x+(400-box[2])/2,635),sub,font=small,fill=muted)
 draw.line((40,695,2060,695),fill=(50,59,72),width=2)
 draw.text((40,716),'Locked r08 geometry / approved r02 UVs / separate recolourable paint masks. Native shader preview.',font=small,fill=muted)
 board.save(folder/('Bulwark-fleet-'+view+'.png'))
 board.save(folder/('Bulwark-fleet-'+view+'.webp'),lossless=True)
scale=Image.new('RGB',(1180,310),bg);d=ImageDraw.Draw(scale)
d.text((28,18),'FLEET COLORS / 96 px per ship',font=font,fill=fg)
d.text((28,62),'Current game materials; Bulwark uses the revised plum paint masks.',font=small,fill=muted)
for i,name in enumerate(names):
 image=Image.open(folder/(name+'-top.png')).convert('RGB')
 bounds=ImageChops.difference(image,Image.new('RGB',image.size,image.getpixel((0,0)))).getbbox()
 image=image.crop(bounds)
 ratio=96/max(image.size);image=image.resize((round(image.width*ratio),round(image.height*ratio)),Image.Resampling.LANCZOS)
 scale.paste(image,(27+i*232+(210-image.width)//2,121+(105-image.height)//2))
 label=labels[i];box=d.textbbox((0,0),label,font=small);d.text((27+i*232+(210-box[2])//2,260),label,font=small,fill=fg)
scale.save(folder/'Bulwark-fleet-96px.png');scale.save(folder/'Bulwark-fleet-96px.webp',lossless=True)
report=json.loads((folder/'capture-report.json').read_text())
report['review_roster']=['Crimson','Vanguard','Nightshade','Valis','Bulwark']
report['excluded_from_review']='Cargo asset, per owner direction.'
report['composition']='Native PNG captures resized and arranged without recoloring or generated edits.'
report['source_unchanged']=True
report['source_sha256']=hashlib.sha256(Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend').read_bytes()).hexdigest()
(folder/'lineup-review-report.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'views':['Bulwark-fleet-top.png','Bulwark-fleet-quarter.png','Bulwark-fleet-96px.png'],'source_sha256':report['source_sha256']}))

