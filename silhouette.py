import json, sys
from pathlib import Path
from shapely.geometry import Polygon
from shapely.ops import unary_union
from PIL import Image, ImageDraw

here = Path(__file__).parent
meshes = json.load(open(here / "vanguard_tris.json"))
keep = {"Hull", "Canopy", "Cores"}
planes = {"xy": (0, 1), "xz": (0, 2), "yz": (1, 2)}
for name, (a, b) in planes.items():
    polys = []
    for m in meshes:
        if m["name"] not in keep:
            continue
        v = m["verts"]
        for t in m["tris"]:
            p = Polygon([(v[i][a], v[i][b]) for i in t])
            if p.area > 1e-9:
                polys.append(p)
    u = unary_union(polys).buffer(0)
    print(name, u.geom_type, [round(x, 3) for x in u.bounds], round(u.area, 3))
    minx, miny, maxx, maxy = u.bounds
    s = 480 / max(maxx - minx, maxy - miny)
    img = Image.new("L", (512, 512), 0)
    d = ImageDraw.Draw(img)
    geoms = getattr(u, "geoms", [u])
    for g in geoms:
        d.polygon([(16 + (x - minx) * s, 496 - (y - miny) * s) for x, y in g.exterior.coords], fill=255)
        for h in g.interiors:
            d.polygon([(16 + (x - minx) * s, 496 - (y - miny) * s) for x, y in h.coords], fill=0)
    img.save(here / f"sil_{name}.png")
    (here / f"sil_{name}.wkt").write_text(u.wkt)
