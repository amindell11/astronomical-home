from PIL import Image,ImageFilter
import numpy as np
from pathlib import Path
p=Path('D:/amind/git/agent-4/results/contour-probe')
counts={}
for frame in [450,600,750]:
    base=np.asarray(Image.open(p/f'{frame}-base.png').convert('RGB'))
    ink=np.asarray(Image.open(p/f'{frame}-ink.png').convert('RGB'))
    mask=Image.fromarray((np.min(base,axis=2)>240).astype('uint8')*255).filter(ImageFilter.MinFilter(17))
    count=int(np.sum((np.asarray(mask)>0)&(np.max(ink,axis=2)<35)))
    counts[frame]=count
    total=int(np.sum(np.max(ink,axis=2)<35))
    assert total>200, f'Contour disappeared: {frame}, {total}'
print('Deep interior black pixels (expected zero):',counts)
assert all(count==0 for count in counts.values()),'Contour shell paints broken interior marks'
