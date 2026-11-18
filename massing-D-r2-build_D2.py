"""Cockpit D, round 2: folded dash and deep, canted housings.

Run locally: blender -b --python out/build_D2.py
Use -- --preview for the fixed camera only; -- --check for geometry checks only.
All faces are modelled explicitly. No textures, text or bevel modifiers.
"""
from pathlib import Path
import math
import sys
import os
import json
import warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)

# DESIGN CONSTANTS -- Blender units, Z up. Angles are degrees.
POD_YAW_DEG = 58.0                 # mirrored; normals turn towards centre
POD_LEAN_DEG = 40.0                # back from vertical
POD_BANK_DEG = 20.0                # mirrored; outer upper corners rise
LOWER_SCREEN_BREAK_DEG = 8.0       # additional back lean, about banked width axis
POD_DEPTH = 2.15                   # horizontal depth behind face, in yaw direction
POD_SHOULDER_DEPTH = 0.62
POD_SHOULDER_EXPANSION = 0.38
POD_INNER_SHOULDER_FLARE = 0.95
LEFT_POD_HINGE = (-5.95, 0.25, 2.45)
RIGHT_POD_HINGE = (5.95, 0.25, 3.15)
LEFT_POD_WIDTH = 5.35
RIGHT_POD_WIDTH = 5.95
SHIP_SCREEN_SIZE = (4.25, 3.50)
DETAIL_SCREEN_SIZE = (3.76, 0.92)
PRIMARY_SCREEN_SIZE = (4.25, 1.60)
ORDNANCE_SCREEN_SIZE = (4.25, 1.60)
SCREEN_CHAMFER = 0.20
SCREEN_RECESS = 0.025
SCREEN_HINGE_GAP = 0.40
DASH_RAKE_DEG = 20.0
DASH_FRONT_Y = -0.95
DASH_FRONT_Z = 0.64
DASH_BACK_Y = 1.90
SHELF_HEIGHT = 1.12               # at front edge
SHELF_FRONT_Y = -3.05
SHELF_BACK_Y = -1.40
SHELF_RAKE_DEG = -3.0             # slight fall away from pilot
CENTRE_SHELF_HALF_WIDTH = 3.70
# Wing stations: x, forward offset, lifts at shelf-front/shelf-back/break/dash-rear.
WING_STATIONS = [
    (-9.0,-.15,(1.35,.70,.25,.10)),(-6.9,-.35,(1.25,.50,.10,0)),
    (-3.7,0,(0,0,0,0)),(0,0,(0,0,0,0)),(3.7,0,(0,0,0,0)),
    (6.9,-.55,(1.40,.70,.25,.20)),(9.0,-.20,(1.65,.85,.45,.30)),
]
STRUT_LANDING_POINTS = ((-3.20, 1.65, 1.68), (3.20, 1.65, 1.68))
STRUT_TOP_POINTS = ((-7.10, 4.15, 10.05), (7.10, 4.15, 10.05))
STRUT_WIDTH = 0.74
STRUT_DEPTH = 1.00
HEADER_WIDTH = 24.8
HEADER_TOP_Z = 10.62
HEADER_HEIGHT = 1.05
SIDE_PILLAR_WIDTH = 1.15
PROJECTOR_LOCATION = (0.0, 0.0, 1.25)
HOLOGRAM_CENTRE = (0.0, 0.0, 4.50)
HOLOGRAM_CLEAR_SIZE = (5.0, 4.0, 4.0)
CAMERA_POSITION = (0.0, -18.0, 6.8)
CAMERA_DIRECTION = (0.0, 0.9936, -0.1132)
CAMERA_VERTICAL_FOV_DEG = 34.11
RESOLUTION = (1920, 1080)
RENDER_SAMPLES = 32
OUT = Path(__file__).resolve().parent
ROOT = OUT.parent
os.environ['OPTIX_CACHE_PATH'] = str(OUT / '.cache/optix')
POSTPROCESS_PYTHON = Path('C:/Users/amind/Miniconda3/python.exe')


def postprocess():
    from PIL import Image, ImageDraw, ImageFont
    sources = [ROOT/'refs/picked-sketch-D.png', ROOT/'round1/D-camera.png', OUT/'D2-camera.png']
    sheet = Image.new('RGB', (3880, 810), '#111b29')
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 25)
    small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
    titles = ['PICKED SKETCH D', 'ROUND 1', 'ROUND 2 / PLANES AND ANGLES']
    subtitles = ['Direction', 'Camera-facing slabs', 'Canted housings / folded dash / landed struts']
    for i, (path, title, sub) in enumerate(zip(sources, titles, subtitles)):
        x = 10+i*1290
        im = Image.open(path).convert('RGB').resize((1280,720), Image.Resampling.LANCZOS)
        sheet.paste(im, (x,80))
        d.text((x+12,12),title,fill='#e0e8ef',font=font)
        d.text((x+12,46),sub,fill='#92a9bc',font=small)
    sheet.save(OUT/'D2-compare.png')

if '--postprocess' in sys.argv:
    postprocess()
    raise SystemExit

import bpy
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view
import subprocess

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 20 if '--preview' in sys.argv else RENDER_SAMPLES
scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'
scene.render.threads = 10
scene.render.resolution_x, scene.render.resolution_y = RESOLUTION
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'
scene.world = bpy.data.worlds.new('D2 | quiet blue ambient')
scene.world.use_nodes = True
world_bg = scene.world.node_tree.nodes.get('Background')
world_bg.inputs['Color'].default_value = (.09,.12,.17,1)
world_bg.inputs['Strength'].default_value = .30


def material(name, rgb, emission=0, pure=False):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb,1)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb,1)
    p.inputs['Roughness'].default_value = 1
    p.inputs['Specular IOR Level'].default_value = .05
    if emission:
        p.inputs['Emission Color'].default_value = (*rgb,1)
        p.inputs['Emission Strength'].default_value = emission
    if pure:
        nt.nodes.clear()
        e = nt.nodes.new('ShaderNodeEmission')
        e.inputs[0].default_value = (*rgb,1)
        e.inputs[1].default_value = emission or 1
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(e.outputs[0],out.inputs[0])
    return m

body = material('01 Structure / ink blue',(.019,.029,.045))
face = material('02 Front planes / charcoal blue',(.040,.057,.078))
shoulder = material('03 Shoulder planes / slate',(.073,.096,.124))
side = material('04 Inner return planes',(.037,.055,.078))
top_mat = material('05 Upper planes',(.092,.119,.153))
dark = material('06 Recess and joints',(.006,.011,.019))
screen_mat = material('07 Screens / cool blue',(.023,.105,.222),.8,pure=True)
cyan = material('08 Small blue inserts',(.035,.48,.72),1.65,pure=True)
orange = material('09 Launch / orange',(.95,.20,.018),1,pure=True)
key_mat = material('10 Control blocks',(.13,.17,.21))
back_mat = material('11 Window / flat night blue',(.005,.013,.029),1,pure=True)
palette = [body,face,shoulder,side,top_mat,dark]

def transparent_material(name, colour, opacity):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*colour,opacity)
    m.use_nodes=True
    nt=m.node_tree; nt.nodes.clear()
    mix=nt.nodes.new('ShaderNodeMixShader'); mix.inputs[0].default_value=opacity
    t=nt.nodes.new('ShaderNodeBsdfTransparent')
    e=nt.nodes.new('ShaderNodeEmission'); e.inputs[0].default_value=(*colour,1)
    o=nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(t.outputs[0],mix.inputs[1]); nt.links.new(e.outputs[0],mix.inputs[2]); nt.links.new(mix.outputs[0],o.inputs[0])
    return m

holo_mat = transparent_material('12 Hologram / translucent mass',(.025,.38,.62),.20)
fan_mat = transparent_material('13 Projector / faint fan',(.02,.3,.5),.025)
holo_line = material('14 Hologram / edges',(.055,.50,.78),1.25,pure=True)

def collection(name):
    c=bpy.data.collections.new(name); scene.collection.children.link(c); return c
shell=collection('01 | Continuous cockpit structure')
pods=collection('02 | Deep folded pod housings')
screens_col=collection('03 | Exact size screens')
controls=collection('04 | Control blocks')
hologram=collection('05 | Hologram placeholder')
stage=collection('06 | Cameras and lights')
guides=collection('07 | Clearance guide - hidden')

def mesh(name, verts, faces, mat=body, col=shell, indices=None):
    data=bpy.data.meshes.new(name)
    data.from_pydata([tuple(v) for v in verts],[],faces); data.update()
    obj=bpy.data.objects.new(name,data); col.objects.link(obj)
    for m in (mat if isinstance(mat,list) else [mat]): data.materials.append(m)
    if indices:
        for p,i in zip(data.polygons,indices): p.material_index=i
    return obj

def cube(name,loc,size,mat=body,col=shell):
    x,y,z=[d/2 for d in size]
    vs=[Vector(loc)+Vector((i*x,j*y,k*z)) for k in [-1,1] for j in [-1,1] for i in [-1,1]]
    return mesh(name,vs,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],mat,col)

def chamfer(w,h,c):
    return [(-w/2+c,-h/2),(w/2-c,-h/2),(w/2,-h/2+c),(w/2,h/2-c),
            (w/2-c,h/2),(-w/2+c,h/2),(-w/2,h/2-c),(-w/2,-h/2+c)]

def point(b,x=0,y=0,d=0): return b[0]+b[1]*x+b[2]*y+b[3]*d
def shifted(b,x=0,y=0,d=0): return (point(b,x,y,d),*b[1:])

def prism(name, outline, front, back, b, mat=body,col=shell):
    vs=[point(b,x,y,d) for d in [front,back] for x,y in outline]
    n=len(outline)
    fs=[tuple(range(n)),tuple(range(2*n-1,n-1,-1))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,mat,col)

def ring(name,outer,inner,b,outer_d=.05,inner_d=0,mat=shoulder,col=pods):
    vs=[point(b,x,y,d) for loop,d in [(outer,outer_d),(inner,inner_d)] for x,y in loop]
    n=len(inner)
    return mesh(name,vs,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat,col)

def beam(name,a,b,width,depth,mat=palette,col=shell):
    a,b=Vector(a),Vector(b)
    along=(b-a).normalized()
    u=Vector((1,0,0)); u=(u-along*u.dot(along)).normalized()
    v=along.cross(u)
    outline=chamfer(width,depth,.12)
    vs=[p+u*x+v*y for p in [a,b] for x,y in outline]
    fs=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    return mesh(name,vs,fs,mat,col,[0,0,3,2,1,2,0,2,3,2] if isinstance(mat,list) else None)

def cylinder(name,loc,radius,depth,mat,col=controls,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    o=bpy.context.object; o.name=name; o.data.materials.append(mat)
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o); return o

def pod_basis(origin,hand):
    a,p,b=map(math.radians,[-hand*POD_YAW_DEG,POD_LEAN_DEG,hand*POD_BANK_DEG])
    u=Vector((math.cos(a),math.sin(a),0))
    v=Vector((-math.sin(a)*math.sin(p),math.cos(a)*math.sin(p),math.cos(p)))
    u,v=u*math.cos(b)+v*math.sin(b),-u*math.sin(b)+v*math.cos(b)
    n=u.cross(v)
    upper=(Vector(origin),u,v,n)
    q=Matrix.Rotation(math.radians(-LOWER_SCREEN_BREAK_DEG),3,u)
    lower=(Vector(origin),u,q@v,q@n)
    back=Vector((-math.sin(a),math.cos(a),0))
    return upper,lower,back

screen_records=[]
control_points={}

def add_screen(name,size,b,x,y):
    w,h=size; b=shifted(b,x,y)
    inner=chamfer(w,h,SCREEN_CHAMFER)
    # The exact blue outline is fully exposed; the aperture opens away from it.
    ring(name+' | aperture slope',chamfer(w+.32,h+.32,.29),inner,b,.06,-SCREEN_RECESS,face)
    ring(name+' | thin dark reveal',chamfer(w+.055,h+.055,.215),inner,b,-.006,-SCREEN_RECESS,dark)
    obj=mesh(name+' | screen', [point(b,u,v,-SCREEN_RECESS) for u,v in inner], [tuple(range(8))],screen_mat,screens_col)
    obj['true_width']=w; obj['true_height']=h
    screen_records.append((name,size,b,obj))
    return b

pod_bases={}
for hand,name,origin,width,upper_size,lower_size in [
    (-1,'Left',LEFT_POD_HINGE,LEFT_POD_WIDTH,SHIP_SCREEN_SIZE,DETAIL_SCREEN_SIZE),
    (1,'Right',RIGHT_POD_HINGE,RIGHT_POD_WIDTH,PRIMARY_SCREEN_SIZE,ORDNANCE_SCREEN_SIZE)]:
    upper,lower,back=pod_basis(origin,hand)
    pod_bases[name]=(upper,lower,back)
    top=upper_size[1]+SCREEN_HINGE_GAP/2+.38
    bottom=-lower_size[1]-SCREEN_HINGE_GAP/2-.40
    profile=[(x,y+(top+bottom)/2) for x,y in chamfer(width,top-bottom,.45)]
    def bend(x,y,d=0): return point(upper if y>=0 else lower,x,y,d)
    front=[bend(x,y,-.10) for x,y in profile]
    # A widening shoulder and a horizontal rear extrusion: the cap is a real
    # top plane, rather than a slab extruded normal to a tilted screen.
    inner_indices=[2,3] if hand<0 else [6,7]
    mid=[bend(x+math.copysign(POD_SHOULDER_EXPANSION,x),y,-.10)+back*POD_SHOULDER_DEPTH+Vector((-hand*POD_INNER_SHOULDER_FLARE if i in inner_indices else 0,0,.17)) for i,(x,y) in enumerate(profile)]
    rear=[p+back*(POD_DEPTH-POD_SHOULDER_DEPTH) for p in mid]
    vs=front+mid+rear
    fs=[]; ids=[]
    for layer in [0,1]:
        for i in range(8):
            j=(i+1)%8
            fs.append((layer*8+i,layer*8+j,(layer+1)*8+j,(layer+1)*8+i))
            ids.append([3,2,3,2,4,2,1,2][i])
    fs.append(tuple(range(23,15,-1))); ids.append(0)
    housing=mesh(name+' | 2.15 deep chamfered housing',vs,fs,palette,pods,ids)
    housing['horizontal_depth']=POD_DEPTH
    # Two front carrier planes joined exactly at the hinge.
    for label,b,lo,hi in [('upper',upper,0,top),('lower',lower,bottom,0)]:
        carrier=([(-width/2,lo),(width/2,lo),(width/2,hi-.45),(width/2-.45,hi),(-width/2+.45,hi),(-width/2,hi-.45)] if label=='upper' else [(-width/2,lo+.45),(-width/2+.45,lo),(width/2-.45,lo),(width/2,lo+.45),(width/2,hi),(-width/2,hi)])
        prism(name+' | '+label+' carrier',carrier,-.11,-.28,b,face,pods)
    xshift=.26 if hand>0 else 0
    s1=add_screen('SHIP' if hand<0 else 'PRIMARY',upper_size,upper,xshift,upper_size[1]/2+SCREEN_HINGE_GAP/2)
    s2=add_screen('DETAIL' if hand<0 else 'ORDNANCE',lower_size,lower,xshift,-lower_size[1]/2-SCREEN_HINGE_GAP/2)
    # Lower housing grows into a broad dash root, not a post or a monitor stand.
    bottom_points=[bend(-width/2+.45,bottom,-.12),bend(width/2-.45,bottom,-.12)]
    root=[]
    for p0 in bottom_points+[p+back*POD_DEPTH for p in bottom_points]:
        # Extend each foot down into the continuous wing rather than suspend it.
        p=p0+Vector((-p0.x*.055,-.28,0))
        landing_z=min(p0.z-.15, 1.18)
        root.append(Vector((p.x,p.y,landing_z)))
    mesh(name+' | widening dash root',bottom_points+[p+back*POD_DEPTH for p in bottom_points]+root,
         [(0,1,3,2),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3),(4,6,7,5)],palette,pods,[1,3,0,2,2,0])
    accent=shifted(upper,hand*(width/2-.08),.8,.02) if hand<0 else shifted(upper,0,top-.10,.02)
    accent_size=(.075,.66) if hand<0 else (.70,.065)
    prism(name+' | edge insert',chamfer(*accent_size,.018),0,-.015,accent,cyan,pods)
    if hand>0:
        for label,sb in [('PRIMARY',s1),('ORDNANCE',s2)]:
            for action,dv in [('PREVIOUS',.29),('NEXT',-.29)]:
                kb=shifted(sb,2.48,dv,.02)
                prism(label+' '+action,chamfer(.39,.40,.06),.10,0,kb,key_mat,controls)
                control_points[label+' '+action]=point(kb,d=.10)

# The continuous dash is one mesh with an articulated cross-section. The centre
# is low enough for the fixed projector; the wings lift into the pod roots.
shelf_back_z=SHELF_HEIGHT+(SHELF_BACK_Y-SHELF_FRONT_Y)*math.tan(math.radians(SHELF_RAKE_DEG))
dash_back_z=DASH_FRONT_Z+(DASH_BACK_Y-DASH_FRONT_Y)*math.tan(math.radians(DASH_RAKE_DEG))
sections=[(SHELF_FRONT_Y,SHELF_HEIGHT),(SHELF_BACK_Y,shelf_back_z),
          (DASH_FRONT_Y,DASH_FRONT_Z),(DASH_BACK_Y,dash_back_z)]
# Outboard columns wrap forward/up toward the pods.
columns=WING_STATIONS
vs=[]
for x,yoff,zoff in columns:
    for j,(y,z) in enumerate(sections):
        # Outboard shelf rises and wraps forward under each slanted pod foot.
        vs.append((x,y+yoff,z+zoff[j]))
fs=[];ids=[]
for i in range(len(columns)-1):
    for j in range(3):
        q=(i*4+j,(i+1)*4+j,(i+1)*4+j+1,i*4+j+1)
        if i in [2,3]:
            fs.append(q);ids.append([2,5,1][j])
        else:
            fs.extend([(q[0],q[1],q[2]),(q[0],q[2],q[3])]);ids.extend([[2,3,1][j]]*2)
# A visible front fascia and lower kick-back complete the third large plane.
for i in range(len(columns)):
    p=Vector(vs[i*4]); vs.append(tuple(p+Vector((0,.13,-.30))))
    vs.append(tuple(p+Vector((0,.50,-.78))))
offset=4*len(columns)
for i in range(len(columns)-1):
    fs.append((i*4,offset+i*2,offset+(i+1)*2,(i+1)*4));ids.append(3)
    fs.append((offset+i*2,offset+i*2+1,offset+(i+1)*2+1,offset+(i+1)*2));ids.append(0)
for i in [0,len(columns)-1]:
    fs.append((i*4,i*4+1,i*4+2,i*4+3,offset+i*2+1,offset+i*2));ids.append(0)
dash=mesh('Dash | unbroken folded surface and rising wings',vs,fs,palette,shell,ids)
# Explicit triangles preserve the intended wing facets instead of implicit warped quads.
# Centre strips remain planar quads.
for poly in dash.data.polygons: poly.use_smooth=False

# Upper dash shoulders meet the struts forward of the window sill.
for hand,name in [(-1,'Left'),(1,'Right')]:
    # Strong triangular buttress between central rake and inboard pod housing.
    upper,lower,back=pod_bases[name]
    inner=point(lower,-hand*(LEFT_POD_WIDTH/2 if hand<0 else RIGHT_POD_WIDTH/2),-.5,-.15)
    a=Vector((hand*3.10,-.55,.88)); b=Vector((hand*3.10,1.85,1.68))
    c=Vector((hand*4.50,1.75,2.05)); d=inner+Vector((0,.15,-.10))
    mesh(name+' | dash to inner shoulder buttress',[a,b,c,d,a+Vector((0,0,-.35)),d+Vector((0,0,-.4))],
         [(0,1,2),(0,2,3),(0,3,5,4),(1,4,5,3,2)],palette,shell,[4,2,3,0])
    foot=STRUT_LANDING_POINTS[0 if hand<0 else 1]
    beam(name+' | strut landing shoe',(foot[0],foot[1],foot[2]-.35),(foot[0],foot[1]+.04,foot[2]+.24),.98,1.10,palette)
    # Accent lies on the raked dash, not on a floating strip.
    rake=math.radians(DASH_RAKE_DEG)
    db=(Vector((hand*2.50,.85,DASH_FRONT_Z+(.85-DASH_FRONT_Y)*math.tan(rake)+.015)),Vector((1,0,0)),Vector((0,math.cos(rake),math.sin(rake))),Vector((0,-math.sin(rake),math.cos(rake))))
    prism(name+' | dash insert',chamfer(.76,.07,.02),0,-.02,db,cyan)

# Low faceted projector pedestal, nested into the raked upper dash.
base=chamfer(1.85,1.52,.24); cap=chamfer(1.14,.95,.17)
pv=[(x,y,DASH_FRONT_Z+(y-DASH_FRONT_Y)*math.tan(math.radians(DASH_RAKE_DEG))-.015) for x,y in base]
pv += [(x,y,1.16) for x,y in cap]
mesh('Projector | tapered pedestal on rake',pv,[tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],palette,shell,[4,2,3,1,3,1,3,2,3])
cylinder('Projector | dark rim',(0,0,1.18),.55,.14,dark)
cylinder('PROJECTOR LENS',PROJECTOR_LOCATION,.43,.025,cyan)
control_points['PROJECTOR LENS']=Vector(PROJECTOR_LOCATION)+Vector((0,0,.0125))

a=math.radians(SHELF_RAKE_DEG)
shelf_basis=(Vector((0,-2.20,SHELF_HEIGHT+(-2.20-SHELF_FRONT_Y)*math.tan(a))),Vector((1,0,0)),Vector((0,math.cos(a),math.sin(a))),Vector((0,-math.sin(a),math.cos(a))))
prism('Launch | dark socket',chamfer(3.15,1.28,.24),.09,.005,shelf_basis,dark,controls)
prism('LAUNCH',chamfer(2.83,1.02,.20),.14,.085,shelf_basis,orange,controls)
control_points['LAUNCH']=point(shelf_basis,d=.14)
for label,x in [('SHIP PREVIOUS',-3.08),('SHIP NEXT',-2.28)]:
    kb=shifted(shelf_basis,x,.03,.02)
    prism(label,chamfer(.60,.96,.075),.18,0,kb,key_mat,controls)
    control_points[label]=point(kb,d=.18)
cylinder('Selector | socket',(2.65,-2.14,SHELF_HEIGHT+.05),.54,.13,dark)
cylinder('SELECTOR KNOB',(2.65,-2.14,SHELF_HEIGHT+.28),.45,.36,key_mat)
control_points['SELECTOR KNOB']=Vector((2.65,-2.14,SHELF_HEIGHT+.46))

# Canopy: wide raked ribs on actual forward dash shoulders; a thick sill behind.
struts=[]
for name,lo,hi in zip(['Left','Right'],STRUT_LANDING_POINTS,STRUT_TOP_POINTS):
    struts.append(beam(name+' | landed canopy rib',lo,hi,STRUT_WIDTH,STRUT_DEPTH))
    lo,hi=Vector(lo),Vector(hi); axis=(hi-lo).normalized()
    p=lo+(hi-lo)*.83+Vector((0,-.49,0))
    beam(name+' | rib blue insert',p-axis*.30,p+axis*.30,.075,.035,cyan)
beam('Window | heavy rear sill',(-11,3.65,1.30),(11,3.65,1.30),.8,.9,palette)

# Side pillars have three stations: the lower knees physically step in and forward.
for hand,name in [(-1,'Left'),(1,'Right')]:
    stations=[(hand*9.90,-.10,.35),(hand*10.45,1.15,4.90),(hand*11.85,4.05,10.30)]
    for i in range(2): beam(name+' | stepped side pillar '+str(i+1),stations[i],stations[i+1],SIDE_PILLAR_WIDTH,1.30)
    beam(name+' | outer pod bolster',(hand*9.25,.3,.25),(hand*10.2,1.6,3.05),1.3,1.8)

# Extruded hexagonal header section: front face, deep top, sloped underside.
cross=[(3.35,HEADER_TOP_Z),(4.75,HEADER_TOP_Z),(4.75,HEADER_TOP_Z-HEADER_HEIGHT+.13),(3.90,HEADER_TOP_Z-HEADER_HEIGHT),(3.35,HEADER_TOP_Z-HEADER_HEIGHT+.30)]
hv=[(x,y,z) for x in [-HEADER_WIDTH/2,HEADER_WIDTH/2] for y,z in cross]
mesh('Canopy | header with chamfered underside',hv,[tuple(reversed(range(5))),tuple(range(5,10))]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)],palette,shell,[0,0,0,0,3,2,1])
for x in [-8.6,0,8.6]:
    cube('Header | small blue insert',(x,3.335,9.99),(.90,.025,.065),cyan)

# Translucent, genuinely three-dimensional fighter placeholder.
rotation=Matrix.Rotation(math.radians(38),3,'X') @ Matrix.Rotation(math.radians(-12),3,'Z')
ship_origin=Vector((0,.05,4.80))
def sp(p): return ship_origin+rotation@Vector(p)
outline=[(0,-2.0),(.43,-1.45),(.55,-.38),(2.08,.15),(2.12,.72),(.70,.43),(.62,1.65),(.28,1.30),(0,1.54),(-.28,1.30),(-.62,1.65),(-.70,.43),(-2.12,.72),(-2.08,.15),(-.55,-.38),(-.43,-1.45)]
k=len(outline)
sv=[sp((x,y,z)) for z in [-.08,.08] for x,y in outline]
sf=[tuple(reversed(range(k))),tuple(range(k,2*k))]+[(i,(i+1)%k,(i+1)%k+k,i+k) for i in range(k)]
ship=mesh('Hologram | swept wing envelope',sv,sf,holo_mat,hologram)

def line(name,points,radius=.008):
    d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=0
    s=d.splines.new('POLY');s.points.add(len(points)-1)
    for p,co in zip(s.points,points): p.co=(*co,1)
    o=bpy.data.objects.new(name,d);hologram.objects.link(o);d.materials.append(holo_line)

line('Hologram | wing perimeter',[sp((x,y,.085)) for x,y in outline+[outline[0]]])
fuselage=[(0,-1.95,.10),(-.40,-.70,.12),(-.32,.85,.12),(0,1.50,.12),(.32,.85,.12),(.40,-.70,.12),(0,-.52,.58),(0,.72,.46)]
ff=[(0,1,6),(1,2,7,6),(2,3,7),(3,4,7),(4,5,6,7),(5,0,6)]
mesh('Hologram | raised fuselage',[sp(p) for p in fuselage],ff,holo_mat,hologram)
for path in [[0,6,7,3],[0,1,2,3,4,5,0]]:line('Hologram | fuselage ridge',[sp(fuselage[i]) for i in path])
for hand in [-1,1]:
    fin=[(hand*.61,.25,.08),(hand*.61,1.43,.08),(hand*.68,1.12,.66)]
    mesh('Hologram | tail fin',[sp(p) for p in fin],[(0,1,2)],holo_mat,hologram)
    line('Hologram | fin edges',[sp(p) for p in fin+[fin[0]]])
mesh('Projector | faint light planes',[(0,0,1.27),(-1.15,.15,3.4),(1.15,.15,3.4),(0,0,1.27),(-.3,-.8,3.3),(.3,.8,3.3)],[(0,1,2),(3,4,5)],fan_mat,hologram)
clear=cube('Guide | 5 x 4 x 4 hologram clear volume',HOLOGRAM_CENTRE,HOLOGRAM_CLEAR_SIZE,side,guides)
clear.hide_render=True;clear.hide_set(True);clear.display_type='WIRE'
backdrop=cube('Window | flat blue space',(0,9,6),(100,.1,60),back_mat,stage)

def camera(name,loc,target=None,ortho=None):
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);stage.objects.link(o);o.location=loc
    direction=Vector(target)-Vector(loc) if target else Vector(CAMERA_DIRECTION)
    o.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
    if ortho:d.type='ORTHO';d.ortho_scale=ortho
    else:
        d.sensor_fit='VERTICAL';d.sensor_height=24
        d.lens=24/(2*math.tan(math.radians(CAMERA_VERTICAL_FOV_DEG)/2))
    d.clip_end=250
    return o

gamecam=camera('D2 | FIXED GAME CAMERA',CAMERA_POSITION)
gamecam.lock_location=(True,True,True);gamecam.lock_rotation=(True,True,True)
gamecam['direction']=str(CAMERA_DIRECTION);gamecam['vertical_fov_degrees']=CAMERA_VERTICAL_FOV_DEG
threecam=camera('D2 | three quarter',(18,-25,18),(0,1,4.6),30)
topcam=camera('D2 | TOP orthographic',(0,0,35),(0,0,0),29)
sidecam=camera('D2 | SIDE orthographic',(32,0,5),(0,0,5),25)

def area(name,loc,power,size,target):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);stage.objects.link(o);o.location=loc
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Key | upper left',(-8,-7,13),2200,7,(0,1,2.5))
area('Fill | weak right',(9,-5,7),390,9,(0,1,3))

bpy.context.view_layer.update()
subject=[o.matrix_world@Vector(c) for col in [shell,pods,screens_col,controls,hologram] for o in col.objects for c in o.bound_box]
for cam in [threecam,topcam,sidecam]:
    inv=cam.matrix_world.inverted();local=[inv@p for p in subject]
    xmin,xmax=min(p.x for p in local),max(p.x for p in local)
    ymin,ymax=min(p.y for p in local),max(p.y for p in local)
    cam.location+=cam.matrix_world.to_3x3()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0))
    cam.data.ortho_scale=max((xmax-xmin)/.89,(ymax-ymin)*(16/9)/.89)
bpy.context.view_layer.update()
scene.camera=gamecam

def projected(p):
    c=world_to_camera_view(scene,gamecam,Vector(p));return Vector((c.x*1920,(1-c.y)*1080))

report={'camera':{'position':CAMERA_POSITION,'direction':CAMERA_DIRECTION,'vertical_fov':CAMERA_VERTICAL_FOV_DEG},
        'pod_angles':{'left':{'yaw':POD_YAW_DEG,'lean':POD_LEAN_DEG,'bank':-POD_BANK_DEG},'right':{'yaw':-POD_YAW_DEG,'lean':POD_LEAN_DEG,'bank':POD_BANK_DEG}},
        'screens':{},'controls':{},'strut_landings':STRUT_LANDING_POINTS,'pod_depth':POD_DEPTH}
deps=bpy.context.evaluated_depsgraph_get()
errors=[]
for name,size,b,obj in screen_records:
    centre=point(b,d=-SCREEN_RECESS);eye=(gamecam.location-centre).normalized()
    wr=math.sqrt(1-b[1].dot(eye)**2);hr=math.sqrt(1-b[2].dot(eye)**2)
    coords=[tuple(projected(v.co)) for v in obj.data.vertices]
    screen_data={'size':size,'width_retention':wr,'height_retention':hr,'centre':list(centre),'projected_corners':coords,
                 'width_centreline_pixels':(projected(centre+b[1]*size[0]/2)-projected(centre-b[1]*size[0]/2)).length,
                 'height_centreline_pixels':(projected(centre+b[2]*size[1]/2)-projected(centre-b[2]*size[1]/2)).length}
    occluded=[];samples=0
    # Dense sampling includes near-outline points and the entire chamfered face.
    for ix in range(41):
        for iy in range(33):
            sx=(ix/40-.5)*(size[0]-.012);sy=(iy/32-.5)*(size[1]-.012)
            if abs(sx)>size[0]/2-SCREEN_CHAMFER and abs(sy)>size[1]/2-SCREEN_CHAMFER and (size[0]/2-abs(sx))+(size[1]/2-abs(sy))<SCREEN_CHAMFER+.002:continue
            target=centre+b[1]*sx+b[2]*sy;samples+=1
            hit,loc,norm,index,ho,matrix=scene.ray_cast(deps,gamecam.location,(target-gamecam.location).normalized())
            if not hit or ho.original!=obj:occluded.append(ho.name if ho else 'none')
    screen_data['visibility_samples']=samples;screen_data['occluded_samples']=len(occluded);screen_data['occluders']=sorted(set(occluded))
    report['screens'][name]=screen_data
    if wr<.80 or hr<.75:errors.append((name,'foreshortening',wr,hr))
    if not all(0<x<1920 and 0<y<1080 for x,y in coords):errors.append((name,'outside frame'))
    if occluded:errors.append((name,'occluded',len(occluded),sorted(set(occluded))))
    print('SCREEN',name,'width',round(wr,4),'height',round(hr,4),'occluded',len(occluded),flush=True)
for name,p in control_points.items():
    px,py=projected(p)
    hit,loc,norm,index,ho,matrix=scene.ray_cast(deps,gamecam.location,(p-gamecam.location).normalized())
    ok=hit and ho.name==name and 0<px<1920 and 0<py<1080
    report['controls'][name]={'visible':ok,'pixel':(px,py),'ray_hit':ho.name if ho else None}
    if not ok:errors.append((name,'control hidden',ho.name if ho else None))
for obj in struts:
    verts=[obj.matrix_world@v.co for v in obj.data.vertices]
    if min(abs(p.x) for p in verts)<=2.5:errors.append((obj.name,'enters hologram x slab'))
holo_vertices=[o.matrix_world@v.co for o in hologram.objects if o.type=='MESH' and o.name.startswith('Hologram') for v in o.data.vertices]
report['hologram_bounds']={'min':[min(v[a] for v in holo_vertices) for a in range(3)],'max':[max(v[a] for v in holo_vertices) for a in range(3)]}
for v in holo_vertices:
    if any(abs(v[a]-HOLOGRAM_CENTRE[a])>HOLOGRAM_CLEAR_SIZE[a]/2 for a in range(3)):errors.append(('hologram','outside clear volume',tuple(v)))
report['modifier_count']=sum(len(o.modifiers) for o in scene.objects)
assert report['modifier_count']==0
report['errors']=errors
(OUT/'measurements.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('CHECKS',errors or 'PASS',flush=True)
if '--check' in sys.argv:raise SystemExit(1 if errors else 0)
if '--preview' not in sys.argv:assert not errors, errors

def write_notes():
    rows=['| Screen | True size | Width retained | Height retained | Visibility rays |','|---|---:|---:|---:|---:|']
    for name,s in report['screens'].items():
        rows.append(f"| {name} | {s['size'][0]:.2f} x {s['size'][1]:.2f} | {s['width_retention']*100:.2f}% | {s['height_retention']*100:.2f}% | {s['visibility_samples']-s['occluded_samples']}/{s['visibility_samples']} clear |")
    text=f'''# Cockpit D / round 2

Rebuilt around the planes: inward-canted folded display faces, deep faceted shoulders, a continuous rising dash, and ribs that land on forward dash shoes. All geometry uses flat colours and explicit polygons. No textures, model text or bevel modifiers.

## Camera and angles

The fixed camera is unchanged: position {CAMERA_POSITION}, direction {CAMERA_DIRECTION}, vertical field of view {CAMERA_VERTICAL_FOV_DEG} degrees, 1920 x 1080.

Left pod: yaw +{POD_YAW_DEG:g} degrees, lean back {POD_LEAN_DEG:g} degrees, bank -{POD_BANK_DEG:g} degrees. Right pod: yaw -{POD_YAW_DEG:g} degrees, lean back {POD_LEAN_DEG:g} degrees, bank +{POD_BANK_DEG:g} degrees. Positive yaw turns the left face inward; bank raises the outer upper corners. Yaw and lean define the initial axes, then bank rotates the plane axes. Each lower screen rotates an additional {LOWER_SCREEN_BREAK_DEG:g} degrees about its banked width axis, so it kicks forward at the bottom. These are explicit local construction angles, not Blender Euler values. Housings extend {POD_DEPTH:g} units horizontally rearward, with a {POD_SHOULDER_DEPTH:g}-unit chamfered shoulder; the extrusion is horizontal so the top caps remain visible.

## Screen checks

{chr(10).join(rows)}

Retention is measured independently for each screen axis, perpendicular to the sightline from the screen centre to the fixed camera: sqrt(1 - dot(unit_axis, unit_sightline)^2). It removes distance scaling and includes yaw, lean and bank. All must retain at least 80% width and 75% height. Screen dimensions describe their full chamfered-outline bounding rectangles in their own planes. The blue polygons have the exact specified dimensions. Dense ray casts cover the blue faces, including points near each outer edge and the corner cuts; screens must also be entirely inside the frame. Detailed measurements and control visibility are in measurements.json.

## Dash and structure

The central upper dash rises {DASH_RAKE_DEG:g} degrees from horizontal, from y={DASH_FRONT_Y:g}, z={DASH_FRONT_Z:g} to y={DASH_BACK_Y:g}, z={dash_back_z:.3f}. The projector pedestal grows out of this rake and keeps its lens at {PROJECTOR_LOCATION}. A narrow recessed break connects the rake to the shelf; its slope is {math.degrees(math.atan2(DASH_FRONT_Z-shelf_back_z,DASH_FRONT_Y-SHELF_BACK_Y)):.2f} degrees. The control shelf slopes {SHELF_RAKE_DEG:g} degrees from horizontal and starts at z={SHELF_HEIGHT:g}, y={SHELF_FRONT_Y:g}. Its front fascia drops 0.30 units, at 66.57 degrees from horizontal, then a second plane kicks back 0.37 and down 0.48 units at 52.38 degrees. These are modelled faces, not shading tricks. The upper rake, break, control shelf and front fascia are continuous across the centre. Outboard columns wrap forward by up to 0.55 units, with the shelf wings rising 1.25 to 1.65 units into the slanted pod bases. The wings are explicitly triangulated into flat facets: shelf wings range from 12.20 to 30.74 degrees, recessed wing breaks from 41.24 to 62.47 degrees, and upper wings from 17.32 to 21.05 degrees to horizontal; two broad inner buttresses meet the housings and strut shoes.

The rib landing centres are {STRUT_LANDING_POINTS}; their shoes overlap the dash buttresses. The rear sill is at y=3.65, so the feet are 2.00 units forward of it. Ribs have a {STRUT_WIDTH:g} x {STRUT_DEPTH:g} chamfered cross-section, and remain entirely outside x=+/-2.5. The ship placeholder fits inside the full 5 x 4 x 4 clearance box centred at (0, 0, 4.5). Side pillars are {SIDE_PILLAR_WIDTH:g} units wide and step from x=+/-11.85, y=4.05 above to x=+/-9.90, y=-0.10 below. The header has a deep polygonal section with a sloping underside.

## Departures from the sketch

The specified 4.25 x 3.50 SHIP screen makes the left pod taller than the right. The right housing is raised at its hinge so the silhouettes stay related without changing any screen size. Lower screens fold separately to strengthen the keyboard-shelf language in the cockpit references. The exaggerated inward yaw uses the new foreshortening allowance. The inner shoulder corners flare 0.95 units toward the centre, exposing broad return faces; the deeper outer flanks remain visible. Weapon navigation keys occupy the outer rails so the landed canopy ribs cannot cover them.

The fixed lens and camera keep the projector lower in frame than the drawing. The dash rises behind and beside it, while the foreground shelf is raised from round 1 and all controls remain visible. The shallow pedestal is integrated into the upper rake. The sketch's star field and painterly surfaces are omitted; space is uniform dark blue and the fighter is a simple translucent volume. These are editable intersecting masses, not a final watertight production asset.

## Rebuild

Run `"C:\\Program Files\\Blender Foundation\\Blender 5.1\\blender.exe" -b --python out/build_D2.py` from this folder. The script rebuilds and verifies the model, saves D2.blend, renders four views, and assembles the comparison from the supplied local images using local Pillow. `-- --preview` renders only the fixed camera; `-- --check` performs geometry and visibility checks without rendering. No network is used.
'''
    (OUT/'notes.md').write_text(text,encoding='utf-8')

write_notes()
def render(cam,filename):
    scene.camera=cam;scene.render.filepath=str(OUT/filename);bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D2.blend'))
render(gamecam,'D2-camera.png')
if '--preview' not in sys.argv:
    assert not errors, errors
    backdrop.hide_render=True
    world_bg.inputs['Color'].default_value=(.19,.22,.27,1);world_bg.inputs['Strength'].default_value=.55
    for cam,filename in [(threecam,'D2-three-quarter.png'),(topcam,'D2-top.png'),(sidecam,'D2-side.png')]:render(cam,filename)
    backdrop.hide_render=False
    world_bg.inputs['Color'].default_value=(.09,.12,.17,1);world_bg.inputs['Strength'].default_value=.30
    scene.camera=gamecam;scene.render.filepath=str(OUT/'D2-camera.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D2.blend'))
    subprocess.run([str(POSTPROCESS_PYTHON),str(Path(__file__).resolve()),'--postprocess'],check=True)
print('D2 BUILD COMPLETE',flush=True)
