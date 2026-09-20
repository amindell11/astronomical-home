import csv,json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
root=Path('results/mpc-rig/terminal-field/independent/fast-traversal')
cases=[('48 cells, weight 3','development-20260910-062109-438',0),('96 cells, weight 3','resolution96-seed4104-20260910-063036-962',0),('48 cells, weight 0.3','weight0.3-seed4104-20260910-063212-500',0),('Field off','development-20260910-062109-438',1)]
fig,axes=plt.subplots(1,2,figsize=(11,4.8))
summary=[]
for label,folder,arm in cases:
 rows=list(csv.DictReader((root/folder/'summary.csv').open()))
 row=next(r for r in rows if r['seed']=='4104' and int(r['arm'])==arm)
 trace=list(csv.DictReader((root/folder/f'seed4104-arm{arm}.csv').open()))
 axes[0].plot([float(r['t']) for r in trace],[(float(r['velX'])**2+float(r['velY'])**2)**.5 for r in trace],label=label)
 summary.append({'case':label,'progressSpeed':float(row['progressSpeed']),'collisions':int(row['sweptCollisionSteps']),'spacing':float(row['fieldSpacing']),'meanTerminalCost':float(row['meanTerminalCost'])})
axes[1].scatter([r['progressSpeed'] for r in summary],[r['case'] for r in summary],s=85,c=['#287e8d','#759bc1','#deaa48','#777777'])
axes[1].set_xlim(19.5,21.1)
for i,r in enumerate(summary): axes[1].text(r['progressSpeed']+.02,i,f"{r['progressSpeed']:.2f}",va='center')
axes[0].set(xlabel='Seconds',ylabel='Actual speed (m/s)',title='Same terrain and seed 4104')
axes[0].legend(fontsize=8);axes[1].set(xlabel='Goalward speed (m/s)',title='All four cases: zero collisions')
fig.suptitle('Lower field weight recovers the slowdown; finer resolution barely changes it')
fig.tight_layout();fig.savefig(root/'diagnostics-4104.png',dpi=150)
(root/'diagnostics-4104.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))
