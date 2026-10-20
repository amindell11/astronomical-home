"""Round 7: codex review r1 scheme — shared rear corner, no perimeter, 16-grid (u,v) construction.
Screen: x = 2 + u + 0.5 v, y = 14 - v  (slash axis 63.4°). v points toward the muzzle (up-right)."""
import math
from PIL import Image
import cutstroke as cs, sheetkit as sk

# ---- knobs (grid units) ----
G = 16.0
MAIN_W = 1.0; CORNER_W = 0.8
CORNER = [(0, 2), (0, 0), (4.5, 0)]        # shared rear corner polyline, (u,v)  (codex r2)
SHOW_CORNER = True
cs.AXIS_DEG = math.degrees(math.atan2(1, 0.5))   # 63.4°, so 'acut' terminals follow the grid's slash

def P(u, v): return ((2 + u + 0.5*v) / G, (14 - v) / G)

def B(u, a, b, w=MAIN_W, caps=("hcut", "hcut")):
    """Solid slash on constant u from v=a to v=b, perpendicular thickness w."""
    return cs.stroke(*P(u, a), *P(u, b), w / G, *caps)

def seg(p, q, w=MAIN_W, caps=("butt", "butt")): return cs.stroke(*P(*p), *P(*q), w / G, *caps)

def polyline(pts, w=MAIN_W):
    out = []
    for p, q in zip(pts, pts[1:]):
        # extend each segment by w/2 at the joints so butt ends overlap into a mitre-ish corner
        (x0, y0), (x1, y1) = P(*p), P(*q); dx, dy = x1 - x0, y1 - y0; n = math.hypot(dx, dy); e = (w / G) / 2
        out.append(cs.stroke(x0 - dx/n*e*(p != pts[0]), y0 - dy/n*e*(p != pts[0]), x1, y1, w / G, "butt", "butt"))
    return out

def spolyline(pts, w, caps=("butt", "butt")):
    """Screen-space polyline (unit coords) with butt free ends and sharp mitres at interior joints."""
    w = w / G; out = []
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        out.append(cs.stroke(*a, *b, w, caps[0] if i == 0 else "butt", caps[1] if i == len(pts) - 2 else "butt"))
    for i in range(1, len(pts) - 1):
        (x0, y0), (xj, yj), (x1, y1) = pts[i-1], pts[i], pts[i+1]
        d1 = (xj - x0, yj - y0); n = math.hypot(*d1); d1 = (d1[0]/n, d1[1]/n)
        d2 = (x1 - xj, y1 - yj); n = math.hypot(*d2); d2 = (d2[0]/n, d2[1]/n)
        n1, n2 = (-d1[1], d1[0]), (-d2[1], d2[0]); sgn = -1 if (d1[0]*d2[1] - d1[1]*d2[0]) > 0 else 1
        m = (n1[0] + n2[0], n1[1] + n2[1]); ml = math.hypot(*m)
        if ml < 1e-6: continue
        cos_half = (m[0]*n1[0] + m[1]*n1[1]) / ml
        tip = (xj + sgn*m[0]/ml*(w/2)/cos_half, yj + sgn*m[1]/ml*(w/2)/cos_half)
        out.append(cs.poly((xj + sgn*n1[0]*w/2, yj + sgn*n1[1]*w/2), tip, (xj + sgn*n2[0]*w/2, yj + sgn*n2[1]*w/2), (xj, yj)))
    return out

def corner(): return spolyline([P(*q) for q in CORNER], CORNER_W) if SHOW_CORNER else []

def offs(c, pts): return [(c[0] + x/G, c[1] + y/G) for x, y in pts]

AXU = (0.5 / math.hypot(0.5, 1), -1 / math.hypot(0.5, 1))      # screen unit vector along the slash (up-right)
PERP = (-AXU[1], AXU[0])                                         # screen unit vector perpendicular (right-down)

def along(u, v, t, s):
    """Screen point: start at grid (u,v), move t grid units along the slash axis and s units perpendicular."""
    x, y = P(u, v); return (x + (AXU[0]*t + PERP[0]*s) / G, y + (AXU[1]*t + PERP[1]*s) / G)

def head(u, v0, length=4.2, halfw=1.05):
    """Isosceles arrowhead on the axis: base centred at (u,v0), apex `length` further along."""
    return cs.poly(along(u, v0, 0, -halfw), along(u, v0, 0, halfw), along(u, v0, length, 0))

def round_(u, v0, halfw=0.85, body=1.6, nose=1.3):
    return cs.poly(along(u, v0, 0, -halfw), along(u, v0, 0, halfw), along(u, v0, body, halfw), along(u, v0, body + nose, 0), along(u, v0, body, -halfw))

def open_head(u, v0, w=0.8):
    """Open two-segment projectile head anchored at grid (u,v0): local (-1.2,-1.2) -> (0,1.8) -> (1.2,-1.2), butt ends."""
    return spolyline([along(u, v0, -1.2, -1.2), along(u, v0, 1.8, 0), along(u, v0, -1.2, 1.2)], w)

RAILGUN_STYLE = "guides"   # "guides" = owner's request (full-length edge lines); "ticks" = codex r3 alternative

def chevrons(u, v, dx=3.3, depth=1.1, half_h=2.6, w=0.75):
    """Screen-space ⟨ ⟩ around grid point (u,v): inward-pointing, vertical, butt ends."""
    cx, cy = P(u, v); out = []
    for sgn in (-1, 1):
        x0 = cx + sgn*dx/G; xi = cx + sgn*(dx - depth)/G
        out += [cs.stroke(x0, cy - half_h/G, xi, cy, w/G, "butt", "butt"), cs.stroke(xi, cy, x0, cy + half_h/G, w/G, "butt", "butt")]
    return out

def burst(cu, cv, d=1.8, r0=2.1, r1=4.6, n=5, w=0.8):
    cx, cy = P(cu, cv); out = [cs.poly(*offs((cx, cy), [(0, -0.9), (0.9, 0), (0, 0.9), (-0.9, 0)]))]
    a0 = -cs.AXIS_DEG   # one tick on the slash axis (screen angle, y down)
    for i in range(n):
        a = math.radians(a0 + 360 * i / n)
        out.append(cs.stroke(cx + math.cos(a)*r0/G, cy + math.sin(a)*r0/G, cx + math.cos(a)*r1/G, cy + math.sin(a)*r1/G, w / G, "butt", "butt"))
    return out

def glyphs():
    g = {}
    g["Lasers"]        = corner() + [B(2.5, 2.8, 11), B(5, 3.4, 10)]
    g["Turbo Lasers"]  = corner() + [B(2.5, 2.8, 11), B(5, 3.4, 10), B(7.5, 4.2, 9.2)]
    C = P(4.5, 6.5)
    g["Charge Lasers"] = corner() + [B(4.5, 4.4, 8.6)] + spolyline(offs(C, [(-2.2, -3.8), (-3.6, -0.6), (-2.7, 2.6)]), 0.8) + spolyline(offs(C, [(2.7, -2.6), (3.6, 0.6), (2.2, 3.8)]), 0.8)
    if RAILGUN_STYLE == "guides":
        g["Railgun"]   = corner() + [B(5, 2.5, 10.5, 2.4), B(2, 2.5, 10.5, 0.8), B(8, 2.5, 10.5, 0.8)]
    else:
        mx, my = P(5, 6.5)   # stripe midpoint; stripe half-width 1.2 horizontally ≈ 1.2/sin(63.4°) = 1.34
        g["Railgun"]   = corner() + [B(5, 2.5, 10.5, 2.4),
                                      cs.stroke(mx - (1.34 + 1.0 + 2.5)/G, my + 1.5/G, mx - (1.34 + 1.0)/G, my + 1.5/G, 0.8/G, "butt", "butt"),
                                      cs.stroke(mx + (1.34 + 1.0)/G, my - 1.5/G, mx + (1.34 + 1.0 + 2.5)/G, my - 1.5/G, 0.8/G, "butt", "butt")]
    g["Missiles"]      = corner() + spolyline([along(4.8, 8, 0, -2.2), along(4.8, 8, 3.6, 0), along(4.8, 8, 0, 2.2)], 1.0) + [B(4.8, 2.8, 6.8, 1.0)]
    g["Rippers"]       = corner() + open_head(1, 8) + open_head(6, 8) + open_head(5.5, 3)
    g["Concussion"]    = corner() + burst(4, 6)
    return {k: (v, ()) for k, v in g.items()}

ENEMIES = [("Lasers", None), ("Missiles", None), ("Rippers", None), ("Turbo Lasers", None), ("Railgun", "Concussion"), ("Lasers", "Missiles")]

if __name__ == "__main__":
    import glyphs7 as me
    gl = glyphs()
    SHOW_CORNER = False; bare = glyphs(); SHOW_CORNER = True
    CORNER[:] = [(0, 0), (4.5, 0)]; base = glyphs(); CORNER[:] = [(0, 2), (0, 0), (4.5, 0)]
    RAILGUN_STYLE = "ticks"; ticks = glyphs(); RAILGUN_STYLE = "guides"
    gl["Railgun (codex ticks)"] = ticks["Railgun"]; base["Railgun (codex ticks)"] = ticks["Railgun"]
    t = sk.table([("shared corner", gl), ("base only", base), ("marks alone", bare)], "Round 9 — open ripper heads; railgun: owner's guides vs codex ticks; corner variants. Sizes 96 / 32 / 24 / 16 px.")
    t.save(sk.OUT / "sheet9_table.png"); print(sk.OUT / "sheet9_table.png", t.size)
