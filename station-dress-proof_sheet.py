"""fd7 beside in-engine, tile by tile: for each station kind, fd7's row above the in-engine row at the same tilt.

python proof_sheet.py <proof stills dir> <fd7 dir> <out dir>
fd7's factions sheets are a 5x3 grid of 600x460 tiles (neutral first); the in-engine tiles are 600x460 crops of
1280x720 flight-camera frames at size 25, the same true size.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

PROOF, FD7, OUT = (Path(a) for a in sys.argv[1:4])
OUT.mkdir(parents=True, exist_ok=True)
KINDS = [("Repair", "REPAIR"), ("Mod", "MOD STATION"), ("Armory", "ARMORY")]
DRESSES = [("shoal", "SHOAL"), ("veil", "VEIL"), ("bastion-red", "BASTION, RED HULL"), ("bastion-bone", "BASTION, BONE HULL")]
SHEETS = [("copper", "open", "COPPER TURQUOISE, OPEN", "fd7-factions-copper.png"),
          ("verdant", "open", "VERDANT GIANT, OPEN", "fd7-factions-verdant.png"),
          ("copper", "dark", "COPPER TURQUOISE, DARK", "fd7-factions-copper-dark.png")]
TW, TH, HEAD = 600, 460, 76
BG = (12, 13, 22)


def font(size):
    for name in ("arialbd.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def crop(path):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    return im.crop(((w - TW) // 2, (h - TH) // 2, (w + TW) // 2, (h + TH) // 2))


for locale, state, title, fd7_name in SHEETS:
    fd7 = Image.open(FD7 / fd7_name).convert("RGB")
    sheet = Image.new("RGB", (TW * len(DRESSES), HEAD + 2 * TH * len(KINDS)), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 10), f"{title}: FD7 SHEET ABOVE, IN ENGINE BELOW, SIZE 25", fill=(235, 235, 240), font=font(26))
    draw.text((14, 46), "fd7 drew each station from one Blender view over flight backdrops. Below is the game's renderer, each "
                        "station at the tilt the game gives its placed point, so the poses differ.",
              fill=(170, 172, 185), font=font(15))
    small = font(14)
    for r, (kind, kind_title) in enumerate(KINDS):
        for c, (dress, dress_title) in enumerate(DRESSES):
            x = c * TW
            y = HEAD + 2 * r * TH
            sheet.paste(fd7.crop(((c + 1) * TW, r * TH, (c + 2) * TW, (r + 1) * TH)), (x, y))
            sheet.paste(crop(PROOF / f"{locale}-{dress}-{kind}-{state}-z25.png"), (x, y + TH))
            for dy, tag in ((0, "FD7"), (TH, "IN ENGINE")):
                draw.rectangle((x, y + dy, x + TW, y + dy + 22), fill=(0, 0, 0))
                draw.text((x + 8, y + dy + 4), f"{tag}: {kind_title} / {dress_title}", fill=(240, 240, 240), font=small)
    out = OUT / f"fd7-vs-engine-{locale}-{state}.png"
    sheet.save(out, optimize=True)
    print("SHEET", out)
