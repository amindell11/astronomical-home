import json,hashlib
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from scipy.ndimage import distance_transform_edt,gaussian_filter
from PIL import Image
root=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r04')
data=json.loads((root/'paint-mesh.json').read_text());size=2048
position=np.zeros((size,size,3),np.float32);normal=position.copy();part_id=np.full((size,size),-1,np.int16)
for part_index,part in enumerate(data['parts']):
 vertices=np.array(part['worldVertices']).reshape(-1,3);normals=np.array(part['normalsWorld']).reshape(-1,3)
 uv=np.array(part['uv']).reshape(-1,2);uv=np.column_stack((uv[:,0]*size,(1-uv[:,1])*size))
 for i in range(0,len(vertices),3):
  t=uv[i:i+3];lo=np.maximum(np.floor(t.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(t.max(axis=0)).astype(int),size-1)
  if (hi<lo).any():continue
  a,b,c=t;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
  if abs(den)<1e-10:continue
  y,x=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];x=x+.5;y=y+.5
  w0=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/den
  w1=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/den;w2=1-w0-w1
  inside=(w0>=-1e-7)&(w1>=-1e-7)&(w2>=-1e-7)
  if not inside.any():continue
  yy,xx=np.nonzero(inside);py=yy+lo[1];px=xx+lo[0];weights=np.column_stack((w0[inside],w1[inside],w2[inside]))
  position[py,px]=weights@vertices[i:i+3];normal[py,px]=weights@normals[i:i+3];part_id[py,px]=part_index
covered=part_id>=0;xyz=position[covered];norm=normal[covered];norm/=np.maximum(np.linalg.norm(norm,axis=1,keepdims=True),1e-6)
light=np.array([-.48,.28,.83]);light/=np.linalg.norm(light);ndl=norm@light
shadow=np.clip(.08+.38*(1-ndl)+.025*np.sin(xyz[:,1]*1.7+xyz[:,0]*.4),.025,.62)
highlight=np.clip(ndl,0,1)**3*.11
ink=np.zeros(len(xyz));regions=np.zeros(len(xyz))
def noise(p,scale):
 q=p*scale;i=np.floor(q);f=q-i;f=f*f*(3-2*f);value=np.zeros(len(p))
 for x in (0,1):
  for y in (0,1):
   for z in (0,1):
    v=np.sin((i+np.array([x,y,z]))@np.array([12.9898,78.233,37.719]))*43758.5453
    v=v-np.floor(v);w=np.prod(np.where(np.array([x,y,z]),f,1-f),axis=1);value+=v*w
 return value
for idx,part in enumerate(data['parts']):
 mask=part_id[covered]==idx;p=xyz[mask];n=norm[mask]
 if not len(p):continue
 if part['name']=='06 Cockpit dome':
  shadow[mask]=np.clip(.18+.18*(1-n[:,2])+.045*(p[:,1]-1.3),0,.5)
  reflection=np.exp(-((p[:,0]+.22)/.13)**2-((p[:,1]-3.12)/.4)**2)
  secondary=np.exp(-((p[:,0]-.16)/.07)**2-((p[:,1]-1.85)/.53)**2)
  highlight[mask]=reflection*.63+secondary*.19+.05*np.clip(n[:,2],0,1)
  continue
 edges=[e for e in data['edges'] if e['part']==part['name']]
 if edges:
  samples=[]
  for e in edges:
   a=np.array(e['a']);b=np.array(e['b']);steps=max(2,int(np.linalg.norm(a-b)/.012)+1)
   samples.append(a+(b-a)*np.linspace(0,1,steps)[:,None])
  dist,_=cKDTree(np.concatenate(samples)).query(p,workers=2)
  edge=np.clip(1-dist/.055,0,1)
  fine=noise(p,17)
  wear=edge*np.clip((fine-.3)*.48,0,.23)
  highlight[mask]+=wear+.28*np.exp(-((dist-.040)/.021)**2)*np.clip(n[:,2],0,1)
  chips=edge*np.clip((noise(p,11)-.56)*2.2,0,.48)
  ink[mask]=np.maximum(np.exp(-(dist/.018)**2)*.44,chips)
 brush=(noise(p*np.array([1,2.5,1]),8)-.5)*.032
 shadow[mask]+=brush
 if part['name'] in ('01 Ram nose','02 Upper hull.001'):
  x=np.abs(p[:,0]);stripe=4.02-.32*np.clip(x-.95,0,.6)-1.5*np.clip(x-1.55,0,None)
  stripe_mask=np.clip((.115-np.abs(p[:,1]-stripe))/.018+.5,0,1)
  rear_boundary=1.95+.55*x
  bone_coat=np.clip((p[:,1]-rear_boundary)/.022+.5,0,1)
  regions[mask]=bone_coat*(1-stripe_mask)
 if part['name'].startswith('10 Tail fins'):
  x=np.abs(p[:,0])
  boundary=(1.82+.251*(p[:,1]+2.38) if part['name']=='10 Tail fins'
            else 1.80+.073*(p[:,1]+1.90))
  regions[mask]=np.clip((x-boundary)/.022+.5,0,1)
 # A few long panel marks follow the armor direction, stopping short of part ends.
 if part['name'] in ('02 Upper hull.001','09 Side armor.001','05 Tail tips.002'):
  if part['name']=='02 Upper hull.001':
   mark=np.exp(-((p[:,1]-.42)/.014)**2)*(np.abs(p[:,0])<1.95)*(n[:,2]>.8)
  elif part['name']=='09 Side armor.001':
   mark=np.exp(-((np.abs(p[:,0])-.52)/.012)**2)*(p[:,1]>-1.3)*(p[:,1]<-.4)*(n[:,2]>.6)
  else:mark=np.zeros(len(p))
  ink[mask]=np.maximum(ink[mask],mark*.54)
 # Alternate bone coverage retains painted plane shading and edge chips.
regions*=np.clip(1-shadow*.45-ink*.70+highlight*.15,.15,1)
layers={'Shadow':shadow,'Light':highlight,'Ink':ink,'BaseRegion':regions}
distance,nearest=distance_transform_edt(~covered,return_indices=True)
for name,values in layers.items():
 a=np.zeros((size,size),np.float32);a[covered]=np.clip(values,0,1)
 padding=(~covered)&(distance<=6);a[padding]=a[tuple(nearest[:,padding])]
 target=root/'layers'/(name+'.png');target.parent.mkdir(exist_ok=True)
 Image.fromarray(np.round(a*255).astype(np.uint8)).save(target)
stats={name:{'min':float(values.min()),'max':float(values.max()),'sha256':hashlib.sha256((root/'layers'/(name+'.png')).read_bytes()).hexdigest()} for name,values in layers.items()}
(root/'mask-audit.json').write_text(json.dumps({'mesh_source_sha256':data['sourceSha256'],'texture_size':size,'covered_texels':int(covered.sum()),
 'grayscale_layers':stats,'colour_baked':False,'layout':'Approved r02 PaintUV; 6px dilation','method':'Geometry-projected painted planes, edge wear, selective panel ink and canopy reflection; deterministic seed-free brush variation'},indent=2))
print(json.dumps({'coverage':int(covered.sum()),'layers':stats}))
