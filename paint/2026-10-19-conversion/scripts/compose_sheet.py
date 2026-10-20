"""Lay captures out on one sheet with a caption under each and a title line.

Usage: python -I compose_sheet.py <out.png> <columns> "<title>" label=path [label=path ...]
Cells keep each image's own size (the largest cell sets the grid); labels may use | for a second line.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

out, columns, title = Path(sys.argv[1]), int(sys.argv[2]), sys.argv[3]
items = [(a.split("=", 1)[0], Path(a.split("=", 1)[1])) for a in sys.argv[4:]]
images = [(label, Image.open(path).convert("RGB")) for label, path in items]
cell_w = max(im.width for _, im in images)
cell_h = max(im.height for _, im in images)
size = max(16, cell_h // 28)
try:
    font = ImageFont.truetype("segoeui.ttf", size)
    title_font = ImageFont.truetype("segoeui.ttf", int(size * 1.4))
except OSError:
    font = title_font = ImageFont.load_default()
pad = size
caption_h = int(size * 2.8)
rows = (len(images) + columns - 1) // columns
title_h = int(size * 2.4) if title else 0
sheet = Image.new("RGB", (columns * (cell_w + pad) + pad, title_h + rows * (cell_h + caption_h + pad) + pad), (24, 26, 30))
draw = ImageDraw.Draw(sheet)
if title:
    draw.text((pad, pad * 0.6), title, fill=(235, 235, 240), font=title_font)
for index, (label, im) in enumerate(images):
    col, row = index % columns, index // columns
    x = pad + col * (cell_w + pad) + (cell_w - im.width) // 2
    y = title_h + pad + row * (cell_h + caption_h + pad)
    sheet.paste(im, (x, y))
    for line_index, line in enumerate(label.split("|")):
        draw.text((pad + col * (cell_w + pad), y + cell_h + size * 0.4 + line_index * size * 1.2), line, fill=(225, 225, 230), font=font)
sheet.save(out)
print("sheet", out.name, sheet.size)
