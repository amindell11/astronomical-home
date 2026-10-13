import bpy, bmesh, hashlib, json, sys, shutil
from pathlib import Path
from mathutils import Vector
sys.path.insert(0, 'D:/amind/git/agent-3/art/tools/ship')
import ship_source

root=Path('D:/amind/git/agent-3/results/bulwark/flaps')
source=root/'owner/Bulwark.blend'
expected='afbc3c140f3716d3d50e11e7e3dd04080c750a443a0cba7b1656c555f48dec1d'
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
scene=bpy.context.scene
before=ship_source.fingerprint(scene)
flap_names=['10 Tail fins','10 Tail fins.001']
flap_vertices={n:[list(v.co) for v in bpy.data.objects[n].data.vertices] for n in flap_names}
flap_transforms={n:[list(r) for r in bpy.data.objects[n].matrix_world] for n in flap_names}
housing=bpy.data.objects['05 Tail tips']
hv=[housing.matrix_world@v.co for v in housing.data.vertices]
boom=bpy.data.objects['04 Tail booms']
bv=[boom.matrix_world@v.co for v in boom.data.vertices]
upper=bpy.data.objects[flap_names[0]]
lower=bpy.data.objects[flap_names[1]]
uv=[upper.matrix_world@v.co for v in upper.data.vertices]
lv=[lower.matrix_world@v.co for v in lower.data.vertices]

collection=bpy.data.collections.new('Flap mounts')
scene.collection.children.link(collection)
group=bpy.data.objects.new('Flap mounts',None)
collection.objects.link(group)
group.parent=bpy.data.objects['SymmetryOrigin']
group.empty_display_type='PLAIN_AXES'
group.empty_display_size=.12

def make_mount(name, shell, seat):
    count=len(shell)
    assert len(seat)==count
    center=sum(shell+seat,Vector())/(2*count)
    vertices=[list(p-center) for p in shell+seat]
    faces=[list(range(count)),list(reversed(range(count,2*count)))]
    for i in range(count):
        j=(i+1)%count
        faces.append([i,j,j+count,i+count])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4])
    assert all(e.is_manifold for e in bm.edges)
    bm.to_mesh(mesh);bm.free()
    mesh.update()
    mesh.materials.append(bpy.data.materials['Bulwark Blockout Gray'])
    mesh.uv_layers.new(name='PaintUV')
    obj=bpy.data.objects.new(name,mesh)
    collection.objects.link(obj)
    bpy.data.collections['Ship'].objects.link(obj)
    bpy.data.collections['role.hull'].objects.link(obj)
    obj.parent=group
    obj.location=center
    mirror=obj.modifiers.new('Linked left / right','MIRROR')
    mirror.mirror_object=bpy.data.objects['SymmetryOrigin']
    mirror.use_clip=True
    bevel=obj.modifiers.new('Mount edge chamfer','BEVEL')
    bevel.width=.012;bevel.segments=1
    obj['part_purpose']='Separate tapered mounting shoe between the owner-positioned flap and engine armor.'
    return obj

# The rear lip follows the existing flap; the forward end nests into the housing.
upper_shell=[
    hv[5]+Vector((.03,.01,.015)),
    hv[4]+Vector((-.03,.01,.015)),
    uv[6]+Vector((-.015,0,.012)),
    uv[7]+Vector((-.015,0,.012)),
    uv[10]+Vector((.01,0,.012)),
]
upper_seat=[
    hv[5]+Vector((.03,.01,-.04)),
    hv[4]+Vector((-.03,.01,-.04)),
    hv[3]+Vector((-.025,-.12,-.065)),
    hv[10]+Vector((-.025,.025,-.06)),
    hv[12].lerp(hv[11],.33)+Vector((0,-.05,-.05)),
]
top_mount=make_mount('11 Upper flap mounts',upper_shell,upper_seat)

lower_shell=[
    bv[1]+Vector((.035,.015,-.018)),
    lv[0]+Vector((-.01,0,-.01)),
    lv[1]+Vector((-.01,0,-.01)),
    lv[4]+Vector((.01,0,-.01)),
]
lower_seat=[
    bv[1]+Vector((.035,.015,.055)),
    bv[9]+Vector((-.015,.02,.08)),
    hv[2]+Vector((-.015,-.21,.08)),
    hv[0].lerp(hv[1],.55)+Vector((0,-.21,.055)),
]
bottom_mount=make_mount('12 Lower flap mounts',lower_shell,lower_seat)
bpy.data.collections['role.hull'].objects.link(lower)
bpy.context.view_layer.update()
after=ship_source.fingerprint(scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert changed==[],changed
assert lower.name in bpy.data.collections['role.hull'].objects
for n in flap_names:
    assert flap_vertices[n]==[list(v.co) for v in bpy.data.objects[n].data.vertices]
    assert flap_transforms[n]==[list(r) for r in bpy.data.objects[n].matrix_world]
added=[n for n in after['parts'] if n not in before['parts']]
assert set(added)=={'Flap mounts','11 Upper flap mounts','12 Lower flap mounts'}
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True
for obj in scene.objects:obj.select_set(False)
top_mount.select_set(True);bottom_mount.select_set(True)
bpy.context.view_layer.objects.active=top_mount
scene['review_stage']='Medium detail: fitted roots for owner-positioned upper and lower flaps'
out=root/'refined';out.mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Bulwark.blend'))
shutil.copy2(root/'owner/ship.json',out/'ship.json')
audit={'owner_checkpoint':'9b7cb3ed6f5ed1834a1ebfc0ad14d04b7cc03514','source_sha256':expected,'output_sha256':hashlib.sha256((out/'Bulwark.blend').read_bytes()).hexdigest(),'added_parts':added,'changed_existing_parts':changed,'existing_geometry_unchanged':True,'flap_transforms_and_vertices_unchanged':True,'role_assignment':'10 Tail fins.001 added to role.hull','design':'Two independent mirrored tapered mounting shoes. Free flap tips, body, booms, engine armor, and engines retained exactly.'}
(out/'flap-roots-audit.json').write_text(json.dumps(audit,indent=2))
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
print(json.dumps(audit))
