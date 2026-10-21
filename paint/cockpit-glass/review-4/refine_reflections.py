import json
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

root = Path('D:/amind/git/agent-8')
out = root / 'scratch/bulwark-glass'
source = root / 'art/ships/bulwark/canopy/Bulwark reflections.png'
original = Image.open(sys.argv[1]).convert('RGBA')
assert original.size == (1024, 512)
green = np.asarray(original)[:, :512, 1].astype(float)
paths = []
report = {}
for name, component, value in [('broad', -1, 158), ('fine', 0, 217)]:
    centers, widths = [], []
    rows = np.arange(100, 380) if name == 'broad' else np.arange(128, 286)
    for y in rows:
        row = green[y, :256]
        mask = row > value / 2
        starts = np.flatnonzero(np.diff(np.pad(mask.astype(int), (1, 1))) == 1)
        ends = np.flatnonzero(np.diff(np.pad(mask.astype(int), (1, 1))) == -1)
        first, end = starts[component], ends[component]
        left = first - 1 + (value / 2 - row[first - 1]) / (row[first] - row[first - 1])
        right = end - 1 + (row[end - 1] - value / 2) / (row[end - 1] - row[end])
        centers.append((left + right) / 2)
        widths.append(right - left)
    curve = np.polynomial.Polynomial.fit(rows, centers, 2 if name == 'broad' else 1).convert()
    width = float(np.median(widths))
    y0, y1 = -64, 576
    span = y1 - y0
    x0, x3 = curve(y0), curve(y1)
    x1, x2 = x0 + curve.deriv()(y0) * span / 3, x3 - curve.deriv()(y1) * span / 3
    color = f'#{value:02x}{value:02x}{value:02x}'
    paths.append(f'      <path d="M {x0:.6f} {y0} C {x1:.6f} {y0 + span / 3:.6f} {x2:.6f} {y1 - span / 3:.6f} {x3:.6f} {y1}" stroke="{color}" stroke-width="{width:.6f}"/>')
    report[name] = {'width_at_512': width, 'rms_center_difference_at_512': float(np.sqrt(np.mean((curve(rows) - centers) ** 2)))}
svg = '\n'.join([
    '<svg xmlns="http://www.w3.org/2000/svg" width="2048" height="1024" viewBox="0 0 1024 512">',
    '  <rect width="1024" height="512" fill="#000"/>',
    '  <defs>',
    '    <g id="strokes" fill="none">', *paths,
    '    </g>',
    '    <g id="side">',
    '      <use href="#strokes"/>',
    '      <use href="#strokes" transform="translate(512 0) scale(-1 1)"/>',
    '    </g>',
    '  </defs>',
    '  <use href="#side"/>',
    '  <use href="#side" transform="translate(512 0)"/>',
    '</svg>', ''])
vector = source.with_name('Bulwark glints.svg')
vector.write_text(svg)
subprocess.run(['C:/Program Files/nodejs/node.exe', str(out / 'rasterize_glints.cjs'), str(vector), str(out / 'resolution-glints.png')], check=True)
glints = np.asarray(Image.open(out / 'resolution-glints.png').convert('RGB'))[:, :, 1]
pixels = np.asarray(original.resize((2048, 1024), Image.Resampling.BILINEAR)).copy()
pixels[:, :, 1] = glints
pixels[:, :, 3] = 255
result = Image.fromarray(pixels)
result.save(source)
result.save(root / 'src/Asteroids3D/Assets/Visuals/Ships/Bulwark/Canopy/Bulwark reflections.png')
report['texture_size'] = list(result.size)
report['fixed_channels'] = 'Bilinear resampling of the previous Shadow and Ink channels. Coverage is full for the continuous reflection field.'
(out / 'resolution-reconstruction.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
