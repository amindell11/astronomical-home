import csv
import json
import math
import sys
from pathlib import Path

root = Path(sys.argv[1])
results = []
for seed in (3401, 3402, 3403):
    for arm in (0, 1):
        terrain = sorted(root.glob(f'*.dummy-closeout-{seed}-arm{arm}-bake*.terrain.csv'))[0]
        with terrain.open(encoding='utf-8-sig') as stream:
            circles = [tuple(float(r[k]) for k in ('x', 'y', 'radius', 'clearance')) for r in csv.DictReader(stream)]
        with Path(str(terrain).replace('.terrain.csv', '.grid.csv')).open(encoding='utf-8-sig') as stream:
            grid = [{k: float(v) for k, v in r.items()} for r in csv.DictReader(stream)]
        xs = sorted({r['x'] for r in grid})
        ys = sorted({r['y'] for r in grid})
        size = len(xs)
        spacing = xs[1] - xs[0]
        path = sorted(root.glob(f'*.dummy-closeout-{seed}-arm{arm}.csv'))[0]
        with path.open(encoding='utf-8-sig') as stream:
            trace = [{k: float(v) for k, v in r.items()} for r in csv.DictReader(stream)]
        examples = []
        for sample in trace:
            if sample['range'] < 10:
                break
            x, y = sample['endpointX'], sample['endpointY']
            clearance = min(math.hypot(x-cx, y-cy)-radius-margin for cx, cy, radius, margin in circles)
            if clearance <= 0 or sample['fieldEndpoint'] < 10:
                continue
            gx, gy = (x-xs[0])/spacing, (y-ys[0])/spacing
            ix, iy = math.floor(gx), math.floor(gy)
            if not (0 <= ix < size-1 and 0 <= iy < size-1):
                continue
            fx, fy = gx-ix, gy-iy
            corners = []
            total = 0
            blocked_contribution = 0
            for dx, dy, weight in ((0, 0, (1-fx)*(1-fy)), (1, 0, fx*(1-fy)), (0, 1, (1-fx)*fy), (1, 1, fx*fy)):
                cell = grid[ix+dx+(iy+dy)*size]
                contribution = cell['excess'] * weight
                total += contribution
                if cell['occupied']:
                    blocked_contribution += contribution
                corners.append({'occupied': bool(cell['occupied']), 'weight': weight, 'excess': cell['excess']})
            examples.append({'seconds': sample['seconds'], 'endpoint': [x, y],
                'inflatedCircleClearance': clearance, 'measuredPenalty': sample['fieldEndpoint'],
                'initialGridReconstruction': total, 'blockedCornerContribution': blocked_contribution,
                'corners': corners})
        examples.sort(key=lambda e: e['measuredPenalty'], reverse=True)
        results.append({'seed': seed, 'arm': arm, 'freeEndpointHighPenaltySamplesBeforeClose': len(examples), 'examples': examples[:3]})
(root / 'endpoint-geometry.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
