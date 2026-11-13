"""Round 6: stylized rings (front-heavy, tapered sides) and a crescent canopy."""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Point, Polygon
from shapely.affinity import rotate, scale, translate
from build import ship_px, disc, rim, CX, CY, HOLE, BODY, AMBER, square_frame, raster, svg
from build2 import CANOPY

HERE = Path(__file__).parent

RX, RY, TILT = 590, 118, -20  # ring outer ellipse, as in the ring you liked


def ell(cx, cy, rx, ry):
    return scale(Point(cx, cy).buffer(1, 720), rx, ry, origin=(cx, cy))


def stylized_ring(front=55, back=18, side=0, tilt=TILT, rx=RX, ry=RY, gap=None):
    """Band between an outer ellipse and an inner one shrunk and shifted back:
    `front`/`back` are band thickness on the near/far side, `side` at the ends (0 = pointed)."""
    outer = ell(CX, CY, rx, ry)
    iry = ry - (front + back) / 2
    inner = ell(CX, CY - (front - back) / 2, rx - side, iry)
    band = outer.difference(inner)
    if gap:  # (position 0..1 across the band, width as a fraction) cut a Cassini-style gap
        pos, w = gap
        def mid(t):
            return ell(CX, CY - (front - back) / 2 * t, rx - side * t, ry - (ry - iry) * t)
        band = band.difference(mid(pos - w / 2).difference(mid(pos + w / 2)))
    band = rotate(band, tilt, origin=(CX, CY))
    t = math.radians(tilt)
    ux, uy = math.cos(t), math.sin(t)
    nx, ny = -uy, ux
    big = 5000
    half = Polygon([(CX - ux * big, CY - uy * big), (CX + ux * big, CY + uy * big),
                    (CX + ux * big + nx * big, CY + uy * big + ny * big), (CX - ux * big + nx * big, CY - uy * big + ny * big)])
    front_g = band.intersection(half)
    return front_g, band.difference(front_g)


def crescent_canopy(offset=0.32, side="left", fill=None):
    """Lit sliver of the canopy: the canopy minus itself shifted away from the light."""
    minx, miny, maxx, maxy = CANOPY.bounds
    d = (maxx - minx) * offset
    dx = d if side == "left" else -d
    return CANOPY.difference(translate(CANOPY, dx, d * 0.35)), fill


def compose(rg, cp):
    front, back = rg
    glass, glass_fill = cp
    layers = [(back, AMBER), (disc, BODY), (rim, AMBER)]
    if glass_fill:
        layers += [(ship_px, HOLE), (glass, glass_fill)]
    else:
        layers += [(ship_px.difference(glass), HOLE)]
    return layers + [(front, AMBER)]


V = [
    ("uniform, thicker", stylized_ring(front=48, back=48, side=48), crescent_canopy()),
    ("front-heavy, pointed sides", stylized_ring(front=60, back=30, side=0), crescent_canopy()),
    ("heavier front, pointed sides", stylized_ring(front=80, back=34, side=0), crescent_canopy()),
    ("front-heavy + gap", stylized_ring(front=80, back=34, side=0, gap=(0.62, 0.12)), crescent_canopy()),
    ("front-heavy, rounded sides", stylized_ring(front=70, back=32, side=14), crescent_canopy()),
    ("#2 + thinner crescent", stylized_ring(front=80, back=34, side=0), crescent_canopy(offset=0.22)),
    ("#2 + crescent filled cyan", stylized_ring(front=80, back=34, side=0), crescent_canopy(fill="#5FD4E8")),
    ("#2 + crescent filled amber", stylized_ring(front=80, back=34, side=0), crescent_canopy(fill=AMBER)),
]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    BACK = {"dark": "#202020", "light": "#F3F3F3"}
    rows = []
    for i, (label, rg, cp) in enumerate(V):
        layers = compose(rg, cp)
        frame = square_frame(layers)
        svg(layers, frame, HERE / f"r6_{i}.svg")
        tiles = []
        for bg in BACK.values():
            tiles += [(bg, raster(layers, frame, 256), 256), (bg, raster(layers, frame, 64), 64),
                      (bg, raster(layers, frame, 32).resize((128, 128), Image.NEAREST), 128),
                      (bg, raster(layers, frame, 16).resize((64, 64), Image.NEAREST), 64)]
        pad = 12
        W = 220 + sum(t[2] + pad for t in tiles)
        c = Image.new("RGBA", (W, 280), "#16181D")
        ImageDraw.Draw(c).multiline_text((pad, pad), f"{i}  " + label.replace(", ", ",\n").replace(" + ", "\n+ "), fill="#D8DCE6", font=font)
        x = 220
        for bg, t, s in tiles:
            b = Image.new("RGBA", (s, s), bg); b.alpha_composite(t)
            c.alpha_composite(b, (x, pad + (256 - s) // 2)); x += s + pad
        rows.append(c)
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), "Sheet 8 - stylized rings, crescent canopy. Per backdrop: 256, 64, then 32 and 16 at 4x", fill="#FFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y)); y += r.height
    out.convert("RGB").save(HERE / "sheet8_stylized.png")
    # large close-up of the canopy for row 2
    layers = compose(V[2][1], V[2][2])
    raster(layers, square_frame(layers), 768).save(HERE / "r6_2_large.png")
