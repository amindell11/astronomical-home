"""Stroke renderer with cut terminals. A stroke is a quad between two points; each end is sliced
either perpendicular (butt), horizontally (hcut), along the slash axis (acut) or to a point (point).
All glyph geometry lives on a unit box (y down). Rendered via supersampled masks."""
from PIL import Image, ImageDraw
import math, numpy as np

SS = 8
INK = (238, 238, 230); INK_A = 240
AXIS_DEG = 65                      # the slash axis, measured from horizontal, rising to the right
AX, AY = math.cos(math.radians(AXIS_DEG)), -math.sin(math.radians(AXIS_DEG))   # unit vector along the axis

def stroke(x1, y1, x2, y2, w, cap1="hcut", cap2="hcut"):
    return ("stroke", x1, y1, x2, y2, w, cap1, cap2)

def poly(*pts): return ("poly", pts)
def dot(cx, cy, r): return ("dot", cx, cy, r)

def _end_edge(px, py, dx, dy, w, cap, sign):
    """Two corners of a stroke end at (px,py); (dx,dy) is the unit direction, sign is +1 at the far end."""
    nx, ny = -dy, dx
    if cap == "butt":
        return [(px + nx*w/2, py + ny*w/2), (px - nx*w/2, py - ny*w/2)]
    if cap == "point":
        tip = (px + dx*w*0.9*sign, py + dy*w*0.9*sign)
        return [(px + nx*w/2, py + ny*w/2), tip, (px - nx*w/2, py - ny*w/2)]
    if cap == "hcut":
        ex, ey = 1.0, 0.0
    else:  # acut: edge runs along the slash axis
        ex, ey = AX, AY
    cross = abs(dx*ey - dy*ex)
    if cross < 0.15:               # edge nearly parallel to the stroke: fall back to butt
        return _end_edge(px, py, dx, dy, w, "butt", sign)
    L = w / cross
    return [(px + ex*L/2, py + ey*L/2), (px - ex*L/2, py - ey*L/2)]

def stroke_polygon(x1, y1, x2, y2, w, cap1, cap2):
    dx, dy = x2 - x1, y2 - y1; n = math.hypot(dx, dy); dx, dy = dx/n, dy/n
    a = _end_edge(x1, y1, dx, dy, w, cap1, -1)
    b = _end_edge(x2, y2, dx, dy, w, cap2, +1)
    # order corners so the quad doesn't twist: a is listed +n,-n ; b needs -n,+n
    return a + list(reversed(b))

def _orient(pts, dx, dy):
    return pts

def draw(prims, S, mask=None, value=255):
    m = mask or Image.new("L", (S, S), 0); d = ImageDraw.Draw(m)
    for p in prims:
        if p[0] == "stroke":
            _, x1, y1, x2, y2, w, c1, c2 = p
            quad = stroke_polygon(x1, y1, x2, y2, w, c1, c2)
            # for 'point' caps the edge lists have 3 points; polygon handles any count
            d.polygon([(x*S, y*S) for x, y in quad], fill=value)
        elif p[0] == "poly":
            d.polygon([(x*S, y*S) for x, y in p[1]], fill=value)
        elif p[0] == "dot":
            _, cx, cy, r = p
            d.ellipse([(cx-r)*S, (cy-r)*S, (cx+r)*S, (cy+r)*S], fill=value)
    return m

def render(prims, h, cut_prims=(), ink=INK, alpha=INK_A):
    """Positive prims drawn in ink; cut_prims subtracted (negative space)."""
    S = h * SS
    m = draw(prims, S)
    if cut_prims:
        c = draw(cut_prims, S)
        m = Image.fromarray((np.array(m, dtype=int) * (255 - np.array(c, dtype=int)) // 255).astype("uint8"))
    a = m.resize((h, h), Image.LANCZOS).point(lambda v: v * alpha // 255)
    out = Image.new("RGBA", (h, h), ink + (0,)); out.putalpha(a); return out

def transform(prims, k=1.0, dx=0.0, dy=0.0, cx=0.5, cy=0.5):
    f = lambda x, y: (cx + (x-cx)*k + dx, cy + (y-cy)*k + dy)
    out = []
    for p in prims:
        if p[0] == "stroke": out.append(("stroke", *f(p[1], p[2]), *f(p[3], p[4]), p[5]*k, p[6], p[7]))
        elif p[0] == "poly": out.append(("poly", tuple(f(x, y) for x, y in p[1])))
        elif p[0] == "dot": out.append(("dot", *f(p[1], p[2]), p[3]*k))
    return out

# axis helpers: t along the slash axis, s perpendicular (positive = toward upper-left)
def pt(t, s, cx=0.5, cy=0.5):
    px, py = -AY, AX    # perpendicular
    return (cx + AX*t - px*s, cy + AY*t - py*s)

def axis_stroke(t0, t1, s, w, cap1="hcut", cap2="hcut", cx=0.5, cy=0.5):
    return stroke(*pt(t0, s, cx, cy), *pt(t1, s, cx, cy), w, cap1, cap2)
