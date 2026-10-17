import json,numpy as np
from pathlib import Path
from PIL import Image
s=Path(__file__).parent;old=s.parent/'2026-10-17-symmetric-lines'
a=np.array(Image.open(old/'BaseRegion.png').convert('L'))
m=json.load(open(s.parent/'2026-10-15-uniform-cleanup/inspection-mesh.json'));p=next(p for p in m['parts'] if p['name']=='Hull - Ram nose');M=np.array(p['matrix']);v=(M@np.c_[p['vertices'],np.ones(len(p['vertices']))].T).T[:,:3]
N=2048;S=4;B=2.60;K=1.4125;D=1.1
# Work in world XY so unequal UV stretch cannot change stripe angles or width.
for f in p['faces']:
 if f['material']!=1:continue
 uv=np.array(f['uv'])*N;uv[:,1]=N-uv[:,1];world=v[f['vertices']]
 lo=np.floor(uv.min(0)-2).astype(int);hi=np.ceil(uv.max(0)+2).astype(int)
 xs=lo[0]+(np.arange((hi[0]-lo[0])*S)+.5)/S;ys=lo[1]+(np.arange((hi[1]-lo[1])*S)+.5)/S
 X,Y=np.meshgrid(xs,ys);points=np.stack([X,Y,np.ones_like(X)],axis=-1);coverage=np.zeros(X.shape,bool);paint=np.zeros(X.shape,bool)
 for j in range(1,len(uv)-1):
  ids=[0,j,j+1];tri=uv[ids];w=points@np.linalg.inv(np.vstack([tri.T,np.ones(3)])).T;inside=np.min(w,axis=-1)>=-1e-7;pos=w@world[ids];x=abs(pos[:,:,0]);y=pos[:,:,1]
  def boundary(offset):return np.maximum(B-offset,B+D*(x-K)-offset*np.sqrt(1+D*D))
  white=(y>=boundary(0))|((y>=boundary(.80))&(y<=boundary(.35)))
  coverage|=inside;paint|=inside&white
 cov=coverage.reshape(hi[1]-lo[1],S,hi[0]-lo[0],S).mean((1,3));col=paint.reshape(hi[1]-lo[1],S,hi[0]-lo[0],S).mean((1,3))
 patch=a[lo[1]:hi[1],lo[0]:hi[0]];patch[:]=np.rint(patch*(1-cov)+255*col).astype('uint8')
a[:,1024:]=a[:,:1024]
assert np.array_equal(a[:,:1024],a[:,1024:])
Image.fromarray(a).save(s/'BaseRegion.png')
(s/'definition.json').write_text(json.dumps({'hood_center_y':B,'hood_corner_x':K,'hood_side_slope':D,'gap':.35,'stripe_width':.45,'mirror_offset_pixels':1024,'symmetry_exact':True},indent=2))
