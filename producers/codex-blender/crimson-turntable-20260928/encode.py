import subprocess,json
from pathlib import Path
import imageio_ffmpeg
from PIL import Image
work=Path('C:/Users/amind/.codex/artifact-archives/crimson-turntable-20260928')
out=Path('D:/amind/git/agent-7/art/ships/crimson/previews')
ff=imageio_ffmpeg.get_ffmpeg_exe()
frames=list((work/'frames').glob('frame-*.png'));assert len(frames)==180,len(frames)
font="C\\:/Windows/Fonts/segoeui.ttf"
labels=f"drawtext=fontfile='{font}':text='ORIGINAL AI MESH':fontcolor=white:fontsize=26:x=w/4-text_w/2:y=54,drawtext=fontfile='{font}':text='NEW HAND-DRAWN':fontcolor=white:fontsize=26:x=3*w/4-text_w/2:y=54"
def run(args):
 p=subprocess.run([ff,'-hide_banner','-loglevel','error','-y']+args,capture_output=True,text=True)
 assert p.returncode==0,p.stderr
mp4=out/'old-vs-new-turntable.mp4';gif=out/'old-vs-new-turntable.gif'
run(['-framerate','18','-i',str(work/'frames/frame-%04d.png'),'-vf',labels,'-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-movflags','+faststart',str(mp4)])
run(['-i',str(mp4),'-vf','fps=12,scale=960:-1:flags=lanczos,palettegen=max_colors=192:stats_mode=diff',str(work/'palette.png')])
run(['-i',str(mp4),'-i',str(work/'palette.png'),'-filter_complex','[0:v]fps=12,scale=960:-1:flags=lanczos[v];[v][1:v]paletteuse=dither=sierra2_4a','-loop','0',str(gif)])
run(['-i',str(mp4),'-frames:v','1',str(out/'old-vs-new-poster.png')])
im=Image.open(gif);assert im.n_frames>=115
print(json.dumps({'mp4':str(mp4),'gif':str(gif),'gif_frames':im.n_frames,'gif_bytes':gif.stat().st_size,'mp4_bytes':mp4.stat().st_size,'duration_seconds':10}))
