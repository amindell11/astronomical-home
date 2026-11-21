"""Assemble render_glass.py's three .temp PNGs into the labelled glass check.

Run with ordinary Python and Pillow. Writes previews/deck-glass-check.png.
Missing input images raise and exit nonzero.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 28)
sheet = Image.new('RGB', (3840, 3*1140), (18, 23, 32))
draw = ImageDraw.Draw(sheet)
for row, (label, width) in enumerate([('16:9', 1920), ('21:9', 2560), ('32:9', 3840)]):
    image = Image.open(ROOT / '.temp' / f'deck-glass-{label.replace(":", "x")}.png').convert('RGB')
    assert image.size == (width, 1080), image.size
    draw.text((24, row*1140+10), f'{label} | {width} x 1080 | RGB (0.30, 0.38, 0.42), alpha = mask x 0.6',
              font=font, fill=(221, 232, 244))
    sheet.paste(image, ((3840-width)//2, row*1140+60))
sheet.save(ROOT / 'previews/deck-glass-check.png')
print('GLASS_SHEET=complete')
