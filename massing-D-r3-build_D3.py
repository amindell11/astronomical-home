"""Cockpit D, round 3: draped cowl, stepped centre and mirrored canted pods.

Run locally: blender -b --factory-startup --python out/build_D3.py
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
# One pod definition, reflected in world X. Width is the face's local width.
POD_YAW_DEG = 52.0
POD_LEAN_DEG = 36.0              # back from vertical
POD_BANK_DEG = 16.0              # outer corners high; sign mirrored
LOWER_SCREEN_BREAK_DEG = 0.0      # shared face plane eliminates unequal lower slopes
POD_DEPTH = 2.15                 # horizontal rearward depth in the yaw direction
POD_SHOULDER_DEPTH = 0.62
POD_SHOULDER_EXPANSION = 0.38
POD_INNER_SHOULDER_FLARE = 0.72
POD_HALF_SPACING = 6.35
POD_ORIGIN_Y = 0.25
POD_ORIGIN_Z = 2.45
POD_WIDTH = 5.85
POD_PROFILE_TOP = 4.08
POD_PROFILE_BOTTOM = -1.52
POD_PROFILE_HEIGHT = POD_PROFILE_TOP - POD_PROFILE_BOTTOM
POD_SCREEN_INBOARD_OFFSET = 0.20
POD_ROOT_Z = 0.70
POD_ROOT_FORWARD_REACH = 0.50
SHIP_SCREEN_SIZE = (4.25, 3.50)
DETAIL_SCREEN_SIZE = (3.76, 0.92)
PRIMARY_SCREEN_SIZE = (4.25, 1.60)
ORDNANCE_SCREEN_SIZE = (4.25, 1.60)
SHIP_CENTRE_V = 1.95
DETAIL_CENTRE_V = -0.66
PRIMARY_CENTRE_V = 2.65
ORDNANCE_CENTRE_V = 0.45
SCREEN_CHAMFER = 0.20
SCREEN_RECESS = 0.025
# Cowl stations: abs(X), forward reach, and Z at front/control-back/crown/rear.
# Every exposed wing falls outward; front is always below crown.
COWL_FRONT_Y = -3.40
COWL_CONTROL_BACK_Y = -1.35
COWL_CROWN_Y = 0.60
COWL_REAR_Y = 2.35
COWL_STATIONS = [
    (0.00, 0.00, (0.86, 1.17, 1.38, 1.35)),
    (1.55, 0.00, (0.86, 1.10, 1.32, 1.30)),
    (3.40,-0.25, (0.58, 0.76, 0.98, 1.05)),
    (5.80,-0.45, (0.42, 0.64, 0.85, 0.95)),
    (9.00, 0.50, (0.36, 0.56, 0.78, 0.90)),
]
COWL_FASCIA_DEPTH = 0.30
LAUNCH_CENTRE_Y = -2.45
LAUNCH_TOP_Z = 1.20
LAUNCH_RAKE_DEG = 9.0            # rises rearward, falls toward the viewer
LAUNCH_HOUSING_WIDTH = 3.45
LAUNCH_HOUSING_LENGTH = 1.55
PEDESTAL_LOOPS = [              # width, depth, Z: real terraces and sloping faces
    (2.30, 1.95, 1.12),
    (2.20, 1.85, 1.25),
    (1.68, 1.30, 1.53),
    (1.48, 1.12, 1.53),
    (1.12, 0.94, 1.74),
]
STRUT_LANDING_POINTS = ((-3.20, 1.65, 1.06), (3.20, 1.65, 1.06))
STRUT_TOP_POINTS = ((-7.10, 4.15, 10.05), (7.10, 4.15, 10.05))
STRUT_WIDTH = 0.74
STRUT_DEPTH = 1.00
HEADER_WIDTH = 24.8
HEADER_TOP_Z = 10.62
HEADER_HEIGHT = 1.05
SIDE_PILLAR_WIDTH = 1.15
REAR_SILL_HALF_WIDTH = 9.00
REAR_SILL_Y = 2.00
REAR_SILL_Z = 0.55
PROJECTOR_LOCATION = (0.0, 0.0, 1.80)
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
    sources = [ROOT/'refs/picked-sketch-D.png', ROOT/'round2/D2-camera.png', OUT/'D3-camera.png']
    sheet = Image.new('RGB', (3880, 810), '#111b29')
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 25)
    small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
    titles = ['PICKED SKETCH D', 'ROUND 2', 'ROUND 3 / FALLING COWL + MIRRORED PODS']
    subtitles = ['Direction', 'Previous massing', 'Raised centre / forward-reaching wings / equal shells']
    for i, (path, title, sub) in enumerate(zip(sources, titles, subtitles)):
        x = 10+i*1290
        im = Image.open(path).convert('RGB').resize((1280,720), Image.Resampling.LANCZOS)
        sheet.paste(im, (x,80))
        d.text((x+12,12),title,fill='#e0e8ef',font=font)
        d.text((x+12,46),sub,fill='#92a9bc',font=small)
    sheet.save(OUT/'D3-compare.png')

if '--postprocess' in sys.argv:
    postprocess()
    raise SystemExit

import bpy
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view
import subprocess

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
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
scene.world = bpy.data.worlds.new('D3 | quiet blue ambient')
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
    u=Vector((0,1,0)) if abs(along.x)>.95 else Vector((1,0,0)); u=(u-along*u.dot(along)).normalized()
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
housing_objects={}
for hand,name,upper_size,lower_size in [
    (-1,'Left',SHIP_SCREEN_SIZE,DETAIL_SCREEN_SIZE),
    (1,'Right',PRIMARY_SCREEN_SIZE,ORDNANCE_SCREEN_SIZE)]:
    origin=(hand*POD_HALF_SPACING,POD_ORIGIN_Y,POD_ORIGIN_Z)
    width=POD_WIDTH
    upper,lower,back=pod_basis(origin,hand)
    pod_bases[name]=(upper,lower,back)
    top,bottom=POD_PROFILE_TOP,POD_PROFILE_BOTTOM
    profile=[(x,y+(top+bottom)/2) for x,y in chamfer(width,top-bottom,.45)]
    def bend(x,y,d=0): return point(upper,x,y,d)
    front=[bend(x,y,-.10) for x,y in profile]
    inner_indices=[2,3] if hand<0 else [6,7]
    mid=[bend(x+math.copysign(POD_SHOULDER_EXPANSION,x),y,-.10)+back*POD_SHOULDER_DEPTH+Vector((-hand*POD_INNER_SHOULDER_FLARE if i in inner_indices else 0,0,.17)) for i,(x,y) in enumerate(profile)]
    rear=[p+back*(POD_DEPTH-POD_SHOULDER_DEPTH) for p in mid]
    vs=front+mid+rear
    fs=[];ids=[]
    for layer in [0,1]:
        for i in range(8):
            j=(i+1)%8
            fs.append((layer*8+i,layer*8+j,(layer+1)*8+j,(layer+1)*8+i))
            ids.append([3,2,3,2,4,2,1,2][i])
    fs.append(tuple(range(23,15,-1)));ids.append(0)
    housing=mesh(name+' | mirrored deep faceted housing',vs,fs,palette,pods,ids)
    housing_objects[name]=housing
    housing['horizontal_depth']=POD_DEPTH
    housing['local_profile_width']=POD_WIDTH
    housing['local_profile_height']=POD_PROFILE_HEIGHT
    prism(name+' | full face carrier',profile,-.11,-.28,upper,face,pods)
    xshift=-hand*POD_SCREEN_INBOARD_OFFSET
    v1=SHIP_CENTRE_V if hand<0 else PRIMARY_CENTRE_V
    v2=DETAIL_CENTRE_V if hand<0 else ORDNANCE_CENTRE_V
    s1=add_screen('SHIP' if hand<0 else 'PRIMARY',upper_size,upper,xshift,v1)
    s2=add_screen('DETAIL' if hand<0 else 'ORDNANCE',lower_size,upper,xshift,v2)
    # Identical wide feet reach forward, intersecting the continuous cowl.
    bottom_points=[bend(-width/2+.45,bottom,-.12),bend(width/2-.45,bottom,-.12)]
    rear_points=[p+back*POD_DEPTH for p in bottom_points]
    root=[]
    for p0 in bottom_points+rear_points:
        p=p0+Vector((-p0.x*.045,-POD_ROOT_FORWARD_REACH,0))
        root.append(Vector((p.x,p.y,POD_ROOT_Z)))
    mesh(name+' | broad forward dash root',bottom_points+rear_points+root,
         [(0,1,3,2),(0,4,5),(0,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3),(4,6,7,5)],palette,pods,[1,3,1,0,2,2,0])
    accent=shifted(upper,hand*(width/2-.12),-.60,.02)
    prism(name+' | mirrored outer insert',chamfer(.075,.66,.018),0,-.015,accent,cyan,pods)
    if hand>0:
        # A recessed blank service panel uses the extra lower frame. It is not a screen.
        fb=shifted(upper,xshift,-.88,-.055)
        prism('Right | lower blank filler',chamfer(3.80,.48,.13),0,-.035,fb,side,pods)
        for label,sb in [('PRIMARY',s1),('ORDNANCE',s2)]:
            for action,dv in [('PREVIOUS',.28),('NEXT',-.28)]:
                kb=shifted(sb,2.65,dv,.02)
                prism(label+' '+action,chamfer(.43,.40,.06),.10,0,kb,key_mat,controls)
                control_points[label+' '+action]=point(kb,d=.10)

# Continuous draped cowl. One connected mesh, explicitly faceted between stations.
# Crown is high and central; both control wings fall forward AND outward.
cowl_columns=[(-x,y,z) for x,y,z in reversed(COWL_STATIONS[1:])]+COWL_STATIONS
base_ys=[COWL_FRONT_Y,COWL_CONTROL_BACK_Y,COWL_CROWN_Y,COWL_REAR_Y]
vs=[]
for x,reach,heights in cowl_columns:
    for j,(y,z) in enumerate(zip(base_ys,heights)):
        vs.append((x,y+reach*[1,.5,.1,0][j],z))
fs=[];ids=[]
for i in range(len(cowl_columns)-1):
    xmid=abs((cowl_columns[i][0]+cowl_columns[i+1][0])/2)
    for j in range(3):
        a,b,c,d=i*4+j,(i+1)*4+j,(i+1)*4+j+1,i*4+j+1
        # Reflect triangulation, too, so the folds themselves are mirrors.
        triangles=[(a,b,d),(b,c,d)] if cowl_columns[i][0]<0 else [(a,b,c),(a,c,d)]
        fs.extend(triangles)
        base=4 if xmid<1.55 else (2 if xmid<3.4 else 1)
        ids.extend([base,base if j==2 else (2 if base==4 else 3)])
offset=len(vs)
for i in range(len(cowl_columns)):
    p=Vector(vs[i*4])
    vs.extend([tuple(p+Vector((0,.12,-COWL_FASCIA_DEPTH))),tuple(p+Vector((0,.48,-.60)))])
for i in range(len(cowl_columns)-1):
    fs.extend([(i*4,offset+i*2,offset+(i+1)*2,(i+1)*4),
               (offset+i*2,offset+i*2+1,offset+(i+1)*2+1,offset+(i+1)*2)])
    ids.extend([3,0])
for i in [0,len(cowl_columns)-1]:
    fs.append((i*4,i*4+1,i*4+2,i*4+3,offset+i*2+1,offset+i*2));ids.append(0)
dash=mesh('Dash | crown falling forward and outward',vs,fs,palette,shell,ids)

def horizontal_basis(loc):
    return (Vector(loc),Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))

def loft(name,loops,mat=palette,col=shell):
    verts=[(x,y+yc,z) for w,h,z,yc in loops for x,y in chamfer(w,h,min(w,h)*.16)]
    faces=[];indices=[]
    for layer in range(len(loops)-1):
        for i in range(8):
            faces.append((layer*8+i,layer*8+(i+1)%8,(layer+1)*8+(i+1)%8,(layer+1)*8+i))
            indices.append([2,2,3,2,1,2,3,2][i] if layer%2==0 else [1,2,3,2,4,2,3,2][i])
    faces.append(tuple(range((len(loops)-1)*8,len(loops)*8)));indices.append(4)
    return mesh(name,verts,faces,mat,col,indices)

# Front-to-back raised spine links launch block and terraced projector, with
# inclined side walls. It sits above the cowl rather than making a level floor.
a=math.radians(LAUNCH_RAKE_DEG)
shelf_basis=(Vector((0,LAUNCH_CENTRE_Y,LAUNCH_TOP_Z)),Vector((1,0,0)),
             Vector((0,math.cos(a),math.sin(a))),Vector((0,-math.sin(a),math.cos(a))))
lo=chamfer(LAUNCH_HOUSING_WIDTH+.34,LAUNCH_HOUSING_LENGTH+.27,.25)
hi=chamfer(LAUNCH_HOUSING_WIDTH,LAUNCH_HOUSING_LENGTH,.22)
ring('Launch | sloping spine shoulders',lo,hi,shelf_basis,-.27,-.025,palette[2],shell)
prism('Launch | raised housing',hi,-.025,-.22,shelf_basis,face,shell)
prism('Launch | dark socket',chamfer(3.10,1.25,.22),.035,-.025,shelf_basis,dark,controls)
prism('LAUNCH',chamfer(2.83,1.02,.20),.10,.03,shelf_basis,orange,controls)
control_points['LAUNCH']=point(shelf_basis,d=.10)
mesh('Spine | tapered link to pedestal',
     [(-1.47,-1.69,1.31),(1.47,-1.69,1.31),(.95,-.65,1.36),(-.95,-.65,1.36),
      (-1.70,-1.69,1.09),(1.70,-1.69,1.09),(1.15,-.65,1.18),(-1.15,-.65,1.18)],
     [(0,1,2,3),(0,3,7,4),(1,5,6,2)],palette,shell,[1,2,3])
loft('Projector | stepped faceted pedestal',[(w,h,z,0) for w,h,z in PEDESTAL_LOOPS])
cylinder('Projector | dark rim',(0,0,1.755),.55,.08,dark)
cylinder('PROJECTOR LENS',PROJECTOR_LOCATION,.43,.025,cyan)
control_points['PROJECTOR LENS']=Vector(PROJECTOR_LOCATION)+Vector((0,0,.0125))

# The visible front of the pedestal carries two inset steps, following its slope.
for i,(y,z,w) in enumerate([(-.88,1.29,1.38),(-.69,1.46,1.09)]):
    bb=(Vector((0,y,z)),Vector((1,0,0)),Vector((0,.56,.8285)),Vector((0,-.8285,.56)))
    prism('Projector | front step '+str(i+1),chamfer(w,.13,.035),.035,0,bb,dark,shell)
bb=(Vector((0,-.95,1.19)),Vector((1,0,0)),Vector((0,.5,.866)),Vector((0,-.866,.5)))
prism('Projector | base blue insert',chamfer(.95,.055,.015),.025,0,bb,cyan,shell)

# Two key blocks and one knob on dedicated wing landing pads, below the spine.
kbasis=(Vector((-2.72,-2.45,.88)),Vector((1,0,0)),Vector((0,math.cos(a),math.sin(a))),Vector((0,-math.sin(a),math.cos(a))))
prism('Ship keys | mounting pad',chamfer(1.57,1.19,.12),0,-.16,kbasis,side,controls)
for label,x in [('SHIP PREVIOUS',-.39),('SHIP NEXT',.39)]:
    kb=shifted(kbasis,x,0,.015)
    prism(label,chamfer(.60,.94,.075),.18,0,kb,key_mat,controls)
    control_points[label]=point(kb,d=.18)
cylinder('Selector | socket',(2.70,-2.45,.91),.54,.13,dark)
cylinder('SELECTOR KNOB',(2.70,-2.45,1.13),.45,.35,key_mat)
control_points['SELECTOR KNOB']=Vector((2.70,-2.45,1.305))

for hand,name in [(-1,'Left'),(1,'Right')]:
    foot=STRUT_LANDING_POINTS[0 if hand<0 else 1]
    beam(name+' | strut landing shoe',(foot[0],foot[1],foot[2]-.20),(foot[0],foot[1]+.04,foot[2]+.22),.98,1.10,palette)
    # Small inset on each cowl facet. Basis follows its actual local slope.
    u=Vector((1,0,-hand*.16)).normalized()
    v=Vector((0,1,.15));v=(v-u*v.dot(u)).normalized();n=u.cross(v)
    db=(Vector((hand*2.50,.75,1.175)),u,v,n)
    prism(name+' | cowl insert',chamfer(.76,.065,.018),0,-.018,db,cyan)

# Canopy: wide raked ribs on actual forward dash shoulders; a thick sill behind.
struts=[]
for name,lo,hi in zip(['Left','Right'],STRUT_LANDING_POINTS,STRUT_TOP_POINTS):
    struts.append(beam(name+' | landed canopy rib',lo,hi,STRUT_WIDTH,STRUT_DEPTH))
    lo,hi=Vector(lo),Vector(hi); axis=(hi-lo).normalized()
    p=lo+(hi-lo)*.83+Vector((0,-.49,0))
    beam(name+' | rib blue insert',p-axis*.30,p+axis*.30,.075,.035,cyan)
beam('Window | heavy rear sill',(-REAR_SILL_HALF_WIDTH,REAR_SILL_Y,REAR_SILL_Z),(REAR_SILL_HALF_WIDTH,REAR_SILL_Y,REAR_SILL_Z),.8,.9,palette)

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
mesh('Projector | faint light planes',[(0,0,1.82),(-1.15,.15,3.4),(1.15,.15,3.4),(0,0,1.82),(-.3,-.8,3.3),(.3,.8,3.3)],[(0,1,2),(3,4,5)],fan_mat,hologram)
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

gamecam=camera('D3 | FIXED GAME CAMERA',CAMERA_POSITION)
gamecam.lock_location=(True,True,True);gamecam.lock_rotation=(True,True,True)
gamecam['direction']=str(CAMERA_DIRECTION);gamecam['vertical_fov_degrees']=CAMERA_VERTICAL_FOV_DEG
threecam=camera('D3 | three quarter',(18,-25,18),(0,1,4.6),30)
topcam=camera('D3 | TOP orthographic',(0,0,35),(0,0,0),29)
sidecam=camera('D3 | SIDE orthographic',(-32,0,5),(0,0,5),25)
frontcam=camera('D3 | FRONT orthographic from viewer',(0,-35,5),(0,0,5),29)

def area(name,loc,power,size,target):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);stage.objects.link(o);o.location=loc
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Key | upper left',(-8,-7,13),2200,7,(0,1,2.5))
# One key light only; uniform world ambient separates the shadow planes.

bpy.context.view_layer.update()
subject=[o.matrix_world@Vector(c) for col in [shell,pods,screens_col,controls,hologram] for o in col.objects for c in o.bound_box]
for cam in [threecam,topcam,sidecam,frontcam]:
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
    left_half=name in ['SHIP','DETAIL']
    own=pod_bases['Left' if left_half else 'Right'][0]
    opposite=pod_bases['Right' if left_half else 'Left'][0]
    delta=b[0]-own[0]
    local_x,local_v=delta.dot(own[1]),delta.dot(own[2])
    # Place a same-size virtual aperture on the actual opposite plane. This
    # checks the two construction frames, including the projected edge slopes.
    mirror_errors=[]
    for u,v in chamfer(*size,SCREEN_CHAMFER):
        p=projected(point(b,u,v,-SCREEN_RECESS))
        q=projected(point(opposite,-local_x-u,local_v+v,-SCREEN_RECESS))
        mirror_errors.append((q-Vector((1920-p.x,p.y))).length)
    screen_data['mirrored_aperture_projection_error_pixels']=max(mirror_errors)
    def angle_between(a,b):
        d=Vector(b)-Vector(a)
        return math.degrees(math.atan2(d.y,d.x))
    screen_data['top_edge_image_slope_degrees']=angle_between(coords[5],coords[4])
    screen_data['bottom_edge_image_slope_degrees']=angle_between(coords[0],coords[1])
    if max(mirror_errors)>.002:errors.append((name,'asymmetric projected plane',max(mirror_errors)))
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
report['pod_bounds']={}
for name,obj in housing_objects.items():
    vv=[obj.matrix_world@v.co for v in obj.data.vertices]
    lower=[min(v[i] for v in vv) for i in range(3)]
    upper=[max(v[i] for v in vv) for i in range(3)]
    report['pod_bounds'][name]={'min':lower,'max':upper,'world_width_depth_height':[upper[i]-lower[i] for i in range(3)],
                              'face_profile_width_height':[POD_WIDTH,POD_PROFILE_HEIGHT]}
left_vertices=[v.co for v in housing_objects['Left'].data.vertices]
right_vertices=[v.co for v in housing_objects['Right'].data.vertices]
reflection_error=max(min((Vector((-p.x,p.y,p.z))-q).length for q in right_vertices) for p in left_vertices)
report['pod_shell_reflection_error_units']=reflection_error
if reflection_error>1e-5:errors.append(('pods','not exact mirrors',reflection_error))
report['cowl_stations']=COWL_STATIONS
report['cowl_forward_outward_fall_verified']=all(h[0]<h[1]<h[2] for x,y,h in COWL_STATIONS)
report['cowl_forward_outward_fall_verified'] &= all(COWL_STATIONS[i][2][j]>=COWL_STATIONS[i+1][2][j]
                                                   for i in range(len(COWL_STATIONS)-1) for j in range(4))
if not report['cowl_forward_outward_fall_verified']:errors.append(('cowl','fall direction failure'))
report['light_count']=sum(o.type=='LIGHT' for o in scene.objects)
if report['light_count']!=1:errors.append(('lighting','expected one key light'))
report['errors']=errors
(OUT/'measurements.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('CHECKS',errors or 'PASS',flush=True)
if '--check' in sys.argv:raise SystemExit(1 if errors else 0)
if '--preview' not in sys.argv:assert not errors, errors

def write_notes():
    rows=['| Screen | True size | Width retained | Height retained | Visibility rays |','|---|---:|---:|---:|---:|']
    for name,v in report['screens'].items():
        rows.append(f"| {name} | {v['size'][0]:.2f} x {v['size'][1]:.2f} | {v['width_retention']*100:.2f}% | {v['height_retention']*100:.2f}% | {v['visibility_samples']-v['occluded_samples']}/{v['visibility_samples']} clear |")
    bounds=report['pod_bounds']['Left']
    world_w,world_d,world_h=bounds['world_width_depth_height']
    text=f'''# Cockpit D / round 3

Round 2 is the source. The fixed camera, heavy canopy, landed struts, faceted pod depth, blue screens and translucent ship remain. This revision replaces the raised outer wings and low centre trough with a draped cowl, a stepped projector and a raised launch spine. Pod housings are exact reflections of one geometry definition.

## Centre planes and direction of fall

Coordinates use +Y away from the viewer and +Z up. The sketch reads as a high projector crown feeding a console that reaches forward; it does not require every small surface to have the same slope. Round 2's most disruptive features were its low central break followed by a raised foreground shelf, and the strong outward climb. Both are removed.

The cowl crown is at Y=0.60. The central support beneath the pedestal is Z=1.38, falling through Z=1.17 at Y=-1.35 to Z=0.86 at Y=-3.40. There is no trough and counter-ramp. From the centre, shoulder stations at |X|=1.55, 3.40, 5.80 and 9.00 step progressively down: crown heights are 1.32, 0.98, 0.85, 0.78; front heights are 0.86, 0.58, 0.42, 0.36. The inner shoulder, wing and outer apron are separate geometric facets, explicitly triangulated and mirrored. No large single floor spans between the pods.

The centre's rear return eases down from the crown to Z=1.35 at Y=2.35; the outside rear rail blends to the strut feet. The pod-base aprons reach forward to Y=-3.85, while the central shelf reaches Y=-3.40. A two-plane fascia drops 0.30, then another 0.30 while returning away from the viewer.

The pedestal has five octagonal rings, with heights {', '.join(f'{z:.2f}' for w,h,z in PEDESTAL_LOOPS)}. Its wide base, sloping lower faces, horizontal terrace and smaller upper cap create real relief. The lens is at {PROJECTOR_LOCATION}. Two inset blocks articulate its front face. The launch housing is a separate raised spine: its control plane at Y={LAUNCH_CENTRE_Y:g}, Z={LAUNCH_TOP_Z:g} rises {LAUNCH_RAKE_DEG:g} degrees rearward, hence falls toward the viewer. Its side walls fall outward to the cowl. A narrowing link meets the pedestal. The ship keys and selector sit on lower wing pads.

## Equal pods and symmetric slope

One set of construction angles: yaw magnitude {POD_YAW_DEG:g} degrees, back lean {POD_LEAN_DEG:g} degrees, bank magnitude {POD_BANK_DEG:g} degrees. Left yaw/bank are +{POD_YAW_DEG:g}/-{POD_BANK_DEG:g}; right are -{POD_YAW_DEG:g}/+{POD_BANK_DEG:g}. The width axis is yawed, the height axis leans, then the pair banks. These are construction angles, not Blender Euler values. All four screens share their pod's plane (lower break {LOWER_SCREEN_BREAK_DEG:g} degrees). The shallower angles replace round 2's 58/40/20 and additional 8-degree lower fold.

Both face outlines are {POD_WIDTH:g} wide by {POD_PROFILE_HEIGHT:g} high, with the same chamfered silhouette, shoulder expansion, inward flare, {POD_DEPTH:g} horizontal depth and origin Z={POD_ORIGIN_Z:g}. Each shell's world bounding box is {world_w:.3f} wide x {world_d:.3f} deep x {world_h:.3f} tall, spanning Z={bounds['min'][2]:.3f} to Z={bounds['max'][2]:.3f}. The maximum shell reflection error is {report['pod_shell_reflection_error_units']:.8f} units. The roots extend to Z={POD_ROOT_Z:g} and reach {POD_ROOT_FORWARD_REACH:g} forward from the shell. The right pod uses a larger separator and a lower blank filler around its smaller screens. Control keys are in its outer rail, outside the screen boundaries. Blue outer inserts are mirrored.

Perspective slopes are compared at mirrored positions on the two planes, rather than comparing unrelated edges at different heights. Different screen heights naturally sample different positions on the same vanishing-point family. Their virtual mirrored rectangles project identically after reflection in the centre of the image; the housing silhouettes do too.

## Screen and visibility checks

{chr(10).join(rows)}

Retention is the length of each unit screen axis projected perpendicular to the sightline from its centre to the fixed camera: sqrt(1-dot(axis,sightline)^2). Distance magnification is removed. Thresholds are 80% width and 75% height. Exact blue-outline sizes are unchanged. Dense rays sample each screen interior and near its chamfered edges; every screen must be unobscured and completely inside the image. Controls have individual visibility checks. Hologram bounds and strut clearance are checked. Detailed results are in measurements.json.

The camera remains {CAMERA_POSITION}, direction {CAMERA_DIRECTION}, vertical field of view {CAMERA_VERTICAL_FOV_DEG:g} degrees, 1920 x 1080. The orthographic front is viewed from negative Y, the player's side. All review cameras frame the entire assembly. There is one area key light with uniform ambient; flat colours only, no textures, text or modifiers.

## Departures from the sketch

The fixed camera and the lens limit (Z=1.8) place the pedestal lower in the image than the drawing; the raised terraced housing uses the full allowed height. Screen dimensions are exact, so the right shell contains spare frame instead of stretching the weapon screens. The right filler is intentionally a plain solid panel. The forward shelf and outboard aprons have a sharper faceted separation than the drawing to make the corrected fall easy to inspect at massing stage. Both pods move outward to X=+/-{POD_HALF_SPACING:g} so the canopy ribs do not cover any screen. The rear sill remains heavy but is lowered to Z={REAR_SILL_Z:g}, keeping its top below the central crown rather than forming a high bar behind the lens. Space stays a flat night blue; the ship is a translucent placeholder. This is an editable assembly of intersecting masses, not a watertight production mesh.

## Rebuild

Run `& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D3.py` from the supplied folder. `-- --preview` renders the fixed camera only; `-- --check` runs geometry and visibility checks without rendering. The full build saves the blend, five 1920 x 1080 renders and the three-image comparison using local Pillow. All inputs are local; no network is used.
'''
    (OUT/'notes.md').write_text(text,encoding='utf-8')

write_notes()
def render(cam,filename):
    scene.camera=cam;scene.render.filepath=str(OUT/filename);bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D3.blend'))
render(gamecam,'D3-camera.png')
if '--preview' not in sys.argv:
    assert not errors, errors
    backdrop.hide_render=True
    world_bg.inputs['Color'].default_value=(.19,.22,.27,1);world_bg.inputs['Strength'].default_value=.55
    for cam,filename in [(threecam,'D3-three-quarter.png'),(topcam,'D3-top.png'),(sidecam,'D3-side.png'),(frontcam,'D3-front.png')]:render(cam,filename)
    backdrop.hide_render=False
    world_bg.inputs['Color'].default_value=(.09,.12,.17,1);world_bg.inputs['Strength'].default_value=.30
    scene.camera=gamecam;scene.render.filepath=str(OUT/'D3-camera.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D3.blend'))
    subprocess.run([str(POSTPROCESS_PYTHON),str(Path(__file__).resolve()),'--postprocess'],check=True)
print('D3 BUILD COMPLETE',flush=True)
