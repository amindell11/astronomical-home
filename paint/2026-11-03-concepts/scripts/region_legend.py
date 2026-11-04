"""Scratch: one tile per region, that region highlighted on the idmap render. python region_legend.py IDMAP OUT"""
import json, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
idp, out = sys.argv[1:3]
im = np.asarray(Image.open(idp).convert("RGB")).astype(float)
reg = json.load(open(idp + ".json"))
enc = lambda v: v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055
names = list(reg); stack = np.stack([np.array([enc(c) for c in reg[n]]) * 255 for n in names])
d = np.linalg.norm(im[:, :, None] - stack[None, None], axis=3); lab = d.argmin(2); ok = d.min(2) < 30
font = ImageFont.truetype("arial.ttf", 22)
tiles = []
for i, n in enumerate(names):
    t = (im * 0.25).astype(np.uint8); m = (lab == i) & ok; t[m] = [255, 60, 200]
    ti = Image.fromarray(t).resize((300, 300)); ImageDraw.Draw(ti).text((5, 5), f"{n} ({m.sum()})", fill=(255, 255, 255), font=font); tiles.append(ti)
cols = 5; rows = (len(tiles) + cols - 1) // cols
sh = Image.new("RGB", (cols * 300, rows * 300))
for i, t in enumerate(tiles): sh.paste(t, ((i % cols) * 300, (i // cols) * 300))
sh.save(out)
