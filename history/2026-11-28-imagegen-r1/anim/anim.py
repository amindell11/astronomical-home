"""Composite imagegen exhaust strips onto the player-ship lineup and write 12 fps GIFs."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

HERE = Path(__file__).parent
R1 = HERE.parent / "r1"
OUT = HERE.parent / "anim"
OUT.mkdir(exist_ok=True)
BG = np.array([14, 16, 26], float)

STRIPS = ["codex-a6f-1", "codex-a6f-2", "codex-b4f-1", "codex-b4f-2"]

# Nozzles found by diffing factions-2500 against factions-flames-2500: (centre x, top y, current flame length).
NOZZLES = {
    "Crimson": [(370.5, 415, 64), (440, 415, 52)],
    "Valis": [(829, 386, 53)],
    "Nightshade": [(1249, 352, 60)],
    "Bulwark": [(1632, 410, 34), (1714, 410, 45)],
    "Vanguard": [(2047, 365, 53), (2143, 364, 68)],
}


def slice_strip(path):
    im = np.asarray(Image.open(path).convert("RGB")).astype(float)
    glow = np.clip((im - BG).max(-1), 0, None)
    cols = (glow > 60).any(0)
    lab, n = ndimage.label(cols)
    frames = []
    for s in ndimage.find_objects(lab):
        x0, x1 = s[0].start, s[0].stop
        if x1 - x0 < 20:
            continue
        rows = np.where((glow[:, x0:x1] > 60).any(1))[0]
        y0, y1 = rows[0], rows[-1] + 1
        pad = 30
        crop = im[max(0, y0 - pad):y1 + pad, max(0, x0 - pad):x1 + pad]
        # Emissive: keep only the light added over the background, for additive compositing.
        add = np.clip(crop - BG, 0, 255)
        frames.append((add, pad, y1 - y0))
    return frames


def place(canvas, add, pad, core_len, cx, top, length):
    s = length / core_len
    h, w = add.shape[:2]
    img = Image.fromarray(add.astype(np.uint8)).resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    a = np.asarray(img).astype(float)
    x0 = round(cx - a.shape[1] / 2)
    y0 = round(top - pad * s)
    H, W = canvas.shape[:2]
    ys, xs = max(0, y0), max(0, x0)
    ye, xe = min(H, y0 + a.shape[0]), min(W, x0 + a.shape[1])
    canvas[ys:ye, xs:xe] += a[ys - y0:ye - y0, xs - x0:xe - x0]


def build(strip, scale_len, base, crop, label, font):
    frames = slice_strip(R1 / f"{strip}.png")
    n = len(frames)
    out = []
    for t in range(24):
        c = base.copy()
        k = 0
        for ship, nozzles in NOZZLES.items():
            for cx, top, cur in nozzles:
                add, pad, core = frames[(t + k * 2) % n]
                place(c, add, pad, core, cx, top, cur * scale_len)
                k += 1
        img = Image.fromarray(np.clip(c, 0, 255).astype(np.uint8)).crop(crop)
        ImageDraw.Draw(img).text((16, 10), label, fill=(210, 210, 220), font=font)
        out.append(img)
    return out, n


def main():
    base = np.asarray(Image.open(HERE / "factions-2500.png").convert("RGB")).astype(float)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 26)
    crop = (200, 40, 2300, 560)
    sheets = {}
    for strip in STRIPS:
        n = len(slice_strip(R1 / f"{strip}.png"))
        frames, n = build(strip, 1.0, base, crop, f"{strip}  ({n} frames, 12 fps, current flame length)", font)
        frames[0].save(OUT / f"{strip}-lineup.gif", save_all=True, append_images=frames[1:], duration=83, loop=0)
        sheets[strip] = (frames, n)
    # Close-up: Crimson and Vanguard at 3x, every strip stacked.
    zoom = 3
    regions = [(300, 330, 510, 520), (1980, 290, 2210, 480)]
    rows = []
    for t in range(24):
        tiles = []
        for strip in STRIPS:
            full = build_cache[strip][t]
            parts = [full.crop(r).resize(((r[2] - r[0]) * zoom, (r[3] - r[1]) * zoom), Image.NEAREST) for r in regions]
            w = sum(p.width for p in parts) + 20
            row = Image.new("RGB", (w, parts[0].height + 40), tuple(BG.astype(int)))
            x = 0
            for p in parts:
                row.paste(p, (x, 40))
                x += p.width + 20
            ImageDraw.Draw(row).text((10, 6), f"{strip} (3x zoom)", fill=(210, 210, 220), font=font)
            tiles.append(row)
        H = sum(r.height for r in tiles)
        sheet = Image.new("RGB", (tiles[0].width, H), tuple(BG.astype(int)))
        y = 0
        for r in tiles:
            sheet.paste(r, (0, y))
            y += r.height
        rows.append(sheet.resize((sheet.width // 2, sheet.height // 2), Image.LANCZOS))
    rows[0].save(OUT / "closeup-3x-all.gif", save_all=True, append_images=rows[1:], duration=83, loop=0)


build_cache = {}

if __name__ == "__main__":
    base = np.asarray(Image.open(HERE / "factions-2500.png").convert("RGB")).astype(float)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 26)
    for strip in STRIPS:
        frames, n = build(strip, 1.0, base, (0, 0, base.shape[1], base.shape[0]), "", font)
        build_cache[strip] = frames
    main()
