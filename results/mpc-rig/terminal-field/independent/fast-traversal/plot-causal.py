import csv,json,math
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
root=Path('results/mpc-rig/terminal-field/independent/fast-traversal')
fig,ax=plt.subplots(1,3,figsize=(15,4.8))
for arm,label,color in [(0,'Field on','#16877e'),(1,'Field off','#bc6032')]:
 rows=list(csv.DictReader((root/f'development-20260910-062109-438/seed4103-arm{arm}.csv').open()))[:600]
 speeds=[-math.sin(math.radians(float(r['yawDeg'])))*float(r['velX'])+math.cos(math.radians(float(r['yawDeg'])))*float(r['velY']) for r in rows]
 ax[0].plot([float(r['t']) for r in rows],speeds,label=label,color=color)
rows=list(csv.DictReader((root/'warm-start-20260910-192547-857/case1.csv').open()))
speeds=[-math.sin(math.radians(float(r['yawDeg'])))*float(r['velX'])+math.cos(math.radians(float(r['yawDeg'])))*float(r['velY']) for r in rows]
ax[0].plot([float(r['t']) for r in rows],speeds,label='Field off, clear plan at 4s',color='#627bc2',ls='--')
ax[0].axhline(0,color='gray',ls='--');ax[0].legend();ax[0].set(title='A slower backward-flight mode',xlabel='Seconds — seed 4103',ylabel='Velocity along ship nose (m/s)')
plateau=list(csv.DictReader(sorted(root.glob('plateau-*.csv'))[-1].open()))
line=[r for r in plateau if r['kind']=='cross-section'];reach=[r for r in plateau if r['kind']=='reachable']
ax[1].plot([float(r['x']) for r in line],[float(r['terminalCost']) for r in line],'o-',color='#287e8d')
lo=min(float(r['x']) for r in reach);hi=max(float(r['x']) for r in reach)
ax[1].axvspan(lo,hi,color='#deaa48',alpha=.35,label='27 sampled endpoint x positions')
ax[1].set(title='Route cost is flat before the turn',xlabel='Lateral endpoint position at y=17 m',ylabel='Terminal field cost');ax[1].legend(fontsize=8)
sensing=list(csv.DictReader(sorted(root.glob('sensing-*'))[-1].joinpath('summary.csv').open()))[:2]
ax[2].scatter([float(r['minimumClearance']) for r in sensing],['Short sensing','Wide sensing'],s=110,c=['#bc6032','#16877e'])
ax[2].axvline(0,color='gray',ls='--');ax[2].set_xlim(-5,1);ax[2].set(title='Same 0.7 s rollout, different sensing',xlabel='Minimum hull clearance (m)')
for i,r in enumerate(sensing):ax[2].annotate(f"{float(r['minimumClearance']):.2f} m",(float(r['minimumClearance']),i),xytext=(8,8),textcoords='offset points')
for a in ax:a.grid(alpha=.2)
fig.suptitle('Short causal probes: flight mode, field-cost plateau, and obstacle visibility')
fig.tight_layout();fig.savefig(root/'causal-mechanisms.png',dpi=160)
