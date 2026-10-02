from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np,json,base64,io
out=Path('D:/amind/git/agent-2/results/valis-crimson-retry/v02')
ref=Path('D:/amind/git/astronomical-home/results/valis-texturing-restart/style')
bg=(59,67,81)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24);small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
def cut_reference(path):
 im=Image.open(path).convert('RGBA');a=np.asarray(im).copy();color=a[0,0,:3];dist=np.max(abs(a[:,:,:3].astype(float)-color),axis=2);a[:,:,3]=np.clip((dist-2)*64,0,255);im=Image.fromarray(a);return im.crop(im.getbbox())
cr=cut_reference(ref/'crimson-top.png');cq=cut_reference(ref/'crimson-quarter.png')
va=Image.open(out/'actual-top.png').convert('RGBA').rotate(180);va=va.crop(va.getbbox())
vq=Image.open(out/'actual-quarter.png').convert('RGBA');vq=vq.crop(vq.getbbox())
board=Image.new('RGB',(1200,1060),bg);d=ImageDraw.Draw(board)
d.text((48,22),'CRIMSON / style reference',font=font,fill=(235,239,245));d.text((652,22),'VALIS / actual locked mesh',font=font,fill=(235,239,245))
for im,x in [(cr,300),(va,900)]:
 p=im.resize((round(im.width*530/im.height),530),Image.Resampling.LANCZOS);board.paste(p,(x-p.width//2,72),p)
d.text((48,630),'GAME SCALE / same nose-to-tail height',font=font,fill=(235,239,245))
for s,x in [(60,170),(100,550),(150,970)]:
 d.text((x-50,680),str(s)+' px',font=small,fill=(211,220,230))
 for im,dx,label in [(cr,-95,'Crimson'),(va,95,'Valis')]:
  p=im.resize((round(im.width*s/im.height),s),Image.Resampling.LANCZOS);board.paste(p,(x+dx-p.width//2,740+(150-s)//2),p);d.text((x+dx-32,916),label,font=small,fill=(223,229,239))
d.text((48,1006),'Blender painted-model previews. Geometry and base palette unchanged. Unity paint integration pending review.',font=small,fill=(211,220,230))
board.save(out/'comparison-top-and-game-scale.png')
qb=Image.new('RGB',(1600,1000),bg);qd=ImageDraw.Draw(qb)
for im,x,label in [(cq,400,'CRIMSON / reference'),(vq,1200,'VALIS / actual model')]:
 p=im.resize((round(im.width*720/max(im.width,im.height)),round(im.height*720/max(im.width,im.height))),Image.Resampling.LANCZOS);qb.paste(p,(x-p.width//2,120+(740-p.height)//2),p);qd.text((x-240,30),label,font=font,fill=(235,239,245))
qb.save(out/'comparison-quarter.png')
def dataurl(im):
 b=io.BytesIO();im.save(b,format='PNG',optimize=True);return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()
payload={'palettes':json.loads(Path('D:/amind/git/astronomical-home/results/valis-texturing-restart/base-model/palettes.json').read_text())['palettes'],'materials':json.loads((out/'material-ids.json').read_text()),'settings':json.loads((out/'settings.json').read_text()),'views':{}}
for view,reference in [('top',cr),('quarter',cq)]:
 imgs={}
 for mode in ['id','mask']:
  im=Image.open(out/f'tuner-{view}-{mode}.png').convert('RGBA')
  if view=='top':im=im.rotate(180)
  im=im.resize((512,512),Image.Resampling.LANCZOS if mode=='mask' else Image.Resampling.NEAREST)
  imgs[mode]=dataurl(im)
 reference.thumbnail((300,300),Image.Resampling.LANCZOS);imgs['reference']=dataurl(reference)
 payload['views'][view]=imgs
(out/'tuner-data.json').write_text(json.dumps(payload,separators=(',',':')))
print('TUNER_DATA_BYTES',len((out/'tuner-data.json').read_bytes()))


