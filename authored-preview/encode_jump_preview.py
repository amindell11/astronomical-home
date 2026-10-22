from pathlib import Path
import sys
from PIL import Image

directory = Path(sys.argv[1])
paths = sorted(directory.glob('f_*.png'))[::2]
frames = []
for path in paths:
    with Image.open(path) as image:
        frames.append(image.convert('RGB').resize((480, 270), Image.Resampling.LANCZOS)
                      .quantize(colors=256, method=Image.Quantize.MAXCOVERAGE))
output = directory.with_suffix('.gif')
frames[0].save(output, save_all=True, append_images=frames[1:], duration=40,
               loop=0, optimize=True)
duration = 0
with Image.open(output) as preview:
    for index in range(preview.n_frames):
        preview.seek(index)
        duration += preview.info['duration']
        if index == 43:
            preview.convert('RGB').save(directory / 'gif-decoded-cruise.png')
    encoded_frames = preview.n_frames
    assert encoded_frames <= len(frames), (encoded_frames, len(frames))
assert duration == len(frames) * 40, duration
print(f'{output}: {encoded_frames} frames (holds combined), {duration} ms, {output.stat().st_size} bytes')
