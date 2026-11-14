"""Contact sheets for the station massing proof. Run: python sheet.py <renders dir> <sheets dir> [station ...]"""
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

SRC, DST = Path(sys.argv[1]), Path(sys.argv[2])
ONLY = sys.argv[3:]
NAVY = (10, 12, 28)
INK_TEXT = (232, 228, 216)
DIM_TEXT = (128, 134, 160)
RULE = (52, 58, 86)
CELL = 480
# 29 u spans about 260 px of a 1280x720 frame in the PR #1344 capture; the renders are 640 px for 38 u.
GAME = round(640 * (260 / 29) / (640 / 38))

TITLES = {
    "gimbal": ("REPAIR  /  GIMBAL DOCK", "A broken ring with a second ring swung through it. The ship parks in the middle."),
    "lantern": ("MOD STATION  /  LANTERN", "A ribbed cage holding one lit prize. One rib is cut open as the way in."),
    "drum": ("ARMORY  /  MAGAZINE DRUM", "Long magazines strapped round an open bore. The ship flies down the bore."),
}
VIEWS = [("face", "FACE"), ("reverse", "REVERSE"), ("edge", "EDGE-ON"), ("tumble1", "TUMBLE 1"),
         ("tumble2", "TUMBLE 2"), ("tumble3", "TUMBLE 3"), ("tumble4", "TUMBLE 4"), ("tumble5", "TUMBLE 5")]


def font(size, bold=False):
    for name in (("bahnschrift.ttf",) if not bold else ("bahnschrift.ttf",)) + ("segoeui.ttf", "arial.ttf"):
        try:
            f = ImageFont.truetype(f"C:/Windows/Fonts/{name}", size)
            if name == "bahnschrift.ttf":
                try:
                    f.set_variation_by_name("SemiBold Condensed" if bold else "Light Condensed")
                except Exception:
                    pass
            return f
        except OSError:
            continue
    return ImageFont.load_default()


def load(name, view, state, size=None):
    im = np.asarray(Image.open(SRC / f"{name}-{view}-{state}.png").convert("RGBA")).astype(np.float32) / 255
    rgb, a = im[..., :3], im[..., 3:]
    comp = rgb * a + (np.array(NAVY, np.float32) / 255) * (1 - a)
    mask = ((rgb.max(axis=2) > 0.97) & (a[..., 0] > 0.5)).astype(np.float32)[..., None]
    src = rgb * mask
    bloom = cv2.GaussianBlur(src, (0, 0), 7) * 0.9 + cv2.GaussianBlur(src, (0, 0), 22) * 0.55
    comp = 1 - (1 - comp) * (1 - np.clip(bloom, 0, 1))
    out = Image.fromarray((np.clip(comp, 0, 1) * 255).astype(np.uint8))
    if size:
        out = out.resize((size, size), Image.LANCZOS)
    return out


def label(draw, xy, text, size=17, fill=DIM_TEXT, bold=False):
    draw.text(xy, text, font=font(size, bold), fill=fill)


def station_sheet(name):
    title, sub = TITLES[name]
    pad, head = 28, 92
    small_w = 320
    width = pad * 2 + CELL * 4
    height = head + CELL * 2 + 56 + GAME + pad
    sheet = Image.new("RGB", (width, height), NAVY)
    d = ImageDraw.Draw(sheet)
    label(d, (pad, 20), title, 34, INK_TEXT, bold=True)
    label(d, (pad, 60), sub, 19)
    d.line([(pad, head - 4), (width - pad, head - 4)], fill=RULE, width=1)
    for i, (view, text) in enumerate(VIEWS):
        x, y = pad + (i % 4) * CELL, head + (i // 4) * CELL
        sheet.paste(load(name, view, "lit", CELL), (x, y))
        label(d, (x + 8, y + 8), text)
    y = head + CELL * 2
    d.line([(pad, y + 8), (width - pad, y + 8)], fill=RULE, width=1)
    label(d, (pad, y + 20), "GAME SIZE, LIT AND DARK  (true downscale of the renders above)")
    y += 56
    crop = (GAME - small_w) // 2
    for i, (view, state) in enumerate([("tumble1", "lit"), ("tumble1", "dark"), ("tumble2", "lit"),
                                       ("tumble2", "dark"), ("tumble3", "lit"), ("tumble3", "dark")]):
        im = load(name, view, state, GAME).crop((crop, 0, crop + small_w, GAME))
        sheet.paste(im, (pad + i * small_w, y))
    sheet.save(DST / f"{name}-sheet.png")


def family_sheet(names):
    pad, head, big = 28, 92, 600
    width = pad * 2 + big * len(names)
    height = head + big + 56 + GAME * 2 + pad
    sheet = Image.new("RGB", (width, height), NAVY)
    d = ImageDraw.Draw(sheet)
    label(d, (pad, 20), "THREE STATIONS, ONE FAMILY", 34, INK_TEXT, bold=True)
    label(d, (pad, 60), "Same scale. The teal mark is the player ship. Clay massing with lights, not finished art.", 19)
    d.line([(pad, head - 4), (width - pad, head - 4)], fill=RULE, width=1)
    for i, name in enumerate(names):
        x = pad + i * big
        sheet.paste(load(name, "face", "lit", big), (x, head))
        label(d, (x + 8, head + 8), TITLES[name][0])
    y = head + big
    d.line([(pad, y + 8), (width - pad, y + 8)], fill=RULE, width=1)
    label(d, (pad, y + 20), "GAME SIZE, THE SAME TUMBLE FOR ALL THREE: LIT ABOVE, DARK BELOW")
    y += 56
    for i, name in enumerate(names):
        x = pad + i * big + (big - GAME) // 2
        sheet.paste(load(name, "tumble2", "lit", GAME), (x, y))
        sheet.paste(load(name, "tumble2", "dark", GAME), (x, y + GAME))
    sheet.save(DST / "family-sheet.png")


DST.mkdir(parents=True, exist_ok=True)
names = [n for n in TITLES if not ONLY or n in ONLY]
for n in names:
    station_sheet(n)
if len(names) == len(TITLES):
    family_sheet(names)
print("sheets", names, "game px", GAME)
