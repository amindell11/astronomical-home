import csv
import json
from pathlib import Path
import numpy as np
from PIL import Image

folder = Path('results/capture/frames/20261030-033658-863-canopy-events')
manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8'))
rows = list(csv.DictReader((folder / manifest['frameMetadata']).open(encoding='utf-8')))
groups = {stage: [r for r in rows if int(r['stage']) == stage] for stage in range(7)}
assert all(groups.values())
assert any(float(r['hit_age']) >= 0 for r in groups[1])
assert all(int(r['state']) == 1 for r in groups[2])
for stage in (3, 4):
    assert any(int(r['state']) == 2 and float(r['transition_age']) >= 0 for r in groups[stage])
assert all(float(r['effects']) == 0 for r in groups[6])
assert len({r['sim_seconds'] for r in rows}) == 1
content = {}
for name in ('f_00018.png', 'f_00039.png', 'f_00075.png', 'f_00101.png', 'f_00123.png', 'f_00145.png'):
    pixels = np.asarray(Image.open(folder / name).convert('RGB'))
    assert pixels.std() > 20
    content[name] = {'rgb_stddev': float(pixels.std()), 'mean_rgb': pixels.mean(axis=(0, 1)).tolist()}
report = {
    'commit': json.loads((folder / 'provenance.json').read_text(encoding='utf-8'))['commit'],
    'frames': len(rows),
    'seconds': float(rows[-1]['unscaled_seconds']) - float(rows[0]['unscaled_seconds']),
    'stages': {str(stage): {'frames': len(group), 'first': group[0]['filename'], 'last': group[-1]['filename'],
        'states': sorted({int(r['state']) for r in group})} for stage, group in groups.items()},
    'assertions': ['applied hit drives hit age', 'low hull selects LowHull',
        'phase start and extraction select BigMoment and transition clocks', 'off disables effects',
        'world simulation remains paused', 'sampled frames contain nonuniform rendered content'],
    'images': content,
    'limits': ['Events are staged in real Combat Playtest.', 'No combat difficulty or timing performance claim.',
        'Per-instrument dip depth and frequency are checked by the focused tests, not inferred from these pixels.']
}
Path('results/canopy-events/capture-check.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'frames': report['frames'], 'seconds': report['seconds'], 'assertions': len(report['assertions'])}))
