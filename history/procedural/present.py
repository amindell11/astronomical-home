"""Presentation sheets for the current glyph set: lineup, size table, and far-readability study."""
import sys
from PIL import Image, ImageDraw, ImageFont
import glyphs7 as g7, sheetkit as sk, cutstroke as cs

tag = sys.argv[1] if len(sys.argv) > 1 else "final"
gl = g7.glyphs(); names = list(gl)
# lineup: all seven at 128 px with labels, plus 28 px row underneath
font = ImageFont.truetype("arial.ttf", 14); W = len(names)*150 + 20
im = Image.new("RGB", (W, 128 + 70), sk.BG); d = ImageDraw.Draw(im)
for i, n in enumerate(names):
    g = cs.render(gl[n][0], 128); im.paste(g, (10 + i*150, 10), g)
    s = cs.render(gl[n][0], 28); im.paste(s, (10 + i*150 + 50, 150), s)
    d.text((10 + i*150, 180), n, fill=(200, 200, 190), font=font)
im.save(sk.OUT / f"{tag}_lineup.png")
sk.table([("shared corner", gl)], f"{tag}: sizes 96 / 32 / 24 / 16 px", sizes=(96, 48, 32, 24, 16)).save(sk.OUT / f"{tag}_table.png")
three = [("Lasers", "Missiles"), ("Rippers", None), ("Railgun", "Concussion")]
study = sk.stack([sk.zoom_study(gl, three, "scales with the bars", zooms=((7.0, 1), (18.0, 3))), sk.zoom_study(gl, three, "with a 22 px on-screen floor", zooms=((7.0, 1), (18.0, 3)), px_floor=22)])
study.save(sk.OUT / f"{tag}_readability.png"); print(im.size, study.size)
