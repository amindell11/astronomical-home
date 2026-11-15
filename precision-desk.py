import bpy, bmesh, math, json
from mathutils import Vector, Matrix
from mathutils.geometry import convex_hull_2d

S=bpy.context.scene
O=bpy.data.objects
C=bpy.data.collections.new('31 | Fitted desk hardware')
S.collection.children.link(C)
palette=list(O['08 | Thick monitor armor frame'].data.materials)
normal=Vector((0,-.15,1)).normalized()
axis_y=Vector((0,1,.15)).normalized()
constant=.72/Vector((0,-.15,1)).length
cam=S.camera

def point(x,y,offset=0):
    t=math.tan(cam.data.angle_x/2)
    ray=cam.matrix_world.to_3x3()@Vector(((2*x/1672-1)*t,(1-2*y/941)*t/(1672/941),-1))
    origin=cam.matrix_world.translation
    return origin+ray*((constant-normal.dot(origin))/normal.dot(ray))+normal*offset

def pose(origin,scales=(1,1,1),up=axis_y,n=normal):
    x=Vector((1,0,0))*scales[0];y=up*scales[1];z=n*scales[2]
    return Matrix(((x.x,y.x,z.x,origin.x),(x.y,y.y,z.y,origin.y),(x.z,y.z,z.z,origin.z),(0,0,0,1)))

def transform(names,matrix):
    for name in names:
        obj=O[name];old=obj.matrix_world.copy()
        if obj.parent is None:
            parent=bpy.data.objects.new('31 | '+name.split('|')[-1].strip()+' placement',None)
            C.objects.link(parent);obj.parent=parent
        obj.matrix_parent_inverse=obj.parent.matrix_world.inverted()@matrix@old@obj.matrix_basis.inverted()
    bpy.context.view_layer.update()

def hide(names):
    for name in names:
        O[name].hide_render=True;O[name].hide_set(True)

def mesh(name,vertices,faces,material=0):
    data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces)
    for mat in palette:data.materials.append(mat)
    for p in data.polygons:p.material_index=material
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(data);bm.free()
    obj=bpy.data.objects.new('31 | '+name,data);C.objects.link(obj)
    return obj

def prism(name,outline,top,bottom,material=0):
    count=len(outline)
    vertices=[p+normal*z for z in (top,bottom) for p in outline]
    faces=[tuple(range(count)),tuple(reversed(range(count,count*2)))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    return mesh(name,vertices,faces,material)

def cut(obj,cutter):
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Fitted opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

def bevel(obj,width=.035):
    mod=obj.modifiers.new('Machined casting edges','BEVEL');mod.width=width;mod.segments=2;mod.material=1
    obj.modifiers.new('Casting face normals','WEIGHTED_NORMAL')

def offset(poly,distance):
    result=[]
    for i,p in enumerate(poly):
        prev=poly[i-1];nxt=poly[(i+1)%len(poly)]
        a=(p-prev).normalized();b=(nxt-p).normalized()
        na=Vector((a.y,-a.x));nb=Vector((b.y,-b.x))
        bisector=(na+nb).normalized()
        result.append(p+bisector*(distance/bisector.dot(na)))
    return result

launch=O['07 | Launch assembly']
old=launch.matrix_world.copy()
launch_pose=pose(point(836,883,-.17),(1.05,1.18,1))
transform(['07 | Launch assembly','19 | Launch legend','19 | Launch glow'],launch_pose@old.inverted())
hide(['14 | Flush Launch gasket','14 | Recessed Launch key boot'])
key=O['08 | Launch broad beveled button']
relative=launch.matrix_world.inverted()@key.matrix_world
key_points=[relative@v.co for v in key.data.vertices]
xy=[Vector((p.x,p.y)) for p in key_points]
hull=[xy[i] for i in convex_hull_2d(xy)]
area=sum(a.x*b.y-b.x*a.y for a,b in zip(hull,hull[1:]+hull[:1]))
if area<0:hull.reverse()
launch_inner=offset(hull,.03)
launch_outer=offset(hull,.095)
launch_cut=offset(hull,.12)

def launch_ring():
    count=len(hull)
    vertices=[launch.matrix_world@Vector((p.x,p.y,z)) for poly,z in [(launch_outer,.205),(launch_inner,.205),(launch_outer,.09),(launch_inner,.09)] for p in poly]
    faces=[]
    for i in range(count):
        j=(i+1)%count
        faces += [(i,j,count+j,count+i),(i,2*count+i,2*count+j,j),(count+i,count+j,3*count+j,3*count+i),(2*count+i,3*count+i,3*count+j,2*count+j)]
    obj=mesh('Launch inset gasket',vertices,faces,4);bevel(obj,.012)
launch_ring()

groups=[
    (['08 | Toggle 1 mounting shoe','08 | Toggle 1 rubber boot','08 | Toggle 1 lever'],'08 | Toggle 1 mounting shoe',.8),
    (['08 | Toggle 2 mounting shoe','08 | Toggle 2 rubber boot','08 | Toggle 2 lever'],'08 | Toggle 2 mounting shoe',.8),
    (['08 | Projection knob foot','08 | Projection knob faceted grip','08 | Projection knob pointer'],'08 | Projection knob foot',.76),
    (['08 | Projection key socket','08 | Projection square key'],'08 | Projection key socket',.8)]
control_records=[]
for roots,base_name,scale in groups:
    base=O[base_name];old=base.matrix_world.copy()
    minimum=min(v.co.z for v in base.data.vertices)
    origin=Vector((old.translation.x,-.72,.72+.15*(-.72)))+normal*(.047-minimum*scale)
    target=pose(origin,(scale,scale,scale))
    transform(roots,target@old.inverted())
    control_records.append({'name':base_name,'normal':list((base.matrix_world.inverted().transposed().to_3x3()@Vector((0,0,1))).normalized()),'base_y':-.72})

projector=O['09 | Projector optical assembly'];old=projector.matrix_world.copy()
up=Vector((0,.95934069,.28225037));pn=Vector((0,-.28225037,.95934069))
position=old.translation.copy();position.x=0
target=pose(position,(.88821,.94470,.89500),up,pn)
transform(['07 | Projector assembly','08 | Projector housing','09 | Projector optical assembly','19 | Projection spill'],target@old.inverted())

hide(['29 | Continuous folded desk shell','29 | Left forearm armor','29 | Right forearm armor',
      '29 | Recessed Launch armor bezel','29 | Launch recess floor','29 | Recessed tube tray armor','29 | Tube tray dark well'])
outline=[point(x,y) for x,y in [(505,799),(562,741),(732,741),(746,724),(931,724),(956,740),(1135,740),(1160,774),(1624,774),(1690,975),(505,975)]]
deck=prism('Continuous desk casting',outline,.045,-.34,0)
launch_outline=[]
for p in launch_cut:
    world=launch.matrix_world@Vector((p.x,p.y,0))
    launch_outline.append(world+normal*(constant-normal.dot(world)))
cut(deck,prism('Launch aperture tool',launch_outline,.50,-.60,3))
tube_pixels=[(1195,847),(1220,823),(1625,823),(1647,848),(1647,899),(1634,912),(1207,912),(1195,900)]
tube_outline=[point(x,y) for x,y in tube_pixels]
cut(deck,prism('Tube aperture tool',tube_outline,.40,-.60,3))
bevel(deck,.035)
floor=prism('Tube recessed floor',tube_outline,-.12,-.30,3);bevel(floor,.018)
under=prism('Desk lower enclosure',outline,-.34,-.57,2);bevel(under,.045)

terminal=O['30 | Weapon terminal frame']
parts=[o for o in bpy.data.collections['30 | Fitted weapon terminal'].objects if o.type=='MESH']
bottom=min(normal.dot(o.matrix_world@v.co) for o in parts for v in o.data.vertices)
camera_position=cam.matrix_world.translation
camera_height=normal.dot(camera_position)
depth_scale=(constant+.08-camera_height)/(bottom-camera_height)
transform([terminal.name],Matrix.Translation(camera_position)@Matrix.Scale(depth_scale,4)@Matrix.Translation(-camera_position))
footprint=[(-2.40,-.025),(2.40,-.025),(2.50,-.125),(2.50,-.40),(2.40,-.50),(-2.40,-.50),(-2.50,-.40),(-2.50,-.125)]
top=[terminal.matrix_world@Vector((x,-2.035,z)) for x,z in footprint]
bottom=[p+normal*(constant+.045-normal.dot(p)) for p in top]
assert min(normal.dot(a-b) for a,b in zip(top,bottom))>.01
faces=[tuple(range(8)),tuple(reversed(range(8,16)))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
foot=mesh('Terminal fitted lower skirt',top+bottom,faces,2);bevel(foot,.005)

assert all(Vector(record['normal']).dot(normal)>.99999 for record in control_records)
report={'controls':control_records,'launch_normal':list((launch.matrix_world.inverted().transposed().to_3x3()@Vector((0,0,1))).normalized()),'desk_normal':list(normal),'launch_gap':.03,'gasket_to_casting_gap':.025,'terminal_depth_scale':depth_scale,'terminal_skirt_contacts_desk':all(abs(normal.dot(p)-constant-.045)<1e-5 for p in bottom)}
print('DESK_ALIGNMENT='+json.dumps(report),flush=True)
