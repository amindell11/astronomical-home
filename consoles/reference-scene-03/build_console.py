"""Build a new editable console. Existing .blend files are never overwritten.

Run: blender --background --python build_console.py -- [--output NAME.blend] [--preview]
The output path is relative to this folder. Exit is nonzero if preservation checks fail.
"""
from pathlib import Path
import argparse
import math
import random
import sys
import json
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parent
args = argparse.ArgumentParser()
args.add_argument('--output', default='reference-console.blend')
args.add_argument('--preview', action='store_true')
opts = args.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT = (ROOT / opts.output).resolve()
if OUT.parent != ROOT or OUT.exists():
    raise RuntimeError('Preserve existing editable sources. Choose a new output filename in this folder.')
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
s.name = 'Reference C - editable console'
s.render.engine = 'CYCLES'
s.cycles.samples = 32 if opts.preview else 96
s.cycles.use_denoising = True
cp=bpy.context.preferences.addons['cycles'].preferences
cp.compute_device_type='OPTIX'
cp.get_devices()
for device in cp.devices:device.use=device.type=='OPTIX'
s.cycles.device='GPU'
s.render.resolution_x = 1600 if opts.preview else 2048
s.render.resolution_y = 900 if opts.preview else 1152
s.render.resolution_percentage = 100
s.render.image_settings.file_format = 'PNG'
s.render.film_transparent = False
s.view_settings.view_transform = 'AgX'
s.view_settings.look = 'AgX - Medium High Contrast'
s.world = bpy.data.worlds.new('Dim cool environment')
s.world.use_nodes = True
s.world.node_tree.nodes['Background'].inputs[0].default_value = (.06,.095,.19,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value = .22
s.unit_settings.system='METRIC'

def collection(name):
    c=bpy.data.collections.new(name);s.collection.children.link(c);return c
cols={n:collection(n) for n in ['00 Cameras and light','01 Console frame','02 Editable display','03 Lower deck','04 Electronics','05 Cable looms','06 Projector','07 Launch control','08 Hologram','09 Space','10 Hidden reference']}
def root(name,col):
    o=bpy.data.objects.new(name,None);col.objects.link(o);o.empty_display_size=.25;return o
roots={k:root(k.upper(),v) for k,v in cols.items() if k not in ['00 Cameras and light','09 Space','10 Hidden reference']}
for key,name in [('01 Console frame','Main console'),('02 Editable display','Editable screen content'),('03 Lower deck','Deck'),('04 Electronics','Electronics'),('05 Cable looms','Cable looms'),('06 Projector','Projector'),('07 Launch control','Launch control'),('08 Hologram','Hologram')]:roots[key].name=name
def link(o,col,parent=None):
    for c in list(o.users_collection):c.objects.unlink(o)
    cols[col].objects.link(o)
    o.parent=parent or roots.get(col)
    return o
def material(name,color,metal=0,rough=.4,emission=0,noise=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1)
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    if noise:
        n=m.node_tree.nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=70;n.inputs['Detail'].default_value=2
        bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.1;bump.inputs['Distance'].default_value=.018
        m.node_tree.links.new(n.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    return m
tan=material('Warm grey painted alloy',(.23,.215,.19),.32,.5,noise=True)
edge=material('Beveled exposed alloy',(.33,.305,.26),.5,.39)
panel=material('Inset warm alloy',(.17,.16,.145),.38,.52,noise=True)
dark=material('Charcoal machined metal',(.018,.021,.027),.72,.32)
rubber=material('Graphite cable jackets',(.009,.012,.016),.08,.62)
brass=material('Cable collar titanium',(.19,.165,.13),.8,.29)
black=material('Recess shadow',(.005,.007,.011),.22,.48)
glass=material('Dark display glass',(.009,.014,.023),.0,.85,.035)
glass.node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=0
ink=material('Ivory screen phosphor',(.78,.51,.27),.0,.7,1.1)
dim=material('Dim screen dividers',(.37,.26,.17),.0,.5,.7)
titleink=material('Dark title ink',(.024,.01,.007),.0,1)
titleink.node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=0
amber=material('Warm amber indicator', (1,.39,.055),.0,.25,4)
green=material('Pale green indicator',(.49,.68,.2),.0,.3,2)
emitter=material('Projector warm aperture',(1,.67,.28),.15,.2,10)
cool=material('Cool status diode',(.15,.6,.8),0,.3,2)
def bevel(o,width=.035,segments=3):
    b=o.modifiers.new('Editable edge bevel','BEVEL');b.width=width;b.segments=segments
    b.limit_method='ANGLE'
    w=o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL');w.keep_sharp=True
    return o
def mesh(name,vertices,faces,mat,col,bev=.025):
    m=bpy.data.meshes.new(name);m.from_pydata(vertices,[],faces);m.update()
    o=bpy.data.objects.new(name,m);cols[col].objects.link(o);o.parent=roots.get(col)
    o.data.materials.append(mat)
    if bev:bevel(o,bev)
    return o
def cube(name,loc,size,mat,col,bev=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc);o=bpy.context.object;o.name=name
    o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    link(o,col);o.data.materials.append(mat)
    if bev:bevel(o,bev)
    return o
def outline(cx,cz,w,h,c):
    x=cx-w/2;z=cz-h/2
    return [(x+c,z),(x+w-c,z),(x+w,z+c),(x+w,z+h-c),(x+w-c,z+h),(x+c,z+h),(x,z+h-c),(x,z+c)]
def prism(name,pts,yfront,yback,mat,col,bev=.025):
    n=len(pts);v=[(x,yfront,z) for x,z in pts]+[(x,yback,z) for x,z in pts]
    f=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    f.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    return mesh(name,v,f,mat,col,bev)
def chamfer(name,cx,cz,w,h,c,yfront,yback,mat,col,bev=.025):
    return prism(name,outline(cx,cz,w,h,c),yfront,yback,mat,col,bev)
def ring(name,cx,cz,w,h,c,iw,ih,ic,yfront,yback,mat,col):
    outer=outline(cx,cz,w,h,c);inner=outline(cx,cz,iw,ih,ic)
    v=[(x,y,z) for y in [yfront,yback] for loop in [outer,inner] for x,z in loop]
    f=[]
    for i in range(8):
        j=(i+1)%8
        f.extend([(i,j,8+j,8+i),(16+i,24+i,24+j,16+j),(i,16+i,16+j,j),(8+i,8+j,24+j,24+i)])
    return mesh(name,v,f,mat,col,.018)
def wire(name,pts,radius,mat,col,smooth=True):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=24;c.bevel_depth=radius;c.bevel_resolution=3
    p=c.splines.new('BEZIER' if smooth else 'POLY')
    if smooth:
        p.bezier_points.add(len(pts)-1)
        for bp,co in zip(p.bezier_points,pts):bp.co=co;bp.handle_left_type=bp.handle_right_type='AUTO'
    else:
        p.points.add(len(pts)-1)
        for bp,co in zip(p.points,pts):bp.co=(*co,1)
    o=bpy.data.objects.new(name,c);cols[col].objects.link(o);o.parent=roots.get(col);o.data.materials.append(mat);return o
def cylinder(name,a,b,r,mat,col,vertices=24):
    a,b=Vector(a),Vector(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();link(o,col);o.data.materials.append(mat);bevel(o,.012,2)
    return o
def bolt(name,x,y,z,col):
    cylinder(name+' recessed seat',(x,y+.025,z),(x,y,z),.054,black,col)
    cylinder(name+' hex fastener',(x,y-.016,z),(x,y-.038,z),.035,brass,col,6)
    cube(name+' slot',(x,y-.042,z),(.029,.006,.006),black,col,.001)
def lamp(name,x,y,z,w,h,mat,col):
    chamfer(name+' socket',x,z,w+.13,h+.12,.035,y+.015,y+.12,black,col,.015)
    chamfer(name+' rim',x,z,w+.05,h+.04,.018,y,y+.04,brass,col,.008)
    chamfer(name+' lens',x,z,w,h,.012,y-.012,y+.02,mat,col,.008)
fontdir=ROOT.parents[1]/'combat-hud/shared/fonts'
sans=bpy.data.fonts.load(str(fontdir/'plex-sans.ttf'));sans.pack()
mono=bpy.data.fonts.load(str(fontdir/'plex-mono.ttf'));mono.pack()
def text(name,body,x,y,z,size,mat=ink,col='02 Editable display',font=mono,align='LEFT'):
    c=bpy.data.curves.new(name,'FONT');c.body=body;c.font=font;c.size=size;c.align_y='CENTER';c.align_x=align;c.extrude=.0008;c.resolution_u=6
    o=bpy.data.objects.new(name,c);cols[col].objects.link(o);o.parent=roots.get(col);o.location=(x,y,z);o.rotation_euler=(math.pi/2,0,0);o.data.materials.append(mat);return o

C='01 Console frame';cx=-4.3;cz=4.1
chamfer('Full-depth main rear chassis',cx,cz,5.85,6.65,.44,-.04,.98,dark,C,.07)
ring('Main armor surround',cx,cz,5.8,6.6,.43,4.98,5.62,.22,-.58,.42,tan,C)
ring('Inner beveled screen reveal',cx,cz+.11,5.34,5.98,.29,4.98,5.59,.19,-.6,-.34,edge,C)
ring('Black resilient display gasket',cx,cz+.11,5.04,5.66,.21,4.84,5.46,.18,-.55,-.34,rubber,C)
chamfer('Display glass with rear enclosure',cx,cz+.11,4.87,5.48,.19,-.475,-.16,glass,'02 Editable display',.008)
ring('Fine ivory etched screen border',cx,cz+.11,4.7,5.31,.17,4.685,5.295,.167,-.488,-.476,dim,'02 Editable display')
for x in [-7.01,-1.59]:
    for z in [1.04,2.0,5.96,7.06]:bolt('Main armor bolt',x,-.596,z,C)
for x in [-6.72,-1.88]:
    for z in [1.28,6.94]:
        cap=chamfer('Corner armor cap',x,z,.52,.5,.12,-.65,-.51,panel,C)
        bolt('Corner screw',x,-.676,z,C)
for x in [-6.87,-1.74]:
    for z in [2.8,5.65]:
        chamfer('Vertical frame machining recess',x,z,.055,.42,.025,-.605,-.585,black,C,.004)
        cube('Rim face groove',(x+.075,-.608,z-.23),(.015,.012,.11),dark,C,.002)
chamfer('Heavy lower chin armor',cx,1.01,4.73,.51,.14,-.62,.26,tan,C)
chamfer('Lower chin center inset',cx,.99,2.84,.32,.08,-.648,-.61,panel,C,.012)
for x in [-5.9,-2.69]:bolt('Chin access fastener',x,-.657,1.0,C)
cube('Left structural jamb',(-7.55,.28,3.8),(1.0,1.55,8.0),tan,C,.11)
for z in [1.4,3.5,5.6]:
    chamfer('Jamb module housing',-7.55,z,.9,1.56,.14,-.63,.24,dark,C)
    ring('Jamb module frame',-7.55,z,.77,1.4,.1,.54,1.15,.07,-.75,-.58,panel,C)
    chamfer('Jamb module cavity',-7.55,z,.55,1.16,.07,-.68,-.6,black,C)
lamp('Jamb amber status',-7.32,-.81,2.73,.11,.3,amber,C)
lamp('Jamb green status',-7.32,-.81,2.27,.11,.14,green,C)
cube('Upper continuation beam',(-4.7,.5,7.58),(7.4,1.5,.48),tan,C,.1)
chamfer('Top left service panel',-5.15,7.58,3.75,.28,.1,-.29,-.19,panel,C,.018)
lamp('Top beam standby',-2.75,-.32,7.58,.32,.09,amber,C)
for x in [-6.55,-2.55]:
    cube('Upper continuing frame support',(x,.74,8.65),(.48,1.15,2.3),dark,C,.04)
    cube('Upper support armor face',(x,.1,8.65),(.32,.14,2.2),tan,C,.025)
    cube('Upper support recessed seam',(x,-.0,8.65),(.035,.035,1.9),black,C,.004)

D='02 Editable display'
chamfer('Heading luminous plate',cx,6.35,4.22,.63,.015,-.493,-.48,ink,D,.003)
text('HANGAR title','HANGAR',-6.31,-.505,6.35,.7,titleink,font=sans)
for i in range(61):
    x=-6.4+i*.068
    wire('Heading diagonal raster',[(x,-.493,5.72),(x+.15,-.493,5.94)],.004,dim,D,False)
labels=[('SHIP','Vanguard'),('ENGINE','Default_Engine'),('SHIELD','Default_Shield'),('PRIMARY','Lasers'),('SECONDARY','Missiles')]
for i,(label,value) in enumerate(labels):
    z=5.1-i*.775
    pts=[(-6.33,z-.245),(-4.89,z-.245),(-4.89,z+.12),(-5.01,z+.245),(-6.33,z+.245)]
    prism(label+' ivory label plate',pts,-.497,-.482,ink,D,.002)
    labeltext=text(label+' editable label',label,-6.19,-.511,z,.35,titleink)
    labeltext.scale.x=.81
    valuetext=text(label+' editable value',value,-4.62,-.511,z,.35,ink)
    valuetext.scale.x=.8
    text(label+' next chevron','›',-2.32,-.511,z,.40,ink)
    if i in [0,3,4]:text(label+' previous chevron','‹',-2.72,-.511,z,.40,ink)
    if i<4:
        for n in range(64):
            x=-6.32+n*.066
            wire(label+' divider hatch',[(x,-.498,z-.39),(x+.058,-.498,z-.31)],.004,ink,D,False)
wire('Column separating rule',[(-4.8,-.501,5.37),(-4.8,-.501,1.79)],.006,ink,D,False)

D='03 Lower deck'
cube('Full-width structural equipment deck',(0,-.3,-.18),(17.8,9.6,.55),dark,D,.1)
cube('Deck upper armor',(0,-.6,.12),(17.7,7.9,.2),tan,D,.055)
for x in [-6.0,-1.8,2.4,6.55]:
    cube('Deck removable plate',(x,-1.85,.248),(4.05,3.9,.055),panel,D,.024)
    for dx in [-1.82,1.82]:
        for y in [-1.95,-.13]:cylinder('Deck countersunk screw',(x+dx,y,.277),(x+dx,y,.284),.043,black,D,12)
cube('Console lower support spine',(-4.35,.38,.57),(5.6,1.6,.58),dark,D,.08)
chamfer('Console base service face',-4.5,.52,4.8,.76,.18,-.88,.08,tan,D,.06)
chamfer('Base service vent recess',-3.73,.52,.9,.42,.07,-.951,-.89,black,D,.018)
for i in range(6):cube('Base angled vent louver',(-3.73,-.966,.36+i*.06),(.71,.08,.025),brass,D,.008)
lamp('Console base amber lamp',-5.5,-.974,.5,.4,.09,amber,D)
chamfer('Primary conduit socket box',-6.67,.56,1.18,.94,.19,-1.04,.2,tan,D,.05)
chamfer('Primary conduit socket inset',-6.67,.56,.83,.62,.08,-1.09,-1.04,black,D,.015)
for dx in [-.44,.44]:bolt('Conduit socket mounting',-6.67+dx,-1.11,.86,D)
cube('Rear equipment shelf',(2.74,1.45,1.18),(8.75,2.5,.27),tan,D,.055)
cube('Rear equipment fascia',(2.74,.13,.94),(8.75,.22,.45),dark,D,.04)
cube('Raised rear terminal rail',(2.85,2.49,1.64),(9.0,.45,.95),panel,D,.07)
for x in [-.83,.4,1.7,3.0,4.3,5.6,6.9]:
    chamfer('Rail removable panel',x,1.64,1.14,.74,.11,2.223,2.27,tan,D,.018)
    for dx in [-.43,.43]:bolt('Rail panel screw',x+dx,2.199,1.89,D)
lamp('Rail amber pair',-.67,2.16,1.88,.15,.09,amber,D)
lamp('Rail green pair',-.37,2.16,1.88,.15,.09,green,D)
for i in range(3):lamp('Rail system status',1.33+i*.48,2.16,1.88,.075,.08,amber,D)

E='04 Electronics'
cube('Finned controller lower housing',(.13,.72,1.54),(2.35,1.48,.5),dark,E,.07)
cube('Controller top surface',(.13,.73,1.8),(2.25,1.37,.08),panel,E,.035)
for i in range(13):
    fin=cube('Controller heat sink fin %02d'%i,(-.96+i*.18,.73,1.91),(.085,1.35,.43),dark,E,.02)
    cube('Fin leading edge %02d'%i,(-.96+i*.18,.034,1.91),(.053,.035,.34),brass,E,.009)
cube('Controller indicator strip',(1.48,.4,1.65),(.25,.82,.12),dark,E,.02)
for i in range(4):
    cube('Controller status %d'%i,(1.48,.12+i*.18,1.72),(.11,.07,.025),green if i<2 else amber,E,.012)
cube('Fascia connector recess',(.16,-.02,.99),(1.7,.11,.23),black,E,.025)
for i in range(18):cylinder('Connector pin',(-.61+i*.089,-.08,.99),(-.61+i*.089,-.11,.99),.012,brass,E,12)
for x in [-1.0,1.38]:
    chamfer('Equipment rack end bracket',x,1.1,.24,.53,.05,-.055,.45,tan,E,.02)
    bolt('Rack bracket fastener',x,-.09,1.23,E)
cube('Right bank service module',(5.85,1.65,1.48),(2.75,1.7,.28),dark,E,.06)
cube('Right bank sloped lid',(5.85,1.65,1.68),(2.65,1.6,.13),tan,E,.04)
for i in range(7):cube('Right bank cooling blade',(6.68,1.03+i*.19,1.8),(.58,.06,.15),dark,E,.016)
for x in [4.78,6.94]:
    for y in [1.05,2.25]:cylinder('Bank cover fastener',(x,y,1.75),(x,y,1.77),.035,black,E,12)

C='05 Cable looms'
def socket(name,x,y,z,rad=.15):
    cylinder(name+' flange',(x,y+.1,z),(x,y-.035,z),rad*1.65,dark,C)
    cylinder(name+' metal collar',(x,y-.025,z),(x,y-.14,z),rad*1.3,brass,C)
    cylinder(name+' rubber gland',(x,y-.13,z),(x,y-.26,z),rad,rubber,C)
for i in range(3):
    x=-1.13+i*.34
    socket('Rear conduit %d'%i,x,2.15,1.59,.105)
    wire('Rear curved conduit %d'%i,[(x,1.9,1.59),(x-.07,1.68,1.49),(x-.03,1.43,1.36),(x+.19,1.13,1.34)],.075,rubber,C)
for i in range(4):
    x=-1.01+i*.12
    socket('Low rack harness %d'%i,x,-.13,1.03,.058)
    pts=[(x,-.39,1.03),(x-.1,-.53,.67),(x+.24,-.63,.36),(x+.88,-.57,.38),(x+1.65,-.31,.74),(2.02+i*.14,.32,1.1)]
    wire('Looped rack harness %d'%i,pts,.046,rubber,C)
    cylinder('Harness crimp %d'%i,(x,-.37,.99),(x-.018,-.42,.86),.069,brass,C)
socket('Left main umbilical',-6.67,-1.13,.56,.23)
wire('Left main umbilical cable',[(-6.67,-1.39,.56),(-6.88,-1.72,.36),(-7.6,-2.01,.27),(-8.6,-2.3,.3)],.19,rubber,C)
for i in range(7):
    x=-6.94-i*.17;y=-1.77-i*.075
    cylinder('Umbilical rib %02d'%i,(x-.035,y-.016,.31),(x+.035,y+.016,.31),.205,brass,C)
socket('Projector power supply',2.01,-.25,1.1,.13)
socket('Launch power inlet',4.5,-.73,.79,.14)
pts=[(2.01,-.51,1.1),(2.35,-.69,.7),(3.15,-.76,.6),(4.15,-.82,.59),(4.5,-.99,.79)]
wire('Projector to launch braided power loom',pts,.105,rubber,C)
for i in range(9):
    x=2.45+i*.18
    cylinder('Power loom split collar %02d'%i,(x-.04,-.75,.61),(x+.04,-.75,.61),.12,brass,C)
for x in [2.78,3.83]:
    cube('Power loom retaining saddle',(x,-.75,.64),(.13,.31,.27),dark,C,.025)
    bolt('Retaining saddle bolt',x,-.93,.7,C)
wire('Rear auxiliary cable',[(4.45,2.04,1.65),(4.12,1.77,1.82),(3.73,1.3,1.76),(3.61,.77,1.51)],.07,rubber,C)

P='06 Projector'
cube('Projector mounting foot',(3.09,.72,1.36),(2.17,1.6,.24),dark,P,.06)
base=chamfer('Projector full rear body',3.1,1.72,2.05,.9,.21,-.1,1.65,tan,P,.055)
chamfer('Projector front service plate',3.1,1.56,1.65,.4,.12,-.16,-.08,panel,P,.018)
for x in [2.44,3.76]:bolt('Projector captive mounting bolt',x,-.191,1.56,P)
cube('Projector upper aperture well',(3.1,.59,2.12),(1.55,1.18,.16),black,P,.09)
cube('Projector aperture metal lip',(3.1,.59,2.17),(1.24,.9,.08),brass,P,.055)
cube('Projector frosted luminous lens',(3.1,.59,2.223),(1.06,.72,.04),emitter,P,.055)
for i in range(6):
    for x in [2.23,3.97]:cube('Projector housing side cooling rib',(x,.15+i*.22,1.87),(.1,.12,.47),dark,P,.02)
lamp('Projector warm power light',2.34,-.183,1.81,.1,.06,amber,P)

L='07 Launch control'
chamfer('Launch base chassis',5.81,.84,3.92,1.28,.28,-1.05,.93,dark,L,.06)
ring('Launch armored surround',5.81,.89,3.87,1.25,.28,3.13,.79,.19,-1.2,-.76,tan,L)
ring('Launch inner rim',5.94,.89,3.15,.83,.18,2.93,.66,.13,-1.217,-1.02,edge,L)
chamfer('Launch display recess',5.94,.89,2.96,.69,.13,-1.16,-.95,glass,L,.014)
ring('Launch fine lit border',5.94,.89,2.79,.54,.12,2.775,.525,.113,-1.18,-1.169,dim,L)
text('LAUNCH editable label','L A U N C H  ›',5.95,-1.19,.89,.245,ink,L,mono,'CENTER')
lamp('Launch ready amber',4.15,-1.239,1.03,.18,.075,amber,L)
lamp('Launch ready green',4.15,-1.239,.77,.18,.075,green,L)
for x in [4.04,7.52]:
    for z in [.44,1.3]:bolt('Launch assembly bolt',x,-1.225,z,L)

def area(name,loc,target,power,color,size,size_y=None):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.shape='RECTANGLE' if size_y else 'DISK';d.size=size
    if size_y:d.size_y=size_y
    o=bpy.data.objects.new(name,d);cols['00 Cameras and light'].objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
area('Screen amber spill',(-4.3,-1.0,4.1),(-4.3,.6,3.8),280,(1,.66,.38),4,4.7)
area('Warm front readability',(-3.2,-6.0,7.5),(0,0,2.2),410,(1,.76,.57),7)
area('Projector lens bounce',(3.1,.6,2.33),(3.6,.4,5.6),270,(1,.65,.29),1.1)
area('Cool space fill',(4.8,4.5,8.5),(1,0,2.4),420,(.25,.4,1),8)
area('Faint camera fill',(2.4,-7,5.5),(1,0,2),180,(.48,.58,.8),6)
beam_mat=bpy.data.materials.new('Warm suspended projector beam');beam_mat.use_nodes=True
bn=beam_mat.node_tree.nodes;bn.clear();bv=bn.new('ShaderNodeVolumePrincipled')
bv.inputs['Density'].default_value=.032;bv.inputs['Color'].default_value=(1,.7,.36,1)
bv.inputs['Anisotropy'].default_value=.3;bv.inputs['Emission Strength'].default_value=.006
bv.inputs['Emission Color'].default_value=(1,.57,.22,1)
bo=bn.new('ShaderNodeOutputMaterial');beam_mat.node_tree.links.new(bv.outputs['Volume'],bo.inputs['Volume'])
verts=[]
for x,y,z,rx,ry in [(3.1,.59,2.24,.5,.34),(3.65,.4,5.2,2.15,1.13)]:
    verts.extend((x+rx*math.cos(i*math.tau/48),y+ry*math.sin(i*math.tau/48),z) for i in range(48))
faces=[tuple(range(47,-1,-1)),tuple(range(48,96))]
faces.extend((i,(i+1)%48,(i+1)%48+48,i+48) for i in range(48))
mesh('Volumetric projection cone',verts,faces,beam_mat,'06 Projector',0)
spotdata=bpy.data.lights.new('Warm projector beam light','SPOT');spotdata.energy=850;spotdata.color=(1,.68,.35);spotdata.spot_size=math.radians(78);spotdata.spot_blend=.65
spot=bpy.data.objects.new('Warm projector beam light',spotdata);cols['00 Cameras and light'].objects.link(spot);spot.location=(3.1,.59,2.29);spot.rotation_euler=(Vector((3.65,.4,5.3))-spot.location).to_track_quat('-Z','Y').to_euler()

def camera(name,loc,target,lens):
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);cols['00 Cameras and light'].objects.link(o)
    o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();d.lens=lens;d.clip_end=150;return o
hero=camera('HERO - reference composition',(0,-21.5,8.3),(0,0,3.87),49)
oblique=camera('OBLIQUE - editable volumes',(13,-17,12),(0,.1,3.65),39)
s.camera=hero

skyimg=bpy.data.images.load(str(ROOT/'inputs/nebula-background.png'));skyimg.pack()
sky=mesh('Real starfield crop background',[(-14,12,-6.5),(14,12,-6.5),(14,12,9.5),(-14,12,9.5)],[(0,1,2,3)],black,'09 Space',0)
uv=sky.data.uv_layers.new(name='Crop of unobstructed original sky')
for loop,co in zip(uv.data,[(0,0),(1,0),(1,1),(0,1)]):loop.uv=co
sm=bpy.data.materials.new('Packed real starfield');sm.use_nodes=True;n=sm.node_tree.nodes;n.clear();ln=sm.node_tree.links
tex=n.new('ShaderNodeTexImage');tex.image=skyimg;e=n.new('ShaderNodeEmission');e.inputs[1].default_value=.26;ln.new(tex.outputs['Color'],e.inputs[0]);out=n.new('ShaderNodeOutputMaterial');ln.new(e.outputs[0],out.inputs['Surface']);sky.data.materials.clear();sky.data.materials.append(sm)
reference=bpy.data.images.load(str(ROOT/'reference/owner-target.png'));reference.pack()
ref=bpy.data.objects.new('REFERENCE C - packed image, hidden',None);ref.empty_display_type='IMAGE';ref.data=reference;ref.empty_display_size=15;cols['10 Hidden reference'].objects.link(ref);ref.location=(0,14,4);ref.rotation_euler=(math.pi/2,0,0);ref.hide_render=True;ref.hide_viewport=True

shipfile=ROOT/'inputs/hologram-vanguard.blend'
if shipfile.exists():
    with bpy.data.libraries.load(str(shipfile),link=False) as (src,dst):
        dst.collections=[n for n in src.collections if n=='HologramVanguard']
    for c in dst.collections:
        if c:s.collection.children.link(c)
    bpy.data.objects['HologramVanguardRoot'].location=(3.1,.4,5.5)
    bpy.data.objects['HologramVanguardRoot'].scale=(1.15,1.15,1.15)
    bpy.data.objects['HologramVanguardRoot'].rotation_euler.x=math.radians(34)
    hn=bpy.data.materials['HOLO / muted red and grey panels'].node_tree.nodes
    hn['Front opacity / back opacity'].inputs['To Min'].default_value=.83
    hn['Rim gain'].inputs[1].default_value=.04
    hn['Scanline contrast'].inputs['To Min'].default_value=.94

nt=bpy.data.node_groups.new('Warm electronics bloom','CompositorNodeTree')
s.compositing_node_group=nt
nt.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
rl=nt.nodes.new('CompositorNodeRLayers');gl=nt.nodes.new('CompositorNodeGlare')
gl.inputs['Type'].default_value='Fog Glow';gl.inputs['Quality'].default_value='High'
gl.inputs['Threshold'].default_value=1.4;gl.inputs['Size'].default_value=.35
nt.links.new(rl.outputs['Image'],gl.inputs['Image']);comp=nt.nodes.new('NodeGroupOutput');nt.links.new(gl.outputs['Image'],comp.inputs['Image'])

guide=bpy.data.texts.new('START HERE - editable 3D console')
guide.write('''REFERENCE C / MEDIUM DETAIL 3D CONSOLE\n\nAll console housings, deck, connectors, fins and cable looms have real depth.\nNative text stays editable in 02 Editable display and 07 Launch control.\nCollection roots group the mechanical assemblies. Bevel modifiers stay live.\nHERO is the reference composition. OBLIQUE shows depth and rear housings.\nThe packed reference image is hidden in 10 Hidden reference.\nThe starfield uses the packed existing NebulaBackground-v2 image.\nThe true 3D Vanguard is banked at 34 degrees above the projector.\nThis is reference modeling placeholder copy, not a game UI specification.\n\nAfter the owner edits this blend, this file is the source of record.\nThe builder refuses to overwrite an existing blend.\n''')
s['Reference']='Image C, full 3D study for owner edits'
s['Source policy']='Preserve owner edits; builder cannot overwrite existing blend'
def place_controls():
    pivots={'Main console':(-4.3,.2,4.1),'Editable screen content':(-4.3,-.48,4.21),'Deck':(0,.6,.2),
            'Electronics':(2.3,1.1,1.3),'Cable looms':(0,-.3,.8),'Projector':(3.1,.59,1.7),
            'Launch control':(5.81,-.5,.89),'Hologram':(3.1,.4,5.5)}
    for name,center in pivots.items():
        parent=bpy.data.objects[name]
        children=[(child,child.matrix_world.copy()) for child in parent.children]
        parent.matrix_world=Matrix.Translation(center)
        bpy.context.view_layer.update()
        for child,world in children:child.matrix_world=world
    links=[('Editable screen content','Main console'),('Screen amber spill','Main console'),
           ('Electronics','Deck'),('Cable looms','Deck'),('Projector','Deck'),('Launch control','Deck'),
           ('Hologram','Projector'),('HologramVanguardRoot','Hologram'),
           ('Projector lens bounce','Projector'),('Warm projector beam light','Projector')]
    for child_name,parent_name in links:
        child=bpy.data.objects.get(child_name)
        if child is None:continue
        world=child.matrix_world.copy();child.parent=bpy.data.objects[parent_name];child.matrix_world=world
    bpy.context.view_layer.update()
place_controls()
bpy.context.view_layer.update()
for o in bpy.context.selected_objects:o.select_set(False)
bpy.context.view_layer.objects.active=None
for scr in bpy.data.screens:
    for a in scr.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.shading.type='MATERIAL'
            a.spaces.active.overlay.show_extras=False
s.render.filepath=str(ROOT/(OUT.stem+'-hero.png' if opts.preview else 'hero.png'))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT))
bpy.ops.render.render(write_still=True)
if not opts.preview:
    s.camera=oblique
    s.render.engine='BLENDER_WORKBENCH'
    s.compositing_node_group=None
    s.display.shading.light='STUDIO'
    s.display.shading.color_type='MATERIAL'
    s.display.shading.show_shadows=True
    s.display.shading.show_cavity=True
    s.display.shading.cavity_type='BOTH'
    s.display.shading.curvature_ridge_factor=1.4
    s.display.shading.curvature_valley_factor=1.1
    s.display.shading.background_type='WORLD'
    s.world.color=(.07,.07,.07)
    sky.hide_render=True
    bpy.data.objects['Volumetric projection cone'].hide_render=True
    s.render.filepath=str(ROOT/'oblique.png')
    bpy.ops.render.render(write_still=True)
print(json.dumps({'blend':str(OUT),'render':s.render.filepath,'objects':len(s.objects)}))
