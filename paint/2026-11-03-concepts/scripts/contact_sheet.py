"""Scratch: one contact sheet per faction round.

python contact_sheet.py ROUND_DIR FACTION SIG OUT_PNG
Top row: today's player paint (signature and Crimson flat renders). Below, every concept in
ROUND_DIR/FACTION, large, with a game-scale strip (each image shrunk so a ship spans ~80 px,
on a dark space background) under it.
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

round_dir, faction, sig, out = Path(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4]
refs = Path(__file__).resolve().parents[1] / "refs"
font = ImageFont.truetype("arial.ttf", 26)
small = ImageFont.truetype("arial.ttf", 20)
TILE_W, TILE_H, GAME_H, PAD = 960, 540, 120, 16
SPACE = (10, 12, 20)


def game_scale(img):
    w = 180  # the 16:9 concept holds two ships side by side; each spans roughly 80 px at this width
    h = round(img.height * w / img.width)
    tiny = img.resize((w, h), Image.LANCZOS)
    strip = Image.new("RGB", (TILE_W, GAME_H), SPACE)
    # dark-space background: knock out the grey card behind the ships
    px = tiny.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if abs(r - g) < 14 and abs(g - b) < 14 and 95 < r < 175:
                px[x, y] = SPACE
    strip.paste(tiny, (PAD, (GAME_H - h) // 2))
    big = tiny.resize((w * 2, h * 2), Image.NEAREST)
    strip.paste(big.crop((0, 0, min(big.width, TILE_W - w - 3 * PAD), min(big.height, GAME_H))), (w + 2 * PAD, max(0, (GAME_H - big.height) // 2)))
    return strip


def tile(img, label):
    canvas = Image.new("RGB", (TILE_W, TILE_H + GAME_H + 40), (32, 34, 40))
    fitted = img.copy()
    fitted.thumbnail((TILE_W, TILE_H))
    canvas.paste(fitted, ((TILE_W - fitted.width) // 2, 40))
    canvas.paste(game_scale(img), (0, 40 + TILE_H))
    ImageDraw.Draw(canvas).text((10, 6), label, fill=(235, 235, 235), font=font)
    return canvas


player = Image.new("RGB", (TILE_W, TILE_H), (120, 122, 128))
for i, name in enumerate((sig.lower(), "crimson")):
    im = Image.open(refs / f"{name}-top-flat.png").convert("RGB")
    im.thumbnail((TILE_H, TILE_H))
    player.paste(im, (i * (TILE_W // 2) + (TILE_W // 2 - im.width) // 2, 0))
tiles = [tile(player, f"TODAY (player paint, flat render): {sig} + Crimson")]
for path in sorted((round_dir / faction).glob("*.jpg")):
    tiles.append(tile(Image.open(path).convert("RGB"), path.stem))
cols = 3
rows = (len(tiles) + cols - 1) // cols
th = tiles[0].height
sheet = Image.new("RGB", (cols * TILE_W + (cols + 1) * PAD, rows * th + (rows + 1) * PAD + 60), (20, 21, 26))
ImageDraw.Draw(sheet).text((PAD, 14), f"{faction.upper()} ({sig} + Crimson): {round_dir.name}. Game-scale strip under each: true size, then 2x nearest.", fill=(255, 255, 255), font=font)
for i, t in enumerate(tiles):
    r, c = divmod(i, cols)
    sheet.paste(t, (PAD + c * (TILE_W + PAD), 60 + PAD + r * (th + PAD)))
sheet.save(out)
print(out)
