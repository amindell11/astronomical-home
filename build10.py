"""Round 13: mute-icon structure — ship + ring one light colour, one-sided gap where they cross; dark planet, light rim."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.affinity import translate
from shapely.ops import unary_union
from build import disc, square_frame, raster, svg
from build5 import CORE
from build4 import clean, rows_sheet
from build9 import SHIP, GLASS, FRONT, BACK

HERE = Path(__file__).parent


def side_halo(shape, gap, down=True, steps=12):
    """Strip swept off one side of `shape`: below it (down) or above it."""
    s = 1 if down else -1
    swept = unary_union([translate(shape, 0, s * gap * k / steps) for k in range(1, steps + 1)])
    return swept.difference(shape)


def build(gap=12, down=True, light="#F4EEDF", planet="#0B1024", rim_col=None, ring_col=None, glass_col=None):
    rim = disc.difference(CORE)
    halo = side_halo(FRONT, gap, down)
    ship = SHIP.difference(halo)
    rim = rim.difference(SHIP.buffer(gap)).difference(halo)
    back = BACK.difference(disc.buffer(gap))
    glass = GLASS.intersection(SHIP)
    ring = FRONT.union(back)
    layers = [(disc, planet)]
    if rim_col or ring_col:
        layers += [(clean(rim), rim_col or light), (clean(ship.difference(glass)), light),
                   (clean(glass), glass_col or planet), (clean(ring), ring_col or light)]
    else:
        layers += [(clean(unary_union([rim, ship.difference(glass), ring])), light)]
    return layers


V = [
    ("1  gap below the ring\n(ring fuses with the ship above)", dict()),
    ("2  gap above the ring\n(fuses below)", dict(down=False)),
    ("3  gap below, wider (18)", dict(gap=18)),
    ("4  colour: cream ship + ring,\namber rim, navy planet", dict(rim_col="#F2A33A", planet="#122A44")),
    ("5  colour: cream ship,\namber ring + rim", dict(rim_col="#F2A33A", ring_col="#F2A33A", planet="#122A44")),
]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    rows_sheet([(label, (lambda bg, kw=kw: build(**kw))) for label, kw in V],
               HERE / "sheet15_mute.png", "Sheet 15 - mute-icon structure. Per backdrop: 256, 64, then 32 and 16 at 4x", font)
    tile = 420
    out = Image.new("RGBA", (tile * len(V) + 12 * (len(V) - 1), tile), "#16181D")
    for i, (label, kw) in enumerate(V):
        L = build(**kw)
        t = Image.new("RGBA", (tile, tile), "#202020")
        t.alpha_composite(raster(L, square_frame(L), tile))
        out.alpha_composite(t, (i * (tile + 12), 0))
        ImageDraw.Draw(out).text((i * (tile + 12) + 8, 6), label.split("\n")[0][:2], fill="#888", font=font)
    out.save(HERE / "r13_large.png")
    for i, (label, kw) in enumerate(V):
        L = build(**kw)
        svg(L, square_frame(L), HERE / f"mute_{i + 1}.svg")
