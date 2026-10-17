import json,cv2,numpy as np
from PIL import Image,ImageDraw
from pathlib import Path
s=Path('results/bulwark/paint/2026-10-17-uniform-cleanup')
a=np.load(s/'owner-mask.npy')[::-1,:,0]
cs,h=cv2.findContours((a>.45).astype('uint8'),cv2.RETR_TREE,cv2.CHAIN_APPROX_SIMPLE)
straight={
60:[[245,1584],[352,1538],[470,1538],[470,1576],[360,1576],[245,1622]],
59:[[1271,1568],[1378,1538],[1494,1538],[1494,1576],[1383,1576],[1271,1606]],
48:[[327,1626],[346,1605],[469,1605],[469,1626]],
47:[[1361,1626],[1383,1605],[1493,1605],[1493,1626]],
76:[[250,1468],[262,1426],[377,1376],[455,1376]],
75:[[1273,1480],[1286,1434],[1377,1378],[1449,1378]],
225:[[526,434],[540,437],[540,634],[526,634]],
224:[[1549,438],[1564,439],[1564,640],[1551,640]],
259:[[551,266],[612,282],[614,408],[603,406],[603,291],[565,281],[562,403],[551,401]],
192:[[667,661],[692,650],[692,868],[667,875]],
190:[[1691,661],[1717,650],[1717,868],[1691,875]],
15:[[402,1810],[427,1797],[427,2018],[402,2024]],
16:[[1427,1808],[1451,1796],[1451,2018],[1427,2024]],
}
can=np.zeros((8192,8192),np.uint8); records=[]
for i,c in enumerate(cs):
 d=0;p=h[0,i,3]
 while p!=-1:d+=1;p=h[0,p,3]
 area=cv2.contourArea(c)
 eps=8 if area>1000 else (2 if area>100 else .6)
 if d%2 and area<60:continue
 if area<5:continue
 poly=cv2.approxPolyDP(c,eps,True)
 if len(poly)<3:poly=c
 if i in straight:poly=np.array(straight[i],dtype=np.int32).reshape(-1,1,2)
 cv2.fillPoly(can,[poly*4],255 if d%2==0 else 0)
 records.append({'id':i,'area':area,'depth':d,'points':poly[:,0].tolist()})
Image.fromarray(can).resize((2048,2048),Image.Resampling.LANCZOS).save(s/'precise-BaseRegion.png')
(s/'polygons.json').write_text(json.dumps(records,indent=2))
im=Image.fromarray(np.repeat(np.uint8(a[:,:,None]*160),3,axis=2));draw=ImageDraw.Draw(im)
for r in records:
 if r['area']>80:
  pts=r['points'];draw.line([tuple(p) for p in pts]+[tuple(pts[0])],fill='cyan',width=1);draw.text(tuple(np.mean(pts,axis=0)),str(r['id']),fill='yellow')
im.save(s/'contour-guide.png')
print('Polygons',len(records))
