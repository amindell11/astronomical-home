"""Round-3 sketches: different ways to make the A, zoomed out, nose stays the hole colour."""
import math
from pathlib import Path
from shapely.geometry import Point, Polygon, LineString
from shapely.affinity import rotate, scale
from PIL import Image, ImageDraw, ImageFont
from sketch2 import ship, fill, SS

HERE = Path(__file__).parent
PAL = dict(hole="#0B1024", body="#2B6F8F", rim="#F2A33A", amber="#F2A33A")
BACK = {"dark": "#202020", "light": "#F3F3F3"}


def bez(pts, u):
    n = len(pts) - 1
    x = y = 0.0
    for i, (px, py) in enumerate(pts):
        b = math.comb(n, i) * (1 - u) ** (n - i) * u ** i
        x += b * px
        y += b * py
    return x, y


def stroke(pts, w0, w1, n=120, ease=1.0):
    """Bezier ribbon whose width runs from w0 to w1."""
    top, bot = [], []
    for i in range(n + 1):
        u = i / n
        x, y = bez(pts, u)
        x2, y2 = bez(pts, min(1, u + 1e-3)) if u < 1 else bez(pts, u)
        x1, y1 = bez(pts, max(0, u - 1e-3))
        dx, dy = x2 - x1, y2 - y1
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        w = (w0 + (w1 - w0) * u ** ease) / 2 + 0.001
        top.append((x + nx * w, y + ny * w))
        bot.append((x - nx * w, y - ny * w))
    return Polygon(top + bot[::-1]).buffer(0)


def base(v):
    R, C = v.get("r", 0.44), (0.5, 0.5)
    disc = Point(*C).buffer(R, 256)
    h = v.get("h", 0.60)
    sy = v.get("sy", 0.5)
    sh = ship(h, 0.5, sy, close=v.get("close", 0.12))
    nose = sy - h / 2
    return disc, sh, nose, h, C, R


def paint(disc, C, R, light=0.045):
    img = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    fill(img, disc, PAL["body"])
    fill(img, disc.difference(Point(C[0] + light * 0.6, C[1] + light * 0.8).buffer(R, 256)), PAL["rim"])
    return img


def v_bar(v):
    disc, sh, nose, h, C, R = base(v)
    yc = nose + v.get("at", 0.58) * h
    img = paint(disc, C, R)
    fill(img, sh, PAL["hole"])
    pts = [(0.5 - v.get("left", 0.26), yc + 0.05), (0.5, yc - 0.02), (0.5 + v.get("right", 0.46), yc - 0.11)]
    fill(img, stroke(pts, v.get("w0", 0.012), v.get("w1", 0.07)), PAL["amber"])
    return img


def v_nike(v):
    disc, sh, nose, h, C, R = base(v)
    yc = nose + v.get("at", 0.58) * h
    img = paint(disc, C, R)
    fill(img, sh, PAL["hole"])
    pts = [(0.27, yc - 0.03), (0.36, yc + 0.07), (0.55, yc + 0.03), (0.86, yc - 0.13)]
    fill(img, stroke(pts, 0.01, 0.075, ease=0.7), PAL["amber"])
    return img


def v_ring(v):
    """Full tilted ring: back arc hidden by the planet, front arc crosses the ship."""
    disc, sh, nose, h, C, R = base(dict(v, r=0.36))
    disc = Point(*C).buffer(0.36, 256)
    yc = nose + v.get("at", 0.58) * h
    rx, ry, tilt = 0.48, 0.12, -14
    ring_out = rotate(scale(Point(0.5, yc - ry + 0.01).buffer(1, 256), rx, ry), tilt)
    ring_in = rotate(scale(Point(0.5, yc - ry + 0.01).buffer(1, 256), rx - 0.045, ry - 0.03), tilt)
    ring = ring_out.difference(ring_in)
    cy = ring_out.centroid.y
    front = ring.difference(Polygon([(0, 0), (1, 0), (1, cy), (0, cy)]).buffer(0))
    back = ring.difference(front)
    img = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    fill(img, back.difference(disc), PAL["amber"])
    img2 = paint(disc, C, 0.36)
    img.alpha_composite(img2)
    fill(img, sh.intersection(disc), PAL["hole"])
    fill(img, front, PAL["amber"])
    return img


def v_negative_a(v):
    """Whole A is one cut-out: ship plus a hole-coloured crossbar."""
    disc, sh, nose, h, C, R = base(v)
    yc = nose + v.get("at", 0.6) * h
    pts = [(0.5 - 0.30, yc + 0.06), (0.5, yc - 0.01), (0.5 + 0.30, yc - 0.06)]
    bar = stroke(pts, 0.02, 0.055).intersection(disc.buffer(-0.035))
    img = paint(disc, C, R)
    fill(img, sh.union(bar), PAL["hole"])
    return img


def v_legs(v):
    """Ship is the apex; amber contrails from the fin tips are the legs, swoosh the crossbar."""
    disc, sh, nose, h, C, R = base(dict(v, h=0.40, sy=0.36))
    img = paint(disc, C, R)
    fill(img, sh, PAL["hole"])
    _, _, maxx, maxy = sh.bounds
    lx = 1 - maxx
    for side in (-1, 1):
        x0 = 0.5 + side * (0.5 - lx) * 0.85
        fill(img, stroke([(x0, maxy - 0.03), (0.5 + side * 0.27, 0.86)], 0.035, 0.0), PAL["hole"])
    yc = 0.66
    fill(img, stroke([(0.27, yc + 0.04), (0.5, yc - 0.0), (0.88, yc - 0.10)], 0.012, 0.065), PAL["amber"])
    return img


def v_ring_negative(v):
    """Ring cut through the planet as a dark band; amber where it leaves the disc."""
    disc, sh, nose, h, C, R = base(v)
    yc = nose + v.get("at", 0.58) * h
    band = stroke([(0.0, yc + 0.10), (0.5, yc + 0.005), (1.0, yc - 0.11)], 0.03, 0.03)
    img = paint(disc, C, R)
    fill(img, sh.union(band.intersection(disc)), PAL["hole"])
    fill(img, band.difference(disc), PAL["amber"])
    return img


VARIANTS = [
    ("1  blade crossbar at 58%,\nthin left, thick right,\nexits rim right only", v_bar, {}),
    ("2  same, crossbar higher (52%)\nand shorter", v_bar, dict(at=0.52, right=0.40, left=0.22)),
    ("3  nike hook crossbar", v_nike, {}),
    ("4  full tilted ring,\nfront arc is the crossbar", v_ring, {}),
    ("5  all-negative A:\nship + dark crossbar\none cut-out", v_negative_a, {}),
    ("6  dark ring band through\nthe planet, amber outside", v_ring_negative, {}),
    ("7  small ship as the apex,\ndark contrails as legs,\namber crossbar", v_legs, {}),
    ("8  #1 but nose kisses the rim\n(bigger ship)", v_bar, dict(h=0.72, sy=0.50, at=0.56)),
]


def row(img, label, font):
    pad = 12
    tiles = []
    for bg in BACK.values():
        tiles += [(bg, img.resize((256, 256), Image.LANCZOS), 256),
                  (bg, img.resize((32, 32), Image.LANCZOS).resize((128, 128), Image.NEAREST), 128),
                  (bg, img.resize((16, 16), Image.LANCZOS).resize((64, 64), Image.NEAREST), 64)]
    w = 230 + sum(t[2] + pad for t in tiles)
    c = Image.new("RGBA", (w, 256 + pad * 2), "#16181D")
    ImageDraw.Draw(c).multiline_text((pad, pad), label, fill="#D8DCE6", font=font)
    x = 230
    for bg, t, s in tiles:
        b = Image.new("RGBA", (s, s), bg)
        b.alpha_composite(t)
        c.alpha_composite(b, (x, pad + (256 - s) // 2))
        x += s + pad
    return c


if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    rows = [row(fn(kw), label, font) for label, fn, kw in VARIANTS]
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), "Sheet 5 - A approaches. Per backdrop: 256, then 32 and 16 magnified 4x", fill="#FFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y))
        y += r.height
    out.convert("RGB").save(HERE / "sheet5_a_approaches.png")
