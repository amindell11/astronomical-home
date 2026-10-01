from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
p=Path('D:/amind/git/agent-2/results/valis-cleanup')
s=Image.new('RGB',(1400,1160),(59,65,77));d=ImageDraw.Draw(s);f=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',25)
for row,view in enumerate(['front-quarter','rear-quarter']):
 for col,state in enumerate(['before','after']):
  im=Image.open(p/(state+'-'+view+'.png')).convert('RGB');im.thumbnail((700,550));s.paste(im,(col*700,40+row*580));d.text((col*700+20,row*580+8),state.upper()+' / '+view.replace('-',' '),font=f,fill='white')
s.save(p/'cleanup-comparison.png')
