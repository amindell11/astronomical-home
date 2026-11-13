"""Round-1 icon sketches: Vanguard as a negative silhouette over a filled body."""
import math, random
from pathlib import Path
from shapely import wkt
from shapely.affinity import scale, translate, rotate
from shapely.geometry import Point, Polygon, box
from PIL import Image, ImageDraw, ImageChops, ImageFont

HERE = Path(__file__).parent
SS = 1024  # supersample canvas

PAIRS = {
    "amber/cyan": dict(bg="#0B1024", body="#2B6F8F", body_dark="#173E5A", rim="#F2A33A", glow="#5FD4E8", ink="#060914"),
    "white+amber/violet": dict(bg="#0D0B22", body="#5B4FB8", body_dark="#2E2766", rim="#F4EEDF", glow="#8F7BFF", ink="#07061A"),
    "coral/teal": dict(bg="#071A1E", body="#1F8F86", body_dark="#0F4E4C", rim="#FF7A4D", glow="#2EC4B6", ink="#030C0E"),
    "gold/ice": dict(bg="#0A1226", body="#4E7FB8", body_dark="#253F66", rim="#F5C451", glow="#9FC8FF", ink="#050A16"),
}


def ship(height, cx, cy, angle=0.0, simplify=0.004):
    g = wkt.loads((HERE / "sil_xy.wkt").read_text()).simplify(simplify)
    minx, miny, maxx, maxy = g.bounds
    g = translate(g, -(minx + maxx) / 2, -(miny + maxy) / 2)
    s = height / (maxy - miny)
    g = scale(g, s, -s, origin=(0, 0))  # flip y: nose up in image space
    g = rotate(g, angle, origin=(0, 0))
    return translate(g, cx, cy)


def asteroid(cx, cy, r, seed=3, n=9):
    rnd = random.Random(seed)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n + rnd.uniform(-0.18, 0.18)
        rr = r * rnd.uniform(0.82, 1.05)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return Polygon(pts)


def facets(poly, light_dir=(-0.7, -0.7)):
    """Split a rock into a lit and a shadow plane along a line through its centroid."""
    c = poly.centroid
    lx, ly = light_dir
    nx, ny = -ly, lx
    big = 2.0
    half = Polygon([(c.x + nx * big, c.y + ny * big), (c.x - nx * big, c.y - ny * big),
                    (c.x - nx * big + lx * big, c.y - ny * big + ly * big),
                    (c.x + nx * big + lx * big, c.y + ny * big + ly * big)])
    lit = poly.intersection(translate(half, lx * 0.04, ly * 0.04))
    return lit, poly.difference(lit)


def crescent(disc_c, r, light=(-0.6, -0.8), width=0.06):
    disc = Point(*disc_c).buffer(r, 128)
    off = Point(disc_c[0] - light[0] * width, disc_c[1] - light[1] * width).buffer(r, 128)
    return disc.difference(off)


def draw(d, g, fill, k=SS):
    for p in getattr(g, "geoms", [g]):
        if p.is_empty or p.geom_type != "Polygon":
            continue
        d.polygon([(x * k, y * k) for x, y in p.exterior.coords], fill=fill)
        for h in p.interiors:
            d.polygon([(x * k, y * k) for x, y in h.coords], fill=None)


def fill_geom(img, g, color):
    mask = Image.new("L", img.size, 0)
    md = ImageDraw.Draw(mask)
    for p in getattr(g, "geoms", [g]):
        if p.is_empty or p.geom_type != "Polygon":
            continue
        md.polygon([(x * SS, y * SS) for x, y in p.exterior.coords], fill=255)
        for h in p.interiors:
            md.polygon([(x * SS, y * SS) for x, y in h.coords], fill=0)
    img.paste(Image.new("RGB", img.size, color), (0, 0), mask)


# --- compositions: each returns layers [(geometry, palette_key)] in paint order, plus the ship hole
def comp_eclipse():
    disc = Point(0.5, 0.5).buffer(0.43, 128)
    sh = ship(0.78, 0.5, 0.5)
    return disc, crescent((0.5, 0.5), 0.43), sh, None


def comp_horizon():
    disc = Point(0.5, 1.12).buffer(0.78, 192)
    sh = ship(0.62, 0.5, 0.62)
    return disc, crescent((0.5, 1.12), 0.78, width=0.035), sh, None


def comp_tilted():
    disc = Point(0.52, 0.55).buffer(0.42, 128)
    sh = ship(0.8, 0.5, 0.5, angle=35)
    return disc, crescent((0.52, 0.55), 0.42), sh, None


def comp_asteroid():
    rock = asteroid(0.5, 0.52, 0.44)
    lit, _ = facets(rock)
    sh = ship(0.74, 0.5, 0.5, angle=20)
    return rock, lit, sh, "rock"


def comp_asteroid_low():
    rock = asteroid(0.52, 0.7, 0.36, seed=11, n=8)
    lit, _ = facets(rock)
    sh = ship(0.7, 0.5, 0.46)
    return rock, lit, sh, "rock"


def comp_ring():
    disc = Point(0.5, 0.5).buffer(0.32, 128)
    ring = Point(0.5, 0.5).buffer(0.46, 128).difference(Point(0.5, 0.5).buffer(0.41, 128))
    ring = scale(ring, 1, 0.38, origin=(0.5, 0.5))
    ring = rotate(ring, -20, origin=(0.5, 0.5))
    sh = ship(0.8, 0.5, 0.5, angle=-20)
    return disc.union(ring), crescent((0.5, 0.5), 0.32), sh, None


COMPS = {
    "eclipse": comp_eclipse,
    "horizon": comp_horizon,
    "tilted": comp_tilted,
    "asteroid": comp_asteroid,
    "asteroid-low": comp_asteroid_low,
    "ring": comp_ring,
}


def render(comp, pal, style="plain", rounded=False):
    body, light, sh, kind = COMPS[comp]()
    img = Image.new("RGB", (SS, SS), pal["bg"])
    shadow = body.difference(light) if kind == "rock" else body
    fill_geom(img, body, pal["body_dark"] if kind == "rock" else pal["body"])
    if kind == "rock":
        fill_geom(img, light, pal["body"])
        fill_geom(img, body.exterior.buffer(0.012).intersection(light.buffer(0.001)), pal["rim"])
    else:
        fill_geom(img, light, pal["rim"])
    hole = sh.intersection(body)
    fill_geom(img, hole, pal["bg"])
    # parts of the ship outside the body stay visible as a thin cool outline
    outside = sh.difference(body)
    if not outside.is_empty:
        fill_geom(img, outside.buffer(0.0).boundary.buffer(0.009).intersection(outside.buffer(0.009)), pal["glow"])
    if style == "inked":
        w = 0.016
        fill_geom(img, body.boundary.buffer(w), pal["ink"])
        fill_geom(img, sh.boundary.buffer(w).intersection(body.buffer(w)), pal["ink"])
    return img, rounded


def finish(img, size, style, rounded, pal):
    im = img.resize((size, size), Image.LANCZOS)
    if style == "hud" and size >= 32:
        r, g, b = im.split()
        o = max(1, size // 128)
        r = ImageChops.offset(r, -o, 0)
        b = ImageChops.offset(b, o, 0)
        im = Image.merge("RGB", (r, g, b))
        sl = Image.new("L", (size, size), 255)
        sd = ImageDraw.Draw(sl)
        step = max(2, size // 64)
        for y in range(0, size, step):
            sd.line([(0, y), (size, y)], fill=170)
        im = ImageChops.multiply(im, Image.merge("RGB", (sl, sl, sl)))
    if rounded:
        m = Image.new("L", (size * 4, size * 4), 0)
        ImageDraw.Draw(m).rounded_rectangle([0, 0, size * 4 - 1, size * 4 - 1], radius=size * 4 * 0.2, fill=255)
        m = m.resize((size, size), Image.LANCZOS)
        out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        out.paste(im, (0, 0), m)
        return out
    return im.convert("RGBA")


SIZES = [256, 64, 32, 16]


def row(img, style, rounded, pal, label, font):
    pad = 16
    w = 220 + sum(SIZES) + pad * 8 + 32 * 4 + 16 * 4
    canvas = Image.new("RGBA", (w, 256 + pad * 2), "#1C1F26")
    d = ImageDraw.Draw(canvas)
    d.text((pad, pad), label, fill="#D8DCE6", font=font)
    x = 220
    for s in SIZES:
        t = finish(img, s, style, rounded, pal)
        canvas.alpha_composite(t, (x, pad + (256 - s) // 2))
        x += s + pad
    for s in (32, 16):  # magnified views of the true small rasters
        t = finish(img, s, style, rounded, pal).resize((s * 4, s * 4), Image.NEAREST)
        canvas.alpha_composite(t, (x, pad + (256 - s * 4) // 2))
        x += s * 4 + pad
    return canvas


def sheet(rows, path, title, font):
    W = max(r.width for r in rows)
    H = sum(r.height for r in rows) + 40
    out = Image.new("RGBA", (W, H), "#1C1F26")
    ImageDraw.Draw(out).text((16, 12), title, fill="#FFFFFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y))
        y += r.height
    out.convert("RGB").save(path)


if __name__ == "__main__":
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    except OSError:
        font = ImageFont.load_default()
    pal = PAIRS["amber/cyan"]
    rows = []
    for c in COMPS:
        img, _ = render(c, pal)
        rows.append(row(img, "plain", False, pal, c, font))
    sheet(rows, HERE / "sheet1_compositions.png",
          "Sheet 1 - compositions (plain, amber/cyan). Columns: 256, 64, 32, 16 at real size, then 32 and 16 magnified 4x", font)

    rows = []
    for st in ("plain", "inked", "hud"):
        for c in ("eclipse", "asteroid"):
            img, _ = render(c, pal, style=st)
            rows.append(row(img, st, False, pal, f"{c} / {st}", font))
    sheet(rows, HERE / "sheet2_styles.png", "Sheet 2 - styles (amber/cyan)", font)

    rows = []
    for name, p in PAIRS.items():
        img, _ = render("eclipse", p)
        rows.append(row(img, "plain", False, p, f"eclipse / {name}", font))
    for name in ("amber/cyan", "coral/teal"):
        img, _ = render("asteroid", PAIRS[name])
        rows.append(row(img, "plain", True, PAIRS[name], f"asteroid / {name} / rounded", font))
    sheet(rows, HERE / "sheet3_colors_framing.png", "Sheet 3 - colour pairs and rounded framing", font)
