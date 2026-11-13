"""Round 12: the owner's edited ship + canopy, traced to clean vectors; colour and mono regenerated."""
import json
from pathlib import Path
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Polygon
from shapely.ops import unary_union
import build8
from build import square_frame, raster, svg
from build4 import clean, rows_sheet

HERE = Path(__file__).parent
OWN = HERE / "owner2"
META = json.load(open(OWN / "layers.json"))
NAVY_LAYERS = ["SHIP - edit me (navy = ship)", "Unnamed", "Unnamed copy", "Vector Layer", "Vector Layer #1", "Vector Layer #2"]


def alpha_union(names):
    a = np.zeros((META["h"], META["w"]), np.uint8)
    for l in META["layers"]:
        if l["name"] in names and l["visible"]:
            im = np.array(Image.open(OWN / f"{l['i']:02d}.png").convert("RGBA"))[..., 3]
            x, y = l["x"], l["y"]
            h, w = im.shape
            x0, y0 = max(x, 0), max(y, 0)
            sub = im[y0 - y:min(h, META["h"] - y), x0 - x:min(w, META["w"] - x)]
            a[y0:y0 + sub.shape[0], x0:x0 + sub.shape[1]] = np.maximum(a[y0:y0 + sub.shape[0], x0:x0 + sub.shape[1]], sub)
    return a


def trace(a, tol, min_area=300):
    """Contours of the opaque area as polygons, simplified to straight runs."""
    m = (a > 127).astype(np.uint8)
    cs, hier = cv2.findContours(m, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_NONE)
    outers, holes = [], []
    for c, h in zip(cs, hier[0]):
        if len(c) < 3:
            continue
        p = Polygon([(x + 0.5, y + 0.5) for x, y in c[:, 0, :]]).buffer(0)
        if p.area < min_area:
            continue
        (holes if h[3] >= 0 else outers).append(p)
    g = unary_union(outers).difference(unary_union(holes)) if holes else unary_union(outers)
    return g.simplify(tol, preserve_topology=True)


SHIP = trace(alpha_union(NAVY_LAYERS), tol=1.6)
GLASS = trace(alpha_union(["canopy glass"]), tol=0.6, min_area=50).buffer(2, join_style=1).buffer(-2, join_style=1)

build8.ship = SHIP
build8.glass = GLASS
FRONT, BACK = build8.ring(60, 26, 16, lean=14, rx=520)
MONO, COLOUR = build8.designs(FRONT, BACK, 10)

if __name__ == "__main__":
    print("ship vertices:", sum(len(p.exterior.coords) for p in getattr(SHIP, "geoms", [SHIP])), SHIP.geom_type)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    mono_l = {"dark": [(MONO, "#F4EEDF")], "light": [(MONO, "#0B1024")]}
    rows_sheet([("colour", lambda bg: COLOUR), ("mono", lambda bg: mono_l[bg])],
               HERE / "sheet14_final.png", "Sheet 14 - your ship, cleaned. Per backdrop: 256, 64, then 32 and 16 at 4x", font)
    tile = 560
    out = Image.new("RGBA", (tile * 3 + 24, tile), "#16181D")
    for i, (L, bg) in enumerate(((COLOUR, "#202020"), (mono_l["dark"], "#202020"), (mono_l["light"], "#F3F3F3"))):
        t = Image.new("RGBA", (tile, tile), bg)
        t.alpha_composite(raster(L, square_frame(L), tile))
        out.alpha_composite(t, (i * (tile + 12), 0))
    out.save(HERE / "r12_large.png")
    svg(COLOUR, square_frame(COLOUR), HERE / "astronomical_icon.svg")
    svg([(MONO, "#F4EEDF")], square_frame([(MONO, "#F4EEDF")]), HERE / "astronomical_mark_mono.svg")
