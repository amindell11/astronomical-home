import csv
import json
import shutil
import sys
from pathlib import Path

destination = Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
names = ['form-before', 'form-after']
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
assert traces[0] == traces[1], 'Motion differs across treatments'
evidence['all_motion_samples_identical'] = True
for subject, folder in [('ship', 'screen-clean-ink-probe'), ('rock', 'screen-rock-probe')]:
    for version, filename in [('before', 'off'), ('after', 'on')]:
        shutil.copy2(Path('results') / folder / (filename + '.png'), destination / f'form-{subject}-{version}.png')
(destination / 'form-study-verification.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
previous = destination / 'previous-exploration.html'
if not previous.exists():
    shutil.copy2(destination / 'comparison.html', previous)
shutil.copy2(Path('scratch/capture/form-lines.html'), destination / 'comparison.html')
shutil.copy2(Path('scratch/capture/form-study-notes.md'), destination / 'form-study-notes.md')
print(json.dumps(evidence, indent=2))
