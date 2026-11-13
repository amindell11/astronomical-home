"""Project Vanguard's meshes into the owner's canvas: a shaded details layer and the canopy outline."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from shapely.geometry import Polygon
from shapely.ops import unary_union

HERE = Path(__file__).parent
MESHES = {m["name"]: m for m in json.load(open(HERE / "vanguard_tris.json"))}
META = json.load(open(HERE / "owner" / "layers.json"))
SHIP = next(l for l in META["layers"] if l["name"] == "ship - original tines")
SIL = [Polygon([(v[0], v[1]) for v in (MESHES[n]["verts"][i] for i in t)]) for n in ("Hull", "Canopy", "Cores") for t in MESHES[n]["tris"][:1]]
BX0, BY0, BX1, BY1 = -0.649, -1.001, 0.649, 0.999
KX = SHIP["w"] / (BX1 - BX0)
KY = SHIP["h"] / (BY1 - BY0)


def to_canvas(x, y):
    return SHIP["x"] + (x - BX0) * KX, SHIP["y"] + (BY1 - y) * KY


COLORS = {"Hull": (200, 204, 212), "Canopy": (90, 200, 235), "Cores": (255, 170, 60), "Ink": (20, 20, 28)}


def details_layer(size):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tris = []
    light = np.array([-0.4, 0.5, 0.77])
    for name in ("Hull", "Canopy", "Cores", "Ink"):
        v = np.array(MESHES[name]["verts"])
        for t in MESHES[name]["tris"]:
            p = v[t]
            n = np.cross(p[1] - p[0], p[2] - p[0])
            ln = np.linalg.norm(n)
            if ln == 0:
                continue
            n /= ln
            if n[2] <= 0:  # back-facing from above
                continue
            tris.append((p[:, 2].mean(), name, p, max(0.35, float(n @ light))))
    tris.sort(key=lambda t: t[0])
    for _, name, p, sh in tris:
        c = COLORS[name]
        col = c if name == "Ink" else tuple(int(ch * sh) for ch in c)
        d.polygon([to_canvas(x, y) for x, y, _ in p], fill=col + (255,))
    return img


def canopy_poly():
    v = MESHES["Canopy"]["verts"]
    polys = [Polygon([to_canvas(v[i][0], v[i][1]) for i in t]) for t in MESHES["Canopy"]["tris"]]
    return unary_union([p for p in polys if p.area > 1e-6]).buffer(0.5).buffer(-0.5)


if __name__ == "__main__":
    img = details_layer((META["w"], META["h"]))
    img.save(HERE / "owner_details.png")
    g = canopy_poly()
    print(g.geom_type, [round(b) for b in g.bounds], round(g.area))
    (HERE / "canopy.wkt").write_text(g.wkt)
