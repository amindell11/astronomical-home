from pathlib import Path
import csv,json,shutil
out=Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
folder=Path('D:/amind/git/agent-4/results/capture/frames/20260924-143538-DrawnContourScenario')
for label,frame in [('flight',100),('inspection',600)]:
    shutil.copy2(folder/f'f_{frame:05d}.png',out/f'contour-thick-{label}.png')
for fn in ['motion.csv','manifest.json','rendering.txt']:
    shutil.copy2(folder/fn,out/f'contour-thick-{fn}')
shutil.copy2(str(folder)+'.mp4',out/'contour-thick.mp4')
base=list(csv.DictReader((out/'control-motion.csv').open()))
rows=list(csv.DictReader((folder/'motion.csv').open()))
assert len(base)==len(rows)==900
errors={key:max(abs(float(a[key])-float(b[key])) for a,b in zip(base,rows)) for key in base[0]}
assert all(v==0 for v in errors.values()),errors
(out/'thicker-motion-verification.json').write_text(json.dumps({'samples':len(rows),'max_delta':errors,'width_pixels':3.2,'run':folder.name},indent=2))
print('900 motion samples match exactly; copied thicker clip and raw stills.')
