from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import subprocess, imageio_ffmpeg
root=Path('scratch/hull-lighting')
out=root/'publish'
out.mkdir(exist_ok=True)
frames=out/'frames'
frames.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
for i,p in enumerate(sorted((root/'motion').glob('*.png'))):
 im=Image.open(p).convert('RGB'); draw=ImageDraw.Draw(im)
 draw.text((28,18),'Current authored shading',font=font,fill='white')
 draw.text((668,18),'Unity-driven painted shading',font=font,fill='white')
 draw.text((28,52),['Ship bank / turn; fixed key light','Light sweep; stationary ship','Camera orbit; stationary ship and light'][i//40],font=small,fill='#b8d5d2')
 draw.line((640,0,640,800),fill='#3d4448')
 draw.text((28,620),'Approx. 100 px ship height',font=small,fill='#b8d5d2')
 im.save(frames/p.name)
for idx,name in [(0,'front'),(8,'bank'),(48,'grazing-light')]:
 Image.open(frames/f'{idx:03}.png').save(out/f'{name}.png')
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
subprocess.run([ffmpeg,'-y','-v','error','-framerate','12','-i',str(frames/'%03d.png'),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'hull-lighting.mp4')],check=True)
subprocess.run([ffmpeg,'-y','-v','error','-i',str(out/'hull-lighting.mp4'),'-vf','fps=10,scale=960:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse',str(out/'hull-lighting.gif')],check=True)
print('Published local comparison:',out.resolve())
