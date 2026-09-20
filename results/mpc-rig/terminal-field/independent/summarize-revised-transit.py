import csv
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
expected = {1234, 7, 99, 2001, 2002, *range(3101, 3116)}
reports = []
for path in sorted(root.glob('validation-*/transit.csv')):
    with path.open(encoding='utf-8-sig', newline='') as stream:
        rows = list(csv.DictReader(stream))
    arms = {}
    for weight in (3.0, 0.0):
        values = [row for row in rows if float(row['weight']) == weight]
        seeds = [int(row['seed']) for row in values]
        assert len(seeds) == 20 and set(seeds) == expected, (path, weight, seeds)
        arms[weight] = {
            'arrivals': sum(float(row['finalRange']) < 20 for row in values),
            'collisionSteps': sum(int(row['collisionSteps']) for row in values),
            'collisionEpisodes': sum(int(row['collisionSteps']) > 0 for row in values),
            'failedSeeds': [int(row['seed']) for row in values if float(row['finalRange']) >= 20],
        }
    assert len(rows) == 40, path
    on, off = arms[3.0], arms[0.0]
    checks = {
        'arrival': on['arrivals'] >= 18,
        'benefit': on['arrivals'] - off['arrivals'] >= 4,
        'collisionSteps': on['collisionSteps'] <= off['collisionSteps'],
        'collisionEpisodes': on['collisionEpisodes'] <= off['collisionEpisodes'],
    }
    reports.append({'source': str(path), 'arms': arms, 'checks': checks})
print(json.dumps({'repeatsPresent': len(reports), 'requiredRepeats': 3, 'reports': reports}, indent=2))
