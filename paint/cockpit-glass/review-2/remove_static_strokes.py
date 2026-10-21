from pathlib import Path
import json
import numpy as np
from PIL import Image
root=Path('D:/amind/git/agent-8')
source=root/'art/ships/bulwark/canopy/Bulwark reflections.png'
p=np.asarray(Image.open(source)).copy()
before=p.copy()
counts=[]
for side in range(2):
 tile=p[:,side*512:(side+1)*512]
 for row in tile:
  clean=(row[:,1]==0)&(row[:,3]>0)
  strokes=(row[:,1]>0)&(row[:,3]>0)
  if not strokes.any(): continue
  values,count=np.unique(row[clean,0],return_counts=True)
  assert len(values)>0
  base=values[np.argmax(count)]
  row[strokes,0]=base
assert np.array_equal(p[:,:,1:],before[:,:,1:])
assert np.all(p[:,:,0][before[:,:,1]==0]==before[:,:,0][before[:,:,1]==0])
for target in (source,root/'src/Asteroids3D/Assets/Visuals/Ships/Bulwark/Canopy/Bulwark reflections.png'):
 Image.fromarray(p).save(target)
report={'changed_shadow_pixels':int(np.count_nonzero(p[:,:,0]!=before[:,:,0])), 'light_ink_coverage_unchanged':True, 'shadow_outside_strokes_unchanged':True}
(root/'scratch/bulwark-glass/static-cleanup.json').write_text(json.dumps(report,indent=2))
print(report)
