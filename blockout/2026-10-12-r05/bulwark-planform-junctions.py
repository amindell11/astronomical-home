import bpy,bmesh,sys,json,hashlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
check=json.load(open('D:/amind/git/agent-3/results/bulwark/planform-2026-10-12/check/ship_check.json'))
assert ship_source.fingerprint(bpy.context.scene)==check['fingerprint'], 'Owner source changed.'
upper=bpy.data.objects['02 Upper hull']
for v in upper.data.vertices:
 p=upper.matrix_world@v.co
 if abs(p.y)<.0001:
  p.x*=2.30/2.35
  v.co=upper.matrix_world.inverted()@p
upper.data.update()
boom=bpy.data.objects['04 Tail booms']
z_top=(upper.matrix_world@upper.data.vertices[10].co).z
z_side=(upper.matrix_world@upper.data.vertices[9].co).z
z_mid=(upper.matrix_world@upper.data.vertices[16].co).z
for i,x,z in [(7,.55,z_mid),(6,2.12,z_mid),(5,2.30,z_mid),(4,2.30,z_side),(3,2.001,z_top),(2,.67,z_top),(1,.55,z_top-.1104)]:
 boom.data.vertices[i].co=boom.matrix_world.inverted()@Vector((x,0,z))
boom.data.update()
tip=bpy.data.objects['05 Tail tips']
for v in tip.data.vertices:
 if v.index>=28:
  p=tip.matrix_world@v.co
  p.z=2.49+(p.z-2.46)/.8*.61
  v.co=tip.matrix_world.inverted()@p
tip.data.update()
engine=bpy.data.objects['08 Nested engines']
for v in engine.data.vertices:
 p=engine.matrix_world@v.co
 p.x=1.23+(p.x-1.23)*.90
 p.z=2.79+(p.z-2.91)*.90
 v.co=engine.matrix_world.inverted()@p
engine.data.update()
for o in [upper,bpy.data.objects['03 Lower hull']]:
 bm=bmesh.new();bm.from_mesh(o.data)
 bad=[]
 for f in bm.faces:
  if len(f.verts)>4:
   a=f.verts[0].co
   if any(abs((v.co-a).dot(f.normal))>1e-4 for v in f.verts):bad.append(f)
 bmesh.ops.triangulate(bm,faces=bad)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bm.to_mesh(o.data);bm.free();o.data.update()
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result={'sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'junctions_aligned':True}
Path('D:/amind/git/agent-3/results/bulwark/planform-2026-10-12/saved-source.json').write_text(json.dumps(result,indent=2))

