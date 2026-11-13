"""Round 7: glass shapes that taper, and a one-colour (space / negative space) version."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import Point, Polygon, MultiPoint
from shapely.affinity import scale, translate
from shapely.ops import unary_union
from build import ship_px, disc, rim, CX, CY, R, HOLE, BODY, AMBER, square_frame, raster, svg
from build2 import CANOPY
from build3 import stylized_ring

HERE = Path(__file__).parent
FRONT, BACK = stylized_ring(front=80, back=34, side=0)
cx0, cy0, cx1, cy1 = CANOPY.bounds
CW, CH = cx1 - cx0, cy1 - cy0
CMX = (cx0 + cx1) / 2


def dagger(point_drop=0.18):
    """Canopy whose lower half narrows to a point below its old bottom."""
    top = CANOPY.intersection(Polygon([(cx0 - 5, cy0 - 5), (cx1 + 5, cy0 - 5), (cx1 + 5, cy0 + CH * 0.55), (cx0 - 5, cy0 + CH * 0.55)]))
    tip = Point(CMX, cy1 + CH * point_drop)
    return MultiPoint(list(top.exterior.coords) + [tip.coords[0]]).convex_hull


def crescent_of(shape, shift=0.38, stretch=1.25):
    """Lit sliver on the left: the shape minus a copy shifted right and stretched so both ends come to points."""
    b = shape.bounds
    w = b[2] - b[0]
    cover = translate(scale(shape, 1.0, stretch, origin="centroid"), w * shift, 0)
    g = shape.difference(cover)
    return max(getattr(g, "geoms", [g]), key=lambda p: p.area)


GLASS = {
    "crescent, pointed both ends": crescent_of(CANOPY),
    "dagger canopy (full)": dagger(),
    "dagger crescent": crescent_of(dagger(), shift=0.40, stretch=1.2),
    "thin crescent, pointed both ends": crescent_of(CANOPY, shift=0.25, stretch=1.3),
}


def colour(glass, glass_fill=AMBER):
    return [(BACK, AMBER), (disc, BODY), (rim, AMBER), (ship_px, HOLE), (glass, glass_fill), (FRONT, AMBER)]


# ---- mono: one ink colour; gaps (knock-outs) separate shapes that would otherwise merge
GAP = 14
CORE = Point(CX + 0.045 * 1024 * 0.6, CY + 0.045 * 1024 * 0.8).buffer(R, 256)  # planet minus its rim light


def clean(g, min_area=150):
    """Drop slivers and pin-holes left by overlapping boolean ops."""
    out = []
    for p in getattr(g, "geoms", [g]):
        if p.geom_type == "Polygon" and p.area > min_area:
            out.append(Polygon(p.exterior, [h for h in p.interiors if Polygon(h).area > min_area]))
    return unary_union(out)


def mono_xor(glass):
    """Each layer flips ink: ship is a hole in the planet, the crossbar flips back to ink over the ship."""
    g = disc.symmetric_difference(ship_px)
    g = g.symmetric_difference(FRONT)
    g = g.symmetric_difference(glass)
    g = g.difference(disc.boundary.buffer(GAP / 2).intersection(BACK.buffer(GAP))).union(BACK.difference(disc.buffer(GAP)))
    return g


def mono_glyph(glass, rim_gap=True):
    """Ship + crossbar form one negative A inside the planet; canopy is the ink counter; ring is ink outside."""
    a = ship_px.union(FRONT.intersection(disc))
    planet = disc.difference(rim.buffer(GAP / 2).intersection(CORE).buffer(0)) if rim_gap else disc
    if rim_gap:
        planet = disc.difference(CORE.boundary.buffer(GAP / 2).intersection(disc.buffer(-GAP)))
    g = planet.difference(a)
    g = g.union(glass.intersection(ship_px))
    g = g.union(FRONT.difference(disc.buffer(GAP)))
    g = g.union(BACK.difference(disc.buffer(GAP)))
    # ship parts outside the planet (nose, tines) stay ink so the silhouette is still there
    g = g.union(ship_px.difference(disc).difference(FRONT.buffer(GAP)))
    return g


def mono_knockout(glass):
    """Ship is a hole; the ring is ink everywhere, separated from what it crosses by a gap halo."""
    planet = disc.difference(CORE.boundary.buffer(GAP / 2).intersection(disc.buffer(-GAP)))
    g = planet.difference(ship_px).union(ship_px.difference(disc))
    g = g.union(glass.intersection(ship_px))
    g = g.difference(FRONT.buffer(GAP)).union(FRONT)
    g = g.difference(BACK.buffer(GAP).difference(disc.buffer(GAP))).union(BACK.difference(disc.buffer(GAP)))
    return g


def rows_sheet(entries, path, title, font):
    BACKS = {"dark": "#202020", "light": "#F3F3F3"}
    rows = []
    for label, layers_by_bg in entries:
        tiles = []
        for bgname, bg in BACKS.items():
            layers = layers_by_bg(bgname)
            frame = square_frame(layers)
            tiles += [(bg, raster(layers, frame, 256), 256), (bg, raster(layers, frame, 64), 64),
                      (bg, raster(layers, frame, 32).resize((128, 128), Image.NEAREST), 128),
                      (bg, raster(layers, frame, 16).resize((64, 64), Image.NEAREST), 64)]
        pad = 12
        W = 220 + sum(t[2] + pad for t in tiles)
        c = Image.new("RGBA", (W, 280), "#16181D")
        ImageDraw.Draw(c).multiline_text((pad, pad), label, fill="#D8DCE6", font=font)
        x = 220
        for bg, t, s in tiles:
            b = Image.new("RGBA", (s, s), bg); b.alpha_composite(t)
            c.alpha_composite(b, (x, pad + (256 - s) // 2)); x += s + pad
        rows.append(c)
    W = max(r.width for r in rows)
    out = Image.new("RGBA", (W, sum(r.height for r in rows) + 40), "#16181D")
    ImageDraw.Draw(out).text((12, 12), title, fill="#FFF", font=font)
    y = 40
    for r in rows:
        out.alpha_composite(r, (0, y)); y += r.height
    out.convert("RGB").save(path)


if __name__ == "__main__":
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    entries = [(f"{i}  glass:\n{k}", (lambda bg, g=g: colour(g))) for i, (k, g) in enumerate(GLASS.items())]
    rows_sheet(entries, HERE / "sheet9_glass.png", "Sheet 9 - glass shapes (amber fill). Per backdrop: 256, 64, then 32 and 16 at 4x", font)
    big = []
    for k, g in GLASS.items():
        big.append(raster(colour(g), square_frame(colour(g)), 384))
    strip = Image.new("RGBA", (384 * len(big) + 12 * (len(big) - 1), 384), "#202020")
    for i, b in enumerate(big):
        strip.alpha_composite(b, (i * 396, 0))
    strip.save(HERE / "glass_strip.png")

    glass = GLASS["crescent, pointed both ends"]
    ink = {"dark": "#F4EEDF", "light": "#0B1024"}
    mono = [("A  XOR: every shape flips\nink/space where it overlaps", clean(mono_xor(glass))),
            ("B  one negative A:\nship + crossbar are space,\ncanopy crescent is ink", clean(mono_glyph(glass))),
            ("C  knockout: ship is space,\nring is ink with gap halos", clean(mono_knockout(glass)))]
    entries = [(label, (lambda bg, g=g: [(g, ink[bg])])) for label, g in mono]
    rows_sheet(entries, HERE / "sheet10_mono.png", "Sheet 10 - mono: one ink colour (cream on dark, navy on light)", font)
    mbig = Image.new("RGBA", (384 * 3 + 24, 384 * 2 + 12), "#16181D")
    for i, (_, g) in enumerate(mono):
        for j, (bgname, bg) in enumerate((("dark", "#202020"), ("light", "#F3F3F3"))):
            t = Image.new("RGBA", (384, 384), bg)
            t.alpha_composite(raster([(g, ink[bgname])], square_frame([(g, ink[bgname])]), 384))
            mbig.alpha_composite(t, (i * 396, j * 396))
    mbig.save(HERE / "mono_large.png")
