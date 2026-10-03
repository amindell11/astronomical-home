import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
manifest = json.loads((OUT / 'manifest.json').read_text())
BG = (12, 18, 24)
INK = (226, 236, 239)
MUTED = (139, 162, 170)
ACCENT = (161, 214, 196)
font = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 23)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 16)
tiny = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 13)
cache = {}

def board(item):
    key = (item['key'], item['label'])
    if key in cache:
        return cache[key]
    panel = Image.new('RGB', (940, 680), BG)
    draw = ImageDraw.Draw(panel)
    draw.text((24, 14), 'VALIS / WING MOTION 01', font=font, fill=INK)
    label = item['label'].upper()
    draw.text((914 - draw.textlength(label, font=font), 14), label, font=font, fill=ACCENT)
    draw.line((24, 54, 916, 54), fill=(42, 60, 67), width=1)
    draw.text((24, 69), 'TOP VIEW', font=small, fill=MUTED)
    draw.text((494, 69), 'ANGLED VIEW', font=small, fill=MUTED)
    images = {}
    for view, x in [('top', 5), ('quarter', 475)]:
        im = Image.open(OUT / 'frames' / f"{item['key']}-{view}.png").convert('RGBA')
        images[view] = im
        panel.paste(im, (x, 96), im)
    draw.line((24, 557, 916, 557), fill=(42, 60, 67), width=1)
    draw.text((24, 575), 'GAME SCALE', font=tiny, fill=MUTED)
    top = images['top']
    top = top.crop(top.getbbox())
    top.thumbnail((160, 90), Image.Resampling.LANCZOS)
    panel.paste(top, (155, 571 + (90 - top.height) // 2), top)
    draw.text((350, 579), 'REVERSE', font=tiny, fill=MUTED)
    draw.text((600, 579), 'IDLE', font=tiny, fill=MUTED)
    draw.text((828, 579), 'FORWARD', font=tiny, fill=MUTED)
    y = 614
    draw.line((370, y, 870, y), fill=(54, 80, 88), width=3)
    draw.line((620, y - 7, 620, y + 7), fill=MUTED, width=1)
    x = 620 + item['command'] * 250
    draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=ACCENT)
    draw.text((350, 644), 'Blender pose preview / smooth transitions / no flight simulation', font=tiny, fill=MUTED)
    cache[key] = panel
    return panel

frames = [board(item) for item in manifest['frames']]
sample_strip = Image.new('RGB', (1410, 700), BG)
for i, (key, label, command) in enumerate([('reverse', 'Reverse thrust', -1), ('idle', 'Idle', 0), ('forward', 'Forward thrust', 1)]):
    sample = board({'key': key, 'label': label, 'command': command})
    sample = sample.crop((0, 0, 470, 680))
    sample_strip.paste(sample, (i * 470, 0))
    d = ImageDraw.Draw(sample_strip)
    d.text((i * 470 + 24, 674), label.upper(), font=small, fill=ACCENT)
sample_strip.save(OUT / 'poses.png')
palette_sheet = Image.new('RGB', (940, 680 * 3))
for i, key in enumerate(('reverse', 'idle', 'forward')):
    item = next(item for item in manifest['frames'] if item['key'] == key)
    palette_sheet.paste(board(item), (0, i * 680))
palette = palette_sheet.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
indexed = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
indexed[0].save(OUT / 'Valis-wing-motion-v01.gif', save_all=True,
                append_images=indexed[1:], duration=round(1000 / manifest['fps']),
                loop=0, optimize=False, disposal=1)
with Image.open(OUT / 'Valis-wing-motion-v01.gif') as gif:
    length = sum(gif.seek(i) or gif.info.get('duration', 0) for i in range(gif.n_frames))
    assert length == len(frames) * round(1000 / manifest['fps'])
    print(json.dumps({'frames': gif.n_frames, 'duration_seconds': length / 1000,
                      'bytes': (OUT / 'Valis-wing-motion-v01.gif').stat().st_size}))
frames[0].save(OUT / 'preview.png')
