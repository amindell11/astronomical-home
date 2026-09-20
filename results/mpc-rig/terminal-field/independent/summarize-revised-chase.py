import csv
import argparse
import json
from pathlib import Path
import statistics

parser = argparse.ArgumentParser()
parser.add_argument('root', type=Path)
parser.add_argument('--on-source', type=Path)
parser.add_argument('--on-frames', type=Path)
args = parser.parse_args()
root = args.root
summary = []
for arm in ('on', 'off'):
    sources = list(root.glob(f'chase-{arm}-*.csv'))
    folders = [p for p in (root / 'capture' / 'frames').glob(f'*-dense-chase-{arm}') if p.is_dir()]
    if arm == 'on' and args.on_source:
        sources = [args.on_source]
    if arm == 'on' and args.on_frames:
        folders = [args.on_frames]
    assert len(sources) == len(folders) == 1, (sources, folders)
    with sources[0].open(encoding='utf-8-sig', newline='') as stream:
        records = [{k: float(v) for k, v in row.items()} for row in csv.DictReader(stream)]
    assert len(records) == 2000, (arm, len(records))
    assert all(abs(r['captureDelta'] - .02) < 1e-7 for r in records)
    assert abs(records[-1]['actualSeconds'] - 40) < .001
    frames = list(folders[0].glob('f_*.png'))
    assert len(frames) == 400, (arm, len(frames))
    previous_gap = 50
    windows = []
    for end in range(99, 2000, 100):
        gap = records[end]['range']
        windows.append({'endSeconds': (end + 1) * .02, 'closingMetres': previous_gap - gap,
                        'stalled': previous_gap - gap <= .5,
                        'meanSpeed': statistics.mean(r['speed'] for r in records[end-99:end+1])})
        previous_gap = gap
    final = records[-1]
    progress = 50 - final['range']
    summary.append({'arm': arm, 'source': str(sources[0]), 'frames': 400, 'seconds': final['actualSeconds'],
                    'finalGap': final['range'], 'closingMetres': progress, 'pathMetres': final['pathLength'],
                    'pathPerClosingMetre': final['pathLength'] / progress if progress > 0 else None,
                    'collisionSteps': final['collisionSteps'], 'stallWindows': sum(w['stalled'] for w in windows),
                    'stallSeconds': 2 * sum(w['stalled'] for w in windows), 'windows': windows})
(root / 'chase-summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print(json.dumps([{k: v for k, v in row.items() if k != 'windows'} for row in summary], indent=2))
