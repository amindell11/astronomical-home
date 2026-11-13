"""Round 14: taper the wing pods' outer edges into A legs; planet brightened; navy outline outside the planet."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Polygon
from shapely.ops import unary_union
import build10
from build import disc, square_frame, raster, svg
from build4 import clean, rows_sheet
from build9 import SHIP

HERE = Path(__file__).parent
PLANET = "#1A3A5C"
NAVY = "#0B1024"
CREAM = "#F4EEDF"
AMBER = "#F2A33A"
# shoulder vertices and their neighbours (wing root above, wingtip below), from the traced ship
LEFT, RIGHT = (7, 8, 9), (46, 45, 44)


def tapered(t):
    """Slide each wing's shoulder corner toward the root-to-tip line; t=1 makes the edge one straight diagonal."""
    pts = list(SHIP.exterior.coords)[:-1]
    for a, s, b in (LEFT, RIGHT):
        (ax, ay), (sx, sy), (bx, by) = pts[a], pts[s], pts[b]
        dx, dy = bx - ax, by - ay
        u = ((sx - ax) * dx + (sy - ay) * dy) / (dx * dx + dy * dy)
        px, py = ax + u * dx, ay + u * dy
        pts[s] = (sx + (px - sx) * t, sy + (py - sy) * t)
    return Polygon(pts).buffer(0)


def design(t, colour):
    build10.SHIP = tapered(t)
    kw = dict(rim_col=AMBER, planet=PLANET) if colour else dict(planet=PLANET)
    layers = build10.build(**kw)
    light = unary_union([g for g, c in layers[1:]])
    outline = light.difference(disc).buffer(7, join_style=2).difference(disc.buffer(-1)).difference(light)
    return [layers[0], (clean(outline, 20), NAVY)] + layers[1:]


V = [("t=0 current", 0.0), ("t=0.5 tapered", 0.5), ("t=1 straight diagonal", 1.0)]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    entries = []
    for label, t in V:
        entries.append((f"colour, {label}", lambda bg, t=t: design(t, True)))
        entries.append((f"mono, {label}", lambda bg, t=t: design(t, False)))
    rows_sheet(entries, HERE / "sheet16_taper.png", "Sheet 16 - tapered wings. Per backdrop: 256, 64, then 32 and 16 at 4x", font)
    tile = 440
    out = Image.new("RGBA", (tile * 3 + 24, tile * 2 + 12), "#16181D")
    d = ImageDraw.Draw(out)
    for i, (label, t) in enumerate(V):
        for j, colour in enumerate((True, False)):
            L = design(t, colour)
            bg = Image.new("RGBA", (tile, tile), "#202020")
            bg.alpha_composite(raster(L, square_frame(L), tile))
            out.alpha_composite(bg, (i * (tile + 12), j * (tile + 12)))
            d.text((i * (tile + 12) + 8, j * (tile + 12) + 6), label, fill="#888", font=font)
    out.save(HERE / "r14_large.png")
