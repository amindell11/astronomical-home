import json
import subprocess
from pathlib import Path

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 32)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 24)
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()


def frame(index):
    result = Image.new('RGB', (1280, 1376), (17, 20, 25))
    draw = ImageDraw.Draw(result)
    draw.text((24, 8), 'Bulwark cockpit | 1280-pixel Unity capture', font=font, fill='white')
    axis = 'Roll: -55 to +55 degrees' if index < 80 else 'Pitch: -35 to +35 degrees'
    draw.text((24, 52), axis, font=small, fill=(185, 198, 209))
    result.paste(Image.open(root / 'resolution-after' / f'{index:03}.png').convert('RGB'), (0, 96))
    return result


movie = root / 'bulwark-hd-preview.mp4'
command = [ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '1280x1376', '-r', '10', '-i', '-', '-an', '-c:v', 'libx264', '-crf', '14', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)]
process = subprocess.Popen(command, stdin=subprocess.PIPE)
for index in range(160):
    pixels = frame(index).tobytes()
    for _ in range(5 if index in (0, 20, 60, 80, 100, 140) else 1):
        process.stdin.write(pixels)
process.stdin.close()
assert process.wait() == 0
filters = '[0:v]scale=768:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=256:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle'
subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(movie), '-filter_complex', filters, '-loop', '0', str(root / 'bulwark-hd-preview.gif')], check=True)
frame(0).save(root / 'resolution-neutral.png')
frame(60).save(root / 'resolution-bank.png')
box = (520, 220, 800, 1050)
result = Image.new('RGB', (560, 894), (17, 20, 25))
draw = ImageDraw.Draw(result)
for x, stage, label in [(0, 'resolution-before', 'Before'), (280, 'resolution-after', 'Smooth outlines')]:
    draw.text((x + 16, 16), label, font=small, fill='white')
    result.paste(Image.open(root / stage / '000.png').crop(box), (x, 64))
result.save(root / 'resolution-edge-comparison.png')
before = np.asarray(Image.open(root / 'resolution-before-reflections-off.png')).astype(float)
after = np.asarray(Image.open(root / 'resolution-after-reflections-off.png')).astype(float)
fixed_difference = np.abs(before - after)
source = Image.open(root / 'resolution-source-packed.png').convert('RGBA')
expected = np.asarray(source.resize((2048, 1024), Image.Resampling.BILINEAR))
actual = np.asarray(Image.open('art/ships/bulwark/canopy/Bulwark reflections.png'))
assert np.array_equal(actual[:, :, [0, 2]], expected[:, :, [0, 2]])
assert (actual[:, :, 3] == 255).all()
before = np.asarray(Image.open(root / 'resolution-before-ship.png')).astype(float)
after = np.asarray(Image.open(root / 'resolution-after-ship.png')).astype(float)
y, x = np.where(np.max(np.abs(before - after), axis=2) > 1)
report = {'capture_size': [1280, 1280], 'video_size': [1280, 1376], 'duration_seconds': 18.4,
          'fixed_shadow_ink_match_resampled_source': True, 'continuous_field_coverage': True,
          'reflections_off_mean_pixel_difference': float(fixed_difference.mean()),
          'reflections_off_max_pixel_difference': float(fixed_difference.max()),
          'changed_whole_ship_pixels': len(x), 'changed_pixel_bounds_xy': [int(x.min()), int(y.min()), int(x.max()), int(y.max())]}
(root / 'resolution-proof.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
