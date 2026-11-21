"""Assemble finish 5 review sheets and measure six silhouettes with ordinary Python.

Reads frozen finish5 originals and regenerated previews. Writes labelled 2x lower
quarters, neutral geometry comparisons, wide/half views and finish5-silhouette.json.
Exit 1 means a silhouette moved more than four pixels from finish 4.
"""
from pathlib import Path
import json
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_erosion, distance_transform_edt

OUT = Path(__file__).resolve().parent / 'previews'
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 30)


def quarters(before_path, after_path, filename, qualifier):
    sheet = Image.new('RGB', (3840, 2280), '#0e1219')
    draw = ImageDraw.Draw(sheet)
    for row, (label, path) in enumerate((('Before | finish 4', before_path),
                                        ('After | finish 5', after_path))):
        frame = Image.open(OUT / path).convert('RGB')
        assert frame.size == (1920, 1080), path
        for col, side in enumerate(('Left', 'Right')):
            x, y = col * 1920, row * 1140
            draw.text((x + 24, y + 12), f'{label} | {side} lower quarter | 2x{qualifier}',
                      font=font, fill='#dce7ed')
            crop = frame.crop((col * 960, 540, (col + 1) * 960, 1080))
            sheet.paste(crop.resize((1920, 1080), Image.Resampling.NEAREST), (x, y + 60))
    sheet.save(OUT / filename)


quarters('deck-finish5-original.png', 'deck-camera.png', 'deck-finish5-crops.png', '')
quarters('deck-finish5-geometry-original.png', 'deck-finish5-geometry.png',
         'deck-finish5-geometry-crops.png', ' | neutral material')
current = Image.open(OUT / 'deck-camera.png').convert('RGB')
current.resize((960, 540), Image.Resampling.LANCZOS).save(OUT / 'deck-finish5-half.png')
shutil.copyfile(OUT / 'deck-camera-21x9.png', OUT / 'deck-finish5-21x9.png')
sheet = Image.new('RGB', (3840, 1140), '#0e1219')
draw = ImageDraw.Draw(sheet)
for x, label, path in ((0, 'Before | finish 4', 'deck-finish5-original.png'),
                       (1920, 'After | finish 5 geometry cleanup', 'deck-camera.png')):
    draw.text((x + 24, 12), label, font=font, fill='#dce7ed')
    sheet.paste(Image.open(OUT / path).convert('RGB'), (x, 60))
sheet.save(OUT / 'deck-finish5-compare.png')

report = {}
for view, suffix in (('16x9', ''), ('16x10', '-16x10'), ('3x2', '-3x2'),
                     ('4x3', '-4x3'), ('21x9', '-21x9'), ('32x9', '-32x9')):
    before = np.asarray(Image.open(OUT / f'deck-finish5-original-mask{suffix}.png'))[:, :, 3] > 127
    after = np.asarray(Image.open(OUT / f'deck-mask{suffix}.png'))[:, :, 3] > 127
    old_edge, new_edge = before ^ binary_erosion(before), after ^ binary_erosion(after)
    distances = np.concatenate((distance_transform_edt(~old_edge)[new_edge],
                                distance_transform_edt(~new_edge)[old_edge]))
    report[view] = {'added_pixels': int((after & ~before).sum()),
                    'removed_pixels': int((before & ~after).sum()),
                    'maximum_edge_distance_px': float(distances.max()),
                    'p95_edge_distance_px': float(np.percentile(distances, 95)),
                    'within_four_pixels': bool(distances.max() <= 4)}
(OUT / 'finish5-silhouette.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report), flush=True)
assert all(v['within_four_pixels'] for v in report.values()), report
print('FINISH5_PREVIEWS=complete', flush=True)
