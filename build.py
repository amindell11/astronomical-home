import bpy, math, json
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view
from pathlib import Path

P=Path('C:/Users/amind/.codex/visualizations/2026/11/15/01a1dc89-c32a-7c82-ba76-5ae7751e1d5f/hangar/reference-pass')
S=bpy.context.scene
O=bpy.data.objects
CAM=S.camera
W,H=1672,941
bpy.context.view_layer.update()
C=bpy.data.collections.new('29 | Integrated weapon console')
S.collection.children.link(C)
palette=list(O['08 | Thick monitor armor frame'].data.materials)

def descendants(o):
    return [o]+[q for ch in o.children for q in descendants(ch)]

def hide(names):
    for n in names:
        for o in descendants(O[n]):
            o.hide_render=True
            o.hide_set(True)

def pix(p):
    p=world_to_camera_view(S,CAM,p)
    return Vector((W*p.x,H*(1-p.y)))

def point(px,py,normal,constant):
    aspect=W/H
    t=math.tan(CAM.data.angle_x/2)
    ray=CAM.matrix_world.to_3x3()@Vector(((2*px/W-1)*t,(1-2*py/H)*t/aspect,-1))
    origin=CAM.matrix_world.translation
    return origin+ray*((constant-normal.dot(origin))/normal.dot(ray))

face_normal=Vector((0,-1,.22)).normalized()
face_constant=Vector((0,-1,.22)).dot(Vector((0,-.6,3))) / Vector((0,-1,.22)).length
desk_normal=Vector((0,-.15,1)).normalized()
desk_constant=.72/Vector((0,-.15,1)).length

def facepoint(x,y,z=0):
    return point(x,y,face_normal,face_constant)+face_normal*z

def deskpoint(x,y,z=0):
    return point(x,y,desk_normal,desk_constant)+desk_normal*z

def interp(a,knots):
    for (x,y),(xx,yy) in zip(knots,knots[1:]):
        if a<=xx:
            return y+(a-x)/(xx-x)*(yy-y)
    x,y=knots[-2];xx,yy=knots[-1]
    return y+(a-x)/(xx-x)*(yy-y)

left=O['07 | Monitor assembly']
left_inv=left.matrix_world.inverted()

def mapped_frame(v):
    y=interp(v.y,[(-.245,795),(.172,758),(1.152,626),(1.372,602),(5.332,480),(5.85,443)])
    u=(v.x+2.895865)/6.294871
    h=(795-y)/(795-443)
    x=(1155+18*h)+(485-42*h)*u
    return facepoint(x,y,(v.z-.5781627)*.85)

def copied_mesh(source,name,mapping):
    src=O[source]
    if src.type=='MESH':
        obj=src.copy();obj.data=src.data.copy();obj.name='29 | '+name
    else:
        data=bpy.data.meshes.new_from_object(src.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        obj=bpy.data.objects.new('29 | '+name,data)
    obj.parent=None;obj.matrix_world=Matrix.Identity(4)
    C.objects.link(obj)
    for v in obj.data.vertices:
        v.co=mapping(left_inv@src.matrix_world@v.co)
    obj.hide_render=False;obj.hide_set(False)
    return obj

for source,name in [
    ('08 | Thick monitor armor frame','Weapon terminal armor casting'),
    ('07 | Continuous inner gasket','Weapon terminal recessed gasket'),
    ('07 | Monitor tapered rear enclosure','Weapon terminal rear enclosure'),
    ('08 | Raised brow cap','Weapon terminal brow'),
    ('09 | Left shoulder armor','Weapon terminal left shoulder'),
    ('09 | Right shoulder armor','Weapon terminal right shoulder'),
    ('09 | Slim screen separation casting','Weapon terminal divider'),
    ('07 | Main rectangular display','Primary blank glass'),
    ('07 | Detail rectangular display','Ordnance blank glass')]:
    obj=copied_mesh(source,name,mapped_frame)
    if 'blank glass' in name:
        obj.data.materials.clear();obj.data.materials.append(bpy.data.materials['21 | Black elastomer'])
        for poly in obj.data.polygons:poly.material_index=0

def control(source,name,center,reference,scale):
    src=O[reference]
    pts=[left_inv@src.matrix_world@v.co for v in src.data.vertices]
    ref=(Vector(tuple(min(p[i] for p in pts) for i in range(3)))+Vector(tuple(max(p[i] for p in pts) for i in range(3))))*.5
    return copied_mesh(source,name,lambda v:facepoint(center[0]+(v.x-ref.x)*scale[0],center[1]-(v.y-ref.y)*scale[1],(v.z-.5781627)*.85))

for i,(direction,y) in enumerate([('Previous',522),('Next',565),('Previous',670),('Next',713)]):
    for suffix in ['deep socket','chunky keycap','inlaid chevron']:
        control(f'08 | {direction} {suffix}',f'Weapon key {i+1} {suffix}',(1225,y),f'08 | {direction} chunky keycap',(72,75))

for i,(x,y) in enumerate([(1197,478),(1590,466),(1178,775),(1615,765)]):
    for source,suffix in [('08 | Captive bolt socket','socket'),('08 | Large round captive bolt','head')]:
        control(source,f'Weapon fastener {i+1} {suffix}',(x,y),'08 | Large round captive bolt',(43,43))

def projected_bounds(roots):
    pts=[]
    for root in roots:
        for o in descendants(O[root]):
            if o.hide_render or o.type not in {'MESH','CURVE','FONT'}:continue
            evaluated=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh=evaluated.to_mesh()
            for p in mesh.vertices:
                pts.append(pix(o.matrix_world@p.co))
            evaluated.to_mesh_clear()
    return min(p.x for p in pts),min(p.y for p in pts),max(p.x for p in pts),max(p.y for p in pts)

def screen_transform(bounds,target):
    a,b,c,d=bounds;aa,bb,cc,dd=target
    sx=(cc-aa)/(c-a);sy=(dd-bb)/(d-b)
    ox=aa-a*sx;oy=bb-b*sy
    tx=math.tan(CAM.data.angle_x/2)
    ty=tx/(W/H)
    dx=tx*(sx-1+2*ox/W)
    dy=ty*(1-sy-2*oy/H)
    m=Matrix(((sx,0,-dx,0),(0,sy,-dy,0),(0,0,1,0),(0,0,0,1)))
    return CAM.matrix_world@m@CAM.matrix_world.inverted()

def transform_roots(roots,mat):
    for n in roots:
        o=O[n]
        old=o.matrix_world.copy()
        if o.parent is None:
            parent=bpy.data.objects.new('29 | '+n.split('|',1)[-1].strip()+' placement',None)
            C.objects.link(parent)
            o.parent=parent
        o.matrix_parent_inverse=o.parent.matrix_world.inverted()@mat@old@o.matrix_basis.inverted()
    bpy.context.view_layer.update()

def fit(roots,target,bounds=None):
    bounds=bounds or projected_bounds(roots)
    transform_roots(roots,screen_transform(bounds,target))

hide(['13 | Cooling deck folded beneath monitor','14 | Projector formed armor',
      '14 | Projector recessed folded sidewalls','15 | Projector rear casing closure',
      '14 | Forearm fold into Launch and controls','14 | Launch casing folded returns',
      '14 | Projector to control folded shoulder','14 | Right shoulder into Launch edge',
      '18 | Projector control and Launch seat','18 | Lower tray mounting return',
      '14 | Dark support beneath apron fold','15 | Projector to control rear end cap'])

projector_bounds=projected_bounds(['09 | Projector optical assembly'])
fit(['07 | Projector assembly','08 | Projector housing','09 | Projector optical assembly','19 | Projection spill'],(747,729,925,800),projector_bounds)
launch_bounds=projected_bounds(['08 | Launch broad beveled button'])
fit(['07 | Launch assembly','19 | Launch legend','19 | Launch glow'],(708,819,959,911),launch_bounds)
hide(['14 | Thin fitted control face','15 | Control pod rear casing closure'])
control_groups=[
    (['08 | Toggle 1 mounting shoe','08 | Toggle 1 rubber boot','08 | Toggle 1 lever'],(957,785,997,829)),
    (['08 | Toggle 2 mounting shoe','08 | Toggle 2 rubber boot','08 | Toggle 2 lever'],(1004,785,1044,829)),
    (['08 | Projection knob foot','08 | Projection knob faceted grip','08 | Projection knob pointer'],(1053,783,1116,835)),
    (['08 | Projection key socket','08 | Projection square key'],(1125,797,1163,828))]
for roots,bounds in control_groups:
    fit(roots,bounds)
    points=[]
    for name in roots:
        for obj in descendants(O[name]):
            if obj.type=='MESH' and not obj.hide_render:
                points.extend(obj.matrix_world@v.co for v in obj.data.vertices)
    camera_position=CAM.matrix_world.translation
    scale=min((desk_constant+.055-desk_normal.dot(camera_position))/desk_normal.dot(p-camera_position) for p in points)
    transform_roots(roots,Matrix.Translation(camera_position)@Matrix.Scale(scale,4)@Matrix.Translation(-camera_position))

tube_roots=['08 | Complete hose loom','10 | Complete tube assembly','14 | Recessed front hose socket',
    '14 | Front entry hose 0','14 | Front entry hose 1','15 | Seated tube gland 0',
    '15 | Seated tube gland 1','28 | Tube entry assembly']
fit(tube_roots,(1210,827,1627,905))

def duplicate_tree(src,parent=None):
    obj=src.copy()
    if src.data:obj.data=src.data.copy()
    obj.name='29 | Right '+src.name.split('|',1)[-1].strip()
    C.objects.link(obj)
    obj.parent=parent
    if parent is None:obj.matrix_world=src.matrix_world.copy()
    else:obj.matrix_local=src.matrix_local.copy()
    for ch in src.children:duplicate_tree(ch,obj)
    return obj

cooling=O['07 | Angled cooling bank']
right_cooling=duplicate_tree(cooling)
bpy.context.view_layer.update()
fit(['07 | Angled cooling bank'],(550,699,745,802))
fit([right_cooling.name],(936,699,1162,773))

def mesh(name,verts,faces,material=0,bevel=.025):
    data=bpy.data.meshes.new(name)
    data.from_pydata(verts,[],faces);data.materials.clear()
    for mat in palette:data.materials.append(mat)
    for poly in data.polygons:poly.material_index=material
    obj=bpy.data.objects.new('29 | '+name,data);C.objects.link(obj)
    if bevel:
        mod=obj.modifiers.new('Armor edge','BEVEL');mod.width=bevel;mod.segments=2;mod.material=1
        obj.modifiers.new('Broad face normals','WEIGHTED_NORMAL')
    return obj

def plate(name,outline,material=0,depth=.18,offset=0):
    n=len(outline)
    verts=[deskpoint(x,y,z) for z in [offset,offset-depth] for x,y in outline]
    faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,verts,faces,material)

def ring(name,outer,inner,material=0,offset=.05,depth=.18):
    n=len(outer)
    verts=[deskpoint(x,y,z) for ring,z in [(outer,offset),(inner,offset),(outer,offset-depth),(inner,offset-depth)] for x,y in ring]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,n+i,n+j,j),(i,j,2*n+j,2*n+i),(n+i,3*n+i,3*n+j,n+j),(2*n+i,2*n+j,3*n+j,3*n+i)]
    return mesh(name,verts,faces,material)

def cut_rectangle(x0,y0,x1,y1,cut):
    return [(x0+cut,y0),(x1-cut,y0),(x1,y0+cut),(x1,y1-cut),(x1-cut,y1),(x0+cut,y1),(x0,y1-cut),(x0,y0+cut)]

for name,bounds in [('Primary',(1251,476,1606,612)),('Ordnance',(1245,622,1622,769))]:
    x0,y0,x1,y1=bounds
    outer=cut_rectangle(x0,y0,x1,y1,12)
    inner=cut_rectangle(x0+12,y0+11,x1-12,y1-11,7)
    verts=[facepoint(x,y,z) for ring,z in [(outer,.045),(inner,-.135),(outer,-.22),(inner,-.22)] for x,y in ring]
    faces=[]
    for i in range(8):
        j=(i+1)%8
        faces += [(i,8+i,8+j,j),(i,j,16+j,16+i),(8+i,24+i,24+j,8+j),(16+i,16+j,24+j,24+i)]
    obj=mesh(name+' recessed screen rim',verts,faces,2,.015)
    for i,poly in enumerate(obj.data.polygons):
        if i%4==2:poly.material_index=3

plate('Continuous folded desk shell',[(510,785),(568,741),(732,741),(746,724),(931,724),(956,740),(1135,740),(1160,774),(1624,774),(1674,941),(520,941)],2,.5)
plate('Left forearm armor',[(541,809),(730,809),(697,838),(664,915),(551,923),(525,905)],0,.18,.025)
plate('Right forearm armor',[(944,805),(1165,782),(1630,782),(1643,813),(1218,813),(1170,858),(1012,918),(981,918),(952,851)],0,.18,.025)
plate('Projector mounting saddle',[(712,731),(738,718),(928,718),(952,739),(949,803),(925,815),(741,815),(713,796)],0,.18,.08)
launch_outer=[(677,850),(707,810),(953,810),(983,850),(983,916),(954,939),(706,939),(677,916)]
launch_inner=[(693,852),(715,821),(946,821),(968,852),(968,909),(950,924),(711,924),(693,909)]
ring('Recessed Launch armor bezel',launch_outer,launch_inner,0,.08,.22)
plate('Launch recess floor',launch_inner,3,.20,-.02)
tube_outer=[(1180,844),(1212,809),(1632,809),(1663,844),(1663,906),(1642,927),(1201,927),(1180,906)]
tube_inner=[(1195,847),(1220,823),(1625,823),(1647,848),(1647,899),(1634,912),(1207,912),(1195,900)]
ring('Recessed tube tray armor',tube_outer,tube_inner,0,.07,.20)
plate('Tube tray dark well',tube_inner,3,.16,-.035)

holo_roots=['07 | Hologram assembly']+[o.name for o in S.objects if o.name.startswith('19 | Vanguard')]
center=Vector((2.35,-.1369667,6.780117))
camera_rotation=CAM.matrix_world.to_quaternion().to_matrix().to_4x4()
rotation=camera_rotation@Matrix.Rotation(math.radians(-112),4,'Z')@Matrix.Rotation(math.radians(25),4,'Y')@Matrix.Rotation(math.radians(35),4,'X')@camera_rotation.inverted()
transform_roots(holo_roots,Matrix.Translation(center)@rotation@Matrix.Translation(-center))
fit(holo_roots,(680,270,1080,654))
frustum=O['11 | Neutral projector frustum']
transform_roots([frustum.name],Matrix.Translation(Vector((-2.35,0,-.35))))
if frustum.type=='MESH':
    inv=frustum.matrix_world.inverted()
    points=[frustum.matrix_world@v.co for v in frustum.data.vertices]
    low=min(p.z for p in points);high=max(p.z for p in points)
    for v,p in zip(frustum.data.vertices,points):
        p.x*=1-.4*(p.z-low)/(high-low)
        v.co=inv@p

S.render.resolution_x=W;S.render.resolution_y=H;S.render.resolution_percentage=100
S.render.filepath='//console-camera.png'
bpy.context.view_layer.update()
assert all(O[n].hide_render is False for n in ['07 | Main rectangular display','07 | Detail rectangular display'])
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath='D:/amind/git/agent-5/art/environments/hangar-console/console.blend')
S.render.resolution_percentage=75;S.cycles.samples=12
S.render.filepath=str(P/'reference-v5.png')
bpy.ops.render.render(write_still=True)
print('REFERENCE_MODEL=ready')
