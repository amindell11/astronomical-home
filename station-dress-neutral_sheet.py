"""Copper Turquoise sheets with the plain (neutral) model as a fifth column beside the four dresses, all from one run.

python neutral_sheet.py <neutral stills dir> <fd7 dir> <out dir>
Writes copper-5col-<state>-z<zoom>.png (rows: kinds, columns: neutral + four dresses) and
fd7-vs-engine-5col-copper-<state>.png (fd7's tile above the in-engine tile, neutral first, as fd7 lays it out).
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

STILLS, FD7, OUT = (Path(a) for a in sys.argv[1:4])
OUT.mkdir(parents=True, exist_ok=True)
KINDS = [("Repair", "REPAIR"), ("Mod", "MOD STATION"), ("Armory", "ARMORY")]
DRESSES = [("neutral", "NEUTRAL (SHIPPED PLAIN)"), ("shoal", "SHOAL"), ("veil", "VEIL"),
           ("bastion-red", "BASTION, RED HULL"), ("bastion-bone", "BASTION, BONE HULL")]
BG = (12, 13, 22)
TW, TH = 600, 460


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
        return im.crop(((w - TW) // 2, (h - TH) // 2, (w + TW) // 2, (h + TH) // 2))
    return im.resize((640, 360), Image.LANCZOS)


def label(draw, x, y, text, w):
    draw.rectangle((x, y, x + w, y + 22), fill=(0, 0, 0))
    draw.text((x + 8, y + 4), text, fill=(240, 240, 240), font=font(14))


for state, state_title in (("open", "OPEN"), ("dark", "DARK")):
    for zoom in (15, 25):
        tiles = [[tile(STILLS / f"copper-{d}-{k}-{state}-z{zoom}.png", zoom) for d, _ in DRESSES] for k, _ in KINDS]
        tw, th = tiles[0][0].size
        head = 76
        im = Image.new("RGB", (tw * len(DRESSES), head + th * len(KINDS)), BG)
        draw = ImageDraw.Draw(im)
        draw.text((14, 10), f"COPPER TURQUOISE, {state_title}, SIZE {zoom}: NEUTRAL CONTROL BESIDE THE FOUR DRESSES",
                  fill=(235, 235, 240), font=font(26))
        draw.text((14, 46), "One run: every column is the same placed station at the same tilt, redressed. "
                            "Neutral is the shipped plain model with its own materials.",
                  fill=(170, 172, 185), font=font(15))
        for r, (kind, kind_title) in enumerate(KINDS):
            for c, (dress, dress_title) in enumerate(DRESSES):
                x, y = c * tw, head + r * th
                im.paste(tiles[r][c], (x, y))
                label(draw, x, y, f"{kind_title} / {dress_title}", tw)
        path = OUT / f"copper-5col-{state}-z{zoom}.png"
        im.save(path, optimize=True)
        print("SHEET", path)

for state, fd7_name in (("open", "fd7-factions-copper.png"), ("dark", "fd7-factions-copper-dark.png")):
    fd7 = Image.open(FD7 / fd7_name).convert("RGB")
    head = 76
    sheet = Image.new("RGB", (TW * len(DRESSES), head + 2 * TH * len(KINDS)), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 10), f"COPPER TURQUOISE, {state.upper()}: FD7 ABOVE, IN ENGINE BELOW, SIZE 25, NEUTRAL FIRST",
              fill=(235, 235, 240), font=font(26))
    draw.text((14, 46), "fd7 drew each station from one Blender view over flight backdrops. Below is the game's renderer, "
                        "each station at the tilt the game gives its placed point, so the poses differ.",
              fill=(170, 172, 185), font=font(15))
    for r, (kind, kind_title) in enumerate(KINDS):
        for c, (dress, dress_title) in enumerate(DRESSES):
            x, y = c * TW, head + 2 * r * TH
            sheet.paste(fd7.crop((c * TW, r * TH, (c + 1) * TW, (r + 1) * TH)), (x, y))
            sheet.paste(tile(STILLS / f"copper-{dress}-{kind}-{state}-z25.png", 25), (x, y + TH))
            label(draw, x, y, f"FD7: {kind_title} / {dress_title}", TW)
            label(draw, x, y + TH, f"IN ENGINE: {kind_title} / {dress_title}", TW)
    path = OUT / f"fd7-vs-engine-5col-copper-{state}.png"
    sheet.save(path, optimize=True)
    print("SHEET", path)
