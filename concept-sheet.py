import json, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root = Path(sys.argv[1]); out = Path(sys.argv[2]); rnd = sys.argv[3]
T = 240; VIEWS = ["top", "side", "three", "rear"]; GAME = [(48, 36), (64, 48)]  # (rival px, minion px at 75%)
font = ImageFont.truetype("arial.ttf", 18); small = ImageFont.truetype("arial.ttf", 14)
blocks = []
for ship, faction in [("valis", "Shoal"), ("bulwark", "Bastion"), ("nightshade", "Veil")]:
    variants = list(json.loads((root.parent / f"{ship}.cuts.json").read_text()))
    w = 200 + T * len(VIEWS) + 2 * T
    img = Image.new("RGB", (w, 40 + T * len(variants)), (24, 24, 28)); d = ImageDraw.Draw(img)
    d.text((10, 10), f"{faction} / {ship.title()}  ({rnd})", font=font, fill=(240, 240, 240))
    for c, v in enumerate(VIEWS + ["game top 48/36px", "game 3/4 64/48px"]):
        d.text((200 + c * T + 8, 14), v, font=small, fill=(170, 170, 170))
    for r, var in enumerate(variants):
        y = 40 + r * T
        d.text((10, y + T // 2 - 10), var.replace("_", " "), font=small, fill=(240, 240, 240))
        for c, view in enumerate(VIEWS):
            img.paste(Image.open(root / ship / f"{var}_{view}.png").convert("RGB").resize((T, T), Image.LANCZOS), (200 + c * T, y))
        for g, (view, (rp, mp)) in enumerate(zip(["top", "three"], GAME)):
            px = rp if var == "rival" else mp
            tile = Image.open(root / ship / f"{var}_{view}.png").convert("RGB").resize((px, px), Image.LANCZOS)
            big = tile.resize((px * 3, px * 3), Image.NEAREST)
            cell = Image.new("RGB", (T, T), (24, 24, 28)); cell.paste(big, ((T - px * 3) // 2, (T - px * 3) // 2))
            img.paste(cell, (200 + (len(VIEWS) + g) * T, y))
    blocks.append(img)
W = max(b.width for b in blocks); H = sum(b.height for b in blocks) + 20 * len(blocks)
sheet = Image.new("RGB", (W, H), (12, 12, 14)); y = 0
for b in blocks:
    sheet.paste(b, (0, y)); y += b.height + 20
sheet.save(out)
print(out, sheet.size)
