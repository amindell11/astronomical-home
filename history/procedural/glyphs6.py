"""Round 6: 7/8/9-sided broken frames; railgun = heavy stripe + two full-length edge lines; rippers bigger.
Readability study: outline vs solid negative-space, dark backing, pixel floor, across zoom levels.
"""
from PIL import Image, ImageDraw, ImageFont
import math, numpy as np
import glyphs3 as g3, glyphs5 as g5
from glyphs3 import L, D, slash, px_per_unit
from glyphs5 import pt, W, tri, axis_dashes, OUT

# ---- knobs ----
FRAME_STROKE = 0.065; MARK_STROKE = 0.075; CUT_STROKE = 0.095   # cut marks (negative space) need more width
FRAME_R = 0.46; BREAK = 0.13
GAP_DEG = 80; GAP_AT = -55
MARK_SCALE = 0.84; PIERCE = 0.18
HEAVY = 1.9
INK = g3.INK; INK_A = g3.INK_A; SS = g3.SS; GLYPH_SCALE = 1.5
PX_FLOOR = 22            # minimum on-screen glyph height when the pixel-floor treatment is on
BACKING = (6, 12, 30, 150)  # dark translucent navy backing disc
SX, SY = g3.SX, g3.SY

MARKS = {
    "Lasers":        g3.GLYPHS["Lasers"]["A"],
    "Turbo Lasers":  g3.GLYPHS["Turbo Lasers"]["A"],
    "Charge Lasers": [slash(0.5, 0.5, 0.62)],
    "Railgun":       [W(*pt(-0.62, 0), *pt(0.62, 0)), L(*pt(-0.62, 0.21), *pt(0.62, 0.21)), L(*pt(-0.62, -0.21), *pt(0.62, -0.21))],
    "Missiles":      [tri(0.34, 0.40)] + axis_dashes(-0.48, -0.10, 2),
    "Rippers":       [tri(-0.30, 0.24, 0.62), tri(0.10, 0.24, 0.62), tri(0.50, 0.24, 0.62)],
    "Concussion":    [D(0.5, 0.5, 0.07)] + [L(0.5 + 0.17*math.cos(math.radians(a)), 0.5 + 0.17*math.sin(math.radians(a)),
                                              0.5 + 0.31*math.cos(math.radians(a)), 0.5 + 0.31*math.sin(math.radians(a))) for a in range(-90, 270, 60)],
}
MARK_MULT = {"Rippers": 1.15}   # per-weapon size tweak
FIRING = g5.FIRING

def poly_pts(n):
    rot = -90 - 180/n if n % 2 == 0 else -90      # flat top for even n, vertex at top for odd n
    return [(0.5 + FRAME_R*math.cos(math.radians(360*i/n + rot)), 0.5 + FRAME_R*math.sin(math.radians(360*i/n + rot))) for i in range(n)]

def wedge(S):
    a0, a1 = GAP_AT - GAP_DEG/2, GAP_AT + GAP_DEG/2
    return [(0.5*S, 0.5*S)] + [((0.5 + 2*math.cos(math.radians(a)))*S, (0.5 + 2*math.sin(math.radians(a)))*S) for a in np.linspace(a0, a1, 14)]

def frame_outline(n, S, sw, open_gap):
    m = Image.new("L", (S, S), 0); d = ImageDraw.Draw(m); pts = poly_pts(n)
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[(i+1) % n]
        a = (x0 + (x1-x0)*BREAK, y0 + (y1-y0)*BREAK); b = (x1 - (x1-x0)*BREAK, y1 - (y1-y0)*BREAK)
        d.line([(a[0]*S, a[1]*S), (b[0]*S, b[1]*S)], fill=255, width=sw)
    if open_gap: d.polygon(wedge(S), fill=0)
    return m

def frame_solid(n, S):
    m = Image.new("L", (S, S), 0); ImageDraw.Draw(m).polygon([(x*S, y*S) for x, y in poly_pts(n)], fill=255); return m

def mark_mask(name, S, sw):
    firing = FIRING[name]; k = MARK_SCALE * MARK_MULT.get(name, 1.0)
    dx, dy = (SX*PIERCE, SY*PIERCE) if firing else (0, 0)
    m = Image.new("L", (S, S), 0); d = ImageDraw.Draw(m)
    for p in g5.shifted(MARKS[name], dx, dy, k):
        if p[0] == "line": d.line([(p[1]*S, p[2]*S), (p[3]*S, p[4]*S)], fill=255, width=sw)
        elif p[0] == "wline": d.line([(p[1]*S, p[2]*S), (p[3]*S, p[4]*S)], fill=255, width=max(1, round(sw*p[5])))
        elif p[0] == "dot": d.ellipse([(p[1]-p[3])*S, (p[2]-p[3])*S, (p[1]+p[3])*S, (p[2]+p[3])*S], fill=255)
        elif p[0] == "poly": d.polygon([(x*S, y*S) for x, y in p[1]], fill=255)
    return m

def render(n, name, h, style="outline", backing=False):
    S = h * SS
    if style == "outline":
        a = np.maximum(np.array(frame_outline(n, S, max(1, round(FRAME_STROKE*S)), FIRING[name])), np.array(mark_mask(name, S, max(1, round(MARK_STROKE*S)))))
    else:   # solid: mark is negative space; marks that pierce cut a notch out of the edge
        solid = np.array(frame_solid(n, S), dtype=int); cut = np.array(mark_mask(name, S, max(1, round(CUT_STROKE*S))), dtype=int)
        a = (solid * (255 - cut) // 255).astype("uint8")
    alpha = Image.fromarray(a).resize((h, h), Image.LANCZOS).point(lambda v: v*INK_A//255)
    out = Image.new("RGBA", (h, h), (0, 0, 0, 0))
    if backing:
        bd = ImageDraw.Draw(out); r = h * 0.56
        bd.ellipse([h/2 - r, h/2 - r, h/2 + r, h/2 + r], fill=BACKING)
    ink = Image.new("RGBA", (h, h), INK + (0,)); ink.putalpha(alpha)
    out.alpha_composite(ink); return out


def mock_stack(entries, ortho, bd, n, style, backing=False, floor=False, sec_gap=0.06):
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3*ppu), max(2, int(0.3*ppu)); gap = int(0.4*ppu)
    gh = int(bar_h*GLYPH_SCALE); gh = max(gh, PX_FLOOR) if floor else gh
    tile_w = bar_w + int(1.6*ppu) + 24; tile_h = bar_h + 2*gap + max(gh, 24) + 12
    Wd, H = tile_w*len(entries), tile_h
    im = bd.crop((0, 0, bd.width, int(bd.width*H/Wd))).resize((Wd, H)).convert("RGBA"); d = ImageDraw.Draw(im)
    for i, (prim, sec) in enumerate(entries):
        x0 = i*tile_w + 12; ys = 12; yh = ys + gap; yr = yh + bar_h + max(4, int(0.1*ppu))
        for y, col, pct in ((ys, (0, 230, 255, 180), 0.7), (yh, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128)); d.rectangle([x0, y, x0 + int(bar_w*pct), y + bar_h], fill=col)
        gx = x0
        for nm in (prim, sec):
            if nm is None: continue
            im.alpha_composite(render(n, nm, gh, style, backing), (gx, yr)); gx += gh + max(2, int(sec_gap*ppu))
    return im


def sheet_sides():
    font = ImageFont.truetype("arial.ttf", 18); small = ImageFont.truetype("arial.ttf", 12)
    BG = (10, 16, 28, 255); BIG = 96; sizes = (32, 24); cols = [(7, "outline"), (8, "outline"), (9, "outline"), (8, "solid")]
    colw = BIG + 24 + sum(s + 14 for s in sizes) + 30
    Wd = 160 + len(cols)*colw + 20; rowh = BIG + 36; H = 60 + rowh*len(MARKS)
    sh = Image.new("RGBA", (Wd, H), BG); d = ImageDraw.Draw(sh)
    d.text((20, 14), "Round 6 — 7 / 8 / 9-sided broken frames (outline), and the 8-sided frame solid with the mark as negative space. Railgun: stripe + two full-length edge lines. Rippers enlarged.", fill=(190, 190, 180), font=small)
    for j, (n, st) in enumerate(cols):
        x = 160 + j*colw; d.text((x, 36), f"{n}-gon {st}", fill=(150, 150, 140), font=small); xx = x + BIG + 24
        for s in sizes: d.text((xx, 36), f"{s}px", fill=(120, 120, 110), font=small); xx += s + 14
    y = 60
    for name in MARKS:
        d.line([(20, y), (Wd - 20, y)], fill=(30, 40, 56), width=1); d.text((20, y + rowh//2 - 10), name, fill=(215, 215, 205), font=font)
        for j, (n, st) in enumerate(cols):
            x = 160 + j*colw; sh.alpha_composite(render(n, name, BIG, st), (x, y + 18)); xx = x + BIG + 24
            for s in sizes: sh.alpha_composite(render(n, name, s, st), (xx, y + 18 + (BIG - s)//2)); xx += s + 14
        y += rowh
    out = OUT / "sheet6_sides.png"; sh.convert("RGB").save(out); print(out, sh.size)


def sheet_readability(n=8):
    bd = Image.open(g3.BACKDROP).convert("RGB").crop((150, 330, 1000, 560))
    small = ImageFont.truetype("arial.ttf", 12); BG = (10, 16, 28, 255)
    enemies = [("Lasers", None), ("Missiles", None), ("Rippers", None), ("Turbo Lasers", None), ("Railgun", "Concussion")]
    treatments = [("outline (as designed, scales with bars)", "outline", False, False),
                  ("solid, mark as negative space", "solid", False, False),
                  ("outline + dark backing", "outline", True, False),
                  ("outline + pixel floor 22 px", "outline", False, True),
                  ("solid + dark backing + pixel floor 22 px", "solid", True, True)]
    zooms = (7.0, 12.0, 18.0)
    rows = []
    for label, st, back, floor in treatments:
        strips = [mock_stack(enemies, z, bd, n, st, back, floor) for z in zooms]
        Wd = sum(s.width for s in strips) + 16*(len(strips)-1); H = max(s.height for s in strips) + 22
        row = Image.new("RGBA", (Wd, H), BG); ImageDraw.Draw(row).text((4, 4), f"{label}   —   ortho 7 | 12 | 18", fill=(170, 170, 160), font=small)
        x = 0
        for s in strips: row.alpha_composite(s, (x, 22)); x += s.width + 16
        rows.append(row)
    Wd = max(r.width for r in rows) + 20; H = sum(r.height + 10 for r in rows) + 10
    sh = Image.new("RGBA", (Wd, H), BG); y = 5
    for r in rows: sh.alpha_composite(r, (10, y)); y += r.height + 10
    out = OUT / "sheet6_readability.png"; sh.convert("RGB").save(out); print(out, sh.size)

if __name__ == "__main__":
    sheet_sides(); sheet_readability()
