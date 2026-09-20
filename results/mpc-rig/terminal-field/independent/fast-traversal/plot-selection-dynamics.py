from pathlib import Path
import csv
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Circle

ROOT = Path(__file__).parent
CAPTURE = ROOT / 'captured-solves-20260911-023227-448'
REPLAY = next(ROOT.glob('frozen-mean-20260911-*'))
DYNAMICS = ROOT / 'terminal-dynamics-20260911-023323-664'

def read(path):
    with path.open() as stream:
        return list(csv.DictReader(stream))

plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 10,
                     'axes.spines.top': False, 'axes.spines.right': False})
fig, (ax, matrix) = plt.subplots(1, 2, figsize=(12, 7),
                               gridspec_kw={'width_ratios': [1, 1.25]})
fig.suptitle('Two distinct reasons a geometric field cannot guarantee safe motion',
             x=.06, ha='left', fontsize=16, fontweight='bold')
paths = [r for r in read(CAPTURE / 'paths.csv')
         if r['horizon'] == '1.7' and r['tick'] == '2' and r['sample'] != '-1']
for sample in sorted({r['sample'] for r in paths}, key=int):
    rows = [r for r in paths if r['sample'] == sample]
    color = '#277da8' if float(rows[-1]['x']) < 0 else '#399b77'
    ax.plot([float(r['x']) for r in rows], [float(r['y']) for r in rows],
            color=color, alpha=.35, lw=1)
frozen = [r for r in read(REPLAY / 'paths.csv') if r['tick'] == '2' and r['dt'] == '0.1']
for kind, label, color in [('left-mean', 'Left-only mean: +2.56 m', '#277da8'),
                            ('right-mean', 'Right-only mean: +2.65 m', '#399b77'),
                            ('mean', 'Combined mean: −0.068 m', '#bf3c37')]:
    rows = [r for r in frozen if r['kind'] == kind]
    xs, ys = [float(r['x']) for r in rows], [float(r['y']) for r in rows]
    ax.plot(xs, ys, color=color, lw=2.5, label=label)
    ax.scatter(xs[-1], ys[-1], color=color, s=28, zorder=5)
ax.add_patch(Circle((0, 40), 5, facecolor='#e7e5e4', edgecolor='#a8a29e', linestyle='--'))
ax.add_patch(Circle((0, 40), 4, facecolor='#a8a29e', edgecolor='#78716c'))
ax.text(0, 40, 'Rock', ha='center', va='center', color='white', weight='bold')
ax.set(xlim=(-9, 9), ylim=(25, 46), xlabel='Lateral position (m)', ylabel='Forward position (m)')
ax.set_aspect('equal')
ax.set_title('Same solve, same candidates\nFinal approach at 100 ms prediction steps', loc='left', pad=14)
ax.legend(loc='lower center', bbox_to_anchor=(.5, -.30), frameon=False, fontsize=9)
ax.text(0, -.39, 'All 14 elites clear within the prediction horizon.\nCombined mean also collides at 20 ms (−0.232 m).\nDashed circle includes the unbanked ship radius;\nreported clearances use the actual bank profile.',
        transform=ax.transAxes, fontsize=8.5, va='top', color='#57534e')

states = [r for r in read(DYNAMICS / 'summary.csv') if r['y'] == '25']
velocities = [(0, 0), (0, 10), (0, 25), (-15, 20), (15, 20)]
headings = [0, 90, 180]
values = np.array([[int(next(r['safeCount'] for r in states
                     if float(r['vx']) == vx and float(r['vy']) == vy and float(r['yaw']) == yaw))
                   for yaw in headings] for vx, vy in velocities])
matrix.imshow(values, vmin=0, vmax=729, cmap='YlGnBu', aspect='auto')
for i in range(5):
    for j in range(3):
        matrix.text(j, i, str(values[i, j]), ha='center', va='center', fontsize=13,
                    color='white' if values[i, j] > 500 else '#292524', weight='bold')
matrix.set_xticks(range(3), ['0°', '90°', '180°'])
matrix.set_yticks(range(5), ['Rest', '(0, 10)', '(0, 25)', '(−15, 20)', '(15, 20)'])
matrix.set_xlabel('Ship heading')
matrix.set_ylabel('World velocity (m/s)')
matrix.set_title('Identical position: 15 m before the rock\nSafe continuations out of 729 tested', loc='left', pad=14)
matrix.text(0, -.19, 'Every cell receives the same terminal-field cost: 11.42072.\nThe last three rows all start at the 25 m/s speed limit.',
            transform=matrix.transAxes, fontsize=9, va='top')
matrix.text(0, -.32, 'Four-second open-loop continuations, switching control once\nafter 0.5 s. Counts describe this finite control family; zero\ndoes not prove that every possible maneuver fails.',
            transform=matrix.transAxes, fontsize=8.5, va='top', color='#57534e')
fig.subplots_adjust(top=.80, bottom=.31, left=.07, right=.98, wspace=.48)
fig.savefig(ROOT / 'selection-dynamics.png', dpi=170, facecolor='white')
