"""Join the three hologram-entrance clips into one labelled MP4 (each plays twice), plus one MP4 per clip."""
import glob
import sys

import imageio.v2 as iio
import numpy as np
from PIL import Image, ImageDraw, ImageFont

out = sys.argv[1]
font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 26)
CLIPS = [("trace", "1  TRACE THEN FILL"), ("grow", "2  GROW FROM THE LENS"), ("dissolve", "3  COALESCE")]


def labelled(path, text):
    im = Image.open(path).convert("RGB")
    im = im.resize((im.width // 8 * 8, im.height // 8 * 8))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 20 + int(d.textlength(text, font=font)), 44), fill=(10, 10, 14))
    d.text((10, 7), text, font=font, fill=(240, 240, 240))
    return np.asarray(im)


all_w = iio.get_writer(out + "/hangar-holo-entrances.mp4", fps=24, codec="libx264", quality=8, pixelformat="yuv420p")
for mode, text in CLIPS:
    frames = [labelled(p, text) for p in sorted(glob.glob(f"{out}/{mode}/f*.png"))]
    with iio.get_writer(f"{out}/holo-{mode}.mp4", fps=24, codec="libx264", quality=8, pixelformat="yuv420p") as w:
        for f in frames:
            w.append_data(f)
    for _ in range(2):
        for f in frames:
            all_w.append_data(f)
        for _ in range(12):   # half a second on the settled frame before the replay
            all_w.append_data(frames[-1])
    print(mode, len(frames))
all_w.close()
