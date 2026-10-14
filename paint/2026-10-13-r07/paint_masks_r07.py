from pathlib import Path
import json, hashlib
import numpy as np
from scipy.ndimage import distance_transform_edt
from PIL import Image

root = Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r07')
data = json.loads((root/'paint-mesh.json').read_text())
size = 2048
position = np.zeros((size,size,3), np.float32)
part_id = np.full((size,size), -1, np.int16)
for index, part in enumerate(data['parts']):
    vertices = np.array(part['worldVertices']).reshape(-1,3)
    uv = np.array(part['uv']).reshape(-1,2)
    uv = np.column_stack((uv[:,0]*size, (1-uv[:,1])*size))
    for i in range(0,len(vertices),3):
        t = uv[i:i+3]
        lo = np.maximum(np.floor(t.min(axis=0)).astype(int),0)
        hi = np.minimum(np.ceil(t.max(axis=0)).astype(int),size-1)
        if (hi<lo).any(): continue
        a,b,c = t
        den = (b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-10: continue
        y,x = np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        x=x+.5; y=y+.5
        w0=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/den
        w1=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/den
        w2=1-w0-w1
        inside=(w0>=-1e-7)&(w1>=-1e-7)&(w2>=-1e-7)
        if not inside.any(): continue
        yy,xx=np.nonzero(inside); py=yy+lo[1]; px=xx+lo[0]
        weights=np.column_stack((w0[inside],w1[inside],w2[inside]))
        position[py,px]=weights@vertices[i:i+3]
        part_id[py,px]=index

covered=part_id>=0
xyz=position[covered]
layers={name:np.array(Image.open(root/'layers'/(name+'.png'))).astype(np.float32)/255
        for name in ('Shadow','Light','Ink','BaseRegion')}
regions=layers['BaseRegion'][covered].copy()
shade=np.clip(1-layers['Shadow'][covered]*.45-layers['Ink'][covered]*.70+layers['Light'][covered]*.15,.15,1)
allowed=np.zeros(len(xyz),bool)
for index, part in enumerate(data['parts']):
    if part['name'] not in ('02 Upper hull.001','05 Tail tips.002','09 Side armor.001'): continue
    mask=part_id[covered]==index
    if part['name']=='02 Upper hull.001': mask&=xyz[:,1]<1.8
    p=xyz[mask]; x=np.abs(p[:,0])
    center=-.70+.65*np.minimum(x,2.1)-.45*np.maximum(x-2.1,0)
    slope=np.where(x<=2.1,.65,-.45)
    distance=np.abs(p[:,1]-center)/np.sqrt(1+slope*slope)
    band=np.clip((.19-distance)/.022+.5,0,1)
    regions[mask]=band*shade[mask]
    allowed|=mask
before=layers['BaseRegion'][covered]
assert np.array_equal(regions[~allowed],before[~allowed])
result=layers['BaseRegion'].copy()
result[covered]=regions
distance,nearest=distance_transform_edt(~covered,return_indices=True)
padding=(~covered)&(distance<=6)
result[padding]=result[tuple(nearest[:,padding])]
Image.fromarray(np.round(np.clip(result,0,1)*255).astype(np.uint8)).save(root/'layers/BaseRegion.png')
audit={'mesh_source_sha256':data['sourceSha256'],'texture_size':size,
    'method':'Single shared model-space rear band across armor tops, slopes and thickness sides',
    'centerline':'Y=-0.70+0.65*min(abs(X),2.1)-0.45*max(abs(X)-2.1,0)',
    'perpendicular_width':.38,'edge_softness':.022,
    'changed_parts':['02 Upper hull.001 rear only','05 Tail tips.002','09 Side armor.001'],
    'unchanged_other_covered_texels':True,'shadow_light_ink_unchanged':True,'colour_baked':False,
    'mask_sha256':{name:hashlib.sha256((root/'layers'/(name+'.png')).read_bytes()).hexdigest() for name in layers}}
(root/'mask-audit.json').write_text(json.dumps(audit,indent=2))
print(json.dumps(audit))
