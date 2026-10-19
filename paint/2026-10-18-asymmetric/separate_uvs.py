import bpy,sys,json,numpy as np
from pathlib import Path
s=Path(__file__).parent;sys.path.insert(0,str(s/'tool/art/tools/ship'))
import ship_source,ship_contract,ship_paint,ship_regions
ship,source=ship_contract.load_ship(s/'generation/ship.json');paint=ship_contract.load_settings(s/'generation/paint.json',ship_contract.parse_paint)
scene=ship_source.open_source(source);roles=ship_source.Roles(scene);e=ship_source.Evaluation(scene);N=2048;targets=['Armor - Front plate','Armor - Side plates'];occupied=np.zeros((N,N),bool)
for name in sorted({n for r in ship_contract.VISUAL_ROLES for n in roles.members.get(r,()) if scene.objects[n].type=='MESH'}):
 if name in targets:continue
 part=ship_paint.Part(e,name,paint)
 for tri,x,y,p in ship_regions.surface_samples(part,N,ship_contract.PAINT_UV):occupied[y,x]=True
occ=occupied[:,:1024]|occupied[:,1024:];occ[:8,:]=True;occ[-8:,:]=True;occ[:,:8]=True;occ[:,-8:]=True
charts=[]
for name in targets:
 o=scene.objects[name];m=o.data
 for p in m.polygons:
  xyz=np.array([o.matrix_world@m.vertices[m.loops[i].vertex_index].co for i in p.loop_indices]);_,_,v=np.linalg.svd(xyz-xyz.mean(0),full_matrices=False);q=(xyz-xyz.mean(0))@v[:2].T;q-=q.min(0);charts.append((name,p.index,q,list(p.loop_indices)))
charts.sort(key=lambda c:np.prod(c[2].max(0)),reverse=True);records=[]
for name,face,q,loops in charts:
 dims=np.ceil(q.max(0)*56).astype(int)+16;found=None
 integral=np.pad(occ.astype(np.int32),((1,0),(1,0))).cumsum(0).cumsum(1)
 for rotate in [False,True]:
  w,h=dims[::-1] if rotate else dims
  if w>=1024 or h>=N:continue
  sums=integral[h:,w:]-integral[:-h,w:]-integral[h:,:-w]+integral[:-h,:-w]
  ys,xs=np.where(sums==0)
  if len(xs):found=(int(xs[0]),int(ys[0]),int(w),int(h),rotate);break
 assert found,(name,face,dims)
 x,y,w,h,rotate=found;occ[y:y+h,x:x+w]=True;uv=(q[:,::-1] if rotate else q)*56+np.array([x+8,y+8])
 layer=scene.objects[name].data.uv_layers['PaintUV']
 for loop,co in zip(loops,uv):layer.data[loop].uv=co/N
 records.append({'part':name,'face':face,'rect':[x,y,w,h]})
bpy.context.window.scene=scene
for o in list(e.scene.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.data.scenes.remove(e.scene)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
(s/'armor-uv-layout.json').write_text(json.dumps(records,indent=2));print('ARMOR_UVS_SEPARATED')

