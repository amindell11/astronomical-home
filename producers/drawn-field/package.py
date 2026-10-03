from pathlib import Path
import csv, json, shutil
out=Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
root=Path('D:/amind/git/agent-4/results/capture/frames')
runs={'control':'20260924-134218-DrawnControlScenario','surface':'20260924-133633-DrawnSurfaceScenario','contour':'20260924-134745-DrawnContourScenario'}
traces={}
for name,run in runs.items():
    folder=root/run
    for label,frame in [('flight',100),('inspection',600)]:
        shutil.copy2(folder/f'f_{frame:05d}.png',out/f'{name}-{label}.png')
    for fn in ['motion.csv','manifest.json','rendering.txt']:
        shutil.copy2(folder/fn,out/f'{name}-{fn}')
    clip=root/(run+'.mp4')
    if clip.exists(): shutil.copy2(clip,out/f'{name}.mp4')
    traces[name]=list(csv.DictReader((folder/'motion.csv').open()))
assert len({len(t) for t in traces.values()})==1
base=traces['control']
deltas={name:{key:max(abs(float(a[key])-float(b[key])) for a,b in zip(base,rows)) for key in base[0]} for name,rows in traces.items()}
assert all(v==0 for row in deltas.values() for v in row.values())
result={'runs':runs,'samples_per_run':len(base),'maximum_motion_delta':deltas,'bank_min':min(float(r['bank']) for r in base),'bank_max':max(float(r['bank']) for r in base)}
(out/'motion-verification.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
