import csv, json, statistics, sys
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Circle
root=Path(sys.argv[1])
rows=list(csv.DictReader((root/'summary.csv').open()))
assert len(rows)==10
pairs={int(r['seed']):{} for r in rows}
for r in rows: pairs[int(r['seed'])][int(r['arm'])]=r
on=[float(p[0]['progressSpeed']) for p in pairs.values()]
off=[float(p[1]['progressSpeed']) for p in pairs.values()]
summary={'medianOn':statistics.median(on),'medianOff':statistics.median(off),'relativeMedianGain':statistics.median(on)/statistics.median(off)-1,'collisionsOn':sum(int(p[0]['sweptCollisionSteps']) for p in pairs.values()),'collisionsOff':sum(int(p[1]['sweptCollisionSteps']) for p in pairs.values()),'seedGains':{s:float(p[0]['progressSpeed'])/float(p[1]['progressSpeed'])-1 for s,p in pairs.items()}}
(root/'analysis.json').write_text(json.dumps(summary,indent=2))
seed=min(pairs,key=lambda s:summary['seedGains'][s])
traces=[list(csv.DictReader((root/f'seed{seed}-arm{a}.csv').open())) for a in range(2)]
fig,axes=plt.subplots(1,3,figsize=(13,6),gridspec_kw={'width_ratios':[1.1,1.2,1]})
colors=['#16877e','#bc6032']
for a,label in enumerate(['Field on','Field off']):
    axes[0].plot(list(pairs),[float(p[a]['progressSpeed']) for p in pairs.values()],'o-',color=colors[a],label=label)
    t=traces[a]
    axes[1].plot([float(r['t']) for r in t],[1500-float(r['range']) for r in t],color=colors[a],label=label)
    axes[2].plot([float(r['t']) for r in t],[(float(r['velX'])**2+float(r['velY'])**2)**.5 for r in t],color=colors[a],label=label)
axes[0].set(title='Goalward speed',xlabel='Development seed',ylabel='m/s'); axes[0].ticklabel_format(useOffset=False,style='plain',axis='x');axes[0].legend()
axes[1].set(title=f'Smallest paired gain: seed {seed}',xlabel='Seconds',ylabel='Progress toward goal (m)')
axes[2].set(xlabel='Seconds',ylabel='m/s',title='Actual speed, same seed')
for ax in axes: ax.grid(alpha=.2)
fig.suptitle(f'Solver-rig development: median gain {summary["relativeMedianGain"]:.1%}; collisions {summary["collisionsOn"]} on / {summary["collisionsOff"]} off\nThreshold: +10% median goalward speed and zero field-on collisions')
fig.tight_layout();fig.savefig(root/'development.png',dpi=160)
print(json.dumps(summary,indent=2))
