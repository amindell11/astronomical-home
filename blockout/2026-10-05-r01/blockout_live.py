import bpy, bmesh, math
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

scene=bpy.context.scene
if scene.name!='Nightshade' or any(o.type=='MESH' for o in scene.objects):
    raise RuntimeError('Expected the untouched Nightshade scaffold.')
origin=bpy.data.objects['SymmetryOrigin']
def material(name,color):
    mat=bpy.data.materials.new(name)
    mat.diffuse_color=(*color,1)
    return mat
slate=material('Hull slate',(.22,.24,.32))
glass=material('Canopy blue violet',(.30,.23,.78))
magenta=material('Tip magenta',(.84,.06,.73))

def part(name,vertices,faces,role='hull',materials=(slate,),smooth=False,solidify=None):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.uv_layers.new(name='PaintUV')
    obj=bpy.data.objects.new(name,mesh)
    bpy.data.collections['role.'+role].objects.link(obj)
    obj.parent=origin
    for mat in materials:
        mesh.materials.append(mat)
    for face in mesh.polygons:
        face.use_smooth=smooth
    mirror=obj.modifiers.new('Live symmetry','MIRROR')
    mirror.mirror_object=origin
    mirror.use_clip=True
    mirror.use_mirror_merge=True
    if solidify is not None:
        shell=obj.modifiers.new('Shell thickness','SOLIDIFY')
        shell.thickness=solidify
        shell.offset=-1
    return obj

def loft(name,rings,role='hull',mat=slate,smooth=False):
    vertices=[p for ring in rings for p in ring]
    count=len(rings[0])
    faces=[]
    for row in range(len(rings)-1):
        for col in range(count-1):
            a=row*count+col
            faces.append((a,a+1,a+count+1,a+count))
    for row in (0,len(rings)-1):
        ring=rings[row]
        index=len(vertices)
        vertices.append((0,ring[0][1],sum(p[2] for p in ring)/count))
        for col in range(count-1):
            faces.append((index,row*count+col,row*count+col+1))
    return part(name,vertices,faces,role,(mat,),smooth)

sections=[(3.55,.15,.22),(3.35,.58,.38),(3.0,.92,.44),(2.50,.99,.44),(1.85,.87,.38),(1.1,.74,.34),(.25,.62,.30),(-.60,.48,.24),(-1.12,.28,.17),(-1.40,.065,.07)]
body_rings=[]
for y,width,height in sections:
    body_rings.append([(width*math.cos(angle),y,-.05+height*math.sin(angle)) for angle in [math.radians(-90+i*22.5) for i in range(9)]])
loft('Central fuselage',body_rings)

canopy_sections=[(1.65,.13,.10),(1.90,.52,.34),(2.28,.73,.56),(2.70,.78,.65),(3.06,.69,.55),(3.38,.43,.31),(3.57,.15,.09)]
for direction,label in [(1,'Upper canopy'),(-1,'Lower canopy')]:
    rings=[]
    for y,width,height in canopy_sections:
        rings.append([(width*math.sin(math.radians(i*15)),y,direction*(.27+height*math.cos(math.radians(i*15)))) for i in range(7)])
    vertices=[v for ring in rings for v in ring]
    faces=[]
    for row in range(len(rings)-1):
        for col in range(6):
            a=row*7+col
            faces.append((a,a+1,a+8,a+7))
    part(label,vertices,faces,'canopy',(glass,),True,.045)

def blade(name,outline,zfunc,thickness,accent=False):
    vertices=[(x,y,zfunc(x,y)+side*thickness/2) for side in (1,-1) for x,y in outline]
    count=len(outline)
    polygon=[Vector((x,y,0)) for x,y in outline]
    triangles=tessellate_polygon([polygon])
    faces=[]
    for triangle in triangles:
        ids=list(triangle)
        faces.append(tuple(ids))
        faces.append(tuple(i+count for i in reversed(ids)))
    for i in range(count):
        j=(i+1)%count
        faces.append((i,j,j+count,i+count))
    obj=part(name,vertices,faces,materials=(slate,magenta) if accent else (slate,))
    if accent:
        for face in obj.data.polygons:
            if face.center.x>4.55:
                face.material_index=1
    return obj

wing=[(1.47,4.05),(1.60,3.62),(2.15,3.27),(2.65,2.63),(3.07,1.74),(3.36,.72),(3.48,.20),(4.04,-1.14),(4.66,-2.51),(5.00,-2.76),(4.98,-3.20),(4.77,-3.29),(4.44,-2.73),(3.76,-1.46),(3.06,-.18),(2.57,1.21),(2.15,2.05),(1.56,2.41),(.88,2.48),(.87,2.85),(1.56,2.88),(1.65,3.08),(1.39,3.22)]
blade('Swept main wings',wing,lambda x,y:.10+.065*max(y,0),.20,True)
fin=[(.68,1.42),(1.30,1.28),(1.84,.48),(1.58,.03),(.93,.31)]
for direction,label in [(1,'Upper inner fins'),(-1,'Lower inner fins')]:
    obj=blade(label,fin,lambda x,y:direction*(.28+.13*(x-.68)),.14)
    obj.data.materials.append(magenta)
    for face in obj.data.polygons:
        if face.center.y<.38:
            face.material_index=1

tail=[(.77,.75,.02,.23,.17),(1.10,.08,.07,.25,.17),(1.42,-.66,.18,.28,.16),(1.53,-1.31,.33,.29,.15),(1.32,-1.99,.53,.27,.14),(.98,-2.66,.80,.25,.12),(.63,-3.32,1.09,.20,.09),(.28,-4.07,1.35,.025,.025)]
tail_rings=[]
for i,(x,y,z,width,height) in enumerate(tail):
    previous=tail[max(i-1,0)]
    following=tail[min(i+1,len(tail)-1)]
    tangent=Vector((following[0]-previous[0],following[1]-previous[1],0)).normalized()
    normal=Vector((-tangent.y,tangent.x,0))
    section=[(-1,0),(-.55,1),(.55,1),(1,0),(.55,-1),(-.55,-1)]
    tail_rings.append([(x+normal.x*width*u,y+normal.y*width*u,z+height*v+.10*u) for u,v in section])
vertices=[v for ring in tail_rings for v in ring]
faces=[]
for i in range(len(tail_rings)-1):
    for j in range(6):
        k=(j+1)%6
        faces.append((i*6+j,i*6+k,(i+1)*6+k,(i+1)*6+j))
for row in (0,len(tail_rings)-1):
    base=row*6
    for j in range(1,5):
        faces.append((base,base+j,base+j+1))
part('Curved rising tails',vertices,faces)

top=bpy.data.objects['reference.top']
top.empty_display_size=10.39
top.location=(0,0,-.85)
side=bpy.data.objects['reference.side']
side.empty_display_size=8.4
side.location=(-5.3,0,.25)
for image in bpy.data.images:
    if image.source=='FILE':
        image.pack()
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.color_type='MATERIAL'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='SOLID'
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.overlay.show_floor=False
            area.spaces.active.region_3d.view_distance=13
            area.spaces.active.region_3d.view_location=(0,0,.2)
for obj in scene.objects:
    obj.select_set(False)
scene.view_layers[0].objects.active=bpy.data.objects['Curved rising tails']
bpy.data.objects['Curved rising tails'].select_set(True)
version=bpy.context.preferences.filepaths.save_version
bpy.context.preferences.filepaths.save_version=0
try:
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
finally:
    bpy.context.preferences.filepaths.save_version=version
