import csv
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Circle

root = Path(sys.argv[1])
fig, axes = plt.subplots(3, 3, figsize=(15, 12), constrained_layout=True)
summary = []
for row, seed in enumerate((3401, 3402, 3403)):
    trajectory_points = []
    for arm, label, color in ((0, 'Field on', '#b74e14'), (1, 'Field off', '#126d9f')):
        files = sorted(root.glob(f'*.dummy-closeout-{seed}-arm{arm}.csv'))
        if not files:
            continue
        with files[-1].open(encoding='utf-8-sig') as stream:
            data = [{k: float(v) for k, v in r.items()} for r in csv.DictReader(stream)]
        if not data:
            continue
        t = [d['seconds'] for d in data]
        trajectory_points.extend((d['x'], d['y']) for d in data)
        axes[row, 0].plot([d['x'] for d in data], [d['y'] for d in data], label=label, color=color)
        axes[row, 0].scatter(data[0]['x'], data[0]['y'], color=color, marker='o', s=20)
        axes[row, 0].scatter(data[-1]['targetX'], data[-1]['targetY'], color='black', marker='x')
        axes[row, 1].plot(t, [d['range'] for d in data], label=label, color=color)
        axes[row, 2].plot(t, [d['fieldEndpoint'] for d in data], label=label, color=color)
        positive = [d for d in data if d['fieldEndpoint'] > 0.01]
        summary.append({'seed': seed, 'arm': arm, 'samples': len(data),
            'maxEndpointPenalty': max(d['fieldEndpoint'] for d in data),
            'positiveEndpointSamples': len(positive),
            'lastPositivePenaltyTime': positive[-1]['seconds'] if positive else None,
            'lastTraceTime': t[-1]})
    terrain_files = sorted(root.glob(f'*.dummy-closeout-{seed}-arm1-bake*.terrain.csv'))
    if terrain_files:
        circles = []
        with terrain_files[0].open(encoding='utf-8-sig') as stream:
            for rock in csv.DictReader(stream):
                x, y, radius, clearance = (float(rock[k]) for k in ('x', 'y', 'radius', 'clearance'))
                circles.append((x, y, radius + clearance))
                axes[row, 0].add_patch(Circle((x, y), radius, color='gray', alpha=.3, zorder=0))
                axes[row, 0].add_patch(Circle((x, y), radius + clearance, fill=False, color='gray', alpha=.4, linewidth=.5, zorder=0))
        with Path(str(terrain_files[0]).replace('.terrain.csv', '.grid.csv')).open(encoding='utf-8-sig') as stream:
            blocked = [r for r in csv.DictReader(stream) if int(r['occupied'])]
        missing = [r for r in blocked if not any((float(r['x'])-x)**2 + (float(r['y'])-y)**2 <= (radius+.001)**2 for x, y, radius in circles)]
        if missing:
            raise ValueError(f'Seed {seed}: {len(missing)} blocked cells lack a corresponding exported inflated obstacle')
        axes[row, 0].scatter([float(r['x']) for r in blocked], [float(r['y']) for r in blocked], marker='x', s=8, color='red', alpha=.5, zorder=1)
        if trajectory_points:
            axes[row, 0].set_xlim(min(p[0] for p in trajectory_points)-8, max(p[0] for p in trajectory_points)+8)
            axes[row, 0].set_ylim(min(p[1] for p in trajectory_points)-8, max(p[1] for p in trajectory_points)+8)
    axes[row, 0].set_title(f'Seed {seed}: trajectory (m)')
    axes[row, 0].set_aspect('equal', adjustable='box')
    axes[row, 1].set_title('Target range (m)')
    axes[row, 1].axhline(10, color='gray', linestyle='--', linewidth=1)
    axes[row, 2].set_title('Selected endpoint: unweighted field penalty (m)')
    for ax in axes[row]:
        ax.grid(alpha=.2)
        ax.legend()
    axes[row, 1].set_xlabel('Simulated seconds')
    axes[row, 2].set_xlabel('Simulated seconds')
fig.suptitle('Dummy development diagnosis — stable grid, goal region, weight 0.4')
fig.savefig(root / 'paired-trajectories.png', dpi=130)
(root / 'trace-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary, indent=2))
