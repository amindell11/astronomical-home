"""Round 9: #6 vs the hybrid edge, and a colour version on #6's structure."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.affinity import translate
from build import ship_px, disc, rim, HOLE, BODY, AMBER, square_frame, raster
from build4 import GLASS, GAP, clean, rows_sheet
from build5 import FRONT0, BACK0, CORE, INK, edge

HERE = Path(__file__).parent
RING_DY, SHIP_DY, LIT, HAIR = 25, 25, 16, 4

ship = translate(ship_px, 0, SHIP_DY)
front, back = translate(FRONT0, 0, -RING_DY), translate(BACK0, 0, -RING_DY)
glass = translate(GLASS["dagger crescent"], 0, SHIP_DY)
planet = disc.difference(CORE.boundary.buffer(GAP / 2).intersection(disc.buffer(-GAP)))


def ship_edge(hybrid):
    e = edge(ship, "lit", LIT)
    if hybrid:
        e = e.union(edge(ship, "outline", HAIR))
    return e.difference(disc)


def mono(hybrid):
    g = planet.difference(ship).union(ship_edge(hybrid)).union(glass.intersection(ship))
    ring = front.union(back.difference(disc.buffer(GAP)))  # one continuous band: no halo between its own halves
    g = g.difference(front.buffer(GAP)).union(ring)
    return clean(g)


def colour(hybrid):
    """Same knockout structure; planet blue, rim/ring/edge/glass amber, ship is navy space."""
    core = planet.intersection(CORE).difference(ship)
    rimg = planet.difference(CORE).difference(ship)
    ringf = front
    ringb = back.difference(disc.buffer(GAP))
    gaps = front.buffer(GAP).difference(front)
    return [(disc.union(ship), HOLE),                       # navy base: ship space reads as navy everywhere
            (clean(core.difference(gaps)), BODY),
            (clean(rimg.difference(gaps)), AMBER),
            (clean(ship_edge(hybrid).difference(gaps)), AMBER),
            (clean(glass.intersection(ship).difference(gaps)), AMBER),
            (clean(ringb), AMBER), (ringf, AMBER)]


if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    m6, mh = clean(mono(False)), clean(mono(True))
    entries = [("6  heavy lit edge", lambda bg: [(m6, INK[bg])]),
               ("H  hybrid: heavy lit edge\n+ hairline elsewhere", lambda bg: [(mh, INK[bg])]),
               ("6c colour on #6", lambda bg: colour(False)),
               ("Hc colour on hybrid", lambda bg: colour(True))]
    rows_sheet(entries, HERE / "sheet12.png", "Sheet 12 - #6 vs hybrid, mono and colour. Per backdrop: 256, 64, then 32 and 16 at 4x", font)
    tile = 400
    big = Image.new("RGBA", (tile * 4 + 36, tile), "#16181D")
    for i, (_, fn) in enumerate(entries):
        t = Image.new("RGBA", (tile, tile), "#202020")
        layers = fn("dark")
        t.alpha_composite(raster(layers, square_frame(layers), tile))
        big.alpha_composite(t, (i * (tile + 12), 0))
    big.save(HERE / "r9_large.png")
