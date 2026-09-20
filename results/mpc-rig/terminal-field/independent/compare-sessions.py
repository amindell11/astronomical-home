import argparse
import json
import statistics
from collections import defaultdict
from pathlib import Path

parser = argparse.ArgumentParser(description='Compare paired terminal-field session records.')
parser.add_argument('path', type=Path)
parser.add_argument('--integrated-dummy', action='store_true', help='Use the explicitly approved dummy movement metric; retain the legacy comparison separately.')
args = parser.parse_args()
path = args.path
integrated_dummy = args.integrated_dummy
groups = defaultdict(list)
for line in path.read_text(encoding='utf-8-sig').splitlines():
    record = json.loads(line)
    groups[(record['row'], record['arm'])].append(record)

summary = []
for row in sorted({key[0] for key in groups}):
    arms = {}
    for arm in range(4):
        values = groups[(row, arm)]
        if not values:
            continue
        arms[arm] = {'episodes': len(values), 'successes': sum(v['success'] for v in values),
            'medianError': statistics.median(v['meanError'] for v in values),
            'medianIntegratedError': statistics.median(v.get('integratedError', v['meanError'] * v['samples'] * .02) for v in values),
            'medianCloseout': statistics.median(v['closeoutSeconds'] for v in values),
            'collisionEpisodes': sum(v['collisionSteps'] > 0 for v in values),
            'collisionSteps': sum(v['collisionSteps'] for v in values)}
    verdict = None
    legacy_movement = None
    if 0 in arms and 1 in arms and arms[0]['episodes'] == arms[1]['episodes']:
        on, off = arms[0], arms[1]
        verdict = {'success': on['successes'] >= off['successes'] - 1,
            'movement': on['medianError'] <= off['medianError'] + max(1., .1*off['medianError']),
            'collisionEpisodes': on['collisionEpisodes'] <= off['collisionEpisodes'],
            'collisionSteps': on['collisionSteps'] <= off['collisionSteps']}
        if row == 'dummy-closeout':
            verdict['closeout'] = on['medianCloseout'] <= off['medianCloseout'] + max(1., .1*off['medianCloseout'])
            if integrated_dummy:
                legacy_movement = verdict['movement']
                verdict['movement'] = on['medianIntegratedError'] <= off['medianIntegratedError'] + max(1., .1*off['medianIntegratedError'])
    summary.append({'row': row, 'arms': arms, 'pairedChecks': verdict,
        'movementMetric': 'integrated-error-m*s' if integrated_dummy and row == 'dummy-closeout' else 'mean-error-m',
        'legacyAverageMovementPass': legacy_movement})

output = {'source': str(path), 'integratedDummyPolicy': integrated_dummy, 'recordCount': sum(len(v) for v in groups.values()), 'rows': summary}
print(json.dumps(output, indent=2))
