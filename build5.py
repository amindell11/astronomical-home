"""Round 8: mono knockout, ring/ship shifted, ship stays space past the planet with a one-sided ink edge."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Point
from shapely.affinity import translate
from build import ship_px, disc, CX, CY, R, square_frame, raster
from build3 import stylized_ring
from build4 import GLASS, GAP, clean, rows_sheet

HERE = Path(__file__).parent
FRONT0, BACK0 = stylized_ring(front=80, back=34, side=0)
CORE = Point(CX + 0.045 * 1024 * 0.6, CY + 0.045 * 1024 * 0.8).buffer(R, 256)
INK = {"dark": "#F4EEDF", "light": "#0B1024"}


def edge(ship, kind, d):
    """Ink that outlines the ship where it leaves the planet."""
    if kind == "lit":  # sliver on the upper-left, like the planet's rim light
        return translate(ship, -d * 0.6, -d * 0.8).difference(ship)
    if kind == "shadow":  # sliver on the lower-right
        return translate(ship, d * 0.6, d * 0.8).difference(ship)
    return ship.buffer(d, join_style=2).difference(ship)  # full thin outline


def mono(ring_dy=0.0, ship_dy=0.0, edge_kind="lit", d=10, glass_key="dagger crescent"):
    ship = translate(ship_px, 0, ship_dy)
    front, back = translate(FRONT0, 0, -ring_dy), translate(BACK0, 0, -ring_dy)
    glass = translate(GLASS[glass_key], 0, ship_dy)
    planet = disc.difference(CORE.boundary.buffer(GAP / 2).intersection(disc.buffer(-GAP)))
    g = planet.difference(ship)
    g = g.union(edge(ship, edge_kind, d).difference(disc))
    g = g.union(glass.intersection(ship))
    g = g.difference(front.buffer(GAP)).union(front)
    g = g.difference(back.buffer(GAP).difference(disc.buffer(GAP))).union(back.difference(disc.buffer(GAP)))
    return clean(g)


V = [
    ("1  ring up 40, lit-side edge", dict(ring_dy=40)),
    ("2  ship down 40, lit-side edge", dict(ship_dy=40)),
    ("3  ring up 25 + ship down 25,\nlit-side edge", dict(ring_dy=25, ship_dy=25)),
    ("4  as 3, shadow-side edge", dict(ring_dy=25, ship_dy=25, edge_kind="shadow")),
    ("5  as 3, full thin outline", dict(ring_dy=25, ship_dy=25, edge_kind="outline", d=7)),
    ("6  as 3, heavier lit edge", dict(ring_dy=25, ship_dy=25, d=16)),
]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    geoms = [(label, mono(**kw)) for label, kw in V]
    rows_sheet([(label, (lambda bg, g=g: [(g, INK[bg])])) for label, g in geoms],
               HERE / "sheet11_mono.png", "Sheet 11 - mono knockout. Per backdrop: 256, 64, then 32 and 16 at 4x", font)
    tile = 360
    big = Image.new("RGBA", (tile * 3 + 24, tile * 2 + 12), "#16181D")
    d = ImageDraw.Draw(big)
    for i, (label, g) in enumerate(geoms):
        t = Image.new("RGBA", (tile, tile), "#202020")
        t.alpha_composite(raster([(g, INK["dark"])], square_frame([(g, INK["dark"])]), tile))
        big.alpha_composite(t, ((i % 3) * (tile + 12), (i // 3) * (tile + 12)))
        d.text(((i % 3) * (tile + 12) + 8, (i // 3) * (tile + 12) + 6), label.split("\n")[0][:3], fill="#888", font=font)
    big.save(HERE / "mono2_large.png")
