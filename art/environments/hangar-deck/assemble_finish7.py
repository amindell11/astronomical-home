"""Build the labelled finish 7 comparison from frozen and regenerated camera PNGs.

Run with ordinary Python, Pillow, NumPy and SciPy after the preview checks.
Refreshes existing review sheets with current labels, then writes the finish 7
comparison: side-by-side 16:9 frames and two full-header crops at exactly 2x.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
for filename in ('assemble_previews.py', 'assemble_finish4.py', 'assemble_finish5.py'):
    source = (ROOT / filename).read_text(encoding='utf-8')
    if filename == 'assemble_finish5.py':
        source = source.split('\nreport = {}')[0]
    for old in ('finish 5 geometry, finish 4 palette', 'finish 5, clean console geometry',
                'finish 5 geometry cleanup', 'finish 5 geometry'):
        source = source.replace(old, 'finish 7, dim header strips')
    source = source.replace('After | finish 5', 'After | finish 7')
    exec(compile(source, str(ROOT / filename), 'exec'), {'__file__': str(ROOT / filename)})

font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 30)
sheet = Image.new('RGB', (3840, 2100), '#0e1219')
draw = ImageDraw.Draw(sheet)
for column, label, filename in (
        (0, 'Previous | six small downlights and structural dashes', 'deck-finish7-original.png'),
        (1, 'Finish 7 | two recessed dim strips', 'deck-camera.png')):
    frame = Image.open(OUT / filename).convert('RGB')
    assert frame.size == (1920, 1080), filename
    draw.text((column * 1920 + 24, 12), label, font=font, fill='#dce7ed')
    sheet.paste(frame, (column * 1920, 60))
    y = 1140 + column * 480
    draw.text((24, y + 12), label + ' | header at 2x', font=font, fill='#dce7ed')
    header = frame.crop((0, 0, 1920, 210))
    sheet.paste(header.resize((3840, 420), Image.Resampling.NEAREST), (0, y + 60))
sheet.save(OUT / 'deck-finish7-compare.png')
print('FINISH7_COMPARE=complete', flush=True)
