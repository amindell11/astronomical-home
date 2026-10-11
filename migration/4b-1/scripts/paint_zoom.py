"""4b-1 paint zoom: crop the most side-specific paint region from the game capture and both renders.

python paint_zoom.py <game.png> <before.png> <after.png> <out.png>
Images are cropped to the ship and resized to 512 px (as paint_side_score.py). The window is the
128 px square where the after render differs most from its own mirror image (high-passed), i.e.
where the paint is least symmetric; the sheet shows it at 3x for game, before and after.
"""
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZE, WINDOW, ZOOM = 512, 128, 3


def ship(path):
    image = Image.open(path).convert("RGB")
    pixels = np.asarray(image, np.float64) / 255
    mask = np.abs(pixels - pixels[2, 2]).max(axis=2) > 0.08
    rows, cols = np.flatnonzero(mask.any(axis=1)), np.flatnonzero(mask.any(axis=0))
    return image.crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1)).resize((SIZE, SIZE), Image.BICUBIC)


game, before, after = (ship(p) for p in sys.argv[1:4])
lum = np.asarray(after.convert("L"), np.float64)
high = lum - np.asarray(after.convert("L").filter(ImageFilter.GaussianBlur(5)), np.float64)
asym = np.abs(high - high[:, ::-1])
best, corner = -1.0, (0, 0)
for y in range(0, SIZE - WINDOW, 8):
    for x in range(0, SIZE // 2 - WINDOW + 1, 8):
        score = asym[y:y + WINDOW, x:x + WINDOW].sum()
        if score > best:
            best, corner = score, (x, y)
x, y = corner
box = (x, y, x + WINDOW, y + WINDOW)
labels = ["today's game (pilot's left)", "source before", "source after"]
sheet = Image.new("RGB", (3 * WINDOW * ZOOM + 40, WINDOW * ZOOM + 50), (24, 24, 28))
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arial.ttf", 22)
except OSError:
    font = ImageFont.load_default()
for i, (image, label) in enumerate(zip((game, before, after), labels)):
    left = 10 + i * (WINDOW * ZOOM + 10)
    sheet.paste(image.crop(box).resize((WINDOW * ZOOM, WINDOW * ZOOM), Image.NEAREST), (left, 45))
    draw.text((left + 6, 12), label, fill=(235, 235, 235), font=font)
sheet.save(sys.argv[4])
overview = after.copy()
ImageDraw.Draw(overview).rectangle(box, outline=(255, 60, 60), width=3)
overview.save(sys.argv[4].replace(".png", "-where.png"))
print("window", box)
