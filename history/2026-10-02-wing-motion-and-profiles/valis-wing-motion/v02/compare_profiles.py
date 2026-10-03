from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import numpy as np
import json

out = Path(__file__).parent
before = Image.open(out / 'before-top.png').convert('RGBA')
after = Image.open(out / 'after-top.png').convert('RGBA')
a = np.asarray(before)[:, :, 3] > 127
b = np.asarray(after)[:, :, 3] > 127
report = {'top_silhouette_changed_pixels':int(np.count_nonzero(a != b)), 'top_silhouette_total_pixels':int(np.count_nonzero(a | b))}
if (out / 'grouped-top.png').exists():
    grouped = Image.open(out / 'grouped-top.png').convert('RGBA')
    report['grouping_changed_pixels'] = int(np.count_nonzero(np.any(np.asarray(after) != np.asarray(grouped), axis=2)))
out.joinpath('render-proof.json').write_text(json.dumps(report, indent=2))
canvas = Image.new('RGB', (1440, 1180), '#151e29')
draw = ImageDraw.Draw(canvas)
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 26)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 19)
draw.text((32, 18), 'Before', font=font, fill='#dce7ec')
draw.text((752, 18), 'Thinner, tapered profile', font=font, fill='#dce7ec')
for view, top, height in [('top', 65, 490), ('low', 595, 300), ('side', 935, 170)]:
    images = [Image.open(out / (label + '-' + view + '.png')).convert('RGBA') for label in ['before', 'after']]
    boxes = [im.getbbox() for im in images]
    box = (min(v[0] for v in boxes)-10, min(v[1] for v in boxes)-10, max(v[2] for v in boxes)+10, max(v[3] for v in boxes)+10)
    for index, im in enumerate(images):
        crop = im.crop(box)
        crop.thumbnail((660,height), Image.Resampling.LANCZOS)
        canvas.paste(crop, (index*720+30+(660-crop.width)//2, top+(height-crop.height)//2), crop)
    draw.text((32, top+height+8), {'top':'Top outline preserved', 'low':'Low-angle profile', 'side':'Side profile'}[view], font=small, fill='#afc2cf')
for index, im in enumerate([before, after]):
    crop = im.crop(im.getbbox())
    crop.thumbnail((140, 140), Image.Resampling.LANCZOS)
    canvas.paste(crop, (index*720+540, 420), crop)
draw.text((1030, 570), '140 px view', font=small, fill='#afc2cf')
canvas.save(out / 'profile-comparison.png')
print(json.dumps(report))
