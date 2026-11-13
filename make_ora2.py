"""Second layered file: the owner's layout plus rings, canopy and the mesh details reference."""
import io, zipfile, json
from pathlib import Path
from xml.sax.saxutils import quoteattr
from PIL import Image, ImageDraw
from build import ship_px, disc, rim, BODY, AMBER, HOLE
from build2 import ring, CANOPY

HERE = Path(__file__).parent
META = json.load(open(HERE / "owner" / "layers.json"))
W, H = META["w"], META["h"]


def lay(g, color):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    for p in getattr(g, "geoms", [g]):
        if p.geom_type != "Polygon" or p.is_empty:
            continue
        d.polygon(list(p.exterior.coords), fill=255)
        for h in p.interiors:
            d.polygon(list(h.coords), fill=0)
    img.paste(Image.new("RGBA", (W, H), color), (0, 0), m)
    return img


wide_f, wide_b = ring(590, 0.20, -20, 70)
gap_f, gap_b = ring(600, 0.20, -20, 80, bands=((0, 0.55), (0.70, 1.0)))
thin_f, thin_b = ring(560, 0.20, -20, 34)

LAYERS = [  # top first: (name, image, visible, opacity)
    ("ring wide - front", lay(wide_f, AMBER), True, 1.0),
    ("ring two-band - front", lay(gap_f, AMBER), False, 1.0),
    ("ring thin - front", lay(thin_f, AMBER), False, 1.0),
    ("REFERENCE - mesh details (canopy, cores, ink)", Image.open(HERE / "owner_details.png").convert("RGBA"), False, 0.6),
    ("canopy shape (cyan, for moving/reshaping)", lay(CANOPY, "#5FD4E8"), False, 1.0),
    ("ship - with canopy cut-out", lay(ship_px.difference(CANOPY), HOLE), True, 1.0),
    ("ship - solid", lay(ship_px, HOLE), False, 1.0),
    ("rim light", lay(rim, AMBER), True, 1.0),
    ("planet disc", lay(disc, BODY), True, 1.0),
    ("ring wide - back (keep below planet)", lay(wide_b, AMBER), True, 1.0),
    ("ring two-band - back", lay(gap_b, AMBER), False, 1.0),
    ("ring thin - back", lay(thin_b, AMBER), False, 1.0),
    ("check - dark taskbar", Image.new("RGBA", (W, H), "#202020"), False, 1.0),
    ("check - light taskbar", Image.new("RGBA", (W, H), "#F3F3F3"), False, 1.0),
]


def png(im):
    b = io.BytesIO(); im.save(b, "PNG"); return b.getvalue()


out = HERE / "icon_rings.ora"
with zipfile.ZipFile(out, "w") as z:
    z.writestr(zipfile.ZipInfo("mimetype"), "image/openraster", compress_type=zipfile.ZIP_STORED)
    stack = []
    comp = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for i, (name, im, vis, op) in enumerate(LAYERS):
        bb = im.getbbox() or (0, 0, 1, 1)
        z.writestr(f"data/{i:02d}.png", png(im.crop(bb)))
        stack.append(f'  <layer name={quoteattr(name)} src="data/{i:02d}.png" x="{bb[0]}" y="{bb[1]}" '
                     f'visibility="{"visible" if vis else "hidden"}" opacity="{op}"/>')
    for name, im, vis, op in reversed(LAYERS):
        if vis:
            comp.alpha_composite(im)
    z.writestr("mergedimage.png", png(comp))
    th = comp.copy(); th.thumbnail((256, 256)); z.writestr("Thumbnails/thumbnail.png", png(th))
    z.writestr("stack.xml", f'<?xml version="1.0" encoding="UTF-8"?>\n<image w="{W}" h="{H}" version="0.0.5">\n<stack>\n'
               + "\n".join(stack) + "\n</stack>\n</image>\n")
print(out)
