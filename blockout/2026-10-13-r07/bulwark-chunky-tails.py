import bpy,bmesh,json,sys,hashlib,math
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
scratch=Path('C:/Users/amind/.codex/visualizations/2026/10/07/01a11457-037a-7c22-8788-f0f5a5d26229')
baseline=json.loads((scratch/'bulwark-owner-baseline.json').read_text())['fingerprint']
assert not bpy.data.is_dirty
assert ship_source.fingerprint(bpy.context.scene)==baseline
assert hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()=='7dbd11f89f844a065dacfd7222f86e9c7a10a2265996acf3ed0abf1b94dfa597'
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.ed.undo_push(message='Owner Bulwark blockout checkpoint')
before=ship_source.fingerprint(bpy.context.scene)
def put(o,i,p):
 o.data.vertices[i].co=o.matrix_world.inverted()@Vector(p)
def section(inner,outer,y,low,high,bevel=.15,sweep=0):
 pts=[(inner,low),(outer-bevel,low),(outer,low+bevel),(outer,high-bevel),(outer-bevel,high),(inner+.12,high),(inner,high-.10)]
 return [(x,y-sweep*(x-inner)/(outer-inner),z) for x,z in pts]
boom=bpy.data.objects['04 Tail booms']
middle=section(.55,1.92,-1.19,1.035,2.84,.16)
joint=section(.55,1.78,-2.30,1.15,2.85,.15)
for ids,row in [([0,8,9,10,11,12,13],middle),(list(range(14,21)),joint)]:
 for i,p in zip(ids,row):put(boom,i,p)
tip=bpy.data.objects['05 Tail tips']
rows=[joint,section(.55,2.12,-3.05,1.12,2.94,.18,.35),section(.62,2.08,-4.55,1.12,2.98,.18,.55)]
for s,row in enumerate(rows):
 for j,p in enumerate(row):put(tip,s*7+j,p)
inner_back=section(.82,1.88,-4.53,1.36,2.73,.12,.43)
inner_front=section(.82,1.86,-2.65,1.39,2.71,.12,0)
for indices,row in [([22,23,24,25,26,27,21],inner_back),([29,30,31,32,33,34,28],inner_front)]:
 for i,p in zip(indices,row):put(tip,i,p)
engine=bpy.data.objects['08 Nested engines']
for start,y,rx,rz in [(0,-2.55,.52,.58),(12,-4.27,.54,.61),(24,-4.27,.45,.51),(36,-3.75,.38,.43)]:
 for j in range(12):
  a=j*math.tau/12
  put(engine,start+j,(1.35+rx*math.cos(a),y,2.045+rz*math.sin(a)))
changed=[boom,tip,engine]
for o in changed:
 o.data.update()
 bm=bmesh.new();bm.from_mesh(o.data)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 nonplanar=[f for f in bm.faces if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-4 for v in f.verts)]
 if nonplanar:bmesh.ops.triangulate(bm,faces=nonplanar)
 bm.to_mesh(o.data);bm.free();o.data.update()
for name in ['05 Tail tips.001','08 Nested engines.001']:
 o=bpy.data.objects[name];o.hide_set(True);o.hide_render=True
bpy.context.view_layer.update()
after=ship_source.fingerprint(bpy.context.scene)
edited={o.name for o in changed}
preserved=[name for name in before['parts'] if name not in edited]
assert all(after['parts'][name]==before['parts'][name] for name in preserved)
for o in bpy.context.selected_objects:o.select_set(False)
tip.select_set(True);bpy.context.view_layer.objects.active=tip
bpy.ops.ed.undo_push(message='Bulwark chunky level engine housings')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
out=Path('D:/amind/git/agent-3/results/bulwark/engines-2026-10-13');out.mkdir(parents=True,exist_ok=True)
result={'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'edited_parts':sorted(edited),'preserved_parts':preserved,'preservation_verified':True,'hidden_alternates':['05 Tail tips.001','08 Nested engines.001'],'owner_checkpoint':'fbb80869','live_mirrors':sum(m.type=='MIRROR' for o in bpy.context.scene.objects for m in o.modifiers),'fingerprint':after}
(out/'edit-audit.json').write_text(json.dumps(result,indent=2))
