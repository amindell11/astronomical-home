# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy", "pillow", "opencv-python"]
# ///
"""Bake the streak shapes into the Keystone Streak atlas and the bow-shock arc texture.

Channels: R coverage, G position along the streak (1 head, 0 tail), B distance inside the edge.
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
TILE_W, TILE_H = 128, 512   # one atlas cell; streaks point up (+V) with the head at the top
COLS, ROWS = 8, 4           # rows 0-1 body streaks, row 2 bow-shock streaks, row 3 lines, motes, star
MARGIN = 6                  # px kept clear around every shape so mips don't bleed between cells
SS = 4                      # supersampling for antialiased coverage
EDGE_PX = 10                # distance inside the edge at which B reaches 1
LINES, MOTES = 4, 2         # row 3 layout: lines, then motes, then one star
ARC_SIZE = 512              # bow-shock arc texture, nose at the bottom centre
ARC_RINGS = 2               # nested pressure arcs
ARC_SPAN_DEG = 150          # how far round the nose each arc reaches
ARC_WIDTH = 0.035           # arc stroke width as a fraction of the texture


def cell(poly, w=TILE_W, h=TILE_H, margin=MARGIN):
    """Rasterise one polygon (x right, y up, any units) into a w×h RGB cell, fitted inside the margin."""
    p = np.asarray(poly, float)
    lo, hi = p.min(0), p.max(0)
    span = np.maximum(hi - lo, 1e-6)
    scale = min((w - 2 * margin) / span[0], (h - 2 * margin) / span[1])
    centre = (lo + hi) / 2
    px = (p - centre) * scale * [1, -1] + [w / 2, h / 2]
    big = np.zeros((h * SS, w * SS), np.uint8)
    cv2.fillPoly(big, [np.round(px * SS).astype(np.int32)], 255, cv2.LINE_AA)
    cover = cv2.resize(big, (w, h), interpolation=cv2.INTER_AREA).astype(float) / 255
    hard = (cover > 0.5).astype(np.uint8)
    inside = cv2.distanceTransform(hard, cv2.DIST_L2, 5)
    dist = np.clip(inside / EDGE_PX, 0, 1)
    ys = np.arange(h, dtype=float)[:, None].repeat(w, 1)
    top, bottom = px[:, 1].min(), px[:, 1].max()
    along = np.clip((bottom - ys) / max(1e-6, bottom - top), 0, 1)
    return np.dstack([cover, along * (cover > 0), dist])


def needle(length=1.0, width=0.06):
    return [(0, length / 2), (width / 2, length * 0.1), (0, -length / 2), (-width / 2, length * 0.1)]


def diamond():
    return [(0, 1), (0.6, 0), (0, -1), (-0.6, 0)]


def star(points=4, inner=0.22):
    out = []
    for i in range(points * 2):
        a = math.pi / 2 + i * math.pi / points
        r = 1 if i % 2 == 0 else inner
        out.append((r * math.cos(a), r * math.sin(a)))
    return out


def small(poly, size=TILE_W):
    """A square shape centred in a tall cell, so motes and stars don't stretch."""
    c = np.zeros((TILE_H, TILE_W, 3))
    off = (TILE_H - size) // 2
    c[off:off + size] = cell(poly, TILE_W, size)
    return c


def streak_shapes(effects):
    """Body streaks from the dash source; bow-shock streaks from the ram source's two flank groups."""
    body = [s["p"] for g in effects["dash"] for s in g["shapes"]]
    ram = effects["ram"]
    front = [s["p"] for i, g in enumerate(ram) if i != (len(ram) - 1) / 2 for s in g["shapes"]]
    return body, front


def atlas(effects):
    body, front = streak_shapes(effects)
    tiles = [cell(p) for p in body[: 2 * COLS]]
    tiles += [cell(p) for p in front[:COLS]]
    tiles += [cell(needle(1.0, 0.04 + 0.02 * i)) for i in range(LINES)]
    tiles += [small(diamond(), 64 + 24 * i) for i in range(MOTES)]
    tiles += [small(star())]
    out = np.zeros((ROWS * TILE_H, COLS * TILE_W, 3))
    for i, t in enumerate(tiles):
        r, c = divmod(i, COLS)
        out[r * TILE_H:(r + 1) * TILE_H, c * TILE_W:(c + 1) * TILE_W] = t
    return out, len(tiles)


def arcs():
    n = ARC_SIZE
    yy, xx = np.mgrid[0:n, 0:n].astype(float) / n
    dx, dy = xx - 0.5, 1 - yy
    r, ang = np.hypot(dx, dy), np.degrees(np.arctan2(dx, dy))
    cover = np.zeros((n, n))
    along = np.zeros((n, n))
    for i in range(ARC_RINGS):
        radius = 0.55 + 0.17 * i
        reach = ARC_SPAN_DEG / 2
        on = (np.abs(r - radius) < ARC_WIDTH * (1 - np.abs(ang) / reach * 0.7)) & (np.abs(ang) < reach)
        cover = np.maximum(cover, on.astype(float) * (1 - 0.3 * i))
        along = np.where(on, 1 - np.abs(ang) / reach, along)
    hard = (cover > 0).astype(np.uint8)
    dist = np.clip(cv2.distanceTransform(hard, cv2.DIST_L2, 5) / (EDGE_PX * 0.6), 0, 1)
    return np.dstack([cover, along, dist])


def save(rgb, path):
    Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--export", type=Path, default=HERE / "out", help="folder for KeystoneStreakAtlas.png and KeystoneArcs.png")
    args = ap.parse_args()
    args.export.mkdir(parents=True, exist_ok=True)
    sheet, n = atlas(json.loads(DATA.read_text(encoding="utf-8"))["effects"])
    save(sheet, args.export / "KeystoneStreakAtlas.png")
    save(arcs(), args.export / "KeystoneArcs.png")
    print(f"atlas {COLS}x{ROWS} cells, {n} used: body 0-15, front 16-23, lines 24-27, motes 28-29, star 30")


if __name__ == "__main__":
    main()
