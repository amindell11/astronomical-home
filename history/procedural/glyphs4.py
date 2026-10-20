"""Weapon glyphs, round 4: the round-3 angular grammar inside one shared bounding frame.

Rule: every weapon sits in the same frame. Firing weapons get a frame that opens at the top-right
(the muzzle, on the slash axis) and their marks pierce the gap; held/dropped weapons (charge, mine)
sit in a closed frame. Frames: cut-hex, chamfer-square, corner-brackets (echoes the lock reticle).
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math, numpy as np
import glyphs3 as g3
from glyphs3 import L, D, slash, dashes, corner, px_per_unit

OUT = g3.OUT
# ---- knobs ----
FRAME_STROKE = 0.06    # frame stroke, fraction of glyph height
MARK_STROKE = 0.07     # inner mark stroke
FRAME_R = 0.44         # frame radius (centre to vertex)
GAP_DEG = 78           # opening in the frame for firing weapons
GAP_AT = -55           # where the opening points (deg, 0 = right, -90 = up); on the slash axis
MARK_SCALE = 0.70      # inner mark box as a fraction of the glyph box
PIERCE = 0.10          # shift of the mark along the slash axis so its tips leave through the gap
INK = g3.INK; INK_A = g3.INK_A; SS = g3.SS
GLYPH_SCALE = 1.5      # glyph height / bar height (framed glyphs need a touch more room than bare ones)

def shifted(prims, dx, dy, k):
    out = []
    for p in prims:
        if p[0] == "line": out.append(("line", .5 + (p[1]-.5)*k + dx, .5 + (p[2]-.5)*k + dy, .5 + (p[3]-.5)*k + dx, .5 + (p[4]-.5)*k + dy))
        elif p[0] == "dot": out.append(("dot", .5 + (p[1]-.5)*k + dx, .5 + (p[2]-.5)*k + dy, p[3]*k))
    return out

# Inner marks: round-3 option A, minus their own enclosures (the frame takes that job).
MARKS = {
    "Lasers":        (g3.GLYPHS["Lasers"]["A"], True),
    "Turbo Lasers":  (g3.GLYPHS["Turbo Lasers"]["A"], True),
    "Charge Lasers": ([slash(0.5, 0.5, 0.62)], False),                      # held: one slash in a closed frame
    "Railgun":       (g3.GLYPHS["Railgun"]["A"], True),
    "Missiles":      (g3.GLYPHS["Missiles"]["A"], True),
    "Rippers":       (g3.GLYPHS["Rippers"]["A"], True),
    "Concussion":    ([D(0.5, 0.5, 0.09)], False),                          # dropped: a charge in a closed frame
}

def poly_frame(kind):
    """Closed frame outline as a point list in the unit box."""
    c, r = 0.5, FRAME_R
    if kind == "cut-hex":
        return [(c + r*math.cos(math.radians(60*i - 90)), c + r*math.sin(math.radians(60*i - 90))) for i in range(6)]
    if kind == "chamfer-square":
        s = r * 0.92; ch = s * 0.42   # square half-size, chamfer length
        return [(c - s + ch, c - s), (c + s - ch, c - s), (c + s, c - s + ch), (c + s, c + s - ch),
                (c + s - ch, c + s), (c - s + ch, c + s), (c - s, c + s - ch), (c - s, c - s + ch)]
    return None

def draw_frame(mask, kind, S, sw, open_gap):
    d = ImageDraw.Draw(mask)
    if kind == "brackets":
        c, s, arm = 0.5, FRAME_R * 0.92, FRAME_R * 0.5
        corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
        for (ux, uy) in corners:
            if open_gap and ux == 1 and uy == -1: continue     # the muzzle corner is left open
            x, y = c + ux*s, c + uy*s
            d.line([(x*S, y*S), ((x - ux*arm)*S, y*S)], fill=255, width=sw)
            d.line([(x*S, y*S), (x*S, (y - uy*arm)*S)], fill=255, width=sw)
        return
    pts = [(x*S, y*S) for x, y in poly_frame(kind)]
    d.line(pts + [pts[0]], fill=255, width=sw, joint="curve")
    if open_gap:   # erase a wedge at the muzzle
        a0, a1 = GAP_AT - GAP_DEG/2, GAP_AT + GAP_DEG/2
        wedge = [(0.5*S, 0.5*S)] + [((0.5 + 2*math.cos(math.radians(a)))*S, (0.5 + 2*math.sin(math.radians(a)))*S) for a in np.linspace(a0, a1, 12)]
        d.polygon(wedge, fill=0)

def render(kind, name, h):
    S = h * SS
    prims, firing = MARKS[name]
    frame = Image.new("L", (S, S), 0)
    draw_frame(frame, kind, S, max(1, round(FRAME_STROKE * S)), firing)
    k = MARK_SCALE
    dx, dy = (g3.SX * PIERCE, g3.SY * PIERCE) if firing else (0, 0)
    inner = shifted(prims, dx, dy, k)
    mark = Image.new("L", (S, S), 0); dm = ImageDraw.Draw(mark); sw = max(1, round(MARK_STROKE * S))
    for p in inner:
        if p[0] == "line": dm.line([(p[1]*S, p[2]*S), (p[3]*S, p[4]*S)], fill=255, width=sw)
        else: dm.ellipse([(p[1]-p[3])*S, (p[2]-p[3])*S, (p[1]+p[3])*S, (p[2]+p[3])*S], fill=255)
    a = Image.fromarray(np.maximum(np.array(frame), np.array(mark))).resize((h, h), Image.LANCZOS).point(lambda v: v * INK_A // 255)
    out = Image.new("RGBA", (h, h), INK + (0,)); out.putalpha(a); return out


def mock_stack(entries, ortho, bd, kind):
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3 * ppu), max(2, int(0.3 * ppu)); gap = int(0.4 * ppu); gh = int(bar_h * GLYPH_SCALE)
    tile_w = bar_w + int(1.6 * ppu) + 24; tile_h = bar_h + 2 * gap + 24
    W, H = tile_w * len(entries), tile_h
    im = bd.crop((0, 0, bd.width, int(bd.width * H / W))).resize((W, H)).convert("RGBA"); d = ImageDraw.Draw(im)
    for i, (prim, sec) in enumerate(entries):
        x0 = i * tile_w + 12; ys = 12; yh = ys + gap; yr = yh + gap - (gh - bar_h) // 2
        for y, col, pct in ((ys, (0, 230, 255, 180), 0.7), (yh, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128))
            d.rectangle([x0, y, x0 + int(bar_w * pct), y + bar_h], fill=col)
        gx = x0
        for nm in (prim, sec):
            if nm is None: continue
            im.alpha_composite(render(kind, nm, gh), (gx, yr)); gx += gh + max(2, int(0.06 * ppu))
    return im


def sheet():
    bd = Image.open(g3.BACKDROP).convert("RGB").crop((150, 330, 1000, 560))
    try: font = ImageFont.truetype("arial.ttf", 18); small = ImageFont.truetype("arial.ttf", 12)
    except Exception: font = small = ImageFont.load_default()
    BG = (10, 16, 28, 255); BIG = 96; sizes = (32, 24, 16); kinds = ("cut-hex", "chamfer-square", "brackets")
    colw = BIG + 24 + sum(s + 14 for s in sizes) + 30
    W = 160 + len(kinds) * colw + 20; rowh = BIG + 36; H = 60 + rowh * len(MARKS)
    sh = Image.new("RGBA", (W, H), BG); d = ImageDraw.Draw(sh)
    d.text((20, 14), "One frame for all. Open at the muzzle (top-right, on the slash axis) for firing weapons, marks pierce the gap; closed for held/dropped (charge, concussion).", fill=(190, 190, 180), font=small)
    for j, kind in enumerate(kinds):
        x = 160 + j * colw; d.text((x, 36), kind, fill=(150, 150, 140), font=small); xx = x + BIG + 24
        for s in sizes: d.text((xx, 36), f"{s}px", fill=(120, 120, 110), font=small); xx += s + 14
    y = 60
    for name in MARKS:
        d.line([(20, y), (W - 20, y)], fill=(30, 40, 56), width=1)
        d.text((20, y + rowh // 2 - 10), name, fill=(215, 215, 205), font=font)
        for j, kind in enumerate(kinds):
            x = 160 + j * colw
            sh.alpha_composite(render(kind, name, BIG), (x, y + 18)); xx = x + BIG + 24
            for s in sizes: sh.alpha_composite(render(kind, name, s), (xx, y + 18 + (BIG - s) // 2)); xx += s + 14
        y += rowh
    enemies = [("Lasers", None), ("Missiles", None), ("Rippers", None), ("Turbo Lasers", None), ("Lasers", "Missiles")]
    strips = []
    for kind in kinds:
        s7 = mock_stack(enemies, 7.0, bd, kind)
        lab = Image.new("RGBA", (s7.width, s7.height + 22), BG); ImageDraw.Draw(lab).text((4, 4), f"{kind} — under the bars at ortho 7: M1 enemies + player pair", fill=(150, 150, 140), font=small)
        lab.alpha_composite(s7, (0, 22)); strips.append(lab)
    SW = max(W, strips[0].width + 20); SH = H + sum(s.height + 8 for s in strips) + 20
    full = Image.new("RGBA", (SW, SH), BG); full.alpha_composite(sh, (0, 0)); y = H + 10
    for s in strips: full.alpha_composite(s, (10, y)); y += s.height + 8
    out = OUT / "sheet4.png"; full.convert("RGB").save(out); print(out, full.size)

if __name__ == "__main__":
    sheet()
