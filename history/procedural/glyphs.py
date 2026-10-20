"""Weapon glyph sketches: candidates per family, rendered large and at game scale.

Each glyph is a list of primitives in a unit box (y down). Knobs are the module constants.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math, sys

OUT = Path(__file__).parent / "out"
OUT.mkdir(exist_ok=True)
BACKDROP = Path(r"D:\amind\git\astronomical-home\doc\design\assets\Pasted image 20260923020110.png")

# ---- knobs ----
STROKE = 0.085         # stroke width, fraction of glyph height
INK = (238, 238, 230)  # off-white, matches thin HUD strokes
INK_A = 235            # ink alpha
DOT_R = 0.075          # detached dot radius
DIAMOND = 0.14         # diamond half-size
SS = 8                 # supersample
GLYPH_SCALE = 1.3      # glyph height / bar height (mockup glyphs sit a touch taller than their bar)

# ---- primitives: ("line",x1,y1,x2,y2) ("arc",cx,cy,r,a0,a1) ("dot",cx,cy,r) ("diamond",cx,cy,s) ("ring",cx,cy,r)
def L(x1, y1, x2, y2): return ("line", x1, y1, x2, y2)
def A(cx, cy, r, a0, a1): return ("arc", cx, cy, r, a0, a1)
def D(cx, cy, r=DOT_R): return ("dot", cx, cy, r)
def M(cx, cy, s=DIAMOND): return ("diamond", cx, cy, s)
def R(cx, cy, r): return ("ring", cx, cy, r)

GLYPHS = {
    # Laser: fast straight beam. Interrupted / offset strokes.
    "laser": {
        "beam-break":  [L(0.05, 0.5, 0.58, 0.5), L(0.70, 0.5, 0.95, 0.5)],
        "emitter":     [D(0.12, 0.5), L(0.30, 0.5, 0.95, 0.5)],
        "offset-pair": [L(0.05, 0.38, 0.62, 0.38), L(0.38, 0.62, 0.95, 0.62)],
    },
    # Missile: guided once locked. Something led toward a detached mark.
    "missile": {
        "homing":   [L(0.08, 0.22, 0.58, 0.5), L(0.08, 0.78, 0.58, 0.5), M(0.84, 0.5)],
        "trail":    [M(0.70, 0.40), L(0.12, 0.80, 0.42, 0.56), L(0.30, 0.92, 0.55, 0.72)],
        "track":    [L(0.05, 0.5, 0.20, 0.5), L(0.32, 0.5, 0.47, 0.5), L(0.59, 0.5, 0.68, 0.5), M(0.86, 0.5)],
    },
    # Ripper: ballistic autocannon, rounds. Rattle / repetition.
    "ripper": {
        "stagger": [L(0.20, 0.30, 0.20, 0.70), L(0.50, 0.42, 0.50, 0.82), L(0.80, 0.18, 0.80, 0.58)],
        "rattle":  [L(0.05, 0.5, 0.20, 0.5), L(0.30, 0.5, 0.45, 0.5), L(0.55, 0.5, 0.70, 0.5), L(0.80, 0.5, 0.95, 0.5)],
        "rounds":  [L(0.05, 0.5, 0.45, 0.5), D(0.63, 0.5), D(0.86, 0.5)],
    },
    # Concussion mine: a dropped charge. Interrupted ring, radial marks.
    "mine": {
        "charge":     [A(0.5, 0.5, 0.26, 20, 320), D(0.5, 0.5)],
        "tri-radial": [D(0.5, 0.5), L(0.5, 0.08, 0.5, 0.24), L(0.14, 0.72, 0.28, 0.63), L(0.86, 0.72, 0.72, 0.63)],
        "shockwave":  [R(0.5, 0.5, 0.17), A(0.5, 0.5, 0.40, 300, 20), A(0.5, 0.5, 0.40, 120, 200)],
    },
    # Railgun: charged hitscan. Rails / a long line with a node.
    "railgun": {
        "offset-diag": [L(0.12, 0.74, 0.52, 0.30), L(0.34, 0.80, 0.74, 0.36), D(0.86, 0.24)],
        "rails":       [L(0.05, 0.40, 0.68, 0.40), L(0.05, 0.60, 0.68, 0.60), M(0.85, 0.5)],
        "node-beam":   [L(0.05, 0.5, 0.35, 0.5), M(0.5, 0.5), L(0.65, 0.5, 0.95, 0.5)],
    },
}

# Variant modifiers, shown on the laser row: a detached dot (charge) / a doubled short stroke (turbo).
MOD_CHARGE = [D(0.10, 0.12, 0.06)]
MOD_TURBO  = [L(0.05, 0.78, 0.35, 0.78)]


def render_glyph(prims, h, w=None, pad=0.0):
    """Render primitives into an RGBA tile of height h (px); width w defaults to h."""
    w = w or h
    W, H = w * SS, h * SS
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = H  # unit box scale (height)
    sw = max(1, int(round(STROKE * s)))
    ink = INK + (INK_A,)
    for p in prims:
        k = p[0]
        if k == "line":
            _, x1, y1, x2, y2 = p
            d.line([(x1 * s, y1 * s), (x2 * s, y2 * s)], fill=ink, width=sw)
            for (x, y) in ((x1, y1), (x2, y2)):  # round caps
                d.ellipse([x * s - sw / 2, y * s - sw / 2, x * s + sw / 2, y * s + sw / 2], fill=ink)
        elif k == "arc":
            _, cx, cy, r, a0, a1 = p
            box = [(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s]
            d.arc(box, a0, a1, fill=ink, width=sw)
            for a in (a0, a1):
                x = cx * s + r * s * math.cos(math.radians(a)); y = cy * s + r * s * math.sin(math.radians(a))
                d.ellipse([x - sw / 2, y - sw / 2, x + sw / 2, y + sw / 2], fill=ink)
        elif k == "dot":
            _, cx, cy, r = p
            d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=ink)
        elif k == "diamond":
            _, cx, cy, hs = p
            d.polygon([(cx * s, (cy - hs) * s), ((cx + hs) * s, cy * s), (cx * s, (cy + hs) * s), ((cx - hs) * s, cy * s)], fill=ink)
        elif k == "ring":
            _, cx, cy, r = p
            d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], outline=ink, width=sw)
    return im.resize((w, h), Image.LANCZOS)


# ---- game-scale mock: bar stack + glyph row over a nebula crop ----
# World: bar 3 x 0.3 u, shield at +2.1, hull at +1.7, gap 0.4 between centres. Glyph row at +1.3, glyph h = 0.3 u.
def px_per_unit(ortho=7.0, screen_h=1080): return screen_h / (2 * ortho)

def mock_stack(glyph_pairs, ortho, backdrop, label):
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3 * ppu), max(2, int(0.3 * ppu))
    gap = int(0.4 * ppu)
    gh = int(bar_h * GLYPH_SCALE)
    n = len(glyph_pairs)
    tile_w = bar_w + int(1.6 * ppu) + 24
    tile_h = bar_h + 2 * gap + 24
    W, H = tile_w * n, tile_h
    im = backdrop.resize((W, H)) if backdrop.width / backdrop.height > W / H else backdrop.crop((0, 0, backdrop.width, int(backdrop.width * H / W))).resize((W, H))
    im = im.convert("RGBA")
    d = ImageDraw.Draw(im)
    for i, (name, prims_a, prims_b) in enumerate(glyph_pairs):
        x0 = i * tile_w + 12
        y_shield = 12
        y_hull = y_shield + gap
        y_row = y_hull + gap
        for y, col, pct in ((y_shield, (0, 230, 255, 180), 0.7), (y_hull, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128))
            d.rectangle([x0, y, x0 + int(bar_w * pct), y + bar_h], fill=col)
        # glyph row: fixed pair, primary then secondary, left-aligned under the hull bar
        gx = x0
        for prims in (prims_a, prims_b):
            if prims is None: continue
            g = render_glyph(prims, gh)
            im.alpha_composite(g, (gx, y_row))
            gx += gh + max(2, int(0.08 * ppu))
    return im


def sheet():
    backdrop = Image.open(BACKDROP).convert("RGB")
    # a quiet nebula crop for the mock strips
    bd = backdrop.crop((150, 330, 1000, 560))
    try:
        font = ImageFont.truetype("arial.ttf", 16); small = ImageFont.truetype("arial.ttf", 12)
    except Exception:
        font = small = ImageFont.load_default()

    BIG = 96
    rows = []
    for fam, cands in GLYPHS.items():
        row_big = Image.new("RGBA", (len(cands) * (BIG + 40) + 120, BIG + 44), (14, 18, 32, 255))
        d = ImageDraw.Draw(row_big)
        d.text((8, 8), fam.upper(), fill=(200, 200, 190), font=font)
        for i, (cname, prims) in enumerate(cands.items()):
            x = 120 + i * (BIG + 40)
            row_big.alpha_composite(render_glyph(prims, BIG), (x, 28))
            d.text((x, 10), cname, fill=(150, 150, 140), font=small)
        # game scale: each candidate as a primary under the stack, secondary = missile 'homing' for context (none on the missile row)
        pairs = []
        for cname, prims in cands.items():
            sec = None if fam == "missile" else GLYPHS["missile"]["homing"]
            pairs.append((cname, prims, sec))
        strip7 = mock_stack(pairs, 7.0, bd, "ortho 7")
        strip15 = mock_stack(pairs, 15.0, bd, "ortho 15")
        rows.append((row_big, strip7, strip15))

    # modifiers row
    base = GLYPHS["laser"]["beam-break"]
    mods = {"laser": base, "+charge": base + MOD_CHARGE, "+turbo": base + MOD_TURBO}
    row_big = Image.new("RGBA", (len(mods) * (BIG + 40) + 120, BIG + 44), (14, 18, 32, 255))
    d = ImageDraw.Draw(row_big)
    d.text((8, 8), "VARIANTS", fill=(200, 200, 190), font=font)
    for i, (cname, prims) in enumerate(mods.items()):
        x = 120 + i * (BIG + 40)
        row_big.alpha_composite(render_glyph(prims, BIG), (x, 28))
        d.text((x, 10), cname, fill=(150, 150, 140), font=small)
    pairs = [(c, p, GLYPHS["missile"]["homing"]) for c, p in mods.items()]
    rows.append((row_big, mock_stack(pairs, 7.0, bd, ""), mock_stack(pairs, 15.0, bd, "")))

    rec = {"laser": GLYPHS["laser"]["beam-break"], "missile": GLYPHS["missile"]["homing"], "ripper": GLYPHS["ripper"]["stagger"],
           "mine": GLYPHS["mine"]["charge"], "railgun": GLYPHS["railgun"]["offset-diag"]}
    row_big = Image.new("RGBA", (len(rec) * (BIG + 40) + 120, BIG + 44), (14, 18, 32, 255))
    d = ImageDraw.Draw(row_big)
    d.text((8, 8), "LINEUP", fill=(200, 200, 190), font=font)
    for i, (cname, prims) in enumerate(rec.items()):
        x = 120 + i * (BIG + 40)
        row_big.alpha_composite(render_glyph(prims, BIG), (x, 28))
        d.text((x, 10), cname, fill=(150, 150, 140), font=small)
    enemies = [("Lasers", rec["laser"], None), ("Missiles", rec["missile"], None), ("Rippers", rec["ripper"], None),
               ("TurboLasers", rec["laser"] + MOD_TURBO, None), ("player", rec["laser"], rec["missile"])]
    rows.append((row_big, mock_stack(enemies, 7.0, bd, ""), mock_stack(enemies, 15.0, bd, "")))

    W = max(max(a.width, b.width + c.width + 16) for a, b, c in rows) + 16
    H = sum(a.height + max(b.height, c.height) + 24 for a, b, c in rows) + 40
    sheet = Image.new("RGBA", (W, H), (10, 12, 22, 255))
    d = ImageDraw.Draw(sheet)
    d.text((8, 8), "Weapon glyph candidates — large | game scale at ortho 7 (default) and ortho 15, under a mocked bar stack. Secondary slot shows missile/homing for context.", fill=(170, 170, 160), font=small)
    y = 32
    for a, b, c in rows:
        sheet.alpha_composite(a, (8, y)); y += a.height
        sheet.alpha_composite(b, (8, y)); sheet.alpha_composite(c, (8 + b.width + 16, y))
        y += max(b.height, c.height) + 24
    out = OUT / "sheet.png"
    sheet.convert("RGB").save(out)
    print(out)


if __name__ == "__main__":
    sheet()
