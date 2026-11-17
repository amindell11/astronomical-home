# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy", "pillow", "opencv-python"]
# ///
"""Bake the streak shapes into the Keystone Streak atlas and the bow-shock arc texture.

Channels: R coverage, G position along the streak (1 head, 0 tail), B distance inside the edge,
A the pale stroke along the leading tip (the two edges that meet at the head).
Streak and bow cells share one scale with the shape's origin, its hull point, at the top centre,
so a quad of the printed cell size times the tuner size, pivoted at that point, draws the shape as the tuner does.
"""
import argparse
import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

HERE = Path(__file__).parent
DATA = HERE.parent / "tuner" / "data.json"
TILE_W, TILE_H = 256, 320   # one atlas cell; streaks point up (+V), head at the top
COLS, ROWS = 8, 4           # rows 0-1 body streaks, row 2 bow-shock streaks, row 3 lines, motes, star
MARGIN = 6                  # px kept clear around every shape so mips don't bleed between cells
UNIT = (TILE_H - 2 * MARGIN) / 1.7   # px per shape unit; the longest tuner streak is 1.64
SS = 4                      # supersampling for antialiased coverage
EDGE_PX = 10                # distance inside the edge at which B reaches 1
LEAD_PX = 4                 # leading-tip stroke width, about the tuner's 1.1 px at its ship size
LINES, MOTES = 4, 2         # row 3 layout: lines, then motes, then one star
ARC_SIZE = 512              # bow-shock arc texture
ARC_SPAN = 1.1              # ship lengths the arc texture covers
ARC_CENTRE_Y = 0.1          # arc texture centre ahead of the ship centre, ship lengths
ARC_ASPECT = 0.6289         # the tuner's Bulwark width / length
ARC_RINGS, ARC_SIZE_K = 2, 0.8
ARC_PX = 9                  # arc stroke width, the tuner's 1.3 px at its ship size


def raster(px, h, w):
    big = np.zeros((h * SS, w * SS), np.uint8)
    cv2.fillPoly(big, [np.round(px * SS).astype(np.int32)], 255, cv2.LINE_AA)
    return cv2.resize(big, (w, h), interpolation=cv2.INTER_AREA).astype(float) / 255


def segment_distance(h, w, a, b):
    ys, xs = np.mgrid[0:h, 0:w].astype(float) + 0.5
    ab = b - a
    t = np.clip(((xs - a[0]) * ab[0] + (ys - a[1]) * ab[1]) / max(1e-9, ab @ ab), 0, 1)
    return np.hypot(xs - (a[0] + t * ab[0]), ys - (a[1] + t * ab[1]))


def channels(px, w, h, lead=True):
    cover = raster(px, h, w)
    hard = (cover > 0.5).astype(np.uint8)
    dist = np.clip(cv2.distanceTransform(hard, cv2.DIST_L2, 5) / EDGE_PX, 0, 1)
    ys = np.arange(h, dtype=float)[:, None].repeat(w, 1)
    top, bottom = px[:, 1].min(), px[:, 1].max()
    along = np.clip((bottom - ys) / max(1e-6, bottom - top), 0, 1) * (cover > 0)
    edge = np.zeros((h, w))
    if lead:
        i = int(np.argmin(px[:, 1]))
        n = len(px)
        d = np.minimum(segment_distance(h, w, px[i], px[(i - 1) % n]), segment_distance(h, w, px[i], px[(i + 1) % n]))
        edge = np.clip(LEAD_PX - d, 0, 1) * cover
    return np.dstack([cover, along, dist, edge])


def streak(poly):
    """A tuner shape in its own units, origin pinned to the top centre of the cell at the shared scale."""
    p = np.asarray(poly, float)
    px = p * [UNIT, -UNIT] + [TILE_W / 2, MARGIN]
    assert px[:, 0].min() >= 0 and px[:, 0].max() <= TILE_W and px[:, 1].max() <= TILE_H, "shape outgrows the cell"
    return channels(px, TILE_W, TILE_H)


def needle(width):
    """A speed line: the cell's full length, tapering to a fifth of its width at the tail."""
    w, top, bottom, c = width, MARGIN, TILE_H - MARGIN, TILE_W / 2
    px = np.array([(c - w / 2, top), (c + w / 2, top), (c + w / 10, bottom), (c - w / 10, bottom)])
    out = channels(px, TILE_W, TILE_H, lead=False)
    out[..., 2] = out[..., 0] > 0  # lines carry no ink
    return out


def small(poly, size, outline=False):
    """A square shape centred in a tall cell, so motes and stars don't stretch."""
    p = np.asarray(poly, float)
    lo, hi = p.min(0), p.max(0)
    scale = (size - 2 * MARGIN) / max(hi - lo)
    px = (p - (lo + hi) / 2) * scale * [1, -1] + [TILE_W / 2, TILE_H / 2]
    out = channels(px, TILE_W, TILE_H, lead=False)
    if outline:
        hard = (out[..., 0] > 0.5).astype(np.uint8)
        out[..., 3] = (cv2.distanceTransform(hard, cv2.DIST_L2, 5) < LEAD_PX) * out[..., 0]
    return out


def diamond():
    return [(0, 1), (0.55, 0), (0, -1), (-0.55, 0)]


def star():
    return [(math.sin(i * math.pi / 8) * (0.38 if i % 2 else 1), math.cos(i * math.pi / 8) * (0.38 if i % 2 else 1)) for i in range(16)]


def streak_shapes(effects):
    """Body streaks from the dash source; bow-shock streaks from the ram source's two flank groups."""
    body = [s["p"] for g in effects["dash"] for s in g["shapes"]]
    ram = effects["ram"]
    front = [s["p"] for i, g in enumerate(ram) if i != (len(ram) - 1) / 2 for s in g["shapes"]]
    return body, front


def atlas(effects):
    body, front = streak_shapes(effects)
    tiles = [streak(p) for p in body[: 2 * COLS]]
    tiles += [streak(p) for p in front[:COLS]]
    tiles += [needle(3 + 0.5 * i) for i in range(LINES)]
    tiles += [small(diamond(), 96 + 32 * i) for i in range(MOTES)]
    tiles += [small(star(), TILE_W, outline=True)]
    out = np.zeros((ROWS * TILE_H, COLS * TILE_W, 4))
    for i, t in enumerate(tiles):
        r, c = divmod(i, COLS)
        out[r * TILE_H:(r + 1) * TILE_H, c * TILE_W:(c + 1) * TILE_W] = t
    return out, len(tiles)


def arcs():
    """The tuner's pressure arcs: quadratic curves from the nose round the flanks, as the A stroke only."""
    n, w = ARC_SIZE, ARC_ASPECT / 2

    def to_px(x, y):
        return (x / ARC_SPAN + 0.5) * n, (0.5 - (y - ARC_CENTRE_Y) / ARC_SPAN) * n

    stroke = np.zeros((n, n))
    along = np.zeros((n, n))
    for i in range(ARC_RINGS):
        for side in (-1, 1):
            z, y0 = ARC_SIZE_K, 0.55 + i * 0.07 * ARC_SIZE_K
            p0 = (side * 0.04, y0)
            p1 = (side * (w * 0.95 + 0.12 * i) * z, y0 - 0.02)
            p2 = (side * (w * 1.05 + 0.2 * i) * z, y0 - (0.5 + 0.18 * i) * z)
            pts = []
            for j in range(33):
                t = j / 32
                q = 1 - t
                pts.append(to_px(q * q * p0[0] + 2 * q * t * p1[0] + t * t * p2[0], q * q * p0[1] + 2 * q * t * p1[1] + t * t * p2[1]))
            for j in range(32):
                mid = 1 - abs(2 * (j + 0.5) / 32 - 1)
                d = segment_distance(n, n, np.array(pts[j]), np.array(pts[j + 1]))
                on = np.clip(ARC_PX / 2 - d, 0, 1) * (1 - i * 0.18)
                stroke = np.maximum(stroke, on)
                along = np.where(on > 0, np.maximum(along, mid), along)
    zero = np.zeros((n, n))
    return np.dstack([zero, along, np.ones((n, n)), stroke])


def save(rgba, path):
    Image.fromarray(np.round(np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--export", type=Path, default=HERE / "out", help="folder for KeystoneStreakAtlas.png and KeystoneArcs.png")
    args = ap.parse_args()
    args.export.mkdir(parents=True, exist_ok=True)
    sheet, n = atlas(json.loads(DATA.read_text(encoding="utf-8"))["effects"])
    save(sheet, args.export / "KeystoneStreakAtlas.png")
    save(arcs(), args.export / "KeystoneArcs.png")
    print(f"atlas {COLS}x{ROWS} cells, {n} used: body 0-15, front 16-23, lines 24-27, motes 28-29, star 30")
    print(f"streak cell {TILE_W / UNIT:.4f} x {TILE_H / UNIT:.4f} shape units, head {0.5 - MARGIN / TILE_H:.4f} of the height above centre")
    print(f"arc quad {ARC_SPAN} ship lengths, centre {ARC_CENTRE_Y} ahead of the ship centre")


if __name__ == "__main__":
    main()
