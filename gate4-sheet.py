"""Contact sheet: rows = stations, columns = open / in use / dark, for one locale and zoom. Labels on each cell."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
d, zoom, out = Path(sys.argv[1]), sys.argv[2], sys.argv[3]
title = sys.argv[4] if len(sys.argv) > 4 else ""
stations = [("repairstation", "Repair"), ("modstation", "Mod station"), ("armory", "Armory")]
looks = [("Open", "open"), ("InUse", "in use"), ("Dark", "dark")]
cw, ch = 640, 360
head = 44 if title else 0
sheet = Image.new("RGB", (cw * 3, ch * 3 + head), (12, 12, 20))
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arialbd.ttf", 18); tfont = ImageFont.truetype("arialbd.ttf", 22)
except OSError:
    font = tfont = ImageFont.load_default()
if title:
    draw.text((12, 10), title, fill=(235, 235, 235), font=tfont)
for r, (slug, label) in enumerate(stations):
    for c, (look, ltxt) in enumerate(looks):
        im = Image.open(d / f"{slug}-z{zoom}-{look}.png").convert("RGB").resize((cw, ch), Image.LANCZOS)
        sheet.paste(im, (c * cw, head + r * ch))
        draw.rectangle((c * cw, head + r * ch, c * cw + 210, head + r * ch + 28), fill=(0, 0, 0))
        draw.text((c * cw + 8, head + r * ch + 4), f"{label} / {ltxt}", fill=(240, 240, 240), font=font)
sheet.save(out)
print(out)
