"""Weapon glyphs, round 5: broken-dodecagon frame; reworked railgun, missile, rippers, concussion.

Projectile family follows the slash family: count is rate (missile = one arrowhead, rippers = three small).
Railgun is the one mark that crosses the whole frame (hitscan goes through everything).
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math, numpy as np
import glyphs3 as g3, glyphs4 as g4
from glyphs3 import L, D, slash, dashes, px_per_unit

OUT = g3.OUT
# ---- knobs ----
FRAME_STROKE = 0.06; MARK_STROKE = 0.07
FRAME_R = 0.46
BREAK = 0.16          # fraction trimmed from each end of a frame edge (the "broken" look)
GAP_DEG = 80; GAP_AT = -55
MARK_SCALE = 0.84; PIERCE = 0.18
HEAVY = 1.9           # railgun stroke multiplier
INK = g3.INK; INK_A = g3.INK_A; SS = g3.SS; GLYPH_SCALE = 1.5
SX, SY = g3.SX, g3.SY
PX, PY = -SY, SX      # perpendicular to the slash axis

def pt(t, s): return (0.5 + SX*t + PX*s, 0.5 + SY*t + PY*s)
def W(x1, y1, x2, y2, m=HEAVY): return ("wline", x1, y1, x2, y2, m)
def P(*pts): return ("poly", pts)

def tri(t, size, aspect=0.55):
    """Solid arrowhead on the slash axis, tip at t."""
    tip = pt(t, 0); b1 = pt(t - size, aspect*size/2); b2 = pt(t - size, -aspect*size/2)
    return P(tip, b1, b2)

def chev(t, size, half_deg=24):
    tip = pt(t, 0)
    a = math.radians(half_deg)
    return [L(*tip, *pt(t - size*math.cos(a), size*math.sin(a))), L(*tip, *pt(t - size*math.cos(a), -size*math.sin(a)))]

def axis_dashes(t0, t1, n, duty=0.55):
    out = []
    for i in range(n):
        a = t0 + (t1-t0)*i/n; b = a + (t1-t0)*duty/n
        out.append(L(*pt(a, 0), *pt(b, 0)))
    return out

MARKS = {
    "Lasers":        {"A": g3.GLYPHS["Lasers"]["A"]},
    "Turbo Lasers":  {"A": g3.GLYPHS["Turbo Lasers"]["A"]},
    "Charge Lasers": {"A": [slash(0.5, 0.5, 0.62)]},
    "Railgun":       {"A": [W(*pt(-0.62, 0), *pt(0.62, 0))],                                   # one heavy shot straight through
                      "B": [W(*pt(-0.55, 0), *pt(0.55, 0)), L(*pt(-0.52, 0.17), *pt(-0.18, 0.17)), L(*pt(-0.52, -0.17), *pt(-0.18, -0.17))]},  # + rails at the tail
    "Missiles":      {"A": [tri(0.34, 0.40)] + axis_dashes(-0.48, -0.10, 2),
                      "B": chev(0.34, 0.36) + axis_dashes(-0.48, -0.06, 2)},
    "Rippers":       {"A": [tri(-0.26, 0.17, 0.62), tri(0.08, 0.17, 0.62), tri(0.42, 0.17, 0.62)],
                      "B": chev(-0.16, 0.18, 30) + chev(0.12, 0.18, 30) + chev(0.40, 0.18, 30)},
    "Concussion":    {"A": [D(0.5, 0.5, 0.07)] + [L(0.5 + 0.17*math.cos(math.radians(a)), 0.5 + 0.17*math.sin(math.radians(a)),
                                                    0.5 + 0.31*math.cos(math.radians(a)), 0.5 + 0.31*math.sin(math.radians(a))) for a in range(-90, 270, 60)],
                      "B": [D(0.5, 0.5, 0.07)] + [L(0.5 + 0.17*math.cos(math.radians(a)), 0.5 + 0.17*math.sin(math.radians(a)),
                                                    0.5 + 0.33*math.cos(math.radians(a)), 0.5 + 0.33*math.sin(math.radians(a))) for a in range(-90, 270, 45)]},
}
FIRING = {"Lasers": True, "Turbo Lasers": True, "Charge Lasers": False, "Railgun": True, "Missiles": True, "Rippers": True, "Concussion": False}

def poly_pts(n, rot=-90):
    return [(0.5 + FRAME_R*math.cos(math.radians(360*i/n + rot)), 0.5 + FRAME_R*math.sin(math.radians(360*i/n + rot))) for i in range(n)]

def draw_frame(mask, kind, S, sw, open_gap):
    d = ImageDraw.Draw(mask)
    if kind == "cut-hex":
        pts = [(x*S, y*S) for x, y in poly_pts(6)]; d.line(pts + [pts[0]], fill=255, width=sw, joint="curve")
    else:
        pts = poly_pts(12, -75)
        for i in range(12):
            (x0, y0), (x1, y1) = pts[i], pts[(i+1) % 12]
            if kind == "dodeca-paired" and i % 2 == 1:   # every other edge missing → six paired... no: six single segments; keep pairs by trimming only odd ends
                pass
            tr = BREAK if kind == "dodeca-broken" else (BREAK if i % 2 == 0 else 0.0)
            a = (x0 + (x1-x0)*tr, y0 + (y1-y0)*tr); b = (x1 - (x1-x0)*tr, y1 - (y1-y0)*tr)
            if kind == "dodeca-paired":
                # pairs: edges 2k and 2k+1 joined, gaps between pairs
                a = (x0 + (x1-x0)*(BREAK if i % 2 == 0 else 0), y0 + (y1-y0)*(BREAK if i % 2 == 0 else 0))
                b = (x1 - (x1-x0)*(0 if i % 2 == 0 else BREAK), y1 - (y1-y0)*(0 if i % 2 == 0 else BREAK))
            d.line([(a[0]*S, a[1]*S), (b[0]*S, b[1]*S)], fill=255, width=sw)
    if open_gap:
        a0, a1 = GAP_AT - GAP_DEG/2, GAP_AT + GAP_DEG/2
        wedge = [(0.5*S, 0.5*S)] + [((0.5 + 2*math.cos(math.radians(a)))*S, (0.5 + 2*math.sin(math.radians(a)))*S) for a in np.linspace(a0, a1, 14)]
        d.polygon(wedge, fill=0)

def shifted(prims, dx, dy, k):
    f = lambda x, y: (.5 + (x-.5)*k + dx, .5 + (y-.5)*k + dy)
    out = []
    for p in prims:
        if p[0] == "line": out.append(("line", *f(p[1], p[2]), *f(p[3], p[4])))
        elif p[0] == "wline": out.append(("wline", *f(p[1], p[2]), *f(p[3], p[4]), p[5]))
        elif p[0] == "dot": out.append(("dot", *f(p[1], p[2]), p[3]*k))
        elif p[0] == "poly": out.append(("poly", tuple(f(x, y) for x, y in p[1])))
    return out

def render(kind, name, h, opt="A"):
    S = h * SS; firing = FIRING[name]
    frame = Image.new("L", (S, S), 0); draw_frame(frame, kind, S, max(1, round(FRAME_STROKE*S)), firing)
    dx, dy = (SX*PIERCE, SY*PIERCE) if firing else (0, 0)
    mark = Image.new("L", (S, S), 0); dm = ImageDraw.Draw(mark); sw = max(1, round(MARK_STROKE*S))
    for p in shifted(MARKS[name][opt], dx, dy, MARK_SCALE):
        if p[0] == "line": dm.line([(p[1]*S, p[2]*S), (p[3]*S, p[4]*S)], fill=255, width=sw)
        elif p[0] == "wline": dm.line([(p[1]*S, p[2]*S), (p[3]*S, p[4]*S)], fill=255, width=max(1, round(sw*p[5])))
        elif p[0] == "dot": dm.ellipse([(p[1]-p[3])*S, (p[2]-p[3])*S, (p[1]+p[3])*S, (p[2]+p[3])*S], fill=255)
        elif p[0] == "poly": dm.polygon([(x*S, y*S) for x, y in p[1]], fill=255)
    a = Image.fromarray(np.maximum(np.array(frame), np.array(mark))).resize((h, h), Image.LANCZOS).point(lambda v: v*INK_A//255)
    out = Image.new("RGBA", (h, h), INK + (0,)); out.putalpha(a); return out

def mock_stack(entries, ortho, bd, kind, opts):
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3*ppu), max(2, int(0.3*ppu)); gap = int(0.4*ppu); gh = int(bar_h*GLYPH_SCALE)
    tile_w = bar_w + int(1.6*ppu) + 24; tile_h = bar_h + 2*gap + 24
    Wd, H = tile_w*len(entries), tile_h
    im = bd.crop((0, 0, bd.width, int(bd.width*H/Wd))).resize((Wd, H)).convert("RGBA"); d = ImageDraw.Draw(im)
    for i, (prim, sec) in enumerate(entries):
        x0 = i*tile_w + 12; ys = 12; yh = ys + gap; yr = yh + gap - (gh - bar_h)//2
        for y, col, pct in ((ys, (0, 230, 255, 180), 0.7), (yh, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128)); d.rectangle([x0, y, x0 + int(bar_w*pct), y + bar_h], fill=col)
        gx = x0
        for nm in (prim, sec):
            if nm is None: continue
            im.alpha_composite(render(kind, nm, gh, opts.get(nm, "A")), (gx, yr)); gx += gh + max(2, int(0.06*ppu))
    return im

def sheet():
    bd = Image.open(g3.BACKDROP).convert("RGB").crop((150, 330, 1000, 560))
    font = ImageFont.truetype("arial.ttf", 18); small = ImageFont.truetype("arial.ttf", 12)
    BG = (10, 16, 28, 255); BIG = 96; sizes = (32, 24); kinds = ("dodeca-broken", "dodeca-paired", "cut-hex")
    cols = [(k, "A") for k in kinds] + [("dodeca-broken", "B")]
    colw = BIG + 24 + sum(s + 14 for s in sizes) + 30
    Wd = 160 + len(cols)*colw + 20; rowh = BIG + 36; H = 60 + rowh*len(MARKS)
    sh = Image.new("RGBA", (Wd, H), BG); d = ImageDraw.Draw(sh)
    d.text((20, 14), "Round 5 — broken dodecagon (two treatments) vs cut-hex; option A marks. Last column: option B marks for the reworked weapons.", fill=(190, 190, 180), font=small)
    for j, (k, o) in enumerate(cols):
        x = 160 + j*colw; d.text((x, 36), f"{k} / {o}", fill=(150, 150, 140), font=small); xx = x + BIG + 24
        for s in sizes: d.text((xx, 36), f"{s}px", fill=(120, 120, 110), font=small); xx += s + 14
    y = 60
    for name, opts in MARKS.items():
        d.line([(20, y), (Wd - 20, y)], fill=(30, 40, 56), width=1); d.text((20, y + rowh//2 - 10), name, fill=(215, 215, 205), font=font)
        for j, (k, o) in enumerate(cols):
            if o not in opts: continue
            x = 160 + j*colw; sh.alpha_composite(render(k, name, BIG, o), (x, y + 18)); xx = x + BIG + 24
            for s in sizes: sh.alpha_composite(render(k, name, s, o), (xx, y + 18 + (BIG - s)//2)); xx += s + 14
        y += rowh
    enemies = [("Lasers", None), ("Missiles", None), ("Rippers", None), ("Turbo Lasers", None), ("Railgun", "Concussion"), ("Lasers", "Missiles")]
    strips = []
    for k, label, opts in (("dodeca-broken", "dodeca-broken, marks A", {}), ("dodeca-broken", "dodeca-broken, marks B", {"Railgun": "B", "Missiles": "B", "Rippers": "B", "Concussion": "B"}), ("dodeca-paired", "dodeca-paired, marks A", {})):
        s7 = mock_stack(enemies, 7.0, bd, k, opts)
        lab = Image.new("RGBA", (s7.width, s7.height + 22), BG); ImageDraw.Draw(lab).text((4, 4), f"{label} — ortho 7: Lasers, Missiles, Rippers, Turbo, Railgun+Concussion, Lasers+Missiles", fill=(150, 150, 140), font=small)
        lab.alpha_composite(s7, (0, 22)); strips.append(lab)
    SW = max(Wd, strips[0].width + 20); SH = H + sum(s.height + 8 for s in strips) + 20
    full = Image.new("RGBA", (SW, SH), BG); full.alpha_composite(sh, (0, 0)); y = H + 10
    for s in strips: full.alpha_composite(s, (10, y)); y += s.height + 8
    out = OUT / "sheet5.png"; full.convert("RGB").save(out); print(out, full.size)

if __name__ == "__main__":
    sheet()
