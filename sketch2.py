"""Round-2 sketches: disc icon, negative Vanguard with nose breaking the rim, swoosh ring crossbar."""
import math
from pathlib import Path
from shapely import wkt
from shapely.affinity import scale, translate, rotate
from shapely.geometry import Point, Polygon
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).parent
SS = 1024
PAL = dict(hole="#0B1024", body="#2B6F8F", rim="#F2A33A", swoosh="#F2A33A")
BACKDROPS = {"dark taskbar": "#202020", "light taskbar": "#F3F3F3"}
SIZES = [256, 64, 32, 16]


def ship(height, cx, cy, close=0.0, angle=0.0):
    g = wkt.loads((HERE / "sil_xy.wkt").read_text()).simplify(0.004)
    minx, miny, maxx, maxy = g.bounds
    g = translate(g, -(minx + maxx) / 2, -(miny + maxy) / 2)
    if close:  # morphological closing fills the gaps between the inner tines
        g = g.buffer(close, join_style=2).buffer(-close, join_style=2)
    s = height / (maxy - miny)
    g = scale(g, s, -s, origin=(0, 0))
    g = rotate(g, angle, origin=(0, 0))
    return translate(g, cx, cy)


def swoosh(cx, cy, rx, ry, tilt, a0, a1, wmax, thick_end="start", n=160):
    """Tapered arc of a tilted ellipse: thick at one end, a hairline at the other."""
    top, bot = [], []
    ct, st = math.cos(math.radians(tilt)), math.sin(math.radians(tilt))
    for i in range(n + 1):
        u = i / n
        a = math.radians(a0 + (a1 - a0) * u)
        x, y = rx * math.cos(a), ry * math.sin(a)
        dx, dy = -rx * math.sin(a), ry * math.cos(a)
        L = math.hypot(dx, dy)
        nx, ny = -dy / L, dx / L
        k = (1 - u) if thick_end == "start" else u
        w = wmax * math.sin(math.pi * 0.5 * k) ** 0.8 + 0.002
        for side, arr in ((1, top), (-1, bot)):
            px, py = x + side * nx * w / 2, y + side * ny * w / 2
            arr.append((cx + px * ct - py * st, cy + px * st + py * ct))
    return Polygon(top + bot[::-1]).buffer(0)


def fill(img, g, color):
    mask = Image.new("L", img.size, 0)
    md = ImageDraw.Draw(mask)
    for p in getattr(g, "geoms", [g]):
        if p.is_empty or p.geom_type != "Polygon":
            continue
        md.polygon([(x * SS, y * SS) for x, y in p.exterior.coords], fill=255)
        for h in p.interiors:
            md.polygon([(x * SS, y * SS) for x, y in h.coords], fill=0)
    layer = Image.new("RGBA", img.size, color)
    img.paste(layer, (0, 0), mask)


def render(v):
    R = v.get("r", 0.40)
    C = (0.5, v.get("cy", 0.54))
    disc = Point(*C).buffer(R, 256)
    sh = ship(v.get("h", 0.80), 0.5, v.get("sy", 0.47), close=v.get("close", 0.0))
    img = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    fill(img, disc, PAL["body"])
    lit = v.get("light", 0.05)
    if lit:
        fill(img, disc.difference(Point(C[0] + lit * 0.6, C[1] + lit * 0.8).buffer(R, 256)), PAL["rim"])
    fill(img, sh.intersection(disc), PAL["hole"])
    fill(img, sh.difference(disc), PAL["rim"])  # nose past the rim flips to the warm colour
    sw = v.get("swoosh")
    if sw:
        g = swoosh(C[0], C[1], **sw)
        gap = v.get("gap", 0.0)
        if gap:  # knock the swoosh out of a thin halo so it separates from what it crosses
            fill(img, g.buffer(gap).intersection(disc), PAL["body"])
            fill(img, g.buffer(gap).intersection(sh).intersection(disc), PAL["hole"])
        fill(img, g, PAL["swoosh"])
    return img


def finish(img, size):
    return img.resize((size, size), Image.LANCZOS)


def row(img, label, font):
    pad = 12
    tiles = []
    for name, bg in BACKDROPS.items():
        for s in SIZES:
            tiles.append((bg, finish(img, s), s, 1))
        for s in (32, 16):
            tiles.append((bg, finish(img, s).resize((s * 4, s * 4), Image.NEAREST), s * 4, 0))
    w = 200 + sum(t[2] + pad for t in tiles)
    canvas = Image.new("RGBA", (w, 256 + pad * 2), "#16181D")
    ImageDraw.Draw(canvas).multiline_text((pad, pad), label, fill="#D8DCE6", font=font)
    x = 200
    for bg, t, s, _ in tiles:
        box = Image.new("RGBA", (s, s), bg)
        box.alpha_composite(t)
        canvas.alpha_composite(box, (x, pad + (256 - s) // 2))
        x += s + pad
    return canvas


def sheet(variants, path, title, font):
    rows = [row(render(v), v["label"], font) for v in variants]
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), title, fill="#FFFFFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y))
        y += r.height
    out.convert("RGB").save(path)


SW_A = dict(rx=0.50, ry=0.14, tilt=-18, a0=200, a1=355, wmax=0.075, thick_end="start")
SW_B = dict(rx=0.50, ry=0.15, tilt=-18, a0=10, a1=175, wmax=0.075, thick_end="end")
SW_C = dict(rx=0.47, ry=0.12, tilt=-22, a0=20, a1=190, wmax=0.10, thick_end="end")

VARIANTS = [
    dict(label="A  tines as-is\nno swoosh", close=0.0),
    dict(label="B  tines merged\nno swoosh", close=0.06),
    dict(label="C  tines merged\nswoosh in front,\nthick at left", close=0.06, swoosh=SW_B, gap=0.018),
    dict(label="D  tines merged\nswoosh in front,\nthick at right", close=0.06, swoosh=dict(SW_B, a0=5, a1=170, thick_end="start", tilt=18), gap=0.018),
    dict(label="E  tines fully merged\nheavier swoosh,\ncrossbar low", close=0.12, swoosh=SW_C, gap=0.018, sy=0.45),
    dict(label="F  tines merged\nswoosh, no gap", close=0.06, swoosh=SW_B),
]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    sheet(VARIANTS, HERE / "sheet4_disc_swoosh.png",
          "Sheet 4 - disc icon, transparent outside. Per backdrop: 256 / 64 / 32 / 16 real size, then 32 and 16 at 4x", font)
