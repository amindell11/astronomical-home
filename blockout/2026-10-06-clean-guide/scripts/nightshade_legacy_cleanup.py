from pathlib import Path
import bpy,bmesh,json,sys
import numpy as np
root=Path('D:/amind/git/agent-1')
source=root/'art/ships/nightshade/Nightshade.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
ob=bpy.data.objects['Legacy body wings and upper lower fins']
before=[v.co[:] for v in ob.data.vertices]
bad=[]
for p in ob.data.polygons:
    if len(p.vertices)<=4:continue
    co=np.array([ob.data.vertices[i].co[:] for i in p.vertices])
    if np.abs((co-co.mean(axis=0))@np.array(p.normal)).max()>1e-4:bad.append(p.index)
bm=bmesh.new();bm.from_mesh(ob.data);bm.faces.ensure_lookup_table()
bmesh.ops.triangulate(bm,faces=[bm.faces[i] for i in bad],quad_method='FIXED',ngon_method='EAR_CLIP')
bm.to_mesh(ob.data);bm.free()
assert before==[v.co[:] for v in ob.data.vertices]
fork=bpy.data.objects['Sculpted compact tail pair']
for v in fork.data.vertices:
    t=max(0,min(1,(-v.co.y-.60)/.415))
    v.co.x-=.12*t*t*(3-2*t)
for e in fork.data.edges:
    a,b=e.vertices
    if a%10==b%10 and a%10 in (0,2,4,6,8):e.use_edge_sharp=True
# Fit the image planes to the native-size ship; source geometry is not stretched.
top=bpy.data.objects['reference.top']
top.empty_display_size=1.90*1531/850
top.location=(0,-.066,-.30)
side=bpy.data.objects['reference.side']
side.empty_display_size=1.90
side.location=(1.05,-.066,.018)
turn=bpy.data.objects['reference.turnaround']
turn.hide_set(True)
out=root/'results/nightshade-legacy-base/round-02'
out.mkdir(parents=True,exist_ok=True)
(out/'cleanup.json').write_text(json.dumps({'nonplanar_imported_faces_triangulated':len(bad),'legacy_coordinates_unchanged':True,'fork_tip_inward_adjustment':.12,'references':'Image planes resized for native mesh; geometry unscaled'},indent=2))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
