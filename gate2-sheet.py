"""Contact sheets for the station massing proof.

Run: python sheet.py <renders dir> <sheets dir> <mode> [station ...]
Modes: stations and blockout (one sheet per named station), family, mod-options, compare, factions, dress.
"""
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

SRC, DST, MODE = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3]
NAMES = sys.argv[4:]
NAVY = (10, 12, 28)
INK_TEXT = (232, 228, 216)
DIM_TEXT = (128, 134, 160)
RULE = (52, 58, 86)
CELL = 480
PAD, HEAD = 28, 92
# 29 u spans about 260 px of a 1280x720 frame in the PR #1344 capture; the renders are 640 px for 38 u.
GAME = round(640 * (260 / 29) / (640 / 38))

TITLES = {
    "gimbal": ("REPAIR  /  GIMBAL DOCK", "A broken ring with a second ring swung through it. The ship parks in the middle."),
    "lantern": ("MOD STATION  /  LANTERN", "A ribbed cage holding one lit prize. One rib is cut open as the way in."),
    "drum": ("ARMORY  /  MAGAZINE DRUM", "Long magazines slung between two rings round an open bore. The ship flies down the bore."),
    "clam": ("MOD STATION  /  CLAM", "Two dished shells hinged open, with the prize in the mouth. The crew pod is the hinge."),
    "kite": ("MOD STATION  /  KITE", "Three broad pitched sails off a split spine, with the prize in the split."),
    "kite0": ("KITE AS SHOWN IN ROUND 4", "Flat sails, crew pod on the end of the spine."),
    "diabolo": ("MOD STATION  /  DIABOLO", "Two plated funnels, mouths outward, tied at the waist by the lit prize."),
}
VIEWS = [("face", "FACE"), ("reverse", "REVERSE"), ("edge", "EDGE-ON"), ("tumble1", "TUMBLE 1"),
         ("tumble2", "TUMBLE 2"), ("tumble3", "TUMBLE 3"), ("tumble4", "TUMBLE 4"), ("tumble5", "TUMBLE 5")]


def font(size, bold=False):
    for name in ("bahnschrift.ttf", "segoeui.ttf", "arial.ttf"):
        try:
            f = ImageFont.truetype(f"C:/Windows/Fonts/{name}", size)
        except OSError:
            continue
        if name == "bahnschrift.ttf":
            try:
                f.set_variation_by_name("SemiBold Condensed" if bold else "Light Condensed")
            except Exception:
                pass
        return f
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


def blank(width, height, title, sub):
    sheet = Image.new("RGB", (width, height), NAVY)
    d = ImageDraw.Draw(sheet)
    label(d, (PAD, 20), title, 34, INK_TEXT, bold=True)
    label(d, (PAD, 60), sub, 19)
    d.line([(PAD, HEAD - 4), (width - PAD, HEAD - 4)], fill=RULE, width=1)
    return sheet, d


def game(name, view, state, crop_to=None):
    im = load(name, view, state, GAME)
    if crop_to:
        off = (GAME - crop_to) // 2
        im = im.crop((off, 0, off + crop_to, GAME))
    return im


def station_sheet(name):
    small_w = 320
    width = PAD * 2 + CELL * 4
    sheet, d = blank(width, HEAD + CELL * 2 + 56 + GAME + PAD, *TITLES[name])
    for i, (view, text) in enumerate(VIEWS):
        x, y = PAD + (i % 4) * CELL, HEAD + (i // 4) * CELL
        sheet.paste(load(name, view, "lit", CELL), (x, y))
        label(d, (x + 8, y + 8), text)
    y = HEAD + CELL * 2
    d.line([(PAD, y + 8), (width - PAD, y + 8)], fill=RULE, width=1)
    label(d, (PAD, y + 20), "GAME SIZE, LIT AND DARK  (true downscale of the renders above)")
    y += 56
    for i, (view, state) in enumerate([("tumble1", "lit"), ("tumble1", "dark"), ("tumble2", "lit"),
                                       ("tumble2", "dark"), ("tumble3", "lit"), ("tumble3", "dark")]):
        sheet.paste(game(name, view, state, small_w), (PAD + i * small_w, y))
    sheet.save(DST / f"{name}-sheet.png")


def family_sheet(names, out="family-sheet.png"):
    big = 600
    width = PAD * 2 + big * len(names)
    sheet, d = blank(width, HEAD + big + 56 + GAME * 2 + PAD, "THREE STATIONS, ONE FAMILY",
                     "Same scale. The teal mark is the player ship. Clay massing with lights, not finished art.")
    for i, name in enumerate(names):
        x = PAD + i * big
        sheet.paste(load(name, "face", "lit", big), (x, HEAD))
        label(d, (x + 8, HEAD + 8), TITLES[name][0])
    y = HEAD + big
    d.line([(PAD, y + 8), (width - PAD, y + 8)], fill=RULE, width=1)
    label(d, (PAD, y + 20), "GAME SIZE, THE SAME TUMBLE FOR ALL THREE: LIT ABOVE, DARK BELOW")
    y += 56
    for i, name in enumerate(names):
        x = PAD + i * big + (big - GAME) // 2
        sheet.paste(game(name, "tumble2", "lit"), (x, y))
        sheet.paste(game(name, "tumble2", "dark"), (x, y + GAME))
    sheet.save(DST / out)


def mod_options_sheet(options, title="MOD STATION: THREE WAYS TO REPLACE THE LANTERN", out="mod-options-sheet.png"):
    cell, trio_w = 400, 300
    views = [("face", "FACE"), ("edge", "EDGE-ON"), ("tumble1", "TUMBLE 1"), ("tumble2", "TUMBLE 2")]
    left = PAD + cell * len(views) + 40
    width = left + trio_w * 3 + PAD
    sheet, d = blank(width, HEAD + (cell + 40) * len(options) + PAD, title,
                     "Each row is one shape. On the right it sits between the repair dock and the armory at game size, "
                     "all three in the same tumble.")
    for row, name in enumerate(options):
        y = HEAD + row * (cell + 40)
        label(d, (PAD, y + 6), TITLES[name][0] + "   " + TITLES[name][1], 19, INK_TEXT)
        y += 36
        for i, (view, text) in enumerate(views):
            sheet.paste(load(name, view, "lit", cell), (PAD + i * cell, y))
            label(d, (PAD + i * cell + 8, y + 8), text, 15)
        for i, other in enumerate(("gimbal", name, "drum")):
            sheet.paste(game(other, "tumble3", "lit", trio_w), (left + i * trio_w, y + (cell - GAME) // 2))
        label(d, (left + 8, y + 8), "REPAIR  /  THIS SHAPE  /  ARMORY, GAME SIZE", 15)
        d.line([(left - 20, y), (left - 20, y + cell)], fill=RULE, width=1)
    sheet.save(DST / out)


def faction_sheet(base, skins, suffix="", title="REPAIR DOCK IN FOUR SKINS",
                  sub="The cheapest reading of 'per faction': the same model with each faction's hull colours "
                      "and window light. The green repair lights stay.", out="faction-skins-sheet.png"):
    width = PAD * 2 + CELL * len(skins)
    sheet, d = blank(width, HEAD + CELL * 2 + 56 + GAME * 2 + PAD, title, sub)
    for i, skin in enumerate(skins):
        name = base if skin == "neutral" else f"{base}-{skin}{suffix}"
        x = PAD + i * CELL
        sheet.paste(load(name, "face", "lit", CELL), (x, HEAD))
        sheet.paste(load(name, "tumble1", "lit", CELL), (x, HEAD + CELL))
        label(d, (x + 8, HEAD + 8), skin.upper(), 22, INK_TEXT, bold=True)
        gx = x + (CELL - GAME) // 2
        sheet.paste(game(name, "tumble2", "lit"), (gx, HEAD + CELL * 2 + 56))
        sheet.paste(game(name, "tumble2", "dark"), (gx, HEAD + CELL * 2 + 56 + GAME))
    y = HEAD + CELL * 2
    d.line([(PAD, y + 8), (width - PAD, y + 8)], fill=RULE, width=1)
    label(d, (PAD, y + 20), "GAME SIZE: LIT ABOVE, DARK BELOW")
    sheet.save(DST / out)


def ring(draw, centre, radius, fill, dash=10):
    """A dashed circle."""
    import math
    steps = max(24, int(2 * math.pi * radius / dash))
    for i in range(0, steps, 2):
        a0, a1 = 360 * i / steps, 360 * (i + 1) / steps
        box = [centre[0] - radius, centre[1] - radius, centre[0] + radius, centre[1] + radius]
        draw.arc(box, a0, a1, fill=fill, width=2)


def blockout_sheet(name):
    import json
    facts = json.loads((SRC / f"{name}.json").read_text(encoding="utf-8"))
    px = CELL / 38.0
    sx, sy, sz = facts["size"]
    width = PAD * 2 + CELL * 4
    title, sub = TITLES[name]
    sheet, d = blank(width, HEAD + CELL * 2 + 56 + GAME + PAD, title + "  /  BLOCKOUT",
                     sub + "  Sized to 29 u, in named parts with pivots.")
    orthos = [("top", "TOP", sx, sy), ("front", "FRONT", sx, sz), ("side", "SIDE", sz, sy)]
    for i, (view, text, w, h) in enumerate(orthos):
        x, y = PAD + i * CELL, HEAD
        sheet.paste(load(name, view, "lit", CELL), (x, y))
        ring(d, (x + CELL // 2, y + CELL // 2), 16 * px, RULE)
        label(d, (x + 8, y + 8), f"{text}   {w:.1f} x {h:.1f} u")
    x, y = PAD + 3 * CELL + 24, HEAD + 16
    lines = [
        ("SIZE", f"{sx:.1f} x {sy:.1f} x {sz:.1f} u. Today's station is 29 u across."),
        ("SCALE", f"x{facts['scale']:.2f} from the massing sheet."),
        ("TRIGGER", f"The dashed circle is the 16 u trigger. Furthest part: {facts['reach']:.1f} u from the centre."),
        ("CENTRE", f"Nearest hull to the centre: {facts['clear']:.1f} u. The ship is about 2 u long."),
        ("MESH", f"{facts['tris']:,} triangles in {facts['part_count']} parts."),
        ("TOP-LEVEL PARTS", ", ".join(facts["parts"])),
    ]
    for head, body in lines:
        label(d, (x, y), head, 15)
        y += 22
        words, line = body.split(), ""
        for word in words:
            if d.textlength(line + " " + word, font=font(18)) > CELL - 56:
                label(d, (x, y), line, 18, INK_TEXT)
                y += 24
                line = word
            else:
                line = (line + " " + word).strip()
        label(d, (x, y), line, 18, INK_TEXT)
        y += 38
    for i, (view, text) in enumerate([("reverse", "REVERSE"), ("tumble1", "TUMBLE 1"), ("tumble2", "TUMBLE 2"),
                                      ("tumble3", "TUMBLE 3")]):
        x, y = PAD + i * CELL, HEAD + CELL
        sheet.paste(load(name, view, "lit", CELL), (x, y))
        label(d, (x + 8, y + 8), text)
    y = HEAD + CELL * 2
    d.line([(PAD, y + 8), (width - PAD, y + 8)], fill=RULE, width=1)
    label(d, (PAD, y + 20), "GAME SIZE, LIT AND DARK  (true downscale of the renders above)")
    y += 56
    for i, (view, state) in enumerate([("tumble1", "lit"), ("tumble1", "dark"), ("tumble2", "lit"),
                                       ("tumble2", "dark"), ("tumble3", "lit"), ("tumble3", "dark")]):
        sheet.paste(game(name, view, state, 320), (PAD + i * 320, y))
    sheet.save(DST / f"{name}-blockout-sheet.png")


DST.mkdir(parents=True, exist_ok=True)
if MODE == "stations":
    for n in NAMES:
        station_sheet(n)
elif MODE == "family":
    family_sheet(NAMES or ["gimbal", "lantern", "drum"])
elif MODE == "mod-options":
    mod_options_sheet(NAMES or ["clam", "kite", "diabolo"])
elif MODE == "compare":
    mod_options_sheet(NAMES, "KITE: ROUND 4 ABOVE, REWORKED BELOW", "kite-compare-sheet.png")
elif MODE == "blockout":
    for n in NAMES:
        blockout_sheet(n)
elif MODE == "factions":
    faction_sheet("gimbal", ["neutral", "shoal", "veil", "bastion"])
elif MODE == "dress":
    faction_sheet("gimbal", ["neutral", "shoal", "veil", "bastion"], "-dress", "REPAIR DOCK DRESSED PER FACTION",
                  "One hull, three sets of faction parts. The ring-in-ring shape and the green repair lights "
                  "are the same in all four.", "faction-dress-sheet.png")
print(MODE, NAMES, "game px", GAME)
