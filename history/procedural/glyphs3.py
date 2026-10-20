"""Weapon glyphs, round 3: angular grammar (sharp strokes at one fixed slash angle).

Grammar: slash = a shot, count = rate (1 long railgun, 2 lasers, 3 turbo); angular brackets = containment
(charge, mine); corner chevron = projectile with heading (missile 1 big, ripper 3 small); dashes = guided path.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math, numpy as np

OUT = Path(__file__).parent / "out"; OUT.mkdir(exist_ok=True)
BACKDROP = Path(r"D:\amind\git\astronomical-home\doc\design\assets\Pasted image 20260923020110.png")

# ---- knobs ----
STROKE = 0.07        # stroke width, fraction of glyph height (competitor sheet ≈ 1.5 px at 24 px)
INK = (238, 238, 230); INK_A = 240
SS = 8
GLYPH_SCALE = 1.3    # glyph height / bar height
# slash direction (unit vector, y down): bottom-left → top-right at ~65°
SX, SY = math.cos(math.radians(65)), -math.sin(math.radians(65))

def L(x1, y1, x2, y2): return ("line", x1, y1, x2, y2)
def D(cx, cy, r): return ("dot", cx, cy, r)

def slash(cx, cy, length):
    """A shot: one stroke through (cx,cy) along the fixed slash angle."""
    h = length / 2
    return L(cx - SX*h, cy - SY*h, cx + SX*h, cy + SY*h)

def dashes(x1, y1, x2, y2, n, duty=0.55):
    out = []
    for i in range(n):
        a = i / n; b = a + duty / n
        out.append(L(x1 + (x2-x1)*a, y1 + (y2-y1)*a, x1 + (x2-x1)*b, y1 + (y2-y1)*b))
    return out

def corner(tx, ty, arm):
    """Projectile heading up-right: a corner chevron with its tip at (tx,ty)."""
    return [L(tx, ty, tx - arm, ty), L(tx, ty, tx, ty + arm)]

def brackets(cx, cy, w, h):
    """Containment: angular ⟨ ⟩ around (cx,cy)."""
    return [L(cx - w*0.6, cy - h/2, cx - w, cy), L(cx - w, cy, cx - w*0.6, cy + h/2),
            L(cx + w*0.6, cy - h/2, cx + w, cy), L(cx + w, cy, cx + w*0.6, cy + h/2)]

def broken_hex(cx, cy, r, trim=0.28):
    out = []
    for i in range(6):
        a0 = math.radians(60*i + 30); a1 = math.radians(60*(i+1) + 30)
        x0, y0 = cx + r*math.cos(a0), cy + r*math.sin(a0); x1, y1 = cx + r*math.cos(a1), cy + r*math.sin(a1)
        out.append(L(x0 + (x1-x0)*trim, y0 + (y1-y0)*trim, x1 - (x1-x0)*trim, y1 - (y1-y0)*trim))
    return out

GLYPHS = {
    "Lasers":        {"A": [slash(0.38, 0.5, 0.62), slash(0.62, 0.5, 0.62)],
                      "B": [slash(0.36, 0.5, 0.62), slash(0.60, 0.5, 0.62), L(0.72, 0.16, 0.90, 0.16)]},   # B keeps the competitor's heat tick
    "Turbo Lasers":  {"A": [slash(0.26, 0.5, 0.62), slash(0.50, 0.5, 0.62), slash(0.74, 0.5, 0.62)],
                      "B": [slash(0.26, 0.5, 0.62), slash(0.50, 0.5, 0.62), slash(0.74, 0.5, 0.62), L(0.76, 0.12, 0.94, 0.12)]},
    "Charge Lasers": {"A": brackets(0.5, 0.5, 0.40, 0.56) + [slash(0.5, 0.5, 0.46)],
                      "B": brackets(0.5, 0.5, 0.40, 0.56) + [slash(0.42, 0.5, 0.44), slash(0.58, 0.5, 0.44)]},
    "Railgun":       {"A": [slash(0.5, 0.5, 0.92), L(0.06, 0.58, 0.34, 0.58), L(0.66, 0.42, 0.94, 0.42)],
                      "B": [slash(0.5, 0.5, 0.92), L(0.04, 0.5, 0.30, 0.5), L(0.70, 0.5, 0.96, 0.5)]},
    "Missiles":      {"A": corner(0.82, 0.18, 0.34) + dashes(0.16, 0.84, 0.66, 0.34, 2),
                      "B": corner(0.82, 0.18, 0.34) + [slash(0.42, 0.58, 0.50)]},
    "Rippers":       {"A": corner(0.34, 0.66, 0.16) + corner(0.58, 0.42, 0.16) + corner(0.82, 0.18, 0.16),
                      "B": dashes(0.14, 0.86, 0.86, 0.14, 3, 0.5)},
    "Concussion":    {"A": broken_hex(0.5, 0.5, 0.38) + [D(0.5, 0.5, 0.07)],
                      "B": brackets(0.5, 0.5, 0.40, 0.56) + [D(0.5, 0.5, 0.07), L(0.5, 0.14, 0.5, 0.26), L(0.5, 0.74, 0.5, 0.86)]},
}


def render(prims, h):
    S = h * SS; sw = max(1, round(STROKE * S))
    im = Image.new("L", (S, S), 0); d = ImageDraw.Draw(im)
    for p in prims:
        if p[0] == "line":
            _, x1, y1, x2, y2 = p
            d.line([(x1*S, y1*S), (x2*S, y2*S)], fill=255, width=sw)   # butt caps: sharp ends
        elif p[0] == "dot":
            _, cx, cy, r = p
            d.ellipse([(cx-r)*S, (cy-r)*S, (cx+r)*S, (cy+r)*S], fill=255)
    a = im.resize((h, h), Image.LANCZOS).point(lambda v: v * INK_A // 255)
    out = Image.new("RGBA", (h, h), INK + (0,)); out.putalpha(a)
    return out


def px_per_unit(ortho=7.0, screen_h=1080): return screen_h / (2 * ortho)

def mock_stack(entries, ortho, bd):
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3 * ppu), max(2, int(0.3 * ppu)); gap = int(0.4 * ppu); gh = int(bar_h * GLYPH_SCALE)
    tile_w = bar_w + int(1.6 * ppu) + 24; tile_h = bar_h + 2 * gap + 24
    W, H = tile_w * len(entries), tile_h
    im = bd.crop((0, 0, bd.width, int(bd.width * H / W))).resize((W, H)).convert("RGBA"); d = ImageDraw.Draw(im)
    for i, (name, prim, sec) in enumerate(entries):
        x0 = i * tile_w + 12; ys = 12; yh = ys + gap; yr = yh + gap
        for y, col, pct in ((ys, (0, 230, 255, 180), 0.7), (yh, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128))
            d.rectangle([x0, y, x0 + int(bar_w * pct), y + bar_h], fill=col)
        gx = x0
        for prims in (prim, sec):
            if prims is None: continue
            im.alpha_composite(render(prims, gh), (gx, yr)); gx += gh + max(2, int(0.08 * ppu))
    return im


def sheet(pick=None):
    bd = Image.open(BACKDROP).convert("RGB").crop((150, 330, 1000, 560))
    try: font = ImageFont.truetype("arial.ttf", 18); small = ImageFont.truetype("arial.ttf", 12)
    except Exception: font = small = ImageFont.load_default()
    BG = (10, 16, 28, 255); BIG = 96; sizes = (32, 24, 16)
    colw = {"name": 150, "A": 130, "B": 130}
    W = 150 + 2 * (BIG + 24 + sum(s + 14 for s in sizes) + 30) + 40
    rowh = BIG + 36
    H = 60 + rowh * len(GLYPHS) + 40
    sh = Image.new("RGBA", (W, H), BG); d = ImageDraw.Draw(sh)
    d.text((20, 14), "Angular grammar — slash = shot (count = rate), brackets = containment, corner chevron = heading, dashes = guided", fill=(190, 190, 180), font=small)
    x_opt = {"A": 160, "B": 160 + (BIG + 24 + sum(s + 14 for s in sizes) + 30)}
    for opt, x in x_opt.items():
        d.text((x, 36), f"option {opt}", fill=(150, 150, 140), font=small)
        xx = x + BIG + 24
        for s in sizes: d.text((xx, 36), f"{s}px", fill=(120, 120, 110), font=small); xx += s + 14
    y = 60
    for name, opts in GLYPHS.items():
        d.line([(20, y), (W - 20, y)], fill=(30, 40, 56), width=1)
        d.text((20, y + rowh // 2 - 10), name, fill=(215, 215, 205), font=font)
        for opt, prims in opts.items():
            x = x_opt[opt]
            sh.alpha_composite(render(prims, BIG), (x, y + 18))
            xx = x + BIG + 24
            for s in sizes:
                sh.alpha_composite(render(prims, s), (xx, y + 18 + (BIG - s) // 2)); xx += s + 14
        y += rowh
    # in-game strip with the picked option per weapon (default A)
    pick = pick or {k: "A" for k in GLYPHS}
    g = lambda k: GLYPHS[k][pick[k]]
    enemies = [("Lasers", g("Lasers"), None), ("Missiles", g("Missiles"), None), ("Rippers", g("Rippers"), None),
               ("TurboLasers", g("Turbo Lasers"), None), ("player", g("Lasers"), g("Missiles"))]
    s7 = mock_stack(enemies, 7.0, bd); s15 = mock_stack(enemies, 15.0, bd)
    strip = Image.new("RGBA", (max(W, s7.width + s15.width + 56), s7.height + 40), BG)
    ImageDraw.Draw(strip).text((20, 8), "Under the bars (option A set): M1 enemies + player pair, ortho 7 | ortho 15", fill=(150, 150, 140), font=small)
    strip.alpha_composite(s7, (20, 28)); strip.alpha_composite(s15, (36 + s7.width, 28))
    full = Image.new("RGBA", (max(W, strip.width), H + strip.height), BG)
    full.alpha_composite(sh, (0, 0)); full.alpha_composite(strip, (0, H))
    out = OUT / "sheet3.png"; full.convert("RGB").save(out); print(out)
    s7.convert("RGB").save(OUT / "sheet3_ortho7.png")


if __name__ == "__main__":
    sheet()
