from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import json

out = Path(__file__).parent
scores = []
for f in range(80):
    on = np.array(Image.open(out / 'extreme' / f'{f:03}.png')).astype(int)[:, 512:1024]
    off = np.array(Image.open(out / 'accents-off' / f'{f:03}.png')).astype(int)[:, 512:1024]
    difference = (on - off).max(axis=2)
    scores.append({'frame': f, 'visiblePixels': int((difference > 3).sum()), 'maxChannelDifference': int(difference.max())})
best = sorted(scores, key=lambda v:v['visiblePixels'], reverse=True)[:8]
print(json.dumps(best))
(out / 'accents-visibility.json').write_text(json.dumps(scores, indent=2))
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 22)
for f in [best[0]['frame'], 0, 20, 60]:
    sheet = Image.new('RGB', (1024, 696), '#181b20')
    draw = ImageDraw.Draw(sheet)
    for col, (folder, label) in enumerate([('accents-off', 'Original glints only'), ('extreme', 'Smaller roll glints enabled')]):
        sheet.paste(Image.open(out / folder / f'{f:03}.png').crop((512, 0, 1024, 640)), (col*512, 56))
        draw.text((col*512+20, 10), label, fill='white', font=font)
    sheet.save(out / f'accents-{f:03}.png')
