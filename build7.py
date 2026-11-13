"""Round 10: a simplification ladder for taskbar sizes, colour and mono."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Point
from shapely.affinity import translate, scale
from build import ship_px, disc, HOLE, BODY, AMBER, CX, CY, R, square_frame, raster
from build3 import stylized_ring
from build4 import GLASS, clean
from build5 import CORE, edge

HERE = Path(__file__).parent
CREAM = "#F4EEDF"


def make(step):
    """step 0 = the full #6 design; each step strips more for small sizes."""
    gap = 14 if step < 2 else 22
    ship = translate(ship_px, 0, 25)
    if step >= 2:  # fill between the tines, drop the small notches
        ship = ship.buffer(26, join_style=2).buffer(-26, join_style=2).simplify(6)
    if step >= 3:  # pull the nose and tines in so the frame can crop tight on the planet
        ship = scale(ship, 0.86, 0.86, origin=(CX, CY + 10))
    if step >= 4:  # bare dart: nose, wing shoulders and outer fin tips only
        ship = ship.convex_hull.difference(ship.convex_hull.difference(ship).buffer(-18).buffer(18))
    rx = 590 if step < 3 else 520
    fr = 80 if step < 2 else 110
    front, back = stylized_ring(front=fr, back=34 if step < 2 else 40, side=0, rx=rx, ry=rx * 0.2)
    front, back = translate(front, 0, -25), translate(back, 0, -25)
    lit = 16 if step < 2 else 26
    e = edge(ship, "lit", lit).difference(disc)
    glass = translate(GLASS["dagger crescent"], 0, 25) if step < 1 else None
    rim_gap = step < 1
    # colour
    base = disc.union(ship)
    core = (CORE.intersection(disc) if rim_gap else CORE.intersection(disc)).difference(ship)
    rimg = disc.difference(CORE).difference(ship)
    if rim_gap:
        core = core.difference(CORE.boundary.buffer(gap / 2))
        rimg = rimg.difference(CORE.boundary.buffer(gap / 2))
    halo = front.buffer(gap).difference(front)
    col = [(base, HOLE), (clean(core.difference(halo)), BODY), (clean(rimg.difference(halo)), AMBER),
           (clean(e.difference(halo)), AMBER)]
    if glass is not None:
        col.append((clean(glass.intersection(ship)), AMBER))
    if step < 2:
        col.append((clean(back.difference(disc.buffer(gap))), AMBER))
    col.append((front, AMBER))
    # mono
    planet = disc.difference(CORE.boundary.buffer(gap / 2).intersection(disc.buffer(-gap))) if rim_gap else disc
    m = planet.difference(ship).union(e)
    if glass is not None:
        m = m.union(glass.intersection(ship))
    m = m.difference(front.buffer(gap)).union(front)
    if step < 2:
        m = m.union(back.difference(disc.buffer(gap)))
    return col, [(clean(m), CREAM)]


LABELS = ["0  full design (#6)",
          "1  drop canopy + rim gap",
          "2  fill tines, drop back ring,\nthicker ring + edge",
          "3  tighter crop: ship and\nring pulled in",
          "4  bare dart ship"]

SIZES = [16, 24, 32, 48]

if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    rows = []
    for step, label in enumerate(LABELS):
        col, mono = make(step)
        tiles = []
        for layers in (col, mono):
            frame = square_frame(layers, margin=0.02)
            for bg in ("#202020", "#F3F3F3"):
                if layers is mono and bg == "#F3F3F3":
                    layers = [(mono[0][0], "#0B1024")]
                    frame = square_frame(layers, margin=0.02)
                for s in SIZES:
                    tiles.append((bg, raster(layers, frame, s), s))
                for s in (24, 32):
                    tiles.append((bg, raster(layers, frame, s).resize((s * 4, s * 4), Image.NEAREST), s * 4))
        pad = 8
        W = 230 + sum(t[2] + pad for t in tiles)
        c = Image.new("RGBA", (W, 128 + pad * 2), "#16181D")
        ImageDraw.Draw(c).multiline_text((pad, pad), label, fill="#D8DCE6", font=font)
        x = 230
        for bg, t, s in tiles:
            b = Image.new("RGBA", (s + 8, s + 8), bg); b.alpha_composite(t, (4, 4))
            c.alpha_composite(b, (x, pad + (128 - s) // 2)); x += s + 8 + pad
        rows.append(c)
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), "Sheet 13 - simplification ladder. Colour then mono; per taskbar: 16, 24, 32, 48 real size, then 24 and 32 at 4x", fill="#FFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y)); y += r.height
    out.convert("RGB").save(HERE / "sheet13_ladder.png")

    # fake taskbar strip at 100% and 150% scaling
    strip = Image.new("RGBA", (900, 140), "#16181D")
    d = ImageDraw.Draw(strip)
    for row, (s, h) in enumerate(((24, 48), (32, 64))):
        y0 = 10 + row * 66
        d.rectangle([0, y0, 900, y0 + h], fill="#202020")
        x = 20
        for step in range(5):
            col, _ = make(step)
            t = raster(col, square_frame(col, margin=0.02), s)
            strip.alpha_composite(t, (x, y0 + (h - s) // 2))
            d.text((x + s + 6, y0 + h // 2 - 9), str(step), fill="#777", font=font)
            x += s + 150
    strip.save(HERE / "taskbar_sim.png")
