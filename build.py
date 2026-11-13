"""Rebuild the owner's GIMP arrangement as vector geometry, add the cockpit cut-out, finish the swoosh."""
import math, json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Point, Polygon, box
from shapely.affinity import scale, translate
from shapely.ops import unary_union
from sketch2 import ship as ship_unit
from sketch3 import bez

HERE = Path(__file__).parent
OWN = HERE / "owner"
META = json.load(open(OWN / "layers.json"))
L = {l["name"]: l for l in META["layers"]}
HOLE, BODY, AMBER = "#0B1024", "#2B6F8F", "#F2A33A"
K = 1024  # original piece canvas


def px(g):
    return scale(g, K, K, origin=(0, 0))


def fit_to(g, layer):
    """Map a piece's original geometry onto the bbox of the owner's layer (scale + move)."""
    minx, miny, maxx, maxy = g.bounds
    sx, sy = layer["w"] / (maxx - minx), layer["h"] / (maxy - miny)
    g = translate(g, -minx, -miny)
    g = scale(g, sx, sy, origin=(0, 0))
    return translate(g, layer["x"], layer["y"]), (minx, miny, sx, sy)


def traced(layer_name, buf=2.0):
    """Polygon of a layer's opaque pixels, in canvas coordinates."""
    import cv2
    l = L[layer_name]
    a = (np.array(Image.open(OWN / f"{l['i']:02d}.png").convert("RGBA"))[..., 3] > 128).astype(np.uint8)
    cs, hier = cv2.findContours(a, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_NONE)
    polys = []
    for c in cs:
        if len(c) >= 3:
            p = Polygon([(x + l["x"] + 0.5, y + l["y"] + 0.5) for x, y in c[:, 0, :]]).buffer(0)
            polys.append(p)
    polys.sort(key=lambda p: -p.area)
    g = polys[0]
    for p in polys[1:]:
        g = g.symmetric_difference(p)
    return g.buffer(buf)


# --- ship: vector edges from the mesh silhouette, clipped to what the owner kept (central spike erased)
ship_vec, _ = fit_to(px(ship_unit(0.60, 0.5, 0.5)), L["ship - original tines"])
ship_px = ship_vec.intersection(traced("ship - original tines"))
ship_px = unary_union([p for p in getattr(ship_px, "geoms", [ship_px]) if p.area > 200]).simplify(1.0)

# --- disc and rim: untouched pieces, only shifted by the canvas growth
dl = L["planet disc"]
R = dl["w"] / 2
CX, CY = dl["x"] + R, dl["y"] + R
disc = Point(CX, CY).buffer(R, 256)
light = 0.045 * K
rim = disc.difference(Point(CX + light * 0.6, CY + light * 0.8).buffer(R, 256))

# --- swoosh: the nike bezier mapped by the owner's scale, so it can be extended along its own curve
NIKE = [(0.27, 0.55), (0.36, 0.65), (0.55, 0.61), (0.86, 0.45)]
W0, W1, EASE = 0.01, 0.075, 0.7


def ribbon(pts, w0, w1, u0=0.0, u1=1.0, ease=EASE, n=240, cap_taper=0.0, sx=1, sy=1):
    """Ribbon along a bezier from u0 to u1 (may extrapolate past 0..1); widths in pre-scale units."""
    top, bot = [], []
    for i in range(n + 1):
        u = u0 + (u1 - u0) * i / n
        x, y = bez(pts, u)
        xa, ya = bez(pts, u - 1e-3)
        xb, yb = bez(pts, u + 1e-3)
        dx, dy = (xb - xa) * sx, (yb - ya) * sy
        Ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / Ln, dx / Ln
        uc = min(max(u, 0), 1)
        w = (w0 + (w1 - w0) * uc ** ease)
        if cap_taper:  # taper to a point over the extended ends
            t = 1.0
            if u < 0:
                t = max(0.0, 1 + u / max(-u0, 1e-6))
            if u > 1:
                t = max(0.0, 1 - (u - 1) / max(u1 - 1, 1e-6))
            w *= t ** cap_taper
        X, Y = x * sx, y * sy
        top.append((X + nx * w * sx / 2, Y + ny * w * sx / 2))
        bot.append((X - nx * w * sx / 2, Y - ny * w * sx / 2))
    return Polygon(top + bot[::-1]).buffer(0)


base = None


def _fit_swoosh():
    """Centreline and width of the owner's swoosh, fitted from its pixels column by column."""
    l = L["crossbar - nike hook"]
    a = np.array(Image.open(OWN / f"{l['i']:02d}.png").convert("RGBA"))[..., 3] > 128
    xs, cs, ws = [], [], []
    for x in range(110, 1100, 4):
        ys = np.where(a[:, x])[0]
        if len(ys):
            xs.append(x + l["x"]); cs.append((ys.min() + ys.max()) / 2 + l["y"]); ws.append(ys.max() - ys.min())
    xs, cs, ws = map(np.array, (xs, cs, ws))
    pc = np.polyfit(xs, cs, 3)
    slope = np.polyval(np.polyder(pc), xs)
    pw = np.polyfit(xs, ws * np.cos(np.arctan(slope)), 1)
    return pc, pw


PC, PW = _fit_swoosh()
SW_X0, SW_X1 = 60.0, 1175.0  # where the owner's swoosh visibly starts and ends


def swoosh(ext0=0.0, ext1=0.0, taper=0.0, cap="round", n=400):
    """Ribbon along the fitted centreline; ext extends each end in px, taper narrows the extension to a point."""
    xa, xb = SW_X0 - ext0, SW_X1 + ext1
    top, bot, ends = [], [], []
    for i in range(n + 1):
        x = xa + (xb - xa) * i / n
        y = np.polyval(PC, x)
        s = np.polyval(np.polyder(PC), x)
        nx, ny = -s / math.hypot(1, s), 1 / math.hypot(1, s)
        w = max(np.polyval(PW, min(max(x, SW_X0), SW_X1)), 1.0)
        if taper and x < SW_X0:
            w *= ((x - xa) / max(ext0, 1e-6)) ** taper
        if taper and x > SW_X1:
            w *= ((xb - x) / max(ext1, 1e-6)) ** taper
        w = max(w, 0.5)
        top.append((x + nx * w / 2, y + ny * w / 2)); bot.append((x - nx * w / 2, y - ny * w / 2))
        if i in (0, n):
            ends.append(Point(x, y).buffer(w / 2, 64))
    g = Polygon(top + bot[::-1]).buffer(0)
    if cap == "round":
        g = unary_union([g, *ends])
    return g


def cockpit(kind, top, bottom, width):
    if kind == "tri":
        return Polygon([(CX_SHIP, top), (CX_SHIP + width / 2, bottom), (CX_SHIP - width / 2, bottom)])
    # canopy teardrop: pointed top, rounded bottom
    pts = []
    h = bottom - top
    for i in range(61):
        t = i / 60 * math.pi
        x = math.sin(t) * width / 2 * (math.sin(t / 2) ** 0.6)
        y = top + h * (1 - math.cos(t)) / 2
        pts.append((CX_SHIP + x, y))
    return Polygon(pts + [(2 * CX_SHIP - x, y) for x, y in reversed(pts)]).buffer(0)


CX_SHIP = (ship_px.bounds[0] + ship_px.bounds[2]) / 2


def compose(sw, cp):
    s = ship_px.difference(cp) if cp is not None else ship_px
    return [(disc, BODY), (rim, AMBER), (s, HOLE), (sw, AMBER)]


def square_frame(layers, margin=0.03):
    u = unary_union([g for g, _ in layers])
    minx, miny, maxx, maxy = u.bounds
    side = max(maxx - minx, maxy - miny) * (1 + 2 * margin)
    cx, cy = (minx + maxx) / 2, (miny + maxy) / 2
    return cx - side / 2, cy - side / 2, side


def raster(layers, frame, size, ss=4):
    x0, y0, side = frame
    S = size * ss
    k = S / side
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for g, col in layers:
        m = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(m)
        for p in getattr(g, "geoms", [g]):
            if p.geom_type != "Polygon" or p.is_empty:
                continue
            d.polygon([((x - x0) * k, (y - y0) * k) for x, y in p.exterior.coords], fill=255)
            for h in p.interiors:
                d.polygon([((x - x0) * k, (y - y0) * k) for x, y in h.coords], fill=0)
        img.paste(Image.new("RGBA", (S, S), col), (0, 0), m)
    return img.resize((size, size), Image.LANCZOS)


def svg(layers, frame, path):
    x0, y0, side = frame
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">']
    k = 512 / side
    for g, col in layers:
        d = []
        for p in getattr(g, "geoms", [g]):
            if p.geom_type != "Polygon" or p.is_empty:
                continue
            for ring in [p.exterior, *p.interiors]:
                c = list(ring.coords)
                d.append("M" + " L".join(f"{(x - x0) * k:.2f} {(y - y0) * k:.2f}" for x, y in c) + " Z")
        out.append(f'  <path fill="{col}" fill-rule="evenodd" d="{" ".join(d)}"/>')
    out.append("</svg>")
    Path(path).write_text("\n".join(out))


SHIP_TOP = ship_px.bounds[1]
VARIANTS = {
    "as you left it (rebuilt as vectors)": dict(sw=dict(cap="butt"), cp=None),
    "teardrop cockpit + round caps": dict(sw=dict(), cp=("drop", 0.17, 0.40, 0.07)),
    "triangle A-counter + round caps": dict(sw=dict(), cp=("tri", 0.17, 0.42, 0.09)),
    "teardrop + ends extended, tapering to rounded points": dict(sw=dict(ext0=60, ext1=90, taper=0.7), cp=("drop", 0.17, 0.40, 0.07)),
    "triangle + ends extended, tapering": dict(sw=dict(ext0=60, ext1=90, taper=0.7), cp=("tri", 0.17, 0.42, 0.09)),
}


def build(v):
    sw = swoosh(**v["sw"])
    cp = None
    if v["cp"]:
        kind, t, b, w = v["cp"]
        H = ship_px.bounds[3] - SHIP_TOP
        cp = cockpit(kind, SHIP_TOP + t * H, SHIP_TOP + b * H, w * H)
    return compose(sw, cp)


if __name__ == "__main__":
    # check the rebuild against the owner's composite
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    BACK = {"dark": "#202020", "light": "#F3F3F3"}
    rows = []
    for i, (label, v) in enumerate(VARIANTS.items()):
        layers = build(v)
        frame = square_frame(layers)
        svg(layers, frame, HERE / f"candidate_{i}.svg")
        tiles = []
        for bg in BACK.values():
            tiles += [(bg, raster(layers, frame, 256), 256),
                      (bg, raster(layers, frame, 64), 64),
                      (bg, raster(layers, frame, 32).resize((128, 128), Image.NEAREST), 128),
                      (bg, raster(layers, frame, 16).resize((64, 64), Image.NEAREST), 64)]
        pad = 12
        W = 220 + sum(t[2] + pad for t in tiles)
        c = Image.new("RGBA", (W, 280), "#16181D")
        ImageDraw.Draw(c).multiline_text((pad, pad), f"{i}  " + label.replace(" + ", "\n+ "), fill="#D8DCE6", font=font)
        x = 220
        for bg, t, s in tiles:
            b = Image.new("RGBA", (s, s), bg); b.alpha_composite(t)
            c.alpha_composite(b, (x, pad + (256 - s) // 2)); x += s + pad
        rows.append(c)
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), "Sheet 6 - your layout rebuilt. Per backdrop: 256, 64, then 32 and 16 at 4x", fill="#FFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y)); y += r.height
    out.convert("RGB").save(HERE / "sheet6_owner_layout.png")
    # overlay check: rebuild vs owner composite
    comp = Image.open(OWN / "composite.png").convert("RGBA")
    layers = build(VARIANTS["as you left it (rebuilt as vectors)"])
    reb = Image.new("RGBA", comp.size, (0, 0, 0, 0))
    full = raster(layers, (0, 0, max(comp.size)), max(comp.size), ss=1).crop((0, 0, *comp.size))
    a = np.array(comp)[..., :3].astype(int); b = np.array(Image.alpha_composite(Image.new("RGBA", comp.size, "#F2F2F2"), full))[..., :3].astype(int)
    print("mean abs diff vs owner composite:", np.abs(a - b).mean())
    Image.blend(comp, Image.alpha_composite(Image.new("RGBA", comp.size, "#F2F2F2"), full), 0.5).save(HERE / "overlay_check.png")
