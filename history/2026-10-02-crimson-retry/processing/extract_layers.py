import json, copy, numpy as np, zipfile, io
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from scipy.interpolate import RBFInterpolator
from scipy.ndimage import map_coordinates, distance_transform_edt, gaussian_filter
from xml.etree.ElementTree import Element,SubElement,tostring
out=Path('D:/amind/git/agent-2/results/valis-crimson-retry/v02')
if (out/'ARTIST_SOURCE_LOCK').exists():raise RuntimeError('Artist source is locked. Use a new revision directory; do not overwrite these layers or this blend.')
(out/'layers').mkdir(exist_ok=True)
charts=json.loads((out/'charts.json').read_text());geom={p['name']:p for p in json.loads((out/'geometry.json').read_text())}; proj=json.loads((out/'projection.json').read_text())
pal=json.loads(Path('D:/amind/git/astronomical-home/results/valis-texturing-restart/base-model/palettes.json').read_text())['palettes']['jade-iris']
source=np.asarray(Image.open(out/'paint-source.png').convert('RGBA'),dtype=float)/255
flat=np.asarray(Image.open(out/'valis-flat-source.png').convert('RGBA'),dtype=float)/255
h,w=source.shape[:2]; valid=source[:,:,3]>.8
inds=distance_transform_edt(~valid,return_distances=False,return_indices=True)
source=source[inds[0],inds[1]]
def linear(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def srgb(x):return np.where(x<=.0031308,x*12.92,1.055*np.maximum(x,0)**(1/2.4)-.055)
weights=np.array([.2126,.7152,.0722]);lum=linear(source[:,:,:3])@weights
# Registration pairs lock the painted source to the native model's landmarks.
a=np.array([[734,153],[866,153],[671,296],[929,296],[598,457],[1002,457],[704,552],[896,552],[148,430],[1452,430],[122,708],[1478,708],[399,691],[1201,691],[657,922],[943,922],[647,1009],[953,1009],[470,1138],[1130,1138],[211,1235],[1389,1235],[670,1447],[930,1447],[800,1365],[800,1062],[800,520],[800,351],[555,1290],[1045,1290]],float)/1600
b=np.array([[574,118],[674,118],[519,229],[729,229],[465,357],[785,357],[550,430],[700,430],[114,336],[1138,336],[93,554],[1157,554],[310,539],[946,539],[508,722],[740,722],[505,789],[744,790],[367,889],[887,889],[162,962],[1088,962],[523,1132],[733,1132],[624,1068],[624,831],[624,405],[624,275],[432,1008],[816,1008]],float)/1280
warp=RBFInterpolator(a,b,kernel='thin_plate_spline',smoothing=0.000002)
leftcharts=copy.deepcopy(charts)
for c in leftcharts:c['rect'][1]+=2048;c['mirrored']=True
charts+=leftcharts
size=(4096,4096);shadow=np.zeros(size[::-1],np.float32);light=shadow.copy();ink=shadow.copy();guide=Image.new('RGB',size,(32,37,46));gd=ImageDraw.Draw(guide)
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',17)
for ci,c in enumerate(charts):
 x,y,cw,ch=c['rect']; xs=np.linspace(c['lo'][0],c['hi'][0],cw);ys=np.linspace(c['hi'][1],c['lo'][1],ch);xx,yy=np.meshgrid(xs,ys)
 maskim=Image.new('L',(cw,ch));md=ImageDraw.Draw(maskim)
 for fi in c['faces']:
  f=geom[c['object']]['faces'][fi]
  pts=[((c['xy'][str(v)][c['axes'][0]]-c['lo'][0])/max(c['hi'][0]-c['lo'][0],1e-7)*(cw-1),(c['hi'][1]-c['xy'][str(v)][c['axes'][1]])/max(c['hi'][1]-c['lo'][1],1e-7)*(ch-1)) for v in f['verts']]
  md.polygon(pts,fill=255)
 mask=np.array(maskim)>0
 base=np.array(pal[c['material']][:3]);by=base@weights
 sample_x=-xx if c.get('mirrored') else xx
 u=(sample_x-proj['center'][0])/proj['span']+.5;v=.5-(yy-proj['center'][1])/proj['span']
 if c['axes']==[0,1] and 'underside' not in c['object'] and c['key']=='top':
  reg=np.stack([u.ravel(),v.ravel()],axis=-1); sx=reg[:,0].reshape(xx.shape)*(w-1);sy=reg[:,1].reshape(yy.shape)*(h-1)
  py=map_coordinates(lum,[sy,sx],order=1,mode='nearest')
  rgb=np.stack([map_coordinates(flat[:,:,i],[v*1599,u*1599],order=0,mode='nearest') for i in range(3)],axis=-1)
  visible=np.max(abs(rgb-srgb(base)),axis=-1)<.045
 else:py=np.full(xx.shape,by*.76);visible=np.zeros(xx.shape,bool)
 # Hidden side and underside surfaces receive broad form tones in their own coordinates.
 ax=(xx-c['lo'][0])/max(c['hi'][0]-c['lo'][0],1e-7);ay=(yy-c['lo'][1])/max(c['hi'][1]-c['lo'][1],1e-7)
 form=(.65+.24*ay+.04*ax)
 if c['material']=='Canopy':form=.65+.42*ax
 if c['material']=='Lavender':form=.70+.24*ay
 if c['object'].startswith('08'):form=.68+.20*ay
 exposure=gaussian_filter(visible.astype(float),1.0);py=exposure*py+(1-exposure)*by*form
 sh=np.clip(1-py/max(by,1e-4),0,.87);li=np.clip((py-by)/max(.90-by,1e-4),0,.68)
 ik=np.clip((.20-py/max(by,1e-4))/.16,0,1)*visible
 if c['key']=='top' and c['object'][:2] in {'04','07','11','12','20','21','30','31','40','41','50','51','61'}:
  padded=np.pad(mask,1);
  if c['axes'][0]==0 and abs(c['lo'][0])<1e-6:padded[1:-1,0]=mask[:,0]
  dist=distance_transform_edt(padded)[1:-1,1:-1]
  rng=np.random.default_rng(ci+415);rough=gaussian_filter(rng.normal(size=mask.shape),1.1)*.60
  edge=np.clip((2.15+rough-dist)*1.7,0,1)*.78
  ik=np.maximum(ik,edge)
 sh=np.where(mask,sh,0);li=np.where(mask,li,0);ik=np.where(mask,ik,0)
 # Nearest-edge dilation fills UV gutters without changing the painted surface.
 near=distance_transform_edt(~mask,return_distances=False,return_indices=True)
 for arr,t in [(shadow,sh),(light,li),(ink,ik)]:
  arr[y:y+ch,x:x+cw]=t[near[0],near[1]]
 color=tuple((np.clip(srgb(base),0,1)*255).astype(int))
 patch=Image.new('RGB',(cw,ch),color);guide.paste(patch,(x,y),maskim)
 for fi in c['faces']:
  f=geom[c['object']]['faces'][fi]
  ps=[(x+(c['xy'][str(v)][c['axes'][0]]-c['lo'][0])/max(c['hi'][0]-c['lo'][0],1e-7)*(cw-1),y+(c['hi'][1]-c['xy'][str(v)][c['axes'][1]])/max(c['hi'][1]-c['lo'][1],1e-7)*(ch-1)) for v in f['verts']]
  gd.line(ps+[ps[0]],fill=(190,207,216),width=1)
 gd.text((x,y+ch+1),c['object']+' / '+c['key'],font=font,fill=(232,233,242))
 print(c['object'],c['key'],'visible',round(float(visible[mask].mean()),2) if mask.any() else 0)
for name,arr in [('Shadow',shadow),('Light',light),('Ink',ink)]:Image.fromarray((arr*255).astype('uint8')).save(out/'layers'/f'{name}.png')
guide.save(out/'UV-guide.png')
settings={'shadow_strength':1.0,'light_strength':1.0,'ink_strength':1.0,'light_color_linear':[.82,.93,.88,1],'ink_color_linear':[.006,.012,.018,1]}
(out/'settings.json').write_text(json.dumps(settings,indent=2))
root=Element('image',w='4096',h='4096',name='Valis Crimson paint masks');stack=SubElement(root,'stack')
with zipfile.ZipFile(out/'Valis-Paint-Layers.ora','w') as z:
 z.writestr('mimetype','image/openraster')
 for name in ['Ink','Light','Shadow']:
  SubElement(stack,'layer',name=name,src=f'data/{name}.png',opacity='1.0',visibility='visible',**{'composite-op':'svg:src-over','x':'0','y':'0'})
  z.write(out/'layers'/f'{name}.png',f'data/{name}.png')
 z.writestr('stack.xml',tostring(root));z.write(out/'UV-guide.png','mergedimage.png')







