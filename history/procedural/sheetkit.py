"""Contact-sheet kit: a table of named glyphs at several sizes plus game-scale strips under mocked bars."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import cutstroke as cs

def resolve(entry, h):
    """A glyph entry is (prims, cuts) or a callable f(h) -> (prims, cuts) for size-dependent variants."""
    return entry(h) if callable(entry) else entry

OUT = Path(__file__).parent / "out"; OUT.mkdir(exist_ok=True)
BACKDROP = Path(r"D:\amind\git\astronomical-home\doc\design\assets\Pasted image 20260923020110.png")
BG = (10, 16, 28)
_font = lambda s: ImageFont.truetype("arial.ttf", s)

def px_per_unit(ortho=7.0, screen_h=1080): return screen_h / (2 * ortho)

def table(sets, title, sizes=(96, 32, 24, 16)):
    """sets: list of (column label, {name: (prims, cuts)}) — one column per set, one row per glyph name."""
    names = list(sets[0][1].keys()); small = _font(12); font = _font(18)
    colw = sum(s + 16 for s in sizes) + 30
    W = 170 + len(sets)*colw + 10; rowh = max(sizes) + 30; H = 60 + rowh*len(names)
    im = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(im)
    d.text((16, 12), title, fill=(200, 200, 190), font=small)
    for j, (label, _) in enumerate(sets):
        x = 170 + j*colw; d.text((x, 38), label, fill=(150, 150, 140), font=small)
    y = 60
    for name in names:
        d.line([(16, y), (W - 16, y)], fill=(30, 40, 56)); d.text((16, y + rowh//2 - 10), name, fill=(215, 215, 205), font=font)
        for j, (_, glyphs) in enumerate(sets):
            if name not in glyphs: continue
            x = 170 + j*colw
            for s in sizes:
                prims, cuts = resolve(glyphs[name], s); g = cs.render(prims, s, cuts); im.paste(g, (x, y + 15 + (max(sizes) - s)//2), g); x += s + 16
        y += rowh
    return im

def strip(glyphs, entries, ortho, glyph_scale=1.5, px_floor=0, sec_gap=0.06, bd=None):
    """entries: list of (primary name, secondary name|None). Bars: 3×0.3 u, shield +2.1, hull +1.7."""
    bd = bd or Image.open(BACKDROP).convert("RGB").crop((150, 330, 1000, 560))
    ppu = px_per_unit(ortho)
    bar_w, bar_h = int(3*ppu), max(2, int(0.3*ppu)); gap = int(0.4*ppu)
    gh = max(int(bar_h*glyph_scale), px_floor)
    tile_w = bar_w + int(1.6*ppu) + 24; tile_h = bar_h + gap + bar_h + max(gh, 24) + 20
    W, H = tile_w*len(entries), tile_h
    im = bd.crop((0, 0, bd.width, int(bd.width*H/W))).resize((W, H)).convert("RGBA"); d = ImageDraw.Draw(im)
    for i, (prim, sec) in enumerate(entries):
        x0 = i*tile_w + 12; ys = 10; yh = ys + gap; yr = yh + bar_h + max(4, int(0.1*ppu))
        for y, col, pct in ((ys, (0, 230, 255, 180), 0.7), (yh, (76, 255, 76, 215), 0.85)):
            d.rectangle([x0, y, x0 + bar_w, y + bar_h], fill=(0, 0, 0, 128)); d.rectangle([x0, y, x0 + int(bar_w*pct), y + bar_h], fill=col)
        gx = x0
        for nm in (prim, sec):
            if nm is None: continue
            prims, cuts = resolve(glyphs[nm], gh); im.alpha_composite(cs.render(prims, gh, cuts), (gx, yr)); gx += gh + max(2, int(sec_gap*ppu))
    return im.convert("RGB")

def zoom_study(glyphs, entries, label, zooms=((7.0, 1), (12.0, 2), (18.0, 3)), **kw):
    small = _font(13); parts = []
    for z, mag in zooms:
        s = strip(glyphs, entries, z, **kw); parts.append(s.resize((s.width*mag, s.height*mag), Image.NEAREST))
    H = max(p.height for p in parts) + 26; W = sum(p.width for p in parts) + 20*(len(parts)-1)
    row = Image.new("RGB", (W, H), BG); ImageDraw.Draw(row).text((4, 4), label + "   |   " + "  ·  ".join(f"ortho {z:g} at {m}x" for z, m in zooms), fill=(200, 200, 190), font=small)
    x = 0
    for p in parts: row.paste(p, (x, 26)); x += p.width + 20
    return row

def stack(images, pad=10):
    W = max(i.width for i in images); H = sum(i.height + pad for i in images)
    im = Image.new("RGB", (W, H), BG); y = 0
    for i in images: im.paste(i, (0, y)); y += i.height + pad
    return im
