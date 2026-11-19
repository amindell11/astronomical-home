"""Assemble finish 2 comparison, five 2x crops, wide view and half-size check.

Run with Python after the Blender build and verify_deck.py. Uses frozen clean and
round 1 renders. Writes deck-finish2 PNGs and finish2-silhouette.json in previews.
Exits nonzero if the approved silhouette moves more than five pixels.
"""
from pathlib import Path
import json
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_erosion, distance_transform_edt

OUT = Path(__file__).resolve().parent / 'previews'
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 30)
current = Image.open(OUT / 'deck-camera.png').convert('RGB')
sheet = Image.new('RGB', (5760, 1150), '#101a28')
draw = ImageDraw.Draw(sheet)
for i, (label, file) in enumerate((('Clean model', 'deck-finish1-original.png'),
                                  ('Finish round 1', 'deck-finish2-original.png'),
                                  ('Finish round 2 | assembled plates, light use', 'deck-camera.png'))):
    sheet.paste(Image.open(OUT / file).convert('RGB'), (i * 1920, 70))
    draw.text((i * 1920 + 24, 15), label, font=font, fill='#dce7ed')
sheet.save(OUT / 'deck-finish2-compare.png')
current.resize((960, 540), Image.Resampling.LANCZOS).save(OUT / 'deck-finish2-half.png')
shutil.copyfile(OUT / 'deck-camera-21x9.png', OUT / 'deck-finish2-21x9.png')

crops = [
    ('Left pod | 2x', (0, 495, 700, 1015), (16, 0)),
    ('Right pod | 2x', (1220, 495, 1920, 1015), (1440, 0)),
    ('Centre console and pedestal | 2x', (610, 700, 1310, 1080), (16, 1120)),
    ('Header and left strut joint | 2x', (360, 0, 1060, 380), (1440, 1120)),
    ('Right band, wing and control well | 2x', (1200, 770, 1900, 1080), (728, 1960)),
]
sheet = Image.new('RGB', (2848, 2660), '#101a28')
draw = ImageDraw.Draw(sheet)
for label, box, (x, y) in crops:
    draw.text((x, y + 15), label, font=font, fill='#dce7ed')
    crop = current.crop(box)
    sheet.paste(crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.NEAREST), (x, y + 65))
sheet.save(OUT / 'deck-finish2-crops.png')

new_mask = np.asarray(Image.open(OUT / 'deck-mask.png'))[:, :, 3] > 127
report = {}
for label, name in (('clean_model', 'deck-finish1-original-mask.png'),
                    ('finish_round_1', 'deck-finish2-original-mask.png')):
    original_mask = np.asarray(Image.open(OUT / name))[:, :, 3] > 127
    old_edge = original_mask ^ binary_erosion(original_mask)
    new_edge = new_mask ^ binary_erosion(new_mask)
    new_distance = distance_transform_edt(~old_edge)[new_edge]
    old_distance = distance_transform_edt(~new_edge)[old_edge]
    report[label] = {
        'added_mask_pixels': int((new_mask & ~original_mask).sum()),
        'removed_mask_pixels': int((original_mask & ~new_mask).sum()),
        'maximum_edge_distance_px': float(max(new_distance.max(), old_distance.max())),
        'edge_distance_95_percentile_px': float(np.percentile(np.concatenate((new_distance, old_distance)), 95)),
        'resolution': [1920, 1080],
    }
(OUT / 'finish2-silhouette.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
assert report['clean_model']['maximum_edge_distance_px'] <= 5, report
assert report['finish_round_1']['removed_mask_pixels'] == 0, report
