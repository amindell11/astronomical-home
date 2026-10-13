import bpy,bmesh,json
from mathutils import Vector
from pathlib import Path
assert len([o for o in bpy.context.scene.objects if o.type=='MESH'])==7
def edit_world(obj,fn):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT')
    bm=bmesh.from_edit_mesh(obj.data);inv=obj.matrix_world.inverted()
    for v in bm.verts:v.co=inv@Vector(fn(obj.matrix_world@v.co))
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.update_edit_mesh(obj.data,loop_triangles=True,destructive=True);bpy.ops.object.mode_set(mode='OBJECT')
def body_z(y,z):
    t=max(0,min(1,(y-2.78)/2.22))
    return (.92*z+.112)*(1-t)+(.60+(z-.78)/.87*1.30)*t
def body_shape(p):
    x,y,z=p;t=max(0,min(1,(y-2.78)/2.22))
    return (x*(1.06+.24*t),y,body_z(y,z))
for name in ('01 Ram nose','02 Upper hull','03 Lower hull'):
    edit_world(bpy.data.objects[name],body_shape)
edit_world(bpy.data.objects['04 Tail booms'],lambda p:(p.x*1.06,p.y,.92*p.z+.112))
old=[(-3.18,.48,1.44,3.06,1.84),(-3.62,.72,1.8,3.57,2.35),(-5.,1.58,1.72,3.68,3.34)]
new=[(-3.18,.48*1.06,1.44*1.06,3.06*.92+.112,1.84*.92+.112),(-3.62,.72*1.06,1.8*1.06,3.08,2.0),(-5.,1.45,1.89,3.02,2.10)]
def tail_shape(p):
    x,y,z=p;i=min(range(3),key=lambda i:abs(y-old[i][0]))
    oy,oi,oo,ot,ob=old[i];ny,ni,no,nt,nb=new[i]
    assert abs(y-oy)<.0001
    return (ni+(x-oi)/(oo-oi)*(no-ni),ny,nb+(z-ob)/(ot-ob)*(nt-nb))
edit_world(bpy.data.objects['05 Tail tips'],tail_shape)
def cockpit_shape(p):
    x,y,z=p;base=(1.65+(5-y)/(5-2.78)*(2.78-1.65) if y>=2.78 else 2.78+(2.78-y)*.045)+.025
    return (x*.96,y,body_z(y,base-.025)+.028+(z-base)*.70)
edit_world(bpy.data.objects['06 Cockpit dome'],cockpit_shape)
edit_world(bpy.data.objects['07 Side insets'],lambda p:(p.x*1.06,1.2+(p.y-1.2)*.97,1.17+(p.z-1.15)*.97))
# The review uses one neutral material, so only the forms carry the design.
gray=bpy.data.materials.new('Bulwark Blockout Gray')
gray.diffuse_color=(.38,.38,.38,1)
for obj in [o for o in bpy.context.scene.objects if o.type=='MESH']:
    obj.data.materials.clear();obj.data.materials.append(gray)
    for polygon in obj.data.polygons:polygon.material_index=0
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        space=area.spaces.active
        space.shading.type='SOLID';space.shading.color_type='MATERIAL';space.shading.light='STUDIO'
        space.overlay.show_floor=False;space.overlay.show_axis_x=False;space.overlay.show_axis_y=False
        space.overlay.show_extras=False
        space.region_3d.view_location=(0,0,1.5)
        space.region_3d.view_distance=14
        space.region_3d.view_rotation=(Vector((9,13,10))-Vector((0,0,1.5))).to_track_quat('Z','Y')
        space.region_3d.view_perspective='ORTHO'
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.update()
result={'stage':'blockout','mesh_count':7,'materials':[gray.name],'live_mirrors':sum(any(m.type=='MIRROR' for m in o.modifiers) for o in bpy.context.scene.objects if o.type=='MESH'),'saved':False}

