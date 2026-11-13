"""Round 5: canopy cut-out from the real Canopy mesh, planetary rings in place of the swoosh."""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely import wkt
from shapely.geometry import Point, Polygon
from shapely.affinity import rotate, scale
from shapely.ops import unary_union
from build import ship_px, disc, rim, CX, CY, R, HOLE, BODY, AMBER, square_frame, raster, svg

HERE = Path(__file__).parent
CANOPY = wkt.loads((HERE / "canopy.wkt").read_text())


def ellipse(rx, ry, tilt):
    return rotate(scale(Point(CX, CY).buffer(1, 512), rx, ry, origin=(CX, CY)), tilt, origin=(CX, CY))


def ring(rx, ratio, tilt, width, bands=((0.0, 1.0),)):
    """Annulus in the planet's plane seen at an angle: concentric ellipses sharing one aspect ratio.
    bands are (start, end) fractions across the ring's width, so (0,.55),(.68,1) leaves a gap."""
    parts = []
    for a, b in bands:
        r_out, r_in = rx + width * (1 - a) - width, rx + width * (1 - a) - width  # placeholder, replaced below
        outer_r = rx - width * a
        inner_r = rx - width * b
        parts.append(ellipse(outer_r, outer_r * ratio, tilt).difference(ellipse(inner_r, inner_r * ratio, tilt)))
    g = unary_union(parts)
    # front half: the side of the ring's major axis nearer the viewer (below it on screen)
    t = math.radians(tilt)
    big = 4000
    ux, uy = math.cos(t), math.sin(t)          # major axis direction
    nx, ny = -uy, ux                           # points "down" on screen for small tilts
    half = Polygon([(CX - ux * big, CY - uy * big), (CX + ux * big, CY + uy * big),
                    (CX + ux * big + nx * big, CY + uy * big + ny * big), (CX - ux * big + nx * big, CY - uy * big + ny * big)])
    front = g.intersection(half)
    return front, g.difference(front)


def compose(rg, cockpit=True, canopy_scale=1.0):
    front, back = rg
    s = ship_px
    if cockpit:
        c = CANOPY if canopy_scale == 1.0 else scale(CANOPY, canopy_scale, canopy_scale, origin=CANOPY.centroid)
        s = s.difference(c)
    return [(back, AMBER), (disc, BODY), (rim, AMBER), (s, HOLE), (front, AMBER)]


V = [
    ("thin ring, steep tilt", ring(560, 0.20, -20, 34)),
    ("wide ring, steep tilt", ring(590, 0.20, -20, 70)),
    ("wide ring with a gap (two bands)", ring(600, 0.20, -20, 80, bands=((0, 0.55), (0.70, 1.0)))),
    ("flatter tilt, wide ring", ring(590, 0.20, -12, 70)),
    ("more open ring (ratio 0.28)", ring(580, 0.28, -18, 60)),
    ("wide ring, canopy cut-out 1.3x", ring(590, 0.20, -20, 70), 1.3),
]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    BACK = {"dark": "#202020", "light": "#F3F3F3"}
    rows = []
    for i, v in enumerate(V):
        label, rg = v[0], v[1]
        layers = compose(rg, canopy_scale=v[2] if len(v) > 2 else 1.0)
        frame = square_frame(layers)
        svg(layers, frame, HERE / f"ring_{i}.svg")
        tiles = []
        for bg in BACK.values():
            tiles += [(bg, raster(layers, frame, 256), 256), (bg, raster(layers, frame, 64), 64),
                      (bg, raster(layers, frame, 32).resize((128, 128), Image.NEAREST), 128),
                      (bg, raster(layers, frame, 16).resize((64, 64), Image.NEAREST), 64)]
        pad = 12
        W = 220 + sum(t[2] + pad for t in tiles)
        c = Image.new("RGBA", (W, 280), "#16181D")
        ImageDraw.Draw(c).multiline_text((pad, pad), f"{i}  " + label.replace(", ", ",\n"), fill="#D8DCE6", font=font)
        x = 220
        for bg, t, s in tiles:
            b = Image.new("RGBA", (s, s), bg); b.alpha_composite(t)
            c.alpha_composite(b, (x, pad + (256 - s) // 2)); x += s + pad
        rows.append(c)
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), "Sheet 7 - planetary rings + canopy from the mesh. Per backdrop: 256, 64, then 32 and 16 at 4x", fill="#FFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y)); y += r.height
    out.convert("RGB").save(HERE / "sheet7_rings.png")
