"""Adapt the loaded owner-source copy; never factory-reset or overwrite an existing result.

Run Blender --background inputs/owner-source.blend --python adapt_console.py -- --output draft.blend.
Writes the named blend, matching PNG, and adaptation manifest. Existing output raises an error.
"""
import bpy, json, math, hashlib, sys
from pathlib import Path
import numpy as np
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

ROOT=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
output=ROOT/(args[args.index('--output')+1] if '--output' in args else 'console.blend')
if output.exists(): raise RuntimeError('Refusing to overwrite existing owner result: '+str(output))
source=ROOT/'inputs/owner-source.blend'
if Path(bpy.data.filepath).resolve()!=source.resolve(): raise RuntimeError('Load the owner-source copy before adaptation.')
source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
scene=bpy.context.scene
bpy.context.view_layer.update()
original=list(scene.objects)
original_names=[o.name for o in original]
archive=bpy.data.collections.new('99 Owner source archive — hidden')
scene.collection.children.link(archive)
copies={}
for obj in original:
    duplicate=obj.copy()
    if obj.data: duplicate.data=obj.data.copy()
    duplicate.name='SOURCE | '+obj.name
    archive.objects.link(duplicate);copies[obj]=duplicate
for obj,duplicate in copies.items():
    world=obj.matrix_world.copy()
    duplicate.parent=copies.get(obj.parent)
    duplicate.matrix_world=world
archive.hide_render=True;archive.hide_viewport=True
active=bpy.data.collections.new('11 Composition additions')
scene.collection.children.link(active)
def hide(obj): obj.hide_render=True;obj.hide_set(True)
def show(obj): obj.hide_render=False;obj.hide_set(False)
def parent(obj,root):
    bpy.context.view_layer.update()
    world=obj.matrix_world.copy();basis=obj.matrix_basis.copy()
    obj.parent=root
    obj.matrix_parent_inverse=root.matrix_world.inverted()@world@basis.inverted()
    bpy.context.view_layer.update()
def children(root): return [o for o in original if o==root or o in root.children_recursive]
def detach(root):
    world=root.matrix_world.copy();root.parent=None;root.matrix_world=world
def bounds(objects):
    pts=[o.matrix_world@Vector(v) for o in objects if o.type in {'MESH','CURVE','FONT'} for v in o.bound_box]
    return Vector([min(v[i] for v in pts) for i in range(3)]),Vector([max(v[i] for v in pts) for i in range(3)])
def pivot(root,objects):
    bpy.context.view_layer.update()
    lo,hi=bounds(objects);worlds={o:o.matrix_world.copy() for o in root.children};root.matrix_world.translation=(lo+hi)/2
    bpy.context.view_layer.update()
    for o,m in worlds.items():o.matrix_parent_inverse=root.matrix_world.inverted()@m@o.matrix_basis.inverted()
    bpy.context.view_layer.update()
def empty(name,position):
    obj=bpy.data.objects.new(name,None);active.objects.link(obj);obj.location=position;obj.empty_display_size=.2;return obj

hero=bpy.data.objects['HERO - reference composition'];hero.name='Hero camera'
scene.camera=hero;scene.render.resolution_x=1672;scene.render.resolution_y=941;scene.render.resolution_percentage=65
W,H=1672,941
R=hero.matrix_world.to_quaternion()@Vector((1,0,0));U=hero.matrix_world.to_quaternion()@Vector((0,1,0));F=hero.matrix_world.to_quaternion()@Vector((0,0,-1))
def ray(x,y):return F+R*((x/W-.5)*hero.data.sensor_width/hero.data.lens)+U*((.5-y/H)*hero.data.sensor_width/hero.data.lens*H/W)
def point(x,y,depth=24):return hero.location+ray(x,y)*depth
def project(objects):
    pts=[world_to_camera_view(scene,hero,o.matrix_world@Vector(v)) for o in objects if o.type in {'MESH','CURVE','FONT'} for v in o.bound_box]
    xs=[p.x*W for p in pts];ys=[(1-p.y)*H for p in pts];return np.array([min(xs),min(ys),max(xs),max(ys)])
def fit(root,objects,target,uniform=False):
    base=root.matrix_world.copy();base_scale=root.scale.copy();target=np.array(target,dtype=float)
    def result(p):
        root.matrix_world=base.copy();root.scale=Vector((base_scale.x*math.exp(p[0]),base_scale.y*(math.exp(p[0]) if uniform else 1),base_scale.z*math.exp(p[0] if uniform else p[1])))
        root.location+=R*p[-2]+U*p[-1];bpy.context.view_layer.update();box=project(objects)
        return np.array([box[2]-box[0],(box[0]+box[2])/2,(box[1]+box[3])/2]) if uniform else np.array([box[2]-box[0],box[3]-box[1],(box[0]+box[2])/2,(box[1]+box[3])/2])
    wanted=np.array([target[2]-target[0],(target[0]+target[2])/2,(target[1]+target[3])/2]) if uniform else np.array([target[2]-target[0],target[3]-target[1],(target[0]+target[2])/2,(target[1]+target[3])/2])
    p=np.zeros(3 if uniform else 4)
    for _ in range(12):
        got=result(p);err=wanted-got
        if np.max(abs(err))<.15:break
        jac=np.zeros((len(p),len(p)))
        for j in range(len(p)):
            trial=p.copy();trial[j]+=.001;jac[:,j]=(result(trial)-got)/.001
        p+=np.linalg.lstsq(jac,err,rcond=None)[0]*.85
    result(p);return project(objects).tolist()

main=bpy.data.objects['Main console'];display=bpy.data.objects['Editable screen content'];glass=bpy.data.objects['Display glass with rear enclosure']
projector=bpy.data.objects['Projector'];hologram=bpy.data.objects['Hologram'];launch=bpy.data.objects['Launch control'];deck=bpy.data.objects['Deck'];cables=bpy.data.objects['Cable looms']
for root in [hologram,projector,launch,cables]:detach(root)
main_keep={'Full-depth main rear chassis','Main armor surround','Inner beveled screen reveal','Black resilient display gasket','Main console','Editable screen content','Display glass with rear enclosure'}
for obj in children(main):
    if obj.name not in main_keep:hide(obj)
main_fit=fit(main,[glass],[150,497,510,815])
for obj in original:
    if obj.parent and obj in bpy.data.objects['Electronics'].children_recursive:hide(obj)
for obj in children(deck):
    if obj.name not in {'Deck','Full-width structural equipment deck'}:hide(obj)
for obj in children(cables):hide(obj)
for obj in children(launch):hide(obj)
for obj in children(projector):show(obj)
for obj in children(hologram):show(obj)
for obj in original:
    if obj.type=='LIGHT':hide(obj)
for name in ['Volumetric projection cone']:hide(bpy.data.objects[name])
projector_objects=[o for o in children(projector) if o.type=='MESH' and not o.hide_render]
pivot(projector,projector_objects)
projector_fit=fit(projector,projector_objects,[940,686,1125,791])
holo_objects=[o for o in children(hologram) if o.type in {'MESH','CURVE'}]
pivot(hologram,holo_objects)
holo_fit=fit(hologram,holo_objects,[668,152,1415,519],True)

def material(name,color,metallic=.15,roughness=.48,emission=0):
    mat=bpy.data.materials.new(name);mat.use_nodes=True;p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metallic;p.inputs['Roughness'].default_value=roughness
    if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    return mat
taupe=material('06 | Light neutral taupe',(.48,.385,.285),.24,.5)
trim=material('06 | Warm alloy trim',(.28,.25,.21),.5,.4)
dark=material('06 | Charcoal structure',(.038,.044,.052),.35,.43)
black=material('06 | Recess and rubber',(.009,.014,.02),.1,.42)
capmat=material('06 | Warm neutral control caps',(.35,.27,.19),.15,.4)
inkmat=material('06 | Engraved dark lettering',(.018,.015,.012),.15,.5)
amber=material('06 | Amber electronic glass',(.65,.16,.008),.08,.38,.4)
softwhite=material('06 | Warm indicator',(.7,.51,.26),.1,.3,2)
for obj in original:
    if obj.hide_render or obj.type not in {'MESH','CURVE'} or 'Vanguard /' in obj.name:continue
    for i,slot in enumerate(obj.material_slots):
        old=slot.material.name if slot.material else ''
        if old=='Warm grey painted alloy':obj.data.materials[i]=taupe
        elif old in {'Inset warm alloy','Beveled exposed alloy','Cable collar titanium'}:obj.data.materials[i]=trim
        elif old=='Charcoal machined metal':obj.data.materials[i]=dark
        elif old in {'Dark display glass','Recess shadow','Graphite cable jackets'}:obj.data.materials[i]=black
        elif old=='Projector warm aperture':obj.data.materials[i]=amber

def bevel(obj,width=.035,segments=3):
    mod=obj.modifiers.new('Editable bevel','BEVEL');mod.width=width;mod.segments=segments
    if obj.type=='MESH':
        normal=obj.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');normal.keep_sharp=True
def mesh(name,vertices,faces,mat,root=None,edge=.035):
    data=bpy.data.meshes.new(name+' mesh');data.from_pydata(vertices,[],faces);data.update();obj=bpy.data.objects.new(name,data);active.objects.link(obj);obj.data.materials.append(mat)
    if edge:bevel(obj,edge)
    if root:parent(obj,root)
    return obj
def prism(name,front,normal,depth,mat,root=None,edge=.035):
    front=[Vector(v) for v in front];n=len(front);vertices=front+[v-normal*depth for v in front];faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
    return mesh(name,vertices,faces,mat,root,edge)
def plane_at(x,y,depth=24):return point(x,y,depth),-F
local_glass=[v.co for v in glass.data.vertices]
front_center=glass.matrix_world@Vector(((min(v.x for v in local_glass)+max(v.x for v in local_glass))/2,min(v.y for v in local_glass),(min(v.z for v in local_glass)+max(v.z for v in local_glass))/2))
main_normal=(glass.matrix_world.to_3x3().inverted().transposed()@Vector((0,-1,0))).normalized()
def on_plane(x,y,center=front_center,normal=main_normal):
    direction=ray(x,y);return hero.location+direction*((center-hero.location).dot(normal)/direction.dot(normal))
def panel(name,coords,depth,mat,root,center=front_center,normal=main_normal,offset=0,edge=.035):return prism(name,[on_plane(x,y,center,normal)+normal*offset for x,y in coords],normal,depth,mat,root,edge)
def chamfer(x,y,w,h,c=7):return [(x+c,y),(x+w-c,y),(x+w,y+c),(x+w,y+h-c),(x+w-c,y+h),(x+c,y+h),(x,y+h-c),(x,y+c)]
def rect_panel(name,x,y,w,h,depth,mat,root,center=front_center,normal=main_normal,offset=0,c=7):return panel(name,chamfer(x,y,w,h,c),depth,mat,root,center,normal,offset)
def retarget_source(obj,outer,inner=None,offset=0,depth=.3):
    order=[5,4,3,2,1,0,7,6]
    loops=[outer,inner] if inner else [outer]
    positions=[]
    for back in [False,True]:
        for coords in loops:
            positions.extend(on_plane(*coords[i])+main_normal*(offset-(depth if back else 0)) for i in order)
    if len(positions)!=len(obj.data.vertices):raise RuntimeError('Unexpected owner frame topology: '+obj.name)
    inverse=obj.matrix_world.inverted()
    for vertex,position in zip(obj.data.vertices,positions):vertex.co=inverse@position
    obj.data.update()
retarget_source(bpy.data.objects['Full-depth main rear chassis'],chamfer(128,447,415,385,18),offset=-.15,depth=.75)
retarget_source(bpy.data.objects['Main armor surround'],chamfer(128,446,415,386,18),chamfer(143,489,386,334,12),offset=.10,depth=.55)
retarget_source(bpy.data.objects['Inner beveled screen reveal'],chamfer(143,489,386,334,12),chamfer(149,495,374,322,9),offset=.115,depth=.12)
retarget_source(bpy.data.objects['Black resilient display gasket'],chamfer(149,495,374,322,9),chamfer(151,497,370,319,8),offset=.12,depth=.10)
retarget_source(glass,chamfer(150,497,370,319,8),offset=.11,depth=.16)
def textured(name,path,coords,root,center=front_center,normal=main_normal,offset=.01):
    image=bpy.data.images.load(str(path),check_existing=True);image.alpha_mode='STRAIGHT';image.pack();mat=bpy.data.materials.new(name+' material');mat.use_nodes=True;nodes=mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');shader=nodes.new('ShaderNodeEmission');tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Linear';mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Color']);shader.inputs['Strength'].default_value=.85;mat.node_tree.links.new(shader.outputs[0],out.inputs['Surface'])
    obj=mesh(name,[on_plane(x,y,center,normal)+normal*offset for x,y in coords],[tuple(range(len(coords)))],mat,root,0);uv=obj.data.uv_layers.new(name='Screen UV');xs=[p[0]for p in coords];ys=[p[1]for p in coords]
    for polygon in obj.data.polygons:
        for index in polygon.loop_indices:
            x,y=coords[obj.data.loops[index].vertex_index];uv.data[index].uv=((x-min(xs))/(max(xs)-min(xs)),1-(y-min(ys))/(max(ys)-min(ys)))
    return obj
ui=ROOT/'inputs/ui'
textured('Main display | approved B graphics',ui/'main-hud.png',chamfer(150,497,370,319,8),display,offset=.125)
rect_panel('Left control panel | full depth',20,447,124,491,.65,taupe,main,c=20)
detail_root=empty('Detail display',on_plane(328,872));parent(detail_root,main)
rect_panel('Detail housing | full depth',127,826,407,110,.6,taupe,detail_root,c=15)
rect_panel('Detail bevel and gasket',135,834,391,88,.2,dark,detail_root,offset=.025,c=11)
rect_panel('Detail glass | replaceable plane',146,837,364,70,.08,black,detail_root,offset=.06,c=6)
textured('Detail display | Vanguard data',ui/'detail-hud.png',[(146,837),(510,837),(510,907),(146,907)],detail_root,offset=.11)
font_label=bpy.data.fonts.load(str(ROOT.parent/'display-concepts-01/assets/fonts/Rajdhani-Medium.ttf'))
font_heading=bpy.data.fonts.load(str(ROOT.parent/'display-concepts-01/assets/fonts/Michroma.ttf'))
def label(name,body,x,y,size,root,center=front_center,normal=main_normal,mat=inkmat,heading=False,offset=.065):
    p=on_plane(x,y,center,normal)+normal*offset;data=bpy.data.curves.new(name,'FONT');data.body=body;data.font=font_heading if heading else font_label;data.size=size;data.align_x='CENTER';data.align_y='CENTER';data.extrude=.001;obj=bpy.data.objects.new(name,data);active.objects.link(obj);obj.location=p
    right=(on_plane(x+1,y,center,normal)-on_plane(x,y,center,normal)).normalized();up=normal.cross(right).normalized();obj.rotation_euler=Matrix((right,up,normal)).transposed().to_euler();obj.data.materials.append(mat);parent(obj,root);return obj
def cylinder(name,x,y,radius,depth,mat,root,center=front_center,normal=main_normal,offset=.1,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=on_plane(x,y,center,normal)+normal*offset);obj=bpy.context.object;obj.name=name;obj.rotation_euler=normal.to_track_quat('Z','Y').to_euler();obj.data.materials.append(mat);bevel(obj,.028);parent(obj,root);return obj
selector=empty('Selector knob',on_plane(88,669));parent(selector,main)
cylinder('Selector | retaining collar',88,669,.43,.10,dark,selector,offset=.10)
cylinder('Selector | faceted cap',88,669,.36,.22,dark,selector,offset=.24)
rect_panel('Selector | pointer',67,649,5,11,.015,softwhite,selector,offset=.37,c=1)
for name,y,arrow in [('Previous',738,'‹'),('Next',784,'›')]:
    control=empty(name+' button',on_plane(87,y));parent(control,main)
    rect_panel(name+' | socket',56,y-19,63,39,.12,dark,control,offset=.03,c=5)
    rect_panel(name+' | separate cap',63,y-14,48,27,.13,capmat,control,offset=.16,c=3)
    label(name+' | engraved arrow',arrow,87,y,.24,control,offset=.20)
label('DISPLAY | engraved label','DISPLAY',86,548,.17,main)
cylinder('Display | toggle base',70,589,.13,.12,dark,main,offset=.13)
rect_panel('Display | toggle paddle',61,568,17,32,.17,capmat,main,offset=.24,c=4)
rect_panel('Display | status recess',96,576,31,31,.1,dark,main,offset=.07,c=4)
rect_panel('Display | status lens',102,581,20,21,.05,amber,main,offset=.15,c=2)
for x,y in [(71,484),(484,484),(67,857),(515,875)]:cylinder('Console | captive corner bolt',x,y,.105,.035,trim,main,offset=.13,vertices=16)

deckmesh=bpy.data.objects['Full-width structural equipment deck'];show(deckmesh);deckmesh.data.materials.clear();deckmesh.data.materials.append(dark)
deck_surface=point(1100,805,24);deck_surface.z=.55
deckmesh.location.z=.16
deck_add=empty('Deck fittings',point(1110,822,24));parent(deck_add,deck)
deckcenter=point(1110,790,24);decknormal=Vector((0,-.48,.88)).normalized()
rect_panel('Foredeck | broad tan cover',830,775,470,143,.30,taupe,deck_add,deckcenter,decknormal,c=22)
rect_panel('Equipment shelf | continuous base',555,688,1080,172,.5,dark,deck_add,deckcenter,decknormal,offset=-.10,c=18)
fins=empty('Three broad cooling fins',point(738,742,24));parent(fins,deck)
for i in range(3):
    x=644+i*70
    panel('Cooling fin | '+str(i+1),[(x+45,687),(x+88,691),(x+42,792),(x,792)],.35,dark,fins,deckcenter,decknormal,offset=.15,edge=.045)
switches=empty('Projection controls',point(1350,727,24));parent(switches,deck)
rect_panel('Projection | raised switch plate',1145,666,430,120,.42,taupe,switches,deckcenter,decknormal,offset=.24,c=23)
label('PROJECTION | engraved label','PROJECTION',1338,685,.19,switches,deckcenter,decknormal,offset=.29)
for x in [1228,1298]:
    cylinder('Projection | toggle socket',x,738,.14,.10,dark,switches,deckcenter,decknormal,offset=.34)
    cylinder('Projection | toggle stalk',x,722,.075,.31,trim,switches,deckcenter,decknormal,offset=.50)
    cylinder('Projection | toggle handle',x,714,.11,.12,dark,switches,deckcenter,decknormal,offset=.68)
cylinder('Projection | large rotary knob',1394,730,.32,.28,dark,switches,deckcenter,decknormal,offset=.47)
rect_panel('Projection | square control socket',1491,709,41,39,.12,dark,switches,deckcenter,decknormal,offset=.34,c=5)
rect_panel('Projection | warm square cap',1498,715,26,25,.06,capmat,switches,deckcenter,decknormal,offset=.48,c=3)
launch.location=point(1453,837,23)
launchcenter=point(1453,837,23);launchnormal=Vector((0,-.60,.80)).normalized()
rect_panel('Launch | source-style chamfered chassis',1315,787,282,119,.45,dark,launch,launchcenter,launchnormal,c=24)
rect_panel('Launch | separate amber button',1334,799,240,70,.24,amber,launch,launchcenter,launchnormal,offset=.17,c=18)
label('LAUNCH | current editable face','LAUNCH',1454,831,.29,launch,launchcenter,launchnormal,heading=True,offset=.21)

def curve(name,points,radius,mat,root):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.bevel_depth=radius;data.bevel_resolution=4;sp=data.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    obj=bpy.data.objects.new(name,data);active.objects.link(obj);obj.data.materials.append(mat);parent(obj,root);return obj
for index in range(2):curve('Deck cable | editable spline '+str(index+1),[point(670,858+index*9,21.3),point(709,891+index*7,21.3),point(833,896+index*5,21.3),point(1123,893+index*5,21.3),point(1170,912+index*5,21.3)],.065,black,cables)
cablecenter=point(839,894,21.3);cablenormal=-F
rect_panel('Cable | retaining saddle',817,875,42,45,.12,trim,cables,cablecenter,cablenormal,offset=.02,c=5)
rect_panel('Cable | warm connector',1103,883,42,18,.12,softwhite,cables,cablecenter,cablenormal,offset=.05,c=3)

def area(name,location,target,energy,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size;data.color=color;obj=bpy.data.objects.new(name,data);active.objects.link(obj);obj.location=location;obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler();return obj
area('Key | broad neutral warm',(-3,-10,13),(0,0,2),1600,10,(1,.86,.71))
area('Fill | cool space',(8,3,10),(0,0,2),600,8,(.55,.68,1))
area('Front readability',(1,-12,5),(0,0,2),350,9,(1,.94,.85))
lens=bpy.data.objects['Projector frosted luminous lens'];lo,hi=bounds([lens]);lenscenter=(lo+hi)/2
beamlight=area('Projector | warm glow',lenscenter+Vector((0,0,.07)),lenscenter+Vector((0,0,4)),110,1.2,(1,.41,.12));parent(beamlight,projector)
beam_mat=bpy.data.materials.new('06 | Warm volumetric projection');beam_mat.use_nodes=True;n=beam_mat.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');vol=n.new('ShaderNodeVolumePrincipled');vol.inputs['Color'].default_value=(1,.4,.12,1);vol.inputs['Density'].default_value=.035;vol.inputs['Emission Color'].default_value=(1,.23,.04,1);vol.inputs['Emission Strength'].default_value=.12;beam_mat.node_tree.links.new(vol.outputs['Volume'],out.inputs['Volume'])
topcenter=point(1040,437,24);direction=(topcenter-lenscenter).normalized();right=R;across=direction.cross(right).normalized();vertices=[]
for center,rx,ry in [(lenscenter,.55,.30),(topcenter,3.0,1.2)]:
    for i in range(48):a=2*math.pi*i/48;vertices.append(center+right*math.cos(a)*rx+across*math.sin(a)*ry)
faces=[tuple(reversed(range(48))),tuple(range(48,96))]+[(i,(i+1)%48,(i+1)%48+48,i+48)for i in range(48)]
mesh('Projector | editable light volume',vertices,faces,beam_mat,projector,0)
new_holo_materials={}
for obj in holo_objects:
    for i,slot in enumerate(obj.material_slots):
        old=slot.material
        if old.name not in new_holo_materials:
            mat=old.copy();mat.name='06 | '+old.name;new_holo_materials[old.name]=mat
        obj.data.materials[i]=new_holo_materials[old.name]
mat=new_holo_materials['HOLO / muted red and grey panels'];nodes=mat.node_tree.nodes
painted=nodes['Muted painted projection'];surface=nodes.new('ShaderNodeEmission');surface.name='Unlit projected paint';surface.inputs['Strength'].default_value=.45
mat.node_tree.links.new(nodes['Packed muted paint'].outputs['Color'],surface.inputs['Color'])
for link in list(mat.node_tree.links):
    if link.from_node==painted:mat.node_tree.links.new(surface.outputs[0],link.to_socket)
nodes['Front opacity / back opacity'].inputs['To Min'].default_value=.70
for name in ['HOLO / bright contour']:
    mat=new_holo_materials[name]
    for node in mat.node_tree.nodes:
        if node.type=='EMISSION':node.inputs['Color'].default_value=(1,.32,.06,1);node.inputs['Strength'].default_value=3.0

space=empty('Viewport',point(836,420,32))
background=bpy.data.images.get('nebula-background.png')
bgmat=bpy.data.materials.new('06 | Packed blue nebula');bgmat.use_nodes=True;n=bgmat.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');em=n.new('ShaderNodeEmission');em.inputs['Strength'].default_value=.32;tex=n.new('ShaderNodeTexImage');tex.image=background;uvnode=n.new('ShaderNodeTexCoord');distance=n.new('ShaderNodeVectorMath');distance.operation='DISTANCE';distance.inputs[1].default_value=(.52,.53,0);fade=n.new('ShaderNodeMapRange');fade.inputs['From Min'].default_value=.03;fade.inputs['From Max'].default_value=.55;fade.inputs['To Min'].default_value=.08;fade.inputs['To Max'].default_value=1;mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;bgmat.node_tree.links.new(uvnode.outputs['UV'],distance.inputs[0]);bgmat.node_tree.links.new(distance.outputs['Value'],fade.inputs[0]);bgmat.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);bgmat.node_tree.links.new(fade.outputs[0],mix.inputs[2]);bgmat.node_tree.links.new(mix.outputs[0],em.inputs['Color']);bgmat.node_tree.links.new(em.outputs[0],out.inputs['Surface'])
bg=mesh('Viewport | distant nebula backdrop',[point(-100,-100,80),point(W+100,-100,80),point(W+100,H+100,80),point(-100,H+100,80)],[(0,1,2,3)],bgmat,space,0);uv=bg.data.uv_layers.new(name='Nebula UV')
for i,co in enumerate([(0,1),(1,1),(1,0),(0,0)]):uv.data[i].uv=co
rimcenter=point(836,21,29);rimnormal=-F
rect_panel('Viewport | full upper structural rim',-30,-25,1732,67,.8,dark,space,rimcenter,rimnormal,c=25)
rect_panel('Viewport | inner top metal lip',34,23,1604,16,.15,trim,space,rimcenter,rimnormal,offset=.10,c=7)
etched=material('06 | Faint etched glass',(.12,.2,.25),0,.65,.28)
for x,flip in [(20,1),(1651,-1)]:
    curve('Viewport | edge etch', [point(x,90,28),point(x+16*flip,111,28),point(x+16*flip,290,28),point(x,310,28),point(x,620,28)],.003,etched,space)

for root in [main,detail_root,projector,hologram,launch,fins,switches,cables]:
    visible=[o for o in scene.objects if (o==root or o in root.children_recursive) and not o.hide_render and o.type in {'MESH','CURVE','FONT'} and not o.name.startswith('SOURCE |')]
    if visible:pivot(root,visible)
parent(hologram,projector);parent(projector,deck);parent(launch,deck);parent(cables,deck)
for obj in bpy.context.selected_objects:obj.select_set(False)
hero.select_set(True);bpy.context.view_layer.objects.active=hero
hero['composition_locked_for_review']=True
reference=bpy.data.images.load(str(ROOT.parent/'interactive-console-05/preview.png'));reference.pack();hero.data.show_background_images=True;slot=hero.data.background_images.new();slot.image=reference;slot.alpha=.35;slot.display_depth='FRONT';hero.data.show_background_images=False
scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.025,.038,.065,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.3
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=.3
compositor=bpy.data.node_groups.new('06 | Restrained electronics glow','CompositorNodeTree');scene.compositing_node_group=compositor;compositor.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor');nodes=compositor.nodes;rl=nodes.new('CompositorNodeRLayers');glare=nodes.new('CompositorNodeGlare');glare.inputs['Type'].default_value='Fog Glow';glare.inputs['Quality'].default_value='High';glare.inputs['Threshold'].default_value=1.3;glare.inputs['Size'].default_value=.25;comp=nodes.new('NodeGroupOutput');compositor.links.new(rl.outputs['Image'],glare.inputs['Image']);compositor.links.new(glare.outputs['Image'],comp.inputs['Image'])
for screen in bpy.data.screens:
    for area_space in screen.areas:
        if area_space.type=='VIEW_3D':area_space.spaces.active.region_3d.view_perspective='CAMERA';area_space.spaces.active.shading.type='MATERIAL'
for image in bpy.data.images:
    if image.has_data and not image.packed_file:image.pack()
for font in bpy.data.fonts:
    if font.filepath and font.filepath!='<builtin>' and not font.packed_file:font.pack()
manifest={'source':'inputs/owner-source.blend','sourceSha256':source_hash,'sourceObjectCount':len(original_names),'archiveObjectCount':len(copies),'retainedSourceObjects':[o.name for o in original if not o.hide_render],'target':'../interactive-console-05/preview.png','camera':hero.name,'mainGlassPixels':main_fit,'projectorPixels':projector_fit,'hologramPixels':holo_fit,'output':output.name,'notes':['Loaded the saved owner copy; no factory reset.','Original geometry retained in hidden Owner source archive.','Main-console edited angle retained; frame and rear chassis resized through its parent.','Real Vanguard mesh geometry unchanged; uniform assembly fitting only.','Screen graphics are packed replaceable images from accepted HTML.']}
(ROOT/(output.stem+'-manifest.json')).write_text(json.dumps(manifest,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(output))
scene.render.filepath=str(output.with_suffix('.png'));bpy.ops.render.render(write_still=True)
if hashlib.sha256(source.read_bytes()).hexdigest()!=source_hash:raise RuntimeError('Owner source copy changed unexpectedly.')
print('ADAPTATION_COMPLETE '+str(output))
