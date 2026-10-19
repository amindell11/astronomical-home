import bpy,sys,json,numpy as np
from pathlib import Path
s=Path(__file__).parent;sys.path.insert(0,str(s/'tool/art/tools/ship'))
import ship_source,ship_contract,ship_paint,ship_regions
ship,source=ship_contract.load_ship(s/'generation/ship.json');paint=ship_contract.load_settings(s/'generation/paint.json',ship_contract.parse_paint)
scene=ship_source.open_source(source);roles=ship_source.Roles(scene);e=ship_source.Evaluation(scene)
names=sorted(n for r in ship_contract.VISUAL_ROLES for n in roles.members.get(r,()) if scene.objects[n].type=='MESH')
size=ship['texture_size'];occupied=np.zeros((size,size),np.uint8);owner=np.full((size,size),-1,np.int32);reports=[]
for ni,name in enumerate(names):
 part=ship_paint.Part(e,name,paint)
 for tri,x,y,p in ship_regions.surface_samples(part,size,ship_contract.PAINT_UV,tolerance=1e-6):
  side=(p[:,0]<0).astype(np.uint8)+2*(p[:,0]>0).astype(np.uint8);hit=(occupied[y,x]|side)==3
  if hit.any():reports.append({'part':name,'face':tri.polygon_index,'texels':int(hit.sum()),'previous_parts':sorted({names[i] for i in owner[y[hit],x[hit]] if i>=0}),'example_uv':[(int(a),int(b)) for a,b in zip(x[hit][:3],y[hit][:3])]})
  occupied[y,x]|=side;owner[y,x]=ni
(s/'uv-overlap.json').write_text(json.dumps(reports,indent=2));print(json.dumps({'records':len(reports),'affected_parts':sorted({r['part'] for r in reports}),'reported_overlap_samples':sum(r['texels'] for r in reports),'first':reports[:5]},indent=2))
