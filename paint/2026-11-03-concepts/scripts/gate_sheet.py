"""Scratch: gate-1 sheet. Picked concept beside flat renders in the new region colours, today's paint, elite, glow."""
import sys
from PIL import Image, ImageDraw, ImageFont
S = sys.argv[1]
font = ImageFont.truetype("arial.ttf", 30); big = ImageFont.truetype("arial.ttf", 40)
F = [("shoal", "valis", "Valis", "shoal", "shoal-elite", "jade-iris", "round1/shoal/shoal-a-pro.jpg", "round2/shoal/shoal-glow-pro.jpg", "#fe861a"),
     ("veil", "nightshade", "Nightshade", "veil", "veil-elite", "nightshade", "round1/veil/veil-a-pro.jpg", "round2/veil/veil-glow-pro.jpg", "#a8f63b"),
     ("bastion", "bulwark", "Bulwark", "bastion", "bastion-elite", "plum-bone", "round1/bastion/bastion-a-pro.jpg", "round2/bastion/bastion-glow-nb2.jpg", "#2bd2f7")]
T = 460
def flat(ship, pal):
    p = f"{S}/flat/{ship}-{pal}-coat.png" if ship == "bulwark" else f"{S}/flat/{ship}-{pal}.png"
    im = Image.open(p).convert("RGB"); im.thumbnail((T, T)); return im
rows = []
for fac, ship, sig, pal, elite, today, concept, glow, glowhex in F:
    row = Image.new("RGB", (6 * T + 7 * 12 + 820 - T, T + 60), (24, 25, 30))
    d = ImageDraw.Draw(row)
    x = 12
    def put(im, label):
        global x
        row.paste(im, (x, 50)); d.text((x + 4, 10), label, fill=(240, 240, 240), font=font); x += im.width + 12
    put(flat(ship, today), f"{sig} today")
    put(flat("crimson", "crimson"), "Crimson today")
    c = Image.open(f"{S}/{concept}").convert("RGB"); c.thumbnail((820, T)); put(c, f"concept pick: {concept.split('/')[-1]}")
    put(flat(ship, pal), f"{sig} {pal} (flat)")
    put(flat("crimson", fac), f"Crimson {fac} (flat)")
    put(flat(ship, elite), f"{sig} {elite} (flat)")
    sw = Image.new("RGB", (T // 2, T), glowhex); g = Image.open(f"{S}/{glow}").convert("RGB"); g.thumbnail((T, T))
    rows.append(row)
W = max(r.width for r in rows)
sheet = Image.new("RGB", (W, sum(r.height for r in rows) + 70), (16, 17, 21))
ImageDraw.Draw(sheet).text((12, 12), "Gate 1 (palette + regions), PROVISIONAL: today's paint | picked concept | flat render in the new region colours | elite", fill=(255, 255, 255), font=big)
y = 70
for r in rows: sheet.paste(r, (0, y)); y += r.height
sheet.save(f"{S}/sheets/gate1-palettes.png"); print("ok", sheet.size)
