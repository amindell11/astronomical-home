import csv
import json
import shutil
import sys
from pathlib import Path

destination=Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion')
turntable=Path('results/asteroid-fractured-sculptpaint')
for version in ['before','after']:
    shutil.copy2(turntable / f'{version}.mp4', destination / f'asteroid-{version}.mp4')
    shutil.copy2(turntable / version / 'f_00000.png', destination / f'asteroid-{version}.png')
    for i in range(8):
        shutil.copy2(turntable / version / f'f_{i*18:05d}.png', destination / f'asteroid-{version}-{i}.png')
source=Path(sys.argv[1])
shutil.copy2(source.with_suffix('.mp4'),destination/'asteroid-gameplay.mp4')
shutil.copy2(source/'f_00100.png',destination/'asteroid-gameplay.png')
shutil.copy2(source/'f_00600.png',destination/'asteroid-inspection.png')
for filename in ['motion.csv','rendering.txt','manifest.json']:
    shutil.copy2(source/filename,destination/f'asteroid-{filename}')
with (source/'motion.csv').open(encoding='utf-8') as f: trace=list(csv.DictReader(f))
assert len(trace)==900
assert max(float(r['bank']) for r in trace)>10 and min(float(r['bank']) for r in trace)<-10
shutil.copy2('art/asteroid-study/FracturedRockReference.png',destination/'asteroid-reference.png')
shutil.copy2('scratch/capture/fracture-study-notes.md',destination/'asteroid-study-notes.md')
if not (destination/'contour-study.html').exists(): shutil.copy2(destination/'comparison.html',destination/'contour-study.html')
shutil.copy2('scratch/capture/fracture-review.html',destination/'comparison.html')
evidence={'turntable_pose_count':144,'turntable_fps':24,'gameplay_source':str(source.resolve()),'motion_samples':len(trace),'minimum_bank':min(float(r['bank']) for r in trace),'maximum_bank':max(float(r['bank']) for r in trace),'visual_geometry_only':True,'screen_ink':False}
(destination/'asteroid-verification.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8')
print(json.dumps(evidence,indent=2))


