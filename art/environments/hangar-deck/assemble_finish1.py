"""Build labelled finish comparisons and 2x crops from the final camera renders.

Run with Python after verify_deck.py. Writes finish1 preview PNGs and silhouette
measurements beside them. Original references are retained for later reruns.
"""
from pathlib import Path
import json
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_erosion, distance_transform_edt

OUT = Path(__file__).resolve().parent / 'previews'
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 30)
small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 22)
previous = Image.open(OUT / 'deck-finish1-original.png').convert('RGB')
current = Image.open(OUT / 'deck-camera.png').convert('RGB')
sheet = Image.new('RGB', (3840, 1150), '#101a28')
sheet.paste(previous, (0, 70))
sheet.paste(current, (1920, 70))
draw = ImageDraw.Draw(sheet)
draw.text((24, 15), 'Previous | approved clean hardware', font=font, fill='#dce7ed')
draw.text((1944, 15), 'Finish 1 | light use, cyan structure, warm status', font=font, fill='#dce7ed')
sheet.save(OUT / 'deck-finish1-compare.png')
current.resize((960, 540), Image.Resampling.LANCZOS).save(OUT / 'deck-finish1-half.png')
shutil.copyfile(OUT / 'deck-camera-21x9.png', OUT / 'deck-finish1-21x9.png')

crops = [
    ('Left pod | 2x', (0, 495, 700, 1015)),
    ('Right pod | 2x', (1220, 495, 1920, 1015)),
    ('Centre console and pedestal | 2x', (610, 700, 1310, 1080)),
    ('Header and left strut joint | 2x', (360, 20, 1060, 400)),
]
sheet = Image.new('RGB', (2848, 1990), '#101a28')
draw = ImageDraw.Draw(sheet)
for index, (label, box) in enumerate(crops):
    x = 16 + (index % 2) * 1424
    y = 0 if index < 2 else 1120
    draw.text((x, y + 15), label, font=font, fill='#dce7ed')
    crop = current.crop(box)
    sheet.paste(crop.resize((crop.width * 2, crop.height * 2), Image.Resampling.NEAREST), (x, y + 65))
sheet.save(OUT / 'deck-finish1-crops.png')

original_mask = np.asarray(Image.open(OUT / 'deck-finish1-original-mask.png'))[:, :, 3] > 127
new_mask = np.asarray(Image.open(OUT / 'deck-mask.png'))[:, :, 3] > 127
old_edge = original_mask ^ binary_erosion(original_mask)
new_edge = new_mask ^ binary_erosion(new_mask)
added = new_mask & ~original_mask
removed = original_mask & ~new_mask
new_distance = distance_transform_edt(~old_edge)[new_edge]
old_distance = distance_transform_edt(~new_edge)[old_edge]
report = {'added_mask_pixels': int(added.sum()), 'removed_mask_pixels': int(removed.sum()),
          'intersection_over_union': float((original_mask & new_mask).sum() / (original_mask | new_mask).sum()),
          'maximum_edge_distance_px': float(max(new_distance.max(), old_distance.max())),
          'edge_distance_95_percentile_px': float(np.percentile(np.concatenate((new_distance, old_distance)), 95)),
          'resolution': [1920, 1080]}
(OUT / 'finish1-silhouette.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
