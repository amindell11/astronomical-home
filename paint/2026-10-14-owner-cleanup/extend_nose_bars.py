from pathlib import Path
import json
import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

stage=Path(__file__).parent
mesh=json.loads((stage/'owner-mesh.json').read_text())
part=mesh['parts'][5]
owner=np.load(stage/'owner-BaseRegion.npy')[::-1,:,0]
prior=np.asarray(Image.open(stage.parent/'2026-10-13-r07/layers/BaseRegion.png').convert('L'))/255

def samples(face,scale=1):
    uv=np.array(face['uv'])*[2048*scale,-2048*scale]+[0,2048*scale]
    xyz=np.array(part['vertices'])[face['vertices']]
    for i in range(1,len(uv)-1):
        tri=uv[[0,i,i+1]];co=xyz[[0,i,i+1]]
        lo=np.maximum(np.floor(tri.min(axis=0)).astype(int),0)
        hi=np.minimum(np.ceil(tri.max(axis=0)).astype(int),2048*scale-1)
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
        mat=np.stack([tri[1]-tri[0],tri[2]-tri[0]],axis=1)
        if abs(np.linalg.det(mat))<1e-7:continue
        weights=(np.stack([xx+.5,yy+.5],axis=-1)-tri[0])@np.linalg.inv(mat).T
        inside=(weights[:,:,0]>=0)&(weights[:,:,1]>=0)&(weights.sum(axis=2)<=1)
        world=co[0]+weights[:,:,0,None]*(co[1]-co[0])+weights[:,:,1,None]*(co[2]-co[0])
        yield xx[inside],yy[inside],world[inside]

points=[]
for fi in [44,49]:
    for x,y,world in samples(part['faces'][fi]):
        changed=(prior[y,x]>.5)&(owner[y,x]<.15)
        points.extend(world[changed,:2])
points=np.array(points)
center=points.mean(axis=0)
_,_,vt=np.linalg.svd(points-center,full_matrices=False)
normal=vt[1]
dist=(points@normal).astype(np.float32)
_,labels,centers=cv2.kmeans(dist.reshape(-1,1),2,None,(cv2.TERM_CRITERIA_EPS+cv2.TERM_CRITERIA_MAX_ITER,100,.0001),12,cv2.KMEANS_PP_CENTERS)
bars=[]
for k in range(2):
    pts=points[labels[:,0]==k]
    _,_,axes=np.linalg.svd(pts-pts.mean(axis=0),full_matrices=False)
    n=axes[1];d=pts@n
    low,high=np.percentile(d,[1,99])
    bars.append({'normal':n.tolist(),'low':float(low),'high':float(high),'samples':len(pts)})
(stage/'nose-bars.json').write_text(json.dumps(bars,indent=2),encoding='utf-8')
print(json.dumps({'points':len(points),'bars':bars}))

scale=4
cut=np.zeros((2048*scale,2048*scale),np.uint8)
coverage=np.zeros_like(cut)
faces=[]
for fi,face in enumerate(part['faces']):
    xyz=np.array(part['vertices'])[face['vertices']]
    if xyz[:,0].max()<.01 or face['material']!=1:continue
    faces.append(fi)
    for x,y,world in samples(face,scale):
        select=np.zeros(len(world),bool)
        for bar in bars:
            d=world[:,:2]@np.array(bar['normal'])
            select|=(d>=bar['low'])&(d<=bar['high'])
        cut[y[select],x[select]]=255
        coverage[y,x]=255
bleed=cv2.dilate(cut,np.ones((5*scale,5*scale),np.uint8))
cut[coverage==0]=bleed[coverage==0]
mask=np.asarray(Image.open(stage/'clean-BaseRegion.png').convert('L').resize((2048*scale,2048*scale),Image.Resampling.BICUBIC)).copy()
mask[cut>0]=0
Image.fromarray(mask).resize((2048,2048),Image.Resampling.LANCZOS).save(stage/'clean-BaseRegion.png')
print('Projected on nose faces',faces)
