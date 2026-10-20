"""Weapon glyphs, round 2: one shared frame, five inner marks from one vocabulary.

Frames: disc-cut (negative space in a filled disc), arc (interrupted-arc housing, the hangar
mockup's construction), ring (closed thin ring). Same inner marks under every frame.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math

OUT = Path(__file__).parent / "out"; OUT.mkdir(exist_ok=True)
BACKDROP = Path(r"D:\amind\git\astronomical-home\doc\design\assets\Pasted image 20260923020110.png")

# ---- knobs ----
STROKE = 0.085       # frame stroke, fraction of glyph height
MARK_STROKE = 0.11   # inner mark stroke (cut marks need a little more width to survive small sizes)
MARK_SCALE = 0.56    # inner mark box as a fraction of the glyph box
FRAME_R = 0.42       # frame radius
ARC_GAP = 70         # degrees of opening in the arc frame
ARC_GAP_AT = -45     # where the opening points (deg, 0 = right, -90 = up)
INK = (238, 238, 230); INK_A = 235
DOT_R = 0.14; DIAMOND = 0.2
SS = 8
GLYPH_SCALE = 1.3    # glyph height / bar height

def L(x1, y1, x2, y2): return ("line", x1, y1, x2, y2)
def A(cx, cy, r, a0, a1): return ("arc", cx, cy, r, a0, a1)
def D(cx, cy, r=DOT_R): return ("dot", cx, cy, r)
def M(cx, cy, s=DIAMOND): return ("diamond", cx, cy, s)
def R(cx, cy, r): return ("ring", cx, cy, r)

# Inner marks, unit box, scaled into the frame by MARK_SCALE.
MARKS = {
    "laser":   [L(0.02, 0.5, 0.58, 0.5), L(0.74, 0.5, 0.98, 0.5)],          # beam with a break
    "missile": [L(0.22, 0.12, 0.78, 0.5), L(0.22, 0.88, 0.78, 0.5)],        # chevron: guided toward
    "ripper":  [L(0.22, 0.2, 0.22, 0.8), L(0.5, 0.2, 0.5, 0.8), L(0.78, 0.2, 0.78, 0.8)],  # rounds
    "mine":    [D(0.5, 0.5, 0.15), R(0.5, 0.5, 0.42)],                     # charge: dot in a ring
    "railgun": [L(0.12, 0.8, 0.62, 0.22), L(0.38, 0.88, 0.88, 0.3)],       # offset diagonals (hangar mockup)
}
MOD_CHARGE = [D(0.90, 0.10, 0.07)]              # detached dot outside the frame, top-right
MOD_TURBO  = [L(0.68, 0.96, 0.98, 0.96)]        # detached short stroke outside, bottom-right


def scaled(prims, k, cx=0.5, cy=0.5):
    out = []
    for p in prims:
        t = p[0]
        if t == "line": out.append(("line", cx + (p[1]-.5)*k, cy + (p[2]-.5)*k, cx + (p[3]-.5)*k, cy + (p[4]-.5)*k))
        elif t == "arc": out.append(("arc", cx + (p[1]-.5)*k, cy + (p[2]-.5)*k, p[3]*k, p[4], p[5]))
        elif t == "dot": out.append(("dot", cx + (p[1]-.5)*k, cy + (p[2]-.5)*k, p[3]*k))
        elif t == "diamond": out.append(("diamond", cx + (p[1]-.5)*k, cy + (p[2]-.5)*k, p[3]*k))
        elif t == "ring": out.append(("ring", cx + (p[1]-.5)*k, cy + (p[2]-.5)*k, p[3]*k))
    return out


def draw_prims(d, prims, s, sw, ink):
    for p in prims:
        k = p[0]
        if k == "line":
            _, x1, y1, x2, y2 = p
            d.line([(x1*s, y1*s), (x2*s, y2*s)], fill=ink, width=sw)
            for (x, y) in ((x1, y1), (x2, y2)):
                d.ellipse([x*s - sw/2, y*s - sw/2, x*s + sw/2, y*s + sw/2], fill=ink)
        elif k == "arc":
            _, cx, cy, r, a0, a1 = p
            d.arc([(cx-r)*s, (cy-r)*s, (cx+r)*s, (cy+r)*s], a0, a1, fill=ink, width=sw)
            for a in (a0, a1):
                x = cx*s + r*s*math.cos(math.radians(a)); y = cy*s + r*s*math.sin(math.radians(a))
                d.ellipse([x - sw/2, y - sw/2, x + sw/2, y + sw/2], fill=ink)
        elif k == "dot":
            _, cx, cy, r = p
            d.ellipse([(cx-r)*s, (cy-r)*s, (cx+r)*s, (cy+r)*s], fill=ink)
        elif k == "diamond":
            _, cx, cy, hs = p
            d.polygon([(cx*s, (cy-hs)*s), ((cx+hs)*s, cy*s), (cx*s, (cy+hs)*s), ((cx-hs)*s, cy*s)], fill=ink)
        elif k == "ring":
            _, cx, cy, r = p
            d.ellipse([(cx-r)*s, (cy-r)*s, (cx+r)*s, (cy+r)*s], outline=ink, width=sw)


def layer(prims, S, sw):
    im = Image.new("L", (S, S), 0)
    draw_prims(ImageDraw.Draw(im), prims, S, sw, 255)
    return im


def render(frame, mark, h, mods=()):
    S = h * SS
    fsw = max(1, round(STROKE * S)); msw = max(1, round(MARK_STROKE * S))
    inner = scaled(mark, MARK_SCALE)
    if frame == "disc-cut":
        alpha = layer([D(0.5, 0.5, FRAME_R)], S, fsw)
        cut = layer(inner, S, msw)
        alpha = Image.eval(Image.composite(Image.new("L", (S, S), 0), alpha, cut), lambda v: v)
        # soft edge of the cut: subtract the cut mask
        alpha = Image.fromarray((__import__("numpy").array(alpha, dtype=int) * (255 - __import__("numpy").array(cut, dtype=int)) // 255).astype("uint8"))
    elif frame == "arc":
        a0 = ARC_GAP_AT + ARC_GAP / 2; a1 = ARC_GAP_AT - ARC_GAP / 2 + 360
        alpha = layer([A(0.5, 0.5, FRAME_R, a0, a1)], S, fsw)
        alpha = Image.fromarray(__import__("numpy").maximum(__import__("numpy").array(alpha), __import__("numpy").array(layer(inner, S, msw))))
    elif frame == "ring":
        alpha = Image.fromarray(__import__("numpy").maximum(__import__("numpy").array(layer([R(0.5, 0.5, FRAME_R)], S, fsw)), __import__("numpy").array(layer(inner, S, msw))))
    elif frame == "bare":
        alpha = layer(inner, S, msw)
    if mods:
        alpha = Image.fromarray(__import__("numpy").maximum(__import__("numpy").array(alpha), __import__("numpy").array(layer(list(mods), S, fsw))))
    alpha = alpha.point(lambda v: v * INK_A // 255)
    im = Image.new("RGBA", (S, S), INK + (0,)); im.putalpha(alpha)
    return im.resize((h, h), Image.LANCZOS)


def px_per_unit(ortho=7.0, screen_h=1080): return screen_h / (2 * ortho)

def mock_stack(entries, ortho, bd, frame):
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3 * ppu), max(2, int(0.3 * ppu)); gap = int(0.4 * ppu); gh = int(bar_h * GLYPH_SCALE)
    tile_w = bar_w + int(1.6 * ppu) + 24; tile_h = bar_h + 2 * gap + 24
    W, H = tile_w * len(entries), tile_h
    im = bd.crop((0, 0, bd.width, int(bd.width * H / W))).resize((W, H)).convert("RGBA")
    d = ImageDraw.Draw(im)
    for i, (name, prim, sec) in enumerate(entries):
        x0 = i * tile_w + 12; ys = 12; yh = ys + gap; yr = yh + gap
        for y, col, pct in ((ys, (0, 230, 255, 180), 0.7), (yh, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128))
            d.rectangle([x0, y, x0 + int(bar_w * pct), y + bar_h], fill=col)
        gx = x0
        for mk in (prim, sec):
            if mk is None: continue
            mark, mods = mk
            im.alpha_composite(render(frame, mark, gh, mods), (gx, yr))
            gx += gh + max(2, int(0.08 * ppu))
    return im


def sheet():
    bd = Image.open(BACKDROP).convert("RGB").crop((150, 330, 1000, 560))
    try: font = ImageFont.truetype("arial.ttf", 16); small = ImageFont.truetype("arial.ttf", 12)
    except Exception: font = small = ImageFont.load_default()
    BIG = 96
    enemies = [("Lasers", (MARKS["laser"], ()), None), ("Missiles", (MARKS["missile"], ()), None), ("Rippers", (MARKS["ripper"], ()), None),
               ("TurboLasers", (MARKS["laser"], MOD_TURBO), None), ("player", (MARKS["laser"], ()), (MARKS["missile"], ()))]
    rows = []
    for frame in ("disc-cut", "arc", "ring", "bare"):
        cols = list(MARKS.items()) + [("+charge", MARKS["laser"]), ("+turbo", MARKS["laser"])]
        row = Image.new("RGBA", (len(cols) * (BIG + 36) + 130, BIG + 44), (14, 18, 32, 255))
        d = ImageDraw.Draw(row); d.text((8, 8), frame.upper(), fill=(200, 200, 190), font=font)
        for i, (name, mark) in enumerate(cols):
            x = 130 + i * (BIG + 36)
            mods = MOD_CHARGE if name == "+charge" else MOD_TURBO if name == "+turbo" else ()
            row.alpha_composite(render(frame, mark, BIG, mods), (x, 28)); d.text((x, 10), name, fill=(150, 150, 140), font=small)
        rows.append((row, mock_stack(enemies, 7.0, bd, frame), mock_stack(enemies, 15.0, bd, frame)))
    W = max(max(a.width, b.width + c.width + 16) for a, b, c in rows) + 16
    H = sum(a.height + max(b.height, c.height) + 28 for a, b, c in rows) + 40
    sh = Image.new("RGBA", (W, H), (10, 12, 22, 255)); d = ImageDraw.Draw(sh)
    d.text((8, 8), "One frame, five marks. Rows: frame treatment. Strips: M1 enemies (Lasers, Missiles, Rippers, TurboLasers) + player pair, at ortho 7 and ortho 15.", fill=(170, 170, 160), font=small)
    y = 32
    for a, b, c in rows:
        sh.alpha_composite(a, (8, y)); y += a.height
        sh.alpha_composite(b, (8, y)); sh.alpha_composite(c, (8 + b.width + 16, y)); y += max(b.height, c.height) + 28
    out = OUT / "sheet2.png"; sh.convert("RGB").save(out); print(out)
    sh.crop((0, 0, W, H)).convert("RGB")

if __name__ == "__main__":
    sheet()
