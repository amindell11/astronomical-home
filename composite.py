import sys
from PIL import Image, ImageChops, ImageDraw
R = sys.argv[1]; B = sys.argv[2]; O = sys.argv[3]
def common_bbox(n):
    im = Image.open(f"{R}/out2/{n}-common-top.png").convert("RGB")
    diff = ImageChops.difference(im, Image.new("RGB", im.size, (122,122,122))).convert("L").point(lambda v: 255 if v > 6 else 0)
    return diff.getbbox()
def sprite(n, px_per_unit):
    a = Image.open(f"{R}/out2/{n}-top-alpha.png")
    ab = a.getchannel("A").getbbox(); cb = common_bbox(n)
    crop = a.crop(ab)
    common_w = cb[2]-cb[0]              # px at common scale (1024 px = 3.0005 units)
    units = common_w / 1024 * 3.0005188549266144
    w = max(1, round(units * px_per_unit))
    h = max(1, round(crop.height * w / crop.width))
    return crop.resize((w, h), Image.LANCZOS)
ids = ["minion", "rival-a", "rival-b", "elite"]
for loc in ["BlueCrescent", "CoralSunset"]:
    plate = Image.open(f"{B}/{loc}-plate-9.png").convert("RGBA")
    # in game: ortho half-height 9 -> 60 px/world unit; ships at lossyScale 1.35
    for tag, ppu in [("game", 60*1.35), ("large", 60*1.35*2.6)]:
        canvas = plate.copy()
        sprites = [sprite(n, ppu) for n in ids]
        total = sum(s.width for s in sprites); gap = (canvas.width - total) / (len(sprites)+1)
        x = gap
        d = ImageDraw.Draw(canvas)
        for n, s in zip(ids, sprites):
            y = (canvas.height - s.height)//2
            canvas.alpha_composite(s, (int(x), y))
            d.text((int(x), y + s.height + 8), n, fill=(255,255,255,255))
            x += s.width + gap
        canvas.convert("RGB").save(f"{O}/{loc}-regions-{tag}.png")
print("ok")
