import json,numpy as np
from pathlib import Path
from PIL import Image
from scipy.ndimage import distance_transform_edt
s=Path(__file__).parent;g=s/'generation/layers';d=json.loads((s/'canopy-surface.json').read_text());v=np.array(d['vertices']);N=2048
covered=np.zeros((N,N),bool);fields={k:np.zeros((N,N),np.float32) for k in ['Shadow','Light','Ink']}
for tri in d['triangles']:
 uv=np.array(tri['uv'])*N;uv[:,1]=N-uv[:,1];A=np.vstack([uv.T,np.ones(3)])
 if abs(np.linalg.det(A))<1e-9:continue
 lo=np.maximum(0,np.floor(uv.min(0)).astype(int));hi=np.minimum(N,np.ceil(uv.max(0)).astype(int))
 if np.any(hi<=lo):continue
 Y,X=np.mgrid[lo[1]:hi[1],lo[0]:hi[0]];w=np.stack([X+.5,Y+.5,np.ones_like(X)],-1)@np.linalg.inv(A).T;inside=w.min(-1)>=-1e-6
 xyz=w@v[tri['v']];x=np.abs(xyz[...,0]);t=(xyz[...,1]-v[:,1].min())/np.ptp(v[:,1]);z=xyz[...,2]
 shadow=np.where(t<.27,.88,.72);light=np.zeros_like(t);ink=np.where(z<2.185,.9,0.)
 center=.19-.10*t;streak=(np.abs(x-center)<.043)&(t>.19)&(t<.89)&(z>2.24)
 shadow[streak]=.22;light[streak]=.62
 fine=(np.abs(x-(center+.085))<.012)&(t>.38)&(t<.78)&(z>2.24)
 shadow[fine]=.15;light[fine]=.85
 for name,value in [('Shadow',shadow),('Light',light),('Ink',ink)]:fields[name][lo[1]:hi[1],lo[0]:hi[0]][inside]=value[inside]
 covered[lo[1]:hi[1],lo[0]:hi[0]][inside]=True
dist,idx=distance_transform_edt(~covered,return_indices=True);region=dist<=6
report={}
for name,values in fields.items():
 arr=np.array(Image.open(g/(name+'.png')).convert('L'));old=arr.copy();padded=values[tuple(idx)];arr[region]=np.rint(padded[region]*255).astype('uint8');Image.fromarray(arr).save(g/(name+'.png'));report[name]=int(np.count_nonzero(old!=arr))
(s/'canopy-edit.json').write_text(json.dumps({'changed_texels':report,'method':'Painted symmetric reflection bands and lower dark wedge in canopy UV chart; six pixel padding; generated baseline retained'},indent=2))
