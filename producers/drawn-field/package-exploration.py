import csv
import json
import shutil
import sys
from pathlib import Path

destination = Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
names = ['current-top', 'b-top', 'exploration-top']
traces = []
evidence = {}
for name, argument in zip(names, sys.argv[1:], strict=True):
    source = Path(argument)
    with (source / 'motion.csv').open(encoding='utf-8') as stream:
        trace = list(csv.DictReader(stream))
    assert len(trace) == 900, (name, len(trace))
    traces.append(trace)
    shutil.copy2(source.with_suffix('.mp4'), destination / f'{name}.mp4')
    for label, frame in [('flight', 100), ('inspection', 600)]:
        shutil.copy2(source / f'f_{frame:05d}.png', destination / f'{name}-{label}.png')
    for filename in ['motion.csv', 'rendering.txt', 'manifest.json']:
        shutil.copy2(source / filename, destination / f'{name}-{filename}')
    evidence[name] = {'source': str(source.resolve()), 'samples': len(trace), 'min_bank': min(float(r['bank']) for r in trace), 'max_bank': max(float(r['bank']) for r in trace)}
assert traces[0] == traces[1] == traces[2], 'Motion differs across treatments'
evidence['all_motion_samples_identical'] = True
(destination / 'exploration-verification.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
print(json.dumps(evidence, indent=2))
