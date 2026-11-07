"""Load owner-source.blend, add a separate grey construction scene, save a new named blend.

Blender --background ../blender-console-06/inputs/owner-source.blend --python build_perspective.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-angle.png and NAME-measurements.json here.
Existing blend output fails. The loaded owner scene remains intact in the new file. No source writes.
"""
import bpy,bmesh,math,json,sys,hashlib
from pathlib import Path
from mathutils import Vector,Matrix,Euler
from bpy_extras.object_utils import world_to_camera_view

ROOT=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1]
out=ROOT/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
source_path=Path(bpy.data.filepath);source_hash=hashlib.sha256(source_path.read_bytes()).hexdigest()
source_scene=bpy.context.scene
source_main=bpy.data.objects['Main console']
source_projector=bpy.data.objects['Projector']
source_ship=bpy.data.objects['Hologram']
bpy.context.view_layer.update()
source_scene.name='SOURCE - owner saved scene'
s=bpy.data.scenes.new('07 - Perspective and form review');bpy.context.window.scene=s
s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True
s.render.resolution_x=1672;s.render.resolution_y=941;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG'
s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast'
s.world=bpy.data.worlds.new('07 - Neutral inspection world');s.world.use_nodes=True
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.065,.065,.065,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value=.35
col=bpy.data.collections.new('07 - Editable construction');s.collection.children.link(col)

def material(name,value):
    m=bpy.data.materials.new('07 | '+name);m.diffuse_color=(value,value,value,1);m.use_nodes=True
    p=m.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(value,value,value,1);p.inputs['Roughness'].default_value=.5
    return m
body=material('Housing grey',.34);edge=material('Bezel grey',.17);dark=material('Recess grey',.035);deckmat=material('Deck grey',.14);caps=material('Control grey',.25);shipmat=material('Ship grey',.38)

def root(label,location=(0,0,0),rotation=(0,0,0),parent=None):
    o=bpy.data.objects.new('07 | '+label,None);col.objects.link(o);o.parent=parent;o.location=location;o.rotation_euler=rotation;o.empty_display_size=.2;return o
def bevel(o,w=.035):
    b=o.modifiers.new('Editable edge radius','BEVEL');b.width=w;b.segments=3
    b=o.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');b.keep_sharp=True
def mesh(label,verts,faces,mat,parent=None,b=.035):
    data=bpy.data.meshes.new('07 | '+label);data.from_pydata(verts,[],faces);data.update()
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    o=bpy.data.objects.new('07 | '+label,data);col.objects.link(o);o.parent=parent;o.data.materials.append(mat)
    if b:bevel(o,b)
    return o
def poly(w,h,c,cx=0,cy=0):
    return [(cx-w/2+c,cy-h/2),(cx+w/2-c,cy-h/2),(cx+w/2,cy-h/2+c),(cx+w/2,cy+h/2-c),(cx+w/2-c,cy+h/2),(cx-w/2+c,cy+h/2),(cx-w/2,cy+h/2-c),(cx-w/2,cy-h/2+c)]
def plate(label,w,h,depth,mat,parent,location=(0,0,0),chamfer=.1,b=.025):
    loop=poly(w,h,chamfer);verts=[(x,y,z)for z in [-depth,0]for x,y in loop]
    faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8)for i in range(8)]
    o=mesh(label,verts,faces,mat,parent,b);o.location=location;return o
def ring(label,outer,inner,depth,mat,parent,z=0,b=.025):
    verts=[(x,y,zz)for zz in [z-depth,z]for loop in [outer,inner]for x,y in loop]
    faces=[]
    for i in range(8):
        j=(i+1)%8
        faces.extend([(i,j,j+16,i+16),(i+8,i+24,j+24,j+8),(i+16,j+16,j+24,i+24),(i,i+8,j+8,j)])
    return mesh(label,verts,faces,mat,parent,b)
def cube(label,dimensions,location,mat,parent=None,b=.04):
    w,d,h=dimensions;v=[(x*w/2,y*d/2,z*h/2)for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    o=mesh(label,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,parent,b);o.location=location;return o
def cylinder(label,radius,depth,location,mat,parent,vertices=16):
    verts=[(math.cos(i*2*math.pi/vertices)*radius,math.sin(i*2*math.pi/vertices)*radius,z)for z in [-depth/2,depth/2]for i in range(vertices)]
    faces=[tuple(reversed(range(vertices))),tuple(range(vertices,vertices*2))]+[(i,(i+1)%vertices,(i+1)%vertices+vertices,i+vertices)for i in range(vertices)]
    o=mesh(label,verts,faces,mat,parent,.025);o.location=location;return o
def cable(label,points,radius,parent):
    d=bpy.data.curves.new(label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=3
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('07 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(dark);return o
def profile_prism(label,x0,x1,yz,mat,parent,b=.04):
    n=len(yz);verts=[(x,y,z)for x in [x0,x1]for y,z in yz]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
    return mesh(label,verts,faces,mat,parent,b)

# All construction dimensions below are local-space dimensions, independent of the camera.
main=root('Monitor assembly',(-5.70,-2.8,.22))
main.rotation_euler=(source_main.matrix_world.to_quaternion().to_matrix()@Euler((math.pi/2,0,0)).to_matrix()).to_euler()
w,h=6.30,6.10
front=poly(w-.14,h-.12,.34,0,h/2);back=poly(w-.40,h-.72,.26,0,h/2+.25)
verts=[(x,y,-.10)for x,y in front]+[(x,y,-.78)for x,y in back]
mesh('Monitor tapered rear enclosure',verts,[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8)for i in range(8)],edge,main,.05)
outer=poly(w,h,.38,0,h/2);inner=poly(4.67,5.36,.20,.51,3.03)
ring('Monitor single front casting',outer,inner,.26,body,main,.10,.045)
ring('Continuous inner gasket',poly(4.68,5.37,.20,.51,3.03),poly(4.45,5.14,.16,.51,3.03),.13,dark,main,.07,.015)
plate('Display divider',4.67,.17,.22,body,main,(.51,1.52,.10),.035,.02)
main_screen=plate('Main rectangular display',4.45,3.96,.05,dark,main,(.51,3.60,-.06),.15,.012)
detail_screen=plate('Detail rectangular display',4.45,.98,.05,dark,main,(.51,.91,-.06),.13,.012)
plate('Top trim',4.0,.37,.14,edge,main,(0,5.84,.22),.12,.025)
selector=root('Selector',(-2.45,3.10,.15),parent=main)
cylinder('Selector collar',.43,.10,(0,0,.04),dark,selector)
cylinder('Selector cap',.37,.28,(0,0,.20),caps,selector,12)
for label,y in [('Previous',2.37),('Next',1.80)]:
    r=root(label,(-2.45,y,.13),parent=main)
    plate(label+' socket',.73,.42,.10,dark,r,chamfer=.08)
    plate(label+' cap',.60,.30,.12,body,r,(0,0,.12),.05)
plate('Power switch socket',.34,.52,.12,dark,main,(-2.65,4.12,.15),.06)
plate('Power switch',.20,.31,.18,caps,main,(-2.65,4.20,.32),.04)
plate('Status light recess',.32,.34,.09,edge,main,(-2.22,4.12,.14),.04)

deck=root('Deck assembly')
profile_prism('Continuous left console body',-13,-2.65,[(-6.5,-1),(4.5,-1),(4.5,1),(1.5,1.25),(-1.8,0),(-6.5,-.5)],deckmat,deck,.08)
profile_prism('Continuous right console body',-2.65,13,[(-6.5,-1),(4.5,-1),(4.5,1),(3,1.55),(-.8,1.40),(-4.8,-.47),(-6.5,-.5)],deckmat,deck,.08)
bpy.context.view_layer.update()
for side in [-1,1]:
    position=main.matrix_world@Vector((side*3.40,2.50,-.38))
    mount=root('Monitor trunnion support '+str(side),(position.x,position.y,0),(0,0,.162295),deck)
    profile_prism('Monitor support cheek '+str(side),-.20,.20,[(-.50,.05),(1.25,.72),(0,position.z+.22)],edge,mount,.04)
    pin=root('Monitor hinge '+str(side),(position.x,position.y,position.z),parent=deck)
    pin.rotation_euler=(main.matrix_world.to_quaternion()@Vector((1,0,0))).to_track_quat('Z','Y').to_euler()
    cylinder('Monitor pivot boss '+str(side),.40,.70,(0,0,0),caps,pin,16)

def deck_z(y):return .75+.16*(y+2.8)
equipment=root('Shared sloping mounting plane',(0,0,deck_z(0)),(math.atan(.16),0,0),deck)
apron=profile_prism('Projector raked apron',.40,3.95,[(-2.8,.65),(1.45,1.40),(1.45,1.81),(-.65,1.81),(-2.25,1.09),(-2.8,.84)],body,deck,.08)
bank=root('Angled cooling bank',(-.95,-.25,.30),(0,0,math.radians(-12)),equipment)
plate('Cooling bank surround',3.25,3.5,.18,body,bank,chamfer=.28,b=.04)
for i,x in enumerate([-.80,0,.80]):
    profile_prism('Raked cooling rib '+str(i+1),x-.31,x+.31,[(-1.50,.02),(1.50,.02),(1.50,.20),(1.35,.35),(-1.18,.35),(-1.50,.08)],caps,bank,.035)

def duplicate_geometry(source_root,new_root,mat,filter_fn=lambda o:True):
    inverse=source_root.matrix_world.inverted();copies=[]
    for source in source_root.children_recursive:
        if source.type not in {'MESH','CURVE'} or not filter_fn(source):continue
        o=source.copy();o.data=source.data.copy();o.name='07 reuse | '+source.name;col.objects.link(o);o.parent=new_root
        o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_basis=inverse@source.matrix_world;o.hide_render=False;o.hide_viewport=False;o.hide_set(False)
        for i in range(len(o.data.materials)):o.data.materials[i]=mat
        copies.append(o)
    return copies
def object_bounds(objects):
    bpy.context.view_layer.update();points=[o.matrix_world@Vector(v) for o in objects for v in o.bound_box]
    return Vector([min(v[i]for v in points)for i in range(3)]),Vector([max(v[i]for v in points)for i in range(3)])
projector=root('Projector assembly')
projector_parts=duplicate_geometry(source_projector,projector,body,lambda o:o not in source_ship.children_recursive and 'Volumetric' not in o.name)
lo,hi=object_bounds(projector_parts);factor=2.05/(hi.x-lo.x)
projector.scale=(factor,)*3
projector.rotation_euler.x=0
lo,hi=object_bounds(projector_parts)
points=[o.matrix_world@v.co for o in projector_parts if o.type=='MESH' for v in o.data.vertices]
support=min(v.z-.16*v.y for v in points)
projector.location.x=2.20-(lo.x+hi.x)/2;projector.location.y=-.70-(lo.y+hi.y)/2
projector.location.z=deck_z(projector.location.y)-.02-support
bpy.context.view_layer.update()
lens=next(o for o in projector_parts if 'frosted luminous lens' in o.name)
lens_lo,lens_hi=object_bounds([lens]);projector.location+=Vector((2.35,.15,1.86))-(lens_lo+lens_hi)/2
cutter=plate('Projector aperture cutout',1.70,1.30,2.0,dark,None,(2.35,.15,2.4),.12,0)
cutter.hide_render=True;cutter.display_type='WIRE';cutter.hide_set(True)
opening=apron.modifiers.new('Editable recessed projector opening','BOOLEAN');opening.operation='DIFFERENCE';opening.solver='EXACT';opening.object=cutter
for o in projector_parts:
    if any(part in o.name.lower() for part in ['front service plate','captive mounting bolt','mounting foot','power light']):o.hide_render=True;o.hide_set(True)
    if 'lens' in o.name.lower() or 'aperture well' in o.name.lower():
        for i in range(len(o.data.materials)):o.data.materials[i]=dark

switches=root('Projection control assembly',(6.30,.30,1.60),(math.radians(13),0,0),deck)
plate('Switch housing',4.8,2.55,.42,edge,switches,chamfer=.30,b=.055)
plate('Switch plate',4.65,2.42,.12,body,switches,(0,0,.12),.28,.035)
for x in [-1.4,-.62]:
    cylinder('Toggle socket',.18,.12,(x,-.25,.19),dark,switches)
    cylinder('Toggle stalk',.065,.42,(x,-.25,.44),edge,switches)
    cylinder('Toggle cap',.13,.13,(x,-.25,.68),caps,switches)
cylinder('Projection knob',.40,.30,(.55,-.25,.32),caps,switches,12)
plate('Projection square control',.44,.45,.17,edge,switches,(1.6,-.25,.30),.05)

launch=root('Launch assembly',(6.70,-2.15,.95),(math.radians(25),0,0),deck)
plate('Launch enclosure',3.2,1.7,.70,edge,launch,chamfer=.28,b=.05)
plate('Launch socket',2.90,1.40,.12,dark,launch,(0,0,.10),.23,.025)
plate('Launch button',2.65,1.17,.27,body,launch,(0,0,.37),.20,.045)

cables=root('Cable assembly',parent=deck)
for i in range(2):
    cable('Cable '+str(i+1),[(-1.75,-2.85,.65+i*.14),(-1.7,-3.0,.60+i*.14),(-1.2,-3.15,.55+i*.14),(1.2,-3.15,.55+i*.14),(3.6,-3.15,.55+i*.14),(4.0,-2.85,.65+i*.14)],.07,cables)
cube('Cable saddle',(.36,.48,.38),(0,-3.07,.64),body,cables,.04)

ship=root('Hologram assembly')
ship.rotation_euler.x=math.radians(7)
ship_parts=duplicate_geometry(source_ship,ship,shipmat,lambda o:o.type=='MESH' and o.name.startswith('Vanguard /'))
for o in ship_parts:
    if o.name.endswith('Canopy'):
        for i in range(len(o.data.materials)):o.data.materials[i]=edge
lo,hi=object_bounds(ship_parts);factor=8.0/(hi.x-lo.x);ship.scale=(factor,)*3
lo,hi=object_bounds(ship_parts);ship.location=Vector((2.35,.15,6.45))-(lo+hi)/2

viewport=root('Viewport frame')
cube('Viewport upper lintel',(27,.85,.48),(0,4.5,10.5),edge,viewport,.10)
cube('Viewport inner lip',(26,.18,.13),(0,3.99,10.21),body,viewport,.035)
for x in [-12.5,12.5]:cube('Viewport side post',(.5,.8,10.6),(x,4.5,4.9),edge,viewport,.08)

def camera(label,loc,target,lens):
    d=bpy.data.cameras.new(label);o=bpy.data.objects.new(label,d);col.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();d.lens=lens;d.clip_end=200;return o
hero=camera('07 Camera - perspective',(0,-18,6.8),(0,0,4.75),33)
angle=camera('07 Camera - construction',(15,-24,15),(0,-.6,4.2),43)
s.camera=hero
ref=bpy.data.images.load(str(ROOT.parent/'interactive-console-05/preview.png'),check_existing=True);ref.pack()
slot=hero.data.background_images.new();slot.image=ref;slot.alpha=.35;slot.display_depth='FRONT';hero.data.show_background_images=False

def area(label,loc,target,power,size):
    d=bpy.data.lights.new(label,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(label,d);col.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('07 Soft key',(-5,-9,14),(0,0,2),2000,10)
area('07 Fill',(9,-3,9),(0,0,3),700,8)
area('07 Rim',(0,7,11),(0,0,3),1800,8)

bpy.context.view_layer.update()
measurements={'sourceSha256':source_hash,'camera':{'location':list(hero.location),'rotation':list(hero.rotation_euler),'lens':hero.data.lens},'monitorRotation':list(main.rotation_euler),'displayFaces':{},'projectedBounds':{}}
for obj in [main_screen,detail_screen]:
    verts=[obj.matrix_world@v.co for v in obj.data.vertices[8:16]]
    dirs=[(verts[b]-verts[a]).normalized()for a,b in [(0,1),(2,3),(4,5),(6,7)]]
    divergence=[math.degrees(math.atan2(dirs[a].cross(dirs[b]).length,abs(dirs[a].dot(dirs[b]))))for a,b in [(0,2),(1,3)]]
    measurements['displayFaces'][obj.name]={'oppositeEdgeDivergenceDegrees':divergence,'rectangular':max(divergence)<.01}
    assert max(divergence)<.01
for label,objects in [('monitor',list(main.children_recursive)),('projector',projector_parts),('ship',ship_parts),('launch',list(launch.children_recursive))]:
    points=[world_to_camera_view(s,hero,o.matrix_world@Vector(v))for o in objects if o.type=='MESH' for v in o.bound_box]
    measurements['projectedBounds'][label]=[min(p.x for p in points)*1672,(1-max(p.y for p in points))*941,max(p.x for p in points)*1672,(1-min(p.y for p in points))*941]
guide=bpy.data.texts.new('07 START HERE')
guide.write('Perspective correction review. Plain grey materials intentionally isolate shape and camera.\nThe original owner scene is retained as SOURCE - owner saved scene.\nCurrent scene: 07 - Perspective and form review. All new geometry uses real local-space dimensions.\nChange 07 Camera - perspective for framing. Move named assembly empties for placement.\nMonitor tilt is copied from the saved owner scene. Both screen faces are rectangular, with parallel opposite edges.\nDeck hardware is supported by a common sloping equipment deck.\nThis is for camera and form feedback before UI artwork, lighting, or final detail is restored.\n')
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.shading.type='MATERIAL';a.spaces.active.overlay.show_extras=False
for im in bpy.data.images:
    if im.has_data and not im.packed_file:im.pack()
for font in bpy.data.fonts:
    if font.filepath and font.filepath!='<builtin>' and not font.packed_file:font.pack()
s.render.filepath=str(ROOT/(name+'-camera.png'))
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
s.camera=angle;s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
measurements['ownerUnchanged']=hashlib.sha256(source_path.read_bytes()).hexdigest()==source_hash
assert measurements['ownerUnchanged']
(ROOT/(name+'-measurements.json')).write_text(json.dumps(measurements,indent=2))
print(json.dumps(measurements))
