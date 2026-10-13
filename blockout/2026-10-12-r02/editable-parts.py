import bpy, bmesh, math, json
from mathutils import Vector, Matrix
from pathlib import Path

assert bpy.data.filepath.replace('\\','/').lower().endswith('/agent-3/art/ships/bulwark/bulwark.blend')
assert bpy.context.mode == 'OBJECT'
root = bpy.data.objects['SymmetryOrigin']
hull = bpy.data.objects['Bulwark.Hull']
tail = bpy.data.objects['Bulwark.Tail']
canopy = bpy.data.objects['Bulwark.Canopy']
inset = bpy.data.objects['Bulwark.SideInset']
assert len(hull.data.vertices)==30 and len(tail.data.vertices)==35
scratch=Path('D:/amind/git/agent-3/results/bulwark/editable-2026-10-12')
scratch.mkdir(parents=True,exist_ok=True)
baseline={o.name:{'vertices':[list(v.co) for v in o.data.vertices],'faces':[list(p.vertices) for p in o.data.polygons]} for o in (hull,tail,canopy,inset)}
(scratch/'before-geometry.json').write_text(json.dumps(baseline,indent=2))

def duplicate_part(source,name):
    obj=source.copy()
    obj.data=source.data.copy()
    obj.name=name
    obj.data.name=name
    for col in source.users_collection:
        col.objects.link(obj)
    return obj

def trim(obj,axis,value,keep_positive,cap_material):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT')
    bm=bmesh.from_edit_mesh(obj.data)
    plane=Vector((0,0,0)); plane[axis]=value
    normal=Vector((0,0,0)); normal[axis]=1
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
        dist=0.000001,plane_co=plane,plane_no=normal,
        clear_outer=not keep_positive,clear_inner=keep_positive)
    cut_edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co[axis]-value)<0.00001 for v in e.verts)]
    cut_verts=list({v for e in cut_edges for v in e.verts})
    assert len(cut_verts)>=3,(obj.name,len(cut_verts))
    axes=[a for a in range(3) if a!=axis]
    center=sum((v.co for v in cut_verts),Vector())/len(cut_verts)
    cut_verts.sort(key=lambda v:math.atan2(v.co[axes[1]]-center[axes[1]],v.co[axes[0]]-center[axes[0]]))
    cap=bm.faces.new(cut_verts)
    cap.material_index=cap_material
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.normal_update()
    desired=-1 if keep_positive else 1
    if cap.normal[axis]*desired<0:
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bmesh.update_edit_mesh(obj.data,loop_triangles=True,destructive=True)
    bpy.ops.object.mode_set(mode='OBJECT')

nose=duplicate_part(hull,'01 Ram nose')
upper=duplicate_part(hull,'02 Upper hull')
lower=duplicate_part(hull,'03 Lower hull')
trim(nose,1,2.78,True,1)
for obj in (upper,lower):
    trim(obj,1,2.78,False,0)
trim(upper,2,1.5,True,0)
trim(lower,2,1.5,False,2)
booms=duplicate_part(tail,'04 Tail booms')
tips=duplicate_part(tail,'05 Tail tips')
trim(booms,1,-3.18,True,0)
trim(tips,1,-3.18,False,1)

# A ring loft preserves the approved footprint while rounding the forward volume.
outline=[(3.90,0,0),(3.79,.42,.40),(3.50,.68,.75),(3.03,.75,.88),
         (2.63,.70,.75),(1.80,.53,.46),(1.02,.30,.22),(.38,0,0)]
vertices=[]; rings=[]; faces=[]
for y,width,rise in outline:
    base=(1.65+(5-y)/(5-2.78)*(2.78-1.65) if y>=2.78 else 2.78+(2.78-y)*.045)+.025
    ring=[]
    for j in range(1 if width==0 else 7):
        angle=j*math.pi/12
        ring.append(len(vertices))
        vertices.append((0 if width==0 else width*math.cos(angle),y,base+rise*math.sin(angle)))
    rings.append(ring)
for a,b in zip(rings,rings[1:]):
    if len(a)==1:
        faces.extend((a[0],b[j],b[j+1]) for j in range(6))
    elif len(b)==1:
        faces.extend((a[j],b[0],a[j+1]) for j in range(6))
    else:
        faces.extend((a[j],b[j],b[j+1],a[j+1]) for j in range(6))
new_mesh=bpy.data.meshes.new('06 Cockpit dome')
new_mesh.from_pydata(vertices,[],faces)
new_mesh.materials.append(canopy.data.materials[0])
new_mesh.uv_layers.new(name='PaintUV')
new_mesh.update()
canopy.data=new_mesh
canopy.name='06 Cockpit dome'
for poly in canopy.data.polygons: poly.use_smooth=True
inset.name='07 Side insets'
inset.data.name=inset.name
for obj in (hull,tail):
    bpy.data.objects.remove(obj,do_unlink=True)

ship=bpy.data.collections.new('Ship')
bpy.context.scene.collection.children.link(ship)
roles=bpy.data.collections.new('Export roles')
bpy.context.scene.collection.children.link(roles)
for col in list(bpy.context.scene.collection.children):
    if col.name.startswith('role.'):
        roles.children.link(col)
        bpy.context.scene.collection.children.unlink(col)
for col in list(root.users_collection):
    col.objects.unlink(root)
ship.objects.link(root)
root.empty_display_size=.25

def parent_empty(name,parent,position):
    group=bpy.data.objects.new(name,None)
    ship.objects.link(group)
    group.parent=parent
    group.matrix_world=Matrix.Translation(Vector(position))
    group.empty_display_type='PLAIN_AXES'
    group.empty_display_size=.25
    return group

body=parent_empty('Body',root,(0,1.1,1.5))
tail_group=parent_empty('Tail',root,(0,-.62,2))
groups=[
    ('Ram nose',nose,body,(0,2.78,1.5)),
    ('Upper hull',upper,body,(0,1.1,1.5)),
    ('Lower hull',lower,body,(0,1.1,1.5)),
    ('Tail booms',booms,tail_group,(1.1,-.62,2)),
    ('Tail tips',tips,tail_group,(.96,-3.18,2.5)),
    ('Cockpit',canopy,root,(0,3.03,2.7)),
    ('Side insets',inset,root,(2.65,1.2,1.15))]
for name,obj,parent,pivot in groups:
    group=parent_empty(name,parent,pivot)
    old_world=obj.matrix_world.copy()
    world_vertices=[old_world@v.co for v in obj.data.vertices]
    obj.parent=group
    obj.matrix_parent_inverse=Matrix.Identity(4)
    obj.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    inv=obj.matrix_world.inverted()
    for v,world in zip(obj.data.vertices,world_vertices): v.co=inv@world
    ship.objects.link(obj)
    obj.data.update()
    for mod in obj.modifiers:
        if mod.type=='MIRROR':
            mod.name='Linked left / right'
            mod.mirror_object=root
            mod.use_clip=True
            mod.use_mirror_merge=True
    obj['editing_group']=name
bpy.context.view_layer.layer_collection.children['Export roles'].exclude=True
bpy.ops.object.select_all(action='DESELECT')
canopy.select_set(True)
bpy.context.view_layer.objects.active=canopy
bpy.context.view_layer.update()
result={'groups':[name for name,_,_,_ in groups],
        'parts':[{'name':obj.name,'vertices':len(obj.data.vertices),'faces':len(obj.data.polygons),'parent':obj.parent.name,'scale':list(obj.scale)} for _,obj,_,_ in groups]}

# Follow-up operations for the rounded surface.
obj=bpy.data.objects['06 Cockpit dome']
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
bpy.ops.object.mode_set(mode='EDIT')
bm=bmesh.from_edit_mesh(obj.data)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.normal_update()
if sum(f.normal.z*f.calc_area() for f in bm.faces)<0:
    bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
bmesh.update_edit_mesh(obj.data)
bpy.ops.object.mode_set(mode='OBJECT')
sub=obj.modifiers.new('Dome smoothing (editable cage)','SUBSURF')
sub.levels=2
sub.render_levels=2

