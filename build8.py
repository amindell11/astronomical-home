"""Round 11: softer ring crossing — thinner, tapered along its length, narrower halo."""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Point, Polygon
from shapely.affinity import rotate, scale, translate
from build import ship_px, disc, CX, CY, R, HOLE, BODY, AMBER, square_frame, raster
from build3 import RX, RY, TILT
from build4 import GLASS, clean
from build5 import CORE, edge

HERE = Path(__file__).parent
SHIP_DY, RING_DY, LIT = 25, 25, 16
ship = translate(ship_px, 0, SHIP_DY)
glass = translate(GLASS["dagger crescent"], 0, SHIP_DY)


def ell(cx, cy, rx, ry):
    return scale(Point(cx, cy).buffer(1, 720), rx, ry, origin=(cx, cy))


def ring(front, back, side, lean=0.0, rx=RX):
    """Inner ellipse shrunk, shifted back, and shifted sideways by `lean` px so the band is thin at the
    left tip and fuller at the right (positive lean)."""
    ry = rx * RY / RX
    outer = ell(CX, CY, rx, ry)
    inner = ell(CX - lean, CY - (front - back) / 2, rx - side, ry - (front + back) / 2)
    band = rotate(outer.difference(inner), TILT, origin=(CX, CY))
    t = math.radians(TILT)
    ux, uy, big = math.cos(t), math.sin(t), 5000
    nx, ny = -uy, ux
    half = Polygon([(CX - ux * big, CY - uy * big), (CX + ux * big, CY + uy * big),
                    (CX + ux * big + nx * big, CY + uy * big + ny * big), (CX - ux * big + nx * big, CY - uy * big + ny * big)])
    f = band.intersection(half)
    return translate(f, 0, -RING_DY), translate(band.difference(f), 0, -RING_DY)


def designs(front, back, gap):
    planet = disc.difference(CORE.boundary.buffer(14 / 2).intersection(disc.buffer(-14)))
    e = edge(ship, "lit", LIT).difference(disc)
    m = planet.difference(ship).union(e).union(glass.intersection(ship))
    ringall = front.union(back.difference(disc.buffer(gap)))
    m = clean(m.difference(front.buffer(gap)).union(ringall))
    halo = front.buffer(gap).difference(front)
    core = planet.intersection(CORE).difference(ship)
    rimg = planet.difference(CORE).difference(ship)
    col = [(disc.union(ship), HOLE), (clean(core.difference(halo)), BODY), (clean(rimg.difference(halo)), AMBER),
           (clean(e.difference(halo)), AMBER), (clean(glass.intersection(ship).difference(halo)), AMBER),
           (clean(ringall), AMBER)]
    return m, col


V = [
    ("0  current (wide, front 80, halo 14)", ring(80, 34, 18), 14),
    ("1  tight (rx 520), thinner, halo 10", ring(55, 26, 14, rx=520), 10),
    ("2  tight + tapered thin-left/full-right", ring(60, 26, 16, lean=14, rx=520), 10),
    ("3  tighter (rx 490) + tapered, halo 8", ring(52, 22, 12, lean=10, rx=490), 8),
    ("4  tighter + tapered the other way", ring(52, 22, 12, lean=-10, rx=490), 8),
    ("5  wide but thinner + tapered", ring(55, 24, 14, lean=14), 10),
]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    tile = 380
    out = Image.new("RGBA", (tile * 2 + 12, (tile + 12) * len(V)), "#16181D")
    d = ImageDraw.Draw(out)
    for i, (label, (f, b), gap) in enumerate(V):
        m, col = designs(f, b, gap)
        for j, L in enumerate(([(m, "#F4EEDF")], col)):
            t = Image.new("RGBA", (tile, tile), "#202020")
            t.alpha_composite(raster(L, square_frame(L), tile))
            out.alpha_composite(t, (j * (tile + 12), i * (tile + 12)))
        d.text((8, i * (tile + 12) + 6), label, fill="#AAA", font=font)
    out.save(HERE / "r11_soft.png")
