from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt
s=Path(__file__).parent;d=np.load(s/'finish-fields.npz');covered=d['covered'];dist,nearest=distance_transform_edt(~covered,return_indices=True);pad=(~covered)&(dist<=6)
for field,name in [('shadow','FinishShadow'),('light','FinishLight')]:
 a=d[field].copy();a[pad]=a[tuple(nearest[:,pad])];a[:,1024:]=a[:,:1024];Image.fromarray(np.uint8(np.round(np.clip(a,0,1)*255))).save(s/(name+'.png'))
