import json
from pathlib import Path
import numpy as np
from PIL import Image
from project_art import array, image, project, sample

root=Path('D:/amind/git/agent-8')
out=root/'scratch/bulwark-glass'
data=json.loads((out/'Bulwark-unity.json').read_text())
co,uv=array(data['vertices'],'xyz'),array(data['uv'],'xy')
triangles=np.array(data['triangles']).reshape(-1,3)
mapped,covered,low,high=project(co,uv,triangles)
weights=np.stack([sample(image(root/'art/ships/bulwark/layers'/(name+'.png')),mapped)[...,0] for name in ('Shadow','Light','Ink')],axis=-1)
assert sample(image(root/'art/ships/bulwark/layers/DetailEmission.png'),mapped)[...,0][covered].max()==0
rgba=np.concatenate((weights,covered[...,None]),axis=-1)
rgba[~covered]=0
pixels=np.concatenate((rgba[0],rgba[1]),axis=1)[::-1]
recovered=np.uint8(np.rint(np.clip(pixels,0,1)*255))
for side in range(2):
 tile=recovered[:,side*512:(side+1)*512]
 for row in tile:
  clean=(row[:,1]==0)&(row[:,3]>0)
  strokes=(row[:,1]>0)&(row[:,3]>0)
  if not strokes.any(): continue
  values,count=np.unique(row[clean,0],return_counts=True)
  row[strokes,0]=values[np.argmax(count)]
expected=np.asarray(Image.open(root/'art/ships/bulwark/canopy/Bulwark reflections.png'))
assert np.array_equal(recovered,expected)
print('Recovered reflection pixels exactly match the committed asset.')
