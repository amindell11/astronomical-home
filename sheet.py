"""Contact sheet: rows = lit / dark, columns = repair dock / mod station / armory, for one locale and zoom."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

d, locale, zoom, out, title = Path(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5]
stations = [("Repair", "Repair dock"), ("Mod", "Mod station"), ("Armory", "Armory")]
looks = [("lit", "lit"), ("dark", "dark")]
cw, ch, head = 640, 360, 44
sheet = Image.new("RGB", (cw * 3, ch * 2 + head), (12, 12, 20))
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arialbd.ttf", 18)
    tfont = ImageFont.truetype("arialbd.ttf", 22)
except OSError:
    font = tfont = ImageFont.load_default()
draw.text((12, 10), title, fill=(235, 235, 235), font=tfont)
for r, (look, ltxt) in enumerate(looks):
    for c, (kind, label) in enumerate(stations):
        im = Image.open(d / f"{locale}-{kind}-z{zoom}-{look}.png").convert("RGB").resize((cw, ch), Image.LANCZOS)
        x, y = c * cw, head + r * ch
        sheet.paste(im, (x, y))
        draw.rectangle((x, y, x + 190, y + 28), fill=(0, 0, 0))
        draw.text((x + 8, y + 4), f"{label} / {ltxt}", fill=(240, 240, 240), font=font)
sheet.save(out)
print(out)
