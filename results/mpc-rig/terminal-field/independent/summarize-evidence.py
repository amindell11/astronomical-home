import csv
import json
import statistics
from pathlib import Path

root = Path(__file__).resolve().parent

def rows(path):
    with path.open(encoding='utf-8-sig') as stream:
        return [{k: float(v) for k, v in row.items()} for row in csv.DictReader(stream)]

evidence = {'transit': [], 'solver': [], 'chase': []}
for folder in sorted(root.glob('validation-*')):
    if not (folder / 'transit.csv').exists():
        continue
    data = rows(folder / 'transit.csv')
    arms = {}
    for weight in sorted({r['weight'] for r in data}):
        arm = [r for r in data if r['weight'] == weight]
        arms[str(weight)] = {'episodes': len(arm), 'arrivals': sum(r['finalRange'] < 20 for r in arm),
                             'collisionSteps': sum(r['collisionSteps'] for r in arm)}
    evidence['transit'].append({'source': folder.name, 'arms': arms})

for path in sorted(root.glob('solver-loop-*.csv')):
    data = rows(path)
    baseline = statistics.mean(r['noTerrainSourceMs'] for r in data)
    field = statistics.mean(r['withFieldMs'] for r in data)
    evidence['solver'].append({'source': path.name, 'baselineMeanMs': baseline,
                              'fieldMeanMs': field, 'extraMeanMs': field - baseline,
                              'extraPercent': 100 * (field / baseline - 1)})

for arm, stamp, frame in [('on', '20260909-111526-693', '20260909-041526'),
                           ('off', '20260909-111658-306', '20260909-041658')]:
    path = root / f'chase-{arm}-{stamp}.csv'
    data = rows(path)
    assert len(data) == 2000, (arm, len(data))
    assert all(abs(r['captureDelta'] - 0.02) < 1e-7 for r in data)
    final = data[-1]
    progress = 50 - final['range']
    previous = 50
    windows = []
    for end in range(99, 2000, 100):
        gap = data[end]['range']
        windows.append({'endSeconds': (end + 1) * 0.02, 'closingMetres': previous - gap,
                        'stalled': previous - gap <= 0.5,
                        'meanSpeed': statistics.mean(r['speed'] for r in data[end-99:end+1])})
        previous = gap
    frame_dir = root / 'capture' / 'frames' / f'{frame}-terminal-field-dense-chase-{arm}'
    pngs = sorted(frame_dir.glob('f_*.png'))
    assert len(pngs) == 400, (arm, len(pngs))
    evidence['chase'].append({'arm': arm, 'source': path.name, 'frameDirectory': str(frame_dir),
        'frames': len(pngs), 'seconds': final['actualSeconds'], 'finalGap': final['range'],
        'closingMetres': progress, 'pathMetres': final['pathLength'],
        'pathPerClosingMetre': final['pathLength'] / progress if progress > 0 else None,
        'collisionSteps': final['collisionSteps'], 'stallSeconds': 2 * sum(w['stalled'] for w in windows),
        'stallWindows': sum(w['stalled'] for w in windows), 'windows': windows})

(root / 'evidence-summary.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(json.dumps({**evidence, 'chase': [{k:v for k,v in a.items() if k != 'windows'} for a in evidence['chase']]}, indent=2))
