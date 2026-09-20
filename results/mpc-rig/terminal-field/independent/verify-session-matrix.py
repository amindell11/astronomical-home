import json
from pathlib import Path
import sys

path = Path(sys.argv[1])
records = [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]
rows = {'orbit', 'kite', 'cover-take', 'dummy-closeout', 'fire-lane-dodge', 'drift-hold'}
observed = {(r['row'], r['arm'], r['seed']) for r in records}
expected = {(row, arm, seed) for row in rows for arm in range(4) for seed in range(3201, 3216)}
assert len(records) == 360, len(records)
assert observed == expected, {'missing': sorted(expected - observed), 'extra': sorted(observed - expected)}
assert all(r['samples'] > 0 and 'integratedError' in r for r in records)
print(json.dumps({'source': str(path), 'complete': True, 'records': len(records), 'rows': sorted(rows), 'arms': 4, 'seedsPerRowArm': 15}))
