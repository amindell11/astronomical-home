"""Measure PNG coverage, compare the saved 16:9 silhouette, and label six views.

Run with local Python and Pillow/NumPy/SciPy after the Blender verification scripts.
Writes previews/coverage.json and deck-coverage-sheet.png. --require-closed exits
1 when any top/bottom band contains magenta; evidence is written before that check.
"""
from pathlib import Path
import json
import math
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_erosion, distance_transform_edt

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
projections = json.loads((OUT / 'coverage-projections.json').read_text())
report = {'band_fraction': .015, 'band_rounding': 'ceil(height * .015)',
          'magenta_rule_srgb_8bit': 'R > 180 and G < 80 and B > 180',
          'antialias_chroma_rule_srgb_8bit': 'R-G > 40 and B-G > 40', 'views': {}}
sheet = Image.new('RGB', (1920, 1920), '#121c2a')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 24)
small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
for index, (label, projection) in enumerate(projections.items()):
    source = Image.open(OUT / projection['image']).convert('RGB')
    a = np.asarray(source).astype(np.int16)
    magenta = (a[:, :, 0] > 180) & (a[:, :, 1] < 80) & (a[:, :, 2] > 180)
    chroma = (a[:, :, 0] - a[:, :, 1] > 40) & (a[:, :, 2] - a[:, :, 1] > 40)
    rows = math.ceil(a.shape[0] * .015)
    counts = {'band_rows': rows, 'top_magenta_pixels': int(magenta[:rows].sum()),
              'bottom_magenta_pixels': int(magenta[-rows:].sum()),
              'top_chroma_pixels': int(chroma[:rows].sum()),
              'bottom_chroma_pixels': int(chroma[-rows:].sum()),
              'left_edge_magenta_pixels': int(magenta[:, 0].sum()),
              'right_edge_magenta_pixels': int(magenta[:, -1].sum())}
    counts['bands_pass'] = counts['top_chroma_pixels'] == counts['bottom_chroma_pixels'] == 0
    report['views'][label] = projection | counts
    x, y = (index % 2) * 960, (index // 2) * 640
    width, height = projection['resolution']
    draw.text((x + 20, y + 14), f'{label.replace("x", ":")}  |  {width} x {height}', font=font, fill='white')
    draw.text((x + 20, y + 48), f'Magenta pixels: top {counts["top_magenta_pixels"]:,}  /  bottom {counts["bottom_magenta_pixels"]:,}', font=small, fill='#edc774')
    source.thumbnail((920, 540), Image.Resampling.LANCZOS)
    sheet.paste(source, (x + (960 - source.width) // 2, y + 84 + (540 - source.height) // 2))
sheet.save(OUT / 'deck-coverage-sheet.png')

old = np.asarray(Image.open(OUT / 'coverage-reference/deck-mask.png'))[:, :, 3] > 127
new = np.asarray(Image.open(OUT / 'deck-mask.png'))[:, :, 3] > 127
old_edge, new_edge = old ^ binary_erosion(old), new ^ binary_erosion(new)
distances = np.concatenate([distance_transform_edt(~old_edge)[new_edge], distance_transform_edt(~new_edge)[old_edge]])
report['reference_16x9'] = {'reference_image': 'coverage-reference/deck-camera.png',
    'reference_mask': 'coverage-reference/deck-mask.png',
    'hardware_intersection_over_union': float((old & new).sum() / (old | new).sum()),
    'changed_pixels': int((old ^ new).sum()), 'image_pixels': int(old.size),
    'added_pixels': int((new & ~old).sum()), 'removed_pixels': int((old & ~new).sum()),
    'edge_distance_95_percentile_px': float(np.percentile(distances, 95)),
    'edge_distance_max_px': float(distances.max())}
audit = json.loads((OUT / 'mesh-audit.json').read_text())
original_audit = json.loads((OUT / 'coverage-reference/mesh-audit.json').read_text())
report['preservation'] = {'controls_unchanged': audit['controls'] == original_audit['controls'],
                          'screens_unchanged': audit['screens'] == original_audit['screens']}
report['preservation'].update(json.loads((OUT / 'preservation-check.json').read_text()))
report['mesh_totals'] = {'hardware_objects': len(audit['objects']),
    'hardware_vertices': sum(o['vertices'] for o in audit['objects']),
    'hardware_triangles': audit['total_triangles'], 'scene_triangles': audit['scene_total_triangles']}
report['all_bands_pass'] = all(v['bands_pass'] for v in report['views'].values())
report['limitation'] = 'Existing sky gaps inside the protected 16:9 frustum remain; closing them requires changing the approved silhouette.'
(OUT / 'coverage.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
if '--require-closed' in sys.argv:
    assert report['all_bands_pass'], 'Protected reference sky gaps remain; see coverage.json'
