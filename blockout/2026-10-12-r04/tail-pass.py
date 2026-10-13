import bpy, bmesh, json, hashlib, math
from pathlib import Path
from mathutils import Vector, Matrix
assert bpy.context.mode == 'OBJECT'
assert not bpy.data.is_dirty, 'Owner saved state changed; stop before edits.'
ship = bpy.context.scene
tip = bpy.data.objects['05 Tail tips']
assert len(tip.data.vertices) == 21 and len(tip.data.polygons) == 16
assert '08 Nested engines' not in bpy.data.objects
out = Path('D:/amind/git/agent-3/results/bulwark/tail-blockout-2026-10-12')
out.mkdir(parents=True, exist_ok=True)
def part_hash(o):
    return hashlib.sha256(json.dumps({'v':[list(v.co) for v in o.data.vertices],'f':[list(p.vertices) for p in o.data.polygons],'basis':[list(r) for r in o.matrix_basis]},sort_keys=True).encode()).hexdigest()
before = {o.name:part_hash(o) for o in ship.objects if o.type=='MESH'}
(out/'owner-baseline.json').write_text(json.dumps({'source_sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'parts':before},indent=2))
bm = bmesh.new()
bm.from_mesh(tip.data)
bm.verts.ensure_lookup_table()
rear = [(-.26,-.52),(.94,-.60),(1.08,-.44),(1.08,.55),(.91,.72),(-.13,.72),(-.26,.56)]
for i,(x,z) in enumerate(rear):
    bm.verts[14+i].co = (x,-1.82,z)
for i in range(7,14):
    bm.verts[i].co.z = (bm.verts[i].co.z+.5)/1.08*1.16-.54
end_face = next(f for f in bm.faces if {v.index for v in f.verts} == set(range(14,21)))
outer = list(end_face.verts)
center = Vector((.41,-1.82,.06))
inner = [bm.verts.new(center + (v.co-center)*.70) for v in outer]
deep = [bm.verts.new(v.co+Vector((-.045,1.0,0))) for v in inner]
bmesh.ops.delete(bm, geom=[end_face], context='FACES_ONLY')
for i in range(7):
    j=(i+1)%7
    bm.faces.new((outer[i],outer[j],inner[j],inner[i]))
    bm.faces.new((inner[i],inner[j],deep[j],deep[i]))
bm.faces.new(tuple(reversed(deep)))
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
assert all(e.is_manifold for e in bm.edges)
assert bm.calc_volume(signed=True)>0
bm.to_mesh(tip.data)
bm.free()
tip.data.update()
tip['blockout_intent']='Thick rear armor shell wrapping a recessed engine'
verts=[]; faces=[]; rings=[]
for y,r in [(-.79,.34),(-1.35,.35),(-1.35,.265),(-1.01,.23)]:
    ring=[]
    for i in range(12):
        a=2*math.pi*i/12
        ring.append(len(verts));verts.append((.41+r*math.cos(a),y,.06+r*math.sin(a)))
    rings.append(ring)
for a,b in zip(rings,rings[1:]):
    for i in range(12):
        j=(i+1)%12
        faces.append((a[i],a[j],b[j],b[i]))
faces.append(tuple(reversed(rings[0])))
faces.append(tuple(rings[-1]))
mesh=bpy.data.meshes.new('Nested engine blockout')
mesh.from_pydata(verts,[],faces)
mesh.materials.append(bpy.data.materials['Bulwark Blockout Gray'])
mesh.uv_layers.new(name='PaintUV')
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
assert all(e.is_manifold for e in bm.edges) and bm.calc_volume(signed=True)>0
bm.to_mesh(mesh);bm.free()
engine=bpy.data.objects.new('08 Nested engines',mesh)
bpy.data.collections['role.cores'].objects.link(engine)
bpy.data.collections['Ship'].objects.link(engine)
engine.parent=tip
engine.matrix_parent_inverse=Matrix.Identity(4)
engine.matrix_basis=Matrix.Identity(4)
engine['editing_group']='Tail tips'
mirror=engine.modifiers.new('Linked left / right','MIRROR')
mirror.mirror_object=bpy.data.objects['SymmetryOrigin']
mirror.use_clip=True
engine['blockout_intent']='Simple engine mass recessed behind the armor lip'
bpy.context.view_layer.update()
unchanged={name:part_hash(bpy.data.objects[name])==value for name,value in before.items() if name!=tip.name}
assert all(unchanged.values())
for o in bpy.context.selected_objects:o.select_set(False)
tip.select_set(True);engine.select_set(True)
bpy.context.view_layer.objects.active=tip
bpy.ops.ed.undo_push(message='Bulwark protected engine tail blockout')
result={'unchanged_owner_parts':unchanged,'tip_vertices':len(tip.data.vertices),'engine_vertices':len(mesh.vertices),'live_mirrors':sum(m.type=='MIRROR' for o in ship.objects for m in o.modifiers),'source_saved':False}
(out/'edit-audit.json').write_text(json.dumps(result,indent=2))

