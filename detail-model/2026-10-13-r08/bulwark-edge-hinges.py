import bpy,bmesh,sys,json,hashlib,shutil,math
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
root=Path('D:/amind/git/agent-3/results/bulwark/hinged-flaps')
source=root/'owner/Bulwark.blend'
expected='f93b5fb2cd35fcadffdff999ca590cd3152dd9476aa73e1f882cf61915acb81a'
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
scene=bpy.context.scene
before=ship_source.fingerprint(scene)
names=['10 Tail fins','10 Tail fins.001']
old_world={n:bpy.data.objects[n].matrix_world.copy() for n in names}
old_verts={n:[v.co.copy() for v in bpy.data.objects[n].data.vertices] for n in names}
body_verts=[];body_faces=[]
for name in ['04 Tail booms','05 Tail tips']:
    obj=bpy.data.objects[name]
    start=len(body_verts)
    body_verts.extend(obj.matrix_world@v.co for v in obj.data.vertices)
    body_faces.extend([start+i for i in f.vertices] for f in obj.data.polygons)
body=BVHTree.FromPolygons(body_verts,body_faces)
collection=bpy.data.collections.new('Flap hinges')
scene.collection.children.link(collection)
origin=bpy.data.objects['SymmetryOrigin']

def empty(name,parent):
    obj=bpy.data.objects.new(name,None)
    collection.objects.link(obj)
    obj.parent=parent
    obj.empty_display_type='PLAIN_AXES';obj.empty_display_size=.22
    return obj

def beam(bm,a,b,width,thickness):
    direction=(b-a).normalized()
    wide=Vector((1,0,0));wide=(wide-direction*wide.dot(direction)).normalized()*width/2
    thin=direction.cross(wide).normalized()*thickness/2
    pts=[a-wide-thin,a+wide-thin,a+wide+thin,a-wide+thin,b-wide-thin,b+wide-thin,b+wide+thin,b-wide+thin]
    verts=[bm.verts.new(p) for p in pts]
    for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:bm.faces.new([verts[i] for i in f])

def cylinder(bm,x0,x1,radius):
    m=Matrix.Translation((.5*(x0+x1),0,0))@Matrix.Rotation(math.pi/2,4,'Y')
    bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=12,radius1=radius,radius2=radius,depth=x1-x0,matrix=m)

def make_mesh(name,bm,parent):
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges)
    mesh=bpy.data.meshes.new(name)
    bm.to_mesh(mesh);bm.free();mesh.update()
    mesh.materials.append(bpy.data.materials['Bulwark Blockout Gray'])
    mesh.uv_layers.new(name='PaintUV')
    obj=bpy.data.objects.new(name,mesh)
    for c in [collection,bpy.data.collections['Ship'],bpy.data.collections['role.hull']]:c.objects.link(obj)
    obj.parent=parent
    mirror=obj.modifiers.new('Linked left / right','MIRROR');mirror.mirror_object=origin;mirror.use_clip=True
    bevel=obj.modifiers.new('Hinge edge chamfer','BEVEL');bevel.width=.005;bevel.segments=1
    return obj

details=[]
for label,name,edge,number in [('Upper',names[0],(5,0),11),('Lower',names[1],(4,0),13)]:
    flap=bpy.data.objects[name]
    verts=[flap.matrix_world@v.co for v in flap.data.vertices]
    a=(verts[edge[0]]+verts[edge[0]+6])/2
    b=(verts[edge[1]]+verts[edge[1]+6])/2
    x=(b-a).normalized()
    aft=(verts[2]+verts[3]+verts[8]+verts[9])/4-(a+b)/2
    y=(aft-x*aft.dot(x)).normalized()
    z=x.cross(y).normalized()
    center=(a+b)/2-y*.055
    frame_matrix=Matrix((x,y,z)).transposed().to_4x4()
    frame_matrix.translation=center
    axis=empty(label+' flap axis',origin)
    axis.matrix_world=frame_matrix
    control=empty(label+' flap deployment',axis)
    control.rotation_mode='XYZ'
    control.lock_location=(True,True,True)
    control.lock_rotation=(False,True,True)
    control.lock_scale=(True,True,True)
    control['editing']='Rotate local X to deploy both mirrored flaps; X=0 restores the owner pose.'
    control['zero_pose']='Owner save 2026-10-13, checkpoint 82dcdda7cf250d766c6e05800e697ec7ce488823'
    bpy.context.view_layer.update()
    old_parent_world=flap.parent.matrix_world.copy()
    old_inverse=flap.matrix_parent_inverse.copy()
    flap.parent=control
    flap.matrix_parent_inverse=control.matrix_world.inverted()@old_parent_world@old_inverse
    length=(b-a).length
    low=-length*.43;high=length*.43
    span=(high-low)/5
    gap=.018
    fixed=bmesh.new();moving=bmesh.new()
    anchors=[]
    inv=frame_matrix.inverted()
    for i in range(5):
        start=low+i*span+gap/2
        end=low+(i+1)*span-gap/2
        mid=(start+end)/2
        target=fixed if i in (1,3) else moving
        radius=.115 if i in (1,3) else .103
        cylinder(target,start,end,radius)
        if i in (1,3):
            pin_world=frame_matrix@Vector((mid,0,0))
            hit,normal,idx,distance=body.find_nearest(pin_world)
            assert hit is not None
            seat=inv@(hit-normal*.035)
            beam(fixed,Vector((mid,0,0)),seat,end-start,.095)
            anchors.append({'pin':list(pin_world),'seat':list(hit),'distance':distance})
        else:
            beam(moving,Vector((mid,0,0)),Vector((mid,.22,0)),end-start,.095)
    cylinder(fixed,low-.035,low,.126)
    cylinder(fixed,high,high+.035,.126)
    stationary=make_mesh(f'{number:02d} {label} hinge brackets',fixed,axis)
    leaf=make_mesh(f'{number+1:02d} {label} hinge leaves',moving,control)
    details.append({'flap':name,'control':control.name,'axis':axis.name,'edge_vertex_indices':edge,'axis_center':list(center),'axis_direction':list(x),'length':length,'fixed':stationary.name,'moving':leaf.name,'anchors':anchors})

bpy.context.view_layer.update()
max_error=0
for name in names:
    flap=bpy.data.objects[name]
    assert old_verts[name]==[v.co for v in flap.data.vertices]
    for i,v in enumerate(flap.data.vertices):
        max_error=max(max_error,(flap.matrix_world@v.co-old_world[name]@old_verts[name][i]).length)
assert max_error<1e-5,max_error
after=ship_source.fingerprint(scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert set(changed)==set(names),changed
added=[n for n in after['parts'] if n not in before['parts']]
for obj in scene.objects:obj.select_set(False)
control=bpy.data.objects['Upper flap deployment']
control.select_set(True);bpy.context.view_layer.objects.active=control
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True
scene['review_stage']='Medium-detail: compact edge hinges with editable deployment controls'
out=root/'refined';out.mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Bulwark.blend'))
shutil.copy2(root/'owner/ship.json',out/'ship.json')
audit={'source_sha256':expected,'owner_checkpoint':'82dcdda7cf250d766c6e05800e697ec7ce488823','output_sha256':hashlib.sha256((out/'Bulwark.blend').read_bytes()).hexdigest(),'changed_existing_parts':changed,'change_kind':'Only the two flaps are reparented to deployment controls; mesh data and owner world-space poses are preserved.','max_owner_pose_vertex_error':max_error,'all_other_existing_parts_unchanged':True,'added_parts':added,'hinges':details}
(out/'hinge-audit.json').write_text(json.dumps(audit,indent=2))
assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
print(json.dumps(audit))
