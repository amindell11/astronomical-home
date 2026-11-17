"""Contact sheets of the #1593 in-engine stills, laid out like fd7's: rows are station kinds, columns are dresses.

python dress_sheet.py <stills dir> <fd7 dir> <out dir>
Writes, per locale: one sheet per state and zoom (dress-<locale>-<state>-z<zoom>.png), and a comparison
(dress-<locale>-vs-fd7.png) with fd7's sheet above the open size-25 sheet.
At size 25 each tile is a 600x460 crop at true size (720 px tall screen), as on the fd7 sheets.
At size 15 each tile is the whole 1280x720 frame scaled to 640x360.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

STILLS, FD7, OUT = (Path(a) for a in sys.argv[1:4])
OUT.mkdir(parents=True, exist_ok=True)
KINDS = [("Repair", "REPAIR"), ("Mod", "MOD STATION"), ("Armory", "ARMORY")]
DRESSES = [("shoal", "SHOAL"), ("veil", "VEIL"), ("bastion-red", "BASTION, RED HULL"), ("bastion-bone", "BASTION, BONE HULL")]
STATES = [("open", "OPEN"), ("in-use", "IN USE"), ("dark", "DARK")]
LOCALES = [("copper", "COPPER TURQUOISE", "fd7-factions-copper.png"), ("verdant", "VERDANT GIANT", "fd7-factions-verdant.png")]
BG = (12, 13, 22)


def font(size):
    for name in ("arialbd.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def tile(path, zoom):
    im = Image.open(path).convert("RGB")
    if zoom == 25:
        w, h = im.size
        return im.crop(((w - 600) // 2, (h - 460) // 2, (w + 600) // 2, (h + 460) // 2))
    return im.resize((640, 360), Image.LANCZOS)


def sheet(locale, title, state, state_title, zoom):
    tiles = [[tile(STILLS / f"{locale}-{dress}-{kind}-{state}-z{zoom}.png", zoom) for dress, _ in DRESSES]
             for kind, _ in KINDS]
    tw, th = tiles[0][0].size
    head = 70
    im = Image.new("RGB", (tw * len(DRESSES), head + th * len(KINDS)), BG)
    draw = ImageDraw.Draw(im)
    draw.text((14, 10), f"IN ENGINE, {title}: {state_title}, SIZE {zoom}", fill=(235, 235, 240), font=font(26))
    draw.text((14, 44), "The game's renderer and post. Bastion's two palettes are both shown by redressing; "
                        "in play each station rolls one from where it is placed.", fill=(170, 172, 185), font=font(15))
    small = font(14)
    for r, (kind, kind_title) in enumerate(KINDS):
        for c, (dress, dress_title) in enumerate(DRESSES):
            x, y = c * tw, head + r * th
            im.paste(tiles[r][c], (x, y))
            draw.text((x + 8, y + 6), f"{kind_title} / {dress_title}", fill=(240, 240, 240), font=small,
                      stroke_width=2, stroke_fill=(0, 0, 0))
    path = OUT / f"dress-{locale}-{state}-z{zoom}.png"
    im.save(path, optimize=True)
    return path


for locale, title, fd7_name in LOCALES:
    made = {}
    for state, state_title in STATES:
        for zoom in (15, 25):
            made[(state, zoom)] = sheet(locale, title, state, state_title, zoom)
    ours = Image.open(made[("open", 25)])
    fd7 = Image.open(FD7 / fd7_name).convert("RGB")
    # fd7's sheet is 3000 wide with a neutral column first; drop it so the four dress columns line up with ours.
    cell = fd7.width // 5
    fd7 = fd7.crop((cell, 0, fd7.width, fd7.height))
    fd7 = fd7.resize((ours.width, round(fd7.height * ours.width / fd7.width)), Image.LANCZOS)
    both = Image.new("RGB", (ours.width, fd7.height + ours.height), BG)
    both.paste(fd7, (0, 0))
    both.paste(ours, (0, fd7.height))
    out = OUT / f"dress-{locale}-vs-fd7.png"
    both.save(out, optimize=True)
    print("SHEET", out)
