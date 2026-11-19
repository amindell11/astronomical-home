"""Assemble finish 4 review images from the regenerated camera renders.

Python assemble_finish4.py writes the comparison, wide view, crops and half view.
Legacy finish1/finish2 review filenames also show the current finish with explicit labels.
Frozen originals and reference images remain unchanged. Missing inputs exit nonzero.
"""
from pathlib import Path
import shutil

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent / 'previews'
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 30)
current = Image.open(OUT / 'deck-camera.png').convert('RGB')


def comparison(filename, panels):
    sheet = Image.new('RGB', (1920 * len(panels), 1150), '#0e1219')
    draw = ImageDraw.Draw(sheet)
    for i, (label, path) in enumerate(panels):
        source = Image.open(OUT / path).convert('RGB')
        source = source.resize((round(source.width * 1080 / source.height), 1080), Image.Resampling.LANCZOS)
        assert source.width == 1920, path
        sheet.paste(source, (i * 1920, 70))
        draw.text((i * 1920 + 24, 15), label, font=font, fill='#dce7ed')
    sheet.save(OUT / filename)


comparison('deck-finish4-compare.png', [
    ('Previous | finish 2 hardware', 'deck-finish4-original.png'),
    ('Finish 4 | cool near-black hardware', 'deck-camera.png'),
    ('Owner reference | colour and light hierarchy', 'finish4-reference.png')])
comparison('deck-finish1-compare.png', [
    ('Original | approved clean hardware', 'deck-finish1-original.png'),
    ('Current | finish 4 palette', 'deck-camera.png')])
comparison('deck-finish2-compare.png', [
    ('Clean model', 'deck-finish1-original.png'),
    ('Finish round 1', 'deck-finish2-original.png'),
    ('Current | finish 4 palette', 'deck-camera.png')])

crops = [
    ('Left pod | 2x', (0, 495, 700, 1015), (16, 0)),
    ('Right pod | 2x', (1220, 495, 1920, 1015), (1440, 0)),
    ('Centre console and pedestal | 2x', (610, 700, 1310, 1080), (16, 1120)),
    ('Header and left strut joint | 2x', (360, 0, 1060, 380), (1440, 1120)),
    ('Right band, wing and control well | 2x', (1200, 770, 1900, 1080), (728, 1960)),
]
sheet = Image.new('RGB', (2848, 2660), '#0e1219')
draw = ImageDraw.Draw(sheet)
for label, box, (x, y) in crops:
    draw.text((x, y + 15), label, font=font, fill='#dce7ed')
    crop = current.crop(box)
    sheet.paste(crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.NEAREST), (x, y + 65))
sheet.save(OUT / 'deck-finish4-crops.png')
for number in (1, 2, 4):
    current.resize((960, 540), Image.Resampling.LANCZOS).save(OUT / f'deck-finish{number}-half.png')
    shutil.copyfile(OUT / 'deck-camera-21x9.png', OUT / f'deck-finish{number}-21x9.png')
    if number != 4:
        sheet.save(OUT / f'deck-finish{number}-crops.png')
print('FINISH4_PREVIEWS=complete')
