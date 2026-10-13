import bpy,bmesh,json,sys,hashlib,math
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
base=Path('D:/amind/git/agent-3/results/bulwark/engines-2026-10-13')
baseline=json.loads((base/'edit-audit.json').read_text())['fingerprint']
assert ship_source.fingerprint(bpy.context.scene)==baseline
before=ship_source.fingerprint(bpy.context.scene)
shroud_xy={}
for name,ids in [('04 Tail booms',[0]+list(range(8,21))),('05 Tail tips',list(range(35))),('08 Nested engines',list(range(48)))]:
 o=bpy.data.objects[name]
 shroud_xy[name]=[(o.matrix_world@v.co).copy() for v in o.data.vertices]
 for i in ids:
  p=o.matrix_world@o.data.vertices[i].co
  p.z=2.045+(p.z-2.045)*.65
  if name=='08 Nested engines' and i<12:p.x=1.35+.42*math.cos(i*math.tau/12)
  o.data.vertices[i].co=o.matrix_world.inverted()@p
 o.data.update()
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
bpy.context.view_layer.update()
after=ship_source.fingerprint(bpy.context.scene)
edited=set(shroud_xy)
assert all(after['parts'][n]==before['parts'][n] for n in before['parts'] if n not in edited)
max_xy=max(abs((bpy.data.objects[n].matrix_world@v.co)[axis]-shroud_xy[n][i][axis]) for n in ['04 Tail booms','05 Tail tips'] for i,v in enumerate(bpy.data.objects[n].data.vertices) for axis in [0,1])
assert max_xy<1e-5
bpy.ops.ed.undo_push(message='Bulwark reduce engine housing depth only')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
out=base/'slimmer';out.mkdir(parents=True,exist_ok=True)
result={'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'owner_checkpoint':'fbb80869','superseded_depth_checkpoint':'d54cdd6c','edited_parts':sorted(edited),'preservation_verified':True,'top_outline_max_xy_delta':max_xy,'tail_height_ratio':.65,'hidden_alternates':['05 Tail tips.001','08 Nested engines.001'],'fingerprint':after}
(out/'edit-audit.json').write_text(json.dumps(result,indent=2))
