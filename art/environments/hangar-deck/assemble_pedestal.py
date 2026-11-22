"""Refresh review sheets and compare 3x pedestal crops with the owner's references.

Run ordinary Python after the camera and verification renders. Frozen references
and originals stay unchanged. Writes previews/deck-pedestal-compare.png and the
existing review sheets. Missing inputs or wrong camera dimensions exit nonzero.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
source = (ROOT / 'assemble_finish7.py').read_text(encoding='utf-8')
source = source.replace('finish 7, dim header strips', 'pedestal edge lights and ringed lens')
source = source.replace('Finish 7 | two recessed dim strips', 'Current | ringed lens and lit pedestal edges')
source = source.replace('After | finish 7', 'After | lit pedestal and ringed lens')
exec(compile(source, str(ROOT / 'assemble_finish7.py'), 'exec'), {'__file__': str(ROOT / 'assemble_finish7.py')})

font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 25)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 19)
sheet = Image.new('RGB', (3240, 700), '#0e1219')
draw = ImageDraw.Draw(sheet)
for index, (filename, label) in enumerate((
        ('deck-pedestal-original.png', 'Before | 16:9 game camera, 3x'),
        ('deck-camera.png', 'After | 16:9 game camera, 3x'))):
    frame = Image.open(OUT / filename).convert('RGB')
    assert frame.size == (1920, 1080), filename
    crop = frame.crop((825, 725, 1095, 915))
    draw.text((index * 810 + 18, 18), label, font=font, fill='#dce7ed')
    sheet.paste(crop.resize((810, 570), Image.Resampling.NEAREST), (index * 810, 65))
for index in (1, 2):
    reference = Image.open(OUT / f'pedestal-reference-{index}.png').convert('RGB')
    crop = reference.crop((500, 540, 1172, 941))
    x = (index + 1) * 810
    draw.text((x + 18, 18), f'Owner reference {index} | pedestal detail', font=font, fill='#dce7ed')
    sheet.paste(crop.resize((810, 483), Image.Resampling.LANCZOS), (x, 108))
draw.text((18, 655), 'Camera crops use the same saved lighting and preview placeholders. Reference details are fitted to the panels.',
          font=small, fill='#b6c5d1')
sheet.save(OUT / 'deck-pedestal-compare.png')
print('PEDESTAL_COMPARE=complete', flush=True)
