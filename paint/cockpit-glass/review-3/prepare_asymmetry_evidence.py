import json
import subprocess
from pathlib import Path

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 22)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 18)


def frame(index, comparison):
    size = 512 if comparison else 640
    width = size * (2 if comparison else 1)
    result = Image.new('RGB', (width, size + 72), (17, 20, 25))
    draw = ImageDraw.Draw(result)
    entries = [('asym-before', 'Previous', 0), ('asym-after', 'Dominant reflection', size)] if comparison else [('asym-after', 'Bulwark cockpit', 0)]
    for folder, label, x in entries:
        draw.text((x + 18, 6), label, font=font, fill='white')
        axis = 'Roll: -55 to +55 degrees' if index < 80 else 'Pitch: -35 to +35 degrees'
        draw.text((x + 18, 38), axis, font=small, fill=(185, 198, 209))
        source = Image.open(root / folder / f'{index:03}.png').convert('RGB')
        result.paste(source.resize((size, size), Image.Resampling.LANCZOS), (x, 72))
    return result


def movie(comparison):
    name = 'asymmetry-comparison' if comparison else 'bulwark-slow-preview'
    frames = [frame(i, comparison) for i in range(160)]
    durations = [500 if i in (0, 20, 60, 80, 100, 140) else 100 for i in range(160)]
    frames[0].save(root / (name + '.gif'), save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=False)
    width, height = frames[0].size
    command = [imageio_ffmpeg.get_ffmpeg_exe(), '-hide_banner', '-loglevel', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{width}x{height}', '-r', '10', '-i', '-', '-an', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(root / (name + '.mp4'))]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    for image, duration in zip(frames, durations):
        for _ in range(duration // 100):
            process.stdin.write(image.tobytes())
    process.stdin.close()
    assert process.wait() == 0
    return {'frames': len(frames), 'duration_seconds': sum(durations) / 1000, 'gif': name + '.gif', 'mp4': name + '.mp4'}


report = {'movies': [movie(False), movie(True)]}
frame(0, True).save(root / 'asymmetry-neutral.png')
frame(20, True).save(root / 'asymmetry-bank.png')
before = np.asarray(Image.open(root / 'asym-before-reflections-off.png'))
after = np.asarray(Image.open(root / 'asym-after-reflections-off.png'))
report['fixed_glass_identical'] = bool(np.array_equal(before, after))
assert report['fixed_glass_identical']
before = np.asarray(Image.open(root / 'asym-before-ship.png')).astype(int)
after = np.asarray(Image.open(root / 'asym-after-ship.png')).astype(int)
y, x = np.where(np.max(np.abs(before - after), axis=2) > 1)
report['changed_whole_ship_pixels'] = len(x)
report['changed_pixel_bounds_xy'] = [int(x.min()), int(y.min()), int(x.max()), int(y.max())]
(root / 'asymmetry-proof.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
