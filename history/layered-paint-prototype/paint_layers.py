import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
OUT=Path(__file__).parent
SIZE=4096
BRUSH_CONTRAST=.24
SHADE_STRENGTH=.32
EDGE_WIDTH=5
INK_STRENGTH=.68
parts=json.loads((OUT/'uv-layout.json').read_text())['parts']
brush=np.asarray(Image.open(OUT/'brush-source.png').convert('L'),dtype=float)/255
lo,hi=np.percentile(brush,[2,98]);brush=np.clip((brush-lo)/(hi-lo),0,1)
shade=np.zeros((SIZE,SIZE),np.float32);light=np.zeros_like(shade);coverage=np.zeros((SIZE,SIZE),np.uint8)
ink=Image.new('L',(SIZE,SIZE));draw=ImageDraw.Draw(ink);rim=Image.new('L',(SIZE,SIZE));rimdraw=ImageDraw.Draw(rim)
uvdebug=Image.new('RGB',(SIZE,SIZE),(30,35,43));debug=ImageDraw.Draw(uvdebug)
def pixel(uv):return (uv[0]*(SIZE-1),(1-uv[1])*(SIZE-1))
for part in parts:
 allp=np.array([p for t in part['triangles'] for p in t['p']]);low=allp.min(0);span=np.maximum(allp.max(0)-low,.001)
 for tri in part['triangles']:
  q=np.array([pixel(p) for p in tri['uv']]);p=np.array(tri['p']);normal=np.array(tri['n'])
  xmin,ymin=np.maximum(np.floor(q.min(0)).astype(int),0);xmax,ymax=np.minimum(np.ceil(q.max(0)).astype(int),SIZE-1)
  if xmax<=xmin or ymax<=ymin:continue
  xx,yy=np.meshgrid(np.arange(xmin,xmax+1)+.5,np.arange(ymin,ymax+1)+.5)
  den=(q[1,1]-q[2,1])*(q[0,0]-q[2,0])+(q[2,0]-q[1,0])*(q[0,1]-q[2,1])
  if abs(den)<.001:continue
  a=((q[1,1]-q[2,1])*(xx-q[2,0])+(q[2,0]-q[1,0])*(yy-q[2,1]))/den
  b=((q[2,1]-q[0,1])*(xx-q[2,0])+(q[0,0]-q[2,0])*(yy-q[2,1]))/den;c=1-a-b;mask=(a>=0)&(b>=0)&(c>=0)
  pos=a[...,None]*p[0]+b[...,None]*p[1]+c[...,None]*p[2];norm=(pos-low)/span
  axis=np.argmax(abs(normal));axes=[i for i in range(3) if i!=axis];u,v=norm[...,axes[0]],norm[...,axes[1]]
  bx=(u*brush.shape[1]*1.4).astype(int)%brush.shape[1];by=(v*brush.shape[0]*1.7).astype(int)%brush.shape[0];grain=brush[by,bx]
  plane=np.dot(normal,np.array([-.36,-.4,.84]));band=0 if plane>.65 else .30 if plane>.05 else .48
  dark=band+np.maximum(.52-grain,0)*BRUSH_CONTRAST
  bright=np.maximum(grain-.48,0)*.17
  if part['material']=='Canopy':
   dark=np.full_like(u,.25);bright=np.exp(-((norm[...,0]-.23)/.065)**2)*.35+np.exp(-((norm[...,0]-.39)/.018)**2)*.1
  box=(slice(ymin,ymax+1),slice(xmin,xmax+1));shade[box][mask]=dark[mask];light[box][mask]=bright[mask];coverage[box][mask]=255
  debug.polygon([tuple(v) for v in q],fill=(100,140,160),outline=(180,210,230))
 if part['material'] not in ('Canopy','Graphite','Edge','Engine'):
  for edge in part['edges']:
   q=np.array([pixel(v) for v in edge[:2]]);center=np.array(pixel(edge[2]));line=q+(center-q)*.045
   draw.line([tuple(v) for v in line],fill=220,width=5)
   inner=q+(center-q)*.09;rimdraw.line([tuple(v) for v in inner],fill=200,width=4)
 if part['material'] in ('Graphite','Edge','Engine'):
  for tri in part['triangles']:
   q=np.array([pixel(p) for p in tri['uv']]);debug.line([tuple(v) for v in q],fill=(70,90,100),width=1)
 # Sparse marks stay inside the largest upward-facing authored face.
 if part['name'] in ('04 dorsal armor','05 dorsal shoulder bevel','60 engine spine'):
  choices=[t for t in part['triangles'] if t['n'][2]>.55]
  if choices:
   t=max(choices,key=lambda t:np.linalg.norm(np.cross(np.subtract(t['p'][1],t['p'][0]),np.subtract(t['p'][2],t['p'][0]))));q=np.array([pixel(v) for v in t['uv']]);center=q.mean(0);a=q[1]-q[0];a/=np.linalg.norm(a);b=np.array([-a[1],a[0]]);radius=min(np.linalg.norm(center-v) for v in q)*.14
   if part['name']=='04 dorsal armor':
    points=[center+a*x*radius+b*y*radius for x,y in [(-.5,-1),(.5,-1),(.5,1),(-.5,1),(-.5,-1)]];draw.line([tuple(v) for v in points],fill=165,width=2)
   else:
    for shift in (-.45,0,.45):
     start=center+a*radius*shift-b*radius*.55;end=center+a*radius*shift+b*radius*.55;draw.line([tuple(start),tuple(end)],fill=220,width=5)
light=np.maximum(light,np.asarray(rim,dtype=np.float32)/255)
for name,arr in [('Paint_Shadow',shade),('Paint_Light',light)]:
 img=Image.fromarray(np.uint8(np.clip(arr,0,1)*255));img=img.filter(ImageFilter.MaxFilter(5));img.save(OUT/(name+'.png'))
ink.save(OUT/'Paint_Ink.png');uvdebug.save(OUT/'UV_Layout.png')
(OUT/'paint-settings.json').write_text(json.dumps({'brush_contrast':BRUSH_CONTRAST,'shadow_mix':.55,'light_mix':.22,'ink_mix':.72,'detail_direction':'B brushed gouache, reduced technical density'},indent=2))
print('Three editable 4096px mask layers authored')


