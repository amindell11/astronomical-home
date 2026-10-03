"""Animates bolt_flat.webp into a shape-changing crackle flipbook.

Frame 0 is the drawing. Every other frame warps each band's mask vertically, away from
and toward the centreline, with a backward-swept sawtooth (sharp jump, slow ease toward
the head), so the drawn hooks shift and new ones form while the core and nose hold.
"""
import random
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

HERE = Path(__file__).parent
SRC = HERE.parent / "bolt_flat.webp"
BACKDROP = HERE.parent / "refs" / "style_frame.webp"
OUT = HERE / "out"
OUT.mkdir(exist_ok=True)

# Tunables; the crackle-tuner page exports this block ready to paste.
FRAMES = 6
FPS = 14
INCLUDE_DRAWN = True                  # frame 0 is the untouched drawing
SEG = (45, 150)                       # px: length of one sawtooth hook along the bolt
AMP = [0.16, 0.2, 0.22, 0.12]          # per band (rim, mid, hot, core): max edge push, as a fraction of its half-height
NOTCH_PULL = 0.4                      # inward pull, as a fraction of AMP
EASE_END = 0.1                        # fraction of a hook's jump left at its head-side end
DAMP_START = 0.88                     # fraction of the length where head damping begins
HEAD_DAMP = 0.3                       # warp kept at the nose, so the rounded head holds its shape
TAIL_FLICKER = 0.07                   # tail length varies by up to this fraction
GLOW_SIGMA = 30                       # px
GLOW_GAIN = 1.5
GLOW_ALPHA = 0.85
SEED = 7
SMOOTH = "tween"                      # "off", "tween" (in-between shapes baked in) or "crossfade" (Unity blends at runtime)
TWEEN = 2                             # in-between frames per key when SMOOTH is "tween"
TWEEN_EASE = True                     # in-betweens cluster near the keys, so each shape holds a moment
PAINT = [(12, 104, 250), (0, 214, 250), (184, 250, 244), (250, 252, 252)]   # rim, mid, hot, core
GLOW = (20, 70, 255)

# Colours of bolt_flat.webp itself, used only to split it into bands; index = band (0 is background).
SOURCE_PALETTE = np.array([(0, 0, 0), (12, 104, 250), (0, 214, 250), (184, 250, 244), (250, 252, 252)], float)


def classify(rgb):
    d = ((rgb[:, :, None, :] - SOURCE_PALETTE[None, None]) ** 2).sum(-1)
    return d.argmin(-1)


def band_masks(labels):
    """Nested masks (band b = everything at b or hotter), with antialias specks cleaned."""
    return [ndi.binary_opening(labels >= b, iterations=1) for b in range(1, 5)]


def sawtooth(rng, width, amp):
    """Per-column edge push: each segment jumps to a random height then eases to zero."""
    out = np.zeros(width)
    x = 0
    while x < width:
        n = rng.randint(*SEG)
        out[x:x + n] = rng.uniform(-NOTCH_PULL, 1.0) * amp * np.linspace(1, EASE_END, n)[: max(0, min(n, width - x))]
        x += n
    return out


def warp(mask, center, push_up, push_down, tail_scale, head_x):
    """Resample a band mask: per-column vertical scale about the centreline, plus a tail stretch."""
    h, w = mask.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    xs = head_x - (head_x - xx) / tail_scale
    xi = np.clip(xs.round().astype(int), 0, w - 1)
    c = center[xi]
    stretch = 1 + np.where(yy < c, push_up[xi], push_down[xi])
    ys = c + (yy - c) / stretch
    return ndi.map_coordinates(mask.astype(float), [ys, xs], order=1) > 0.5


def random_key(w, head_x, tail_x, rng):
    """A key is the warp's numbers: per band an (up, down) push per column, plus the tail stretch."""
    t = np.clip((np.arange(w) - tail_x) / (head_x - tail_x), 0, 1)
    damp = np.where(t > DAMP_START, np.interp(t, [DAMP_START, 1.0], [1.0, HEAD_DAMP]), 1.0)
    tail = 1 + rng.uniform(-TAIL_FLICKER, TAIL_FLICKER)
    return tail, [(sawtooth(rng, w, amp) * damp, sawtooth(rng, w, amp) * damp) for amp in AMP]


def identity_key(w):
    return 1.0, [(np.zeros(w), np.zeros(w)) for _ in AMP]


def lerp_key(a, b, t):
    return a[0] + (b[0] - a[0]) * t, [(ua + (ub - ua) * t, da + (db - da) * t)
                                       for (ua, da), (ub, db) in zip(a[1], b[1])]


def key_masks(drawn, center, head_x, key):
    tail, pushes = key
    return [warp(m, center, up, down, tail, head_x) for m, (up, down) in zip(drawn, pushes)]


def ease(t):
    return t * t * (3 - 2 * t) if TWEEN_EASE else t


def compose(masks):
    """Inner bands stay inside outer ones with a thin rim of the outer colour showing."""
    labels = np.zeros(masks[0].shape, int)
    outer = None
    for b, m in enumerate(masks, start=1):
        if outer is not None:
            m = m & ndi.binary_erosion(outer, iterations=2)
        labels[m] = b
        outer = m
    return labels


def to_rgba(labels):
    rgb = np.array([(0, 0, 0)] + list(PAINT), float)[labels]
    alpha = (labels > 0) * 255.0
    return Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA")


def glow_layer(labels):
    m = ndi.gaussian_filter((labels > 0).astype(float), GLOW_SIGMA)
    a = np.clip(m * GLOW_GAIN, 0, 1) * GLOW_ALPHA
    rgb = np.broadcast_to(np.array(GLOW, float), labels.shape + (3,))
    return Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA")


def add(dst_rgb, src_rgba):
    s = np.asarray(src_rgba, float)
    d = np.asarray(dst_rgb, float) + s[..., :3] * (s[..., 3:] / 255)
    return Image.fromarray(np.clip(d, 0, 255).astype(np.uint8))


def main():
    rng = random.Random(SEED)
    rgb = np.asarray(Image.open(SRC).convert("RGB"), float)
    labels = classify(rgb)
    ys, xs = np.nonzero(labels > 0)
    pad = 60
    labels = labels[max(0, ys.min() - pad): ys.max() + pad, max(0, xs.min() - pad): xs.max() + pad]

    drawn = band_masks(labels)
    xs = np.flatnonzero(drawn[0].any(0))
    head_x, tail_x = xs.max(), xs.min()
    rows = np.arange(labels.shape[0])[:, None]
    m = drawn[0]
    mid = (np.where(m, rows, m.shape[0]).min(0) + np.where(m, rows, -1).max(0)) / 2
    center = np.interp(np.arange(m.shape[1]), xs, mid[xs])
    center = ndi.gaussian_filter1d(center, 80, mode="nearest")

    w = labels.shape[1]
    keys = [None] if INCLUDE_DRAWN else []
    while len(keys) < FRAMES:
        keys.append(random_key(w, head_x, tail_x, rng))
    per_key = TWEEN + 1 if SMOOTH == "tween" else 1
    frame_labels = []
    for i, key in enumerate(keys):
        frame_labels.append(compose(key_masks(drawn, center, head_x, key)) if key else compose(drawn))
        a, b = key or identity_key(w), keys[(i + 1) % len(keys)] or identity_key(w)
        for j in range(1, per_key):
            frame_labels.append(compose(key_masks(drawn, center, head_x, lerp_key(a, b, ease(j / per_key)))))
    frame_count = len(frame_labels)
    play_fps = FPS * per_key

    bodies = [to_rgba(l) for l in frame_labels]
    for f, body in enumerate(bodies):
        body.save(OUT / f"body_f{f}.png")
    glow = glow_layer(compose(drawn))
    glow.save(OUT / "glow.png")

    bw, bh = bodies[0].size
    sheet = Image.new("RGBA", (bw, bh * frame_count))
    for f, body in enumerate(bodies):
        sheet.paste(body, (0, f * bh))
    sheet.save(OUT / f"body_sheet_1x{frame_count}.png")

    black = Image.new("RGB", (bw, bh))
    big = [Image.alpha_composite(add(black, glow).convert("RGBA"), b).convert("RGB") for b in bodies]
    small = [f.resize((bw // 2, bh // 2), Image.LANCZOS) for f in big]
    small[0].save(OUT / "preview_large.gif", save_all=True, append_images=small[1:], duration=round(1000 / play_fps), loop=0)

    strip = Image.new("RGB", (bw // 2, (bh // 2) * frame_count))
    for f, frame in enumerate(small):
        strip.paste(frame, (0, f * (bh // 2)))
    strip.save(OUT / "frames_strip.png")

    # Game scale over the style frame; shown at 2x so the pixels are inspectable.
    crop = Image.open(BACKDROP).convert("RGB").crop((380, 150, 1180, 600))
    game = []
    for body in bodies:
        frame = crop.copy()
        for length, (x, y), angle in ((150, (300, 250), 36), (100, (560, 120), 36), (60, (140, 380), -20)):
            size = (length, max(1, round(bh * length / bw)))
            b = body.resize(size, Image.LANCZOS).rotate(angle, expand=True, resample=Image.BICUBIC)
            g = glow.resize(size, Image.LANCZOS).rotate(angle, expand=True, resample=Image.BICUBIC)
            layer = Image.new("RGBA", frame.size)
            layer.paste(g, (x - g.width // 2, y - g.height // 2))
            frame = add(frame, layer)
            frame.paste(b, (x - b.width // 2, y - b.height // 2), b)
        game.append(frame.resize((frame.width * 2, frame.height * 2), Image.NEAREST))
    game[0].save(OUT / "preview_ingame.gif", save_all=True, append_images=game[1:], duration=round(1000 / play_fps), loop=0)
    print("keys", len(keys), "frames", frame_count, "size", bw, bh)


if __name__ == "__main__":
    main()
