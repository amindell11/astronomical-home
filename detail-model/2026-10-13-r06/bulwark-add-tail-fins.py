import bpy,bmesh,json,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source

root=Path('D:/amind/git/agent-3/results/bulwark/tail-fins')
source=root/'owner/Bulwark.blend'
sha=hashlib.sha256(source.read_bytes()).hexdigest()
assert sha=='72544cec74fb246faec56a6e1af73dd4e004e4355a9ab0fca284404b110fab6e'
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
scene=bpy.context.scene
before=ship_source.fingerprint(scene)
assert '10 Tail fins' not in bpy.data.objects
ref=bpy.data.objects['reference.top']
width,height=ref.data.size
pixel_scale=ref.empty_display_size/max(width,height)
outline_pixels=[(632,264),(685,235),(795,245),(790,263),(687,331),(682,270)]
outline=[]
for px,py in outline_pixels:
    local=Vector(((px+ref.empty_image_offset[0]*width)*pixel_scale,((height-py)+ref.empty_image_offset[1]*height)*pixel_scale,0))
    outline.append(ref.matrix_world@local)
mount=Vector((1.18,-3.30,3.00))
upper=[]
lower=[]
for p in outline:
    aft=-p.y
    z_top=3.00+.33*(aft-3.0)
    z_bottom=z_top-.18
    cross_slope=.06*(p.x-1.2)
    upper.append(Vector((p.x,p.y,z_top+cross_slope)))
    lower.append(Vector((p.x,p.y,z_bottom+cross_slope)))
verts=[list(v-mount) for v in upper+lower]
count=len(outline)
faces=[list(range(count)),list(reversed(range(count,2*count)))]
for i in range(count):
    j=(i+1)%count
    faces.append([i,j,j+count,i+count])
mesh=bpy.data.meshes.new('Tail fins')
mesh.from_pydata(verts,[],faces)
mesh.update()
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4])
assert all(e.is_manifold for e in bm.edges)
bm.to_mesh(mesh);bm.free()
mesh.uv_layers.new(name='PaintUV')
mesh.materials.append(bpy.data.materials['Bulwark Blockout Gray'])
group=bpy.data.objects.new('Tail fins',None)
bpy.data.collections['Ship'].objects.link(group)
group.parent=bpy.data.objects['SymmetryOrigin']
group.empty_display_type='PLAIN_AXES';group.empty_display_size=.15
group.location=(0,-3.3,3)
part=bpy.data.objects.new('10 Tail fins',mesh)
bpy.data.collections['Ship'].objects.link(part)
bpy.data.collections['role.hull'].objects.link(part)
part.parent=group
part.location=(mount.x,0,0)
collection=bpy.data.collections.new('Tail fins')
scene.collection.children.link(collection)
collection.objects.link(group);collection.objects.link(part)
mirror=part.modifiers.new('Linked left / right','MIRROR')
mirror.mirror_object=bpy.data.objects['SymmetryOrigin']
mirror.use_clip=True
bevel=part.modifiers.new('Small fin edge chamfer','BEVEL')
bevel.width=.012;bevel.segments=1
part['part_purpose']='Swept aft fins traced from the packed top reference; edit the right fin to change both.'
bpy.context.view_layer.update()
assert (part.matrix_world@mesh.vertices[0].co-upper[0]).length<1e-5
after=ship_source.fingerprint(scene)
assert all(after['parts'].get(n)==p for n,p in before['parts'].items())
added=[n for n in after['parts'] if n not in before['parts']]
assert set(added)=={'10 Tail fins','Tail fins'},added
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True
for obj in scene.objects:obj.select_set(False)
part.select_set(True);bpy.context.view_layer.objects.active=part
scene['review_stage']='Separate editable tail fins added to owner medium-detail model'
out=root/'refined';out.mkdir(exist_ok=True)
target=out/'Bulwark.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(target))
audit={'source_sha256':sha,'owner_checkpoint':'44c6a0791767235e6140804d3ca74631b9138ea8','output_sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'added_parts':added,'all_existing_parts_unchanged':True,'outline_reference_pixels':outline_pixels,'base_vertices':len(mesh.vertices),'base_faces':len(mesh.polygons),'mirrored':True,'reference':'Packed Bulwark-turnaround.png on reference.top and reference.side'}
(out/'fins-audit.json').write_text(json.dumps(audit,indent=2))
ship=json.loads((root/'owner/ship.json').read_text());ship['source']='Bulwark.blend'
(out/'ship.json').write_text(json.dumps(ship,indent=2))
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print(json.dumps(audit))
