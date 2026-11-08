"""Throwaway sketch: kinds of blue-tinge screen distortion on a synthetic canopy HUD fragment."""
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
SCENE = ROOT / "hud-src" / "inputs_locale1-gameplay.png"
OUT = ROOT / "sketch" / "out"
OUT.mkdir(parents=True, exist_ok=True)

INK_WHITE = (238, 240, 240)
TEAL = (70, 230, 210)
CORAL = (255, 120, 95)
CROP = (930, 1000, 1630, 1360)  # x0, y0, x1, y1 around the instrument cluster

scene = np.asarray(Image.open(SCENE).convert("RGB"), np.float32) / 255.0
scene = np.roll(scene, (1000 - 0, 930 - 140), axis=(0, 1))  # darker sky under the cluster
H, W = scene.shape[:2]
PIXEL_SCALE = W / 1600.0


def draw_hud():
    ss = 2
    rgb = Image.new("RGB", (W * ss, H * ss), (0, 0, 0))
    mask = Image.new("L", (W * ss, H * ss), 0)
    text_layer = Image.new("RGB", (W * ss, H * ss), (0, 0, 0))
    d, m, t = ImageDraw.Draw(rgb), ImageDraw.Draw(mask), ImageDraw.Draw(text_layer)

    def shape(fn, color, *args, **kw):
        getattr(d, fn)(*args, fill=color, **kw)
        getattr(m, fn)(*args, fill=255, **kw)

    cx, cy = 1280 * ss, 1170 * ss
    r = 72 * ss
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=INK_WHITE, width=4 * ss)
    m.ellipse((cx - r, cy - r, cx + r, cy + r), outline=255, width=4 * ss)
    dia = [(cx, cy - 34 * ss), (cx + 22 * ss, cy), (cx, cy + 34 * ss), (cx - 22 * ss, cy)]
    shape("polygon", INK_WHITE, dia)
    for side, digits in ((-1, "18"), (1, "6")):
        ax = cx + side * 185 * ss
        box = (ax - 62 * ss, cy - 62 * ss, ax + 62 * ss, cy + 62 * ss)
        start, end = (110, 250) if side < 0 else (-70, 70)
        d.arc(box, start, end, fill=INK_WHITE, width=4 * ss)
        m.arc(box, start, end, fill=255, width=4 * ss)
        for k in range(4):
            x = ax - 22 * ss + k * 11 * ss
            shape("polygon", INK_WHITE, [(x, cy + 6 * ss), (x + 6 * ss, cy + 6 * ss), (x + 18 * ss, cy - 14 * ss), (x + 12 * ss, cy - 14 * ss)])
        font = ImageFont.truetype("bahnschrift.ttf", 30 * ss)
        t.text((ax - 14 * ss, cy + 22 * ss), digits, font=font, fill=INK_WHITE)
    for side, label, accent in ((-1, "SHIELD", TEAL), (1, "HULL", None)):
        x0 = cx + (-300 if side < 0 else 40) * ss
        y = cy + 112 * ss
        for k in range(22):
            color = INK_WHITE
            if accent and k < 6:
                color = accent
            if side > 0 and k > 17:
                color = CORAL
            x = x0 + k * 12 * ss
            shape("polygon", color, [(x, y + 16 * ss), (x + 5 * ss, y + 16 * ss), (x + 13 * ss, y), (x + 8 * ss, y)])
        font = ImageFont.truetype("bahnschrift.ttf", 17 * ss)
        t.text((x0 + 90 * ss, y + 26 * ss), label, font=font, fill=(200, 205, 205))
    # target bracket near the top of the crop, a thin-line element
    bx, by = 1180 * ss, 1040 * ss
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        px, py = bx + sx * 26 * ss, by + sy * 18 * ss
        d.line([(px, py), (px - sx * 12 * ss, py)], fill=INK_WHITE, width=2 * ss)
        d.line([(px, py), (px, py - sy * 10 * ss)], fill=INK_WHITE, width=2 * ss)
        m.line([(px, py), (px - sx * 12 * ss, py)], fill=255, width=2 * ss)
        m.line([(px, py), (px, py - sy * 10 * ss)], fill=255, width=2 * ss)
    font = ImageFont.truetype("bahnschrift.ttf", 22 * ss)
    t.text((1460 * ss, 1022 * ss), "0:18", font=font, fill=INK_WHITE)

    size = (W, H)
    shapes = np.asarray(rgb.resize(size, Image.LANCZOS), np.float32) / 255.0
    alpha = np.asarray(mask.resize(size, Image.LANCZOS), np.float32) / 255.0
    text = np.asarray(text_layer.resize(size, Image.LANCZOS), np.float32) / 255.0
    ink = np.maximum(shapes, text)
    return ink, alpha


def shift(img, dx, dy):
    mtx = np.float32([[1, 0, dx], [0, 1, dy]])
    return cv2.warpAffine(img, mtx, (W, H), flags=cv2.INTER_LINEAR, borderValue=0)


def gauss(img, sigma):
    return cv2.GaussianBlur(img, (0, 0), sigma)


def tight_halo(bloom_src, glow):
    radius = (1.5 + glow * 4.0) * PIXEL_SCALE
    taps = [(0, 0, .16)] + [(dx, dy, .12) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))] \
        + [(dx, dy, .09) for dx, dy in ((1, 1), (-1, -1), (1, -1), (-1, 1))]
    halo = sum(shift(bloom_src, dx * radius, dy * radius) * w for dx, dy, w in taps)
    return halo * glow * .65


def wide_halo(bloom_src, reach_px, strength, tint=(1, 1, 1)):
    return gauss(bloom_src, reach_px * PIXEL_SCALE) * strength * np.float32(tint)


ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
U, V = xs / W, ys / H
CENTER = np.stack([U - .5, V - .5], -1)
EDGE = np.clip((np.sum(CENTER ** 2, -1) - .025) / (.45 - .025), 0, 1)
EDGE = EDGE * EDGE * (3 - 2 * EDGE)


def radial_split(light_fn, aberration):
    n = CENTER / (np.linalg.norm(CENTER, axis=-1, keepdims=True) + 1e-4)
    px = aberration * PIXEL_SCALE * EDGE
    mapx_out, mapy_out = xs + n[..., 0] * px, ys + n[..., 1] * px
    mapx_in, mapy_in = xs - n[..., 0] * px, ys - n[..., 1] * px
    r = cv2.remap(light_fn, mapx_out, mapy_out, cv2.INTER_LINEAR, borderValue=0)[..., 0]
    b = cv2.remap(light_fn, mapx_in, mapy_in, cv2.INTER_LINEAR, borderValue=0)[..., 2]
    split = np.stack([r, light_fn[..., 1], b], -1)
    return light_fn * .12 + split * .88


def phosphor(light, scanlines=.915, wear=.294, warmth=0.0):
    rng = np.random.default_rng(3)
    grain = rng.random((H, W)).astype(np.float32)[..., None]
    light = light * (1 - wear * .62 * (1 - grain) * .5)
    light = light * (1 - scanlines * .48 * (.5 + .5 * np.cos(ys * np.pi)))[..., None]
    if warmth:
        chroma = light.max(-1) - light.min(-1)
        k = (warmth * (1 - np.clip((chroma - .06) / .24, 0, 1)))[..., None]
        light = light * (1 - k) + light * np.float32([1.08, 1.0, .67]) * k
    return light


def composite(light):
    return np.clip(scene + np.maximum(light, 0), 0, 1)


ink, alpha = draw_hud()
bloom = ink * alpha[..., None]
WIDE = dict(reach_px=12.0, strength=.9)

panels = []


def panel(title, note, light):
    panels.append((title, note, composite(light)))


# 0 today
light = radial_split(ink, 4.64)
light = phosphor(light, warmth=.878)
light = light + tight_halo(bloom, .434)
panel("TODAY", "warmth .88, tight halo", light)

# 1 radial chromatic aberration, pushed
light = radial_split(ink, 12.0)
light = phosphor(light) + tight_halo(bloom, .434) + wide_halo(bloom, **WIDE)
panel("1 ABERRATION", "red out / blue in, grows to the edges", light)

# 2 misconvergence: blue gun lands off by a fixed offset everywhere
off = ink.copy()
off[..., 2] = shift(ink, 2.4 * PIXEL_SCALE, 1.6 * PIXEL_SCALE)[..., 2]
off[..., 0] = shift(ink, -.4 * PIXEL_SCALE, -.3 * PIXEL_SCALE)[..., 0]
light = off * .6 + ink * .1 + np.stack([ink[..., 0] * .3, ink[..., 1] * .3, off[..., 2] * .3], -1)
light = phosphor(light) + tight_halo(bloom, .434) + wide_halo(bloom, **WIDE)
panel("2 MISCONVERGENCE", "blue beam lands off-target, same way everywhere", light)

# 3 chroma bleed: blue smears to one side of bright strokes
k = np.zeros((1, 25), np.float32)
k[0, 12:] = np.exp(-np.arange(13) / 4.0)
k /= k.sum()
smear = cv2.filter2D(ink, -1, k, anchor=(12, 0))
light = ink + np.stack([smear[..., 0] * 0, smear[..., 1] * .25, smear[..., 2] * 1.0], -1) * 1.1
light = phosphor(light) + tight_halo(bloom, .434) + wide_halo(bloom, **WIDE)
panel("3 SIGNAL BLEED", "blue trails off bright strokes, like a weak cable", light)

# 4 blue halation: white core, the glow scatters in the glass and comes out blue
light = phosphor(ink) + tight_halo(bloom, .434) * np.float32([.6, .85, 1.1]) \
    + wide_halo(bloom, reach_px=14.0, strength=1.3, tint=(.3, .6, 1.2))
panel("4 BLUE HALATION", "white core, blue glow around it", light)

# 5 canopy ghost: faint second reflection off the glass's back face
ghost = gauss(shift(ink, 5 * PIXEL_SCALE, 3.5 * PIXEL_SCALE), 1.2) * np.float32([.25, .5, 1.0]) * .7
light = phosphor(ink + ghost) + tight_halo(bloom, .434) + wide_halo(bloom, **WIDE)
panel("5 GLASS GHOST", "dim blue double image, offset", light)

# 6 coating sheen: the canopy's coating tints light cool, shifting across the glass
hue = .5 + .5 * np.sin(U * 5.0 + V * 3.0)
tint = np.stack([.68 + .2 * hue, .86 + .06 * (1 - hue), 1.12 + .06 * hue], -1).astype(np.float32)
light = phosphor(ink) * tint + (tight_halo(bloom, .434) + wide_halo(bloom, **WIDE)) * tint
panel("6 COATING SHEEN", "cool cast that drifts blue to violet across the glass", light)

# 7 reference: glow change alone
light = phosphor(ink) + tight_halo(bloom, .434) + wide_halo(bloom, **WIDE)
panel("REF: WIDE HALO ONLY", "neutral white, new wide halo, no blue effect", light)

x0, y0, x1, y1 = CROP
cw, ch = x1 - x0, y1 - y0
label_h = 54
cols, rows = 4, 2
sheet = Image.new("RGB", (cols * cw + (cols + 1) * 8, rows * (ch + label_h) + (rows + 1) * 8), (18, 18, 22))
dr = ImageDraw.Draw(sheet)
f_title = ImageFont.truetype("bahnschrift.ttf", 24)
f_note = ImageFont.truetype("bahnschrift.ttf", 17)
for i, (title, note, img) in enumerate(panels):
    c, r = i % cols, i // cols
    px, py = 8 + c * (cw + 8), 8 + r * (ch + label_h + 8)
    crop = (img[y0:y1, x0:x1] * 255 + .5).astype(np.uint8)
    sheet.paste(Image.fromarray(crop), (px, py + label_h))
    dr.text((px + 6, py + 4), title, font=f_title, fill=(235, 235, 235))
    dr.text((px + 6, py + 31), note, font=f_note, fill=(165, 170, 178))
sheet.save(OUT / "blue_candidates_1to1.png")

zoom = 2
zx0, zy0, zx1, zy1 = 1080, 1080, 1290, 1250
zsheet = Image.new("RGB", (cols * (zx1 - zx0) * zoom + (cols + 1) * 8, rows * ((zy1 - zy0) * zoom + label_h) + (rows + 1) * 8), (18, 18, 22))
dz = ImageDraw.Draw(zsheet)
for i, (title, note, img) in enumerate(panels):
    c, r = i % cols, i // cols
    pw, ph = (zx1 - zx0) * zoom, (zy1 - zy0) * zoom
    px, py = 8 + c * (pw + 8), 8 + r * (ph + label_h + 8)
    crop = Image.fromarray((img[zy0:zy1, zx0:zx1] * 255 + .5).astype(np.uint8)).resize((pw, ph), Image.NEAREST)
    zsheet.paste(crop, (px, py + label_h))
    dz.text((px + 6, py + 4), title, font=f_title, fill=(235, 235, 235))
    dz.text((px + 6, py + 31), note, font=f_note, fill=(165, 170, 178))
zsheet.save(OUT / "blue_candidates_zoom2x.png")
print("ok", sheet.size, zsheet.size)
