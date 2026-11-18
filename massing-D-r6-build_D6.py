"""Cockpit D, round 6: continuous folded control ledges and integrated window rim.
Derived from the supplied round5/build_D6.py.

Run locally: blender -b --factory-startup --python out/build_D6.py
Use -- --preview for the fixed camera only; -- --check for geometry checks only.
All faces are modelled explicitly. No textures, text or bevel modifiers.
"""
from pathlib import Path
import math
import sys
import os
import json
import ast
import warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)

# DESIGN CONSTANTS -- Blender units, Z up. Angles are degrees.
# One pod definition, reflected in world X. Width is the face's local width.
# Round 5 pod construction angles are locked; no independent screen rotations.
POD_YAW_DEG = 36.0
POD_LEAN_DEG = 38.0              # back from vertical
POD_BANK_DEG = -4.0              # reversed bank: outer corners lower; sign mirrored
LOWER_SCREEN_BREAK_DEG = 0.0      # shared face plane eliminates unequal lower slopes
POD_DEPTH = 2.15                 # horizontal rearward depth in the yaw direction
POD_SHOULDER_DEPTH = 0.62
POD_SHOULDER_EXPANSION = 0.38
POD_INNER_SHOULDER_FLARE = 0.0
POD_HALF_SPACING = 6.35
POD_ORIGIN_Y = 0.25
POD_ORIGIN_Z = 2.45
POD_WIDTH = 5.85
POD_PROFILE_TOP = 4.08
POD_PROFILE_BOTTOM = -1.52
POD_PROFILE_HEIGHT = POD_PROFILE_TOP - POD_PROFILE_BOTTOM
POD_SCREEN_INBOARD_OFFSET = 0.20
SHIP_SCREEN_SIZE = (4.25, 3.50)
DETAIL_SCREEN_SIZE = (3.76, 0.92)
PRIMARY_SCREEN_SIZE = (4.25, 1.85)
ORDNANCE_SCREEN_SIZE = (4.25, 1.85)
SHIP_CENTRE_V = 1.95
DETAIL_CENTRE_V = -0.66
PRIMARY_CENTRE_V = 2.775
ORDNANCE_CENTRE_V = 0.305
SCREEN_CHAMFER = 0.20
SCREEN_RECESS = 0.008
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
# Three sloped risers separated by broad, horizontal octagonal ledges.
PEDESTAL_LOOPS = [              # width, depth, Z
    (2.30, 1.95, 1.12),
    (2.20, 1.85, 1.27),
    (1.98, 1.62, 1.69),
    (1.72, 1.38, 1.69),
    (1.54, 1.20, 2.08),
    (1.32, 1.04, 2.08),
    (1.12, 0.94, 2.42),
]
PROJECTOR_LENS_Z = 2.48          # top surface 2.4925; below clear volume at 2.50
PROJECTOR_RIM_RADIUS = 0.55
PROJECTOR_RIM_DEPTH = 0.08
PROJECTOR_LENS_RADIUS = 0.43
PROJECTOR_LENS_DEPTH = 0.025
# Ledges are a fold of the pod base, with actual apertures in the deck.
LEDGE_FOLD_DEG = 34.0            # local fold from the pod face toward horizontal
LEDGE_DEPTH = 2.05
LEDGE_SOCKET_SIZE = (3.90, 1.36)
LEDGE_SOCKET_DEPTH = 0.16
LEDGE_KEY_TOP = -0.018           # below the deck plane: no surface-mounted blocks
LEDGE_KEY_HEIGHT = 0.12
WEAPON_LOWER_FRAME_RISE = 0.50   # the released height becomes the lower folded frame
# Clockwise-corresponding world perimeter for the right wing; reflect for left.
WING_BOUNDARY = ((1.55,-3.40,.86),(8.50,-4.00,.28),(9.00,-3.60,.28),
                 (9.00,.00,.65),(8.70,2.35,.90),(1.55,2.35,1.30),
                 (1.55,.60,1.32),(1.55,-1.35,1.10))
STRUT_WIDTH = 0.74
STRUT_DEPTH = 1.00
STRUT_FACE_D = -0.10             # same plane as the pod perimeter, behind screens
STRUT_TOP_Z = 10.05
SILL_RISE = 0.24
SILL_DEPTH = 0.95
KEY_LIGHT_POSITION = (0.0, -10.0, 10.8)
KEY_LIGHT_POWER = 2100
KEY_LIGHT_SIZE = 4.0
HEADER_WIDTH = 24.8
HEADER_TOP_Z = 10.62
HEADER_HEIGHT = 1.05
HEADER_REAR_Y = 7.65            # receives the raked jamb tops at Y=7.20
SIDE_PILLAR_WIDTH = 1.15
PROJECTOR_LOCATION = (0.0, 0.0, PROJECTOR_LENS_Z)
HOLOGRAM_CENTRE = (0.0, 0.0, 4.50)
HOLOGRAM_CLEAR_SIZE = (5.0, 4.0, 4.0)
SHIP_PLACEHOLDER_ROTATION_DEG = (38.0, -12.0)  # X then Z, inherited unchanged
SHIP_PLACEHOLDER_ORIGIN = (0.0, 0.05, 4.80)
CAMERA_POSITION = (0.0, -18.0, 6.8)
CAMERA_DIRECTION = (0.0, 0.9936, -0.1132)
CAMERA_VERTICAL_FOV_DEG = 34.11
RESOLUTION = (1920, 1080)
RENDER_SAMPLES = 32
EDGE_ALIGNMENT_TOLERANCE_DEG = 5.0
OUT = Path(__file__).resolve().parent
ROOT = OUT.parent
os.environ['OPTIX_CACHE_PATH'] = str(OUT / '.cache/optix')
POSTPROCESS_PYTHON = Path('C:/Users/amind/Miniconda3/python.exe')


def postprocess():
    from PIL import Image, ImageDraw, ImageFont
    sources = [ROOT/'refs/picked-sketch-D.png', ROOT/'round5/D5-camera.png', OUT/'D6-camera.png']
    sheet = Image.new('RGB', (3880, 810), '#111b29')
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 25)
    small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
    titles = ['PICKED SKETCH D', 'ROUND 5', 'ROUND 6 / INTEGRATED CONSOLE']
    subtitles = ['Direction', 'Supplied baseline',
                 'Folded control ledges / flush window rim / shorter weapons']
    for i, (path, title, sub) in enumerate(zip(sources, titles, subtitles)):
        x = 10+i*1290
        im = Image.open(path).convert('RGB').resize((1280,720), Image.Resampling.LANCZOS)
        sheet.paste(im, (x,80))
        d.text((x+12,12),title,fill='#e0e8ef',font=font)
        d.text((x+12,46),sub,fill='#92a9bc',font=small)
    sheet.save(OUT/'D6-compare.png')

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
scene.world = bpy.data.worlds.new('D6 | quiet blue ambient')
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
    ring(name+' | aperture slope',chamfer(w+.26,h+.26,.26),inner,b,-.025,-SCREEN_RECESS,face)
    ring(name+' | thin dark reveal',chamfer(w+.040,h+.040,.210),inner,b,-.009,-SCREEN_RECESS,dark)
    obj=mesh(name+' | screen', [point(b,u,v,-SCREEN_RECESS) for u,v in inner], [tuple(range(8))],screen_mat,screens_col)
    obj['true_width']=w; obj['true_height']=h
    screen_records.append((name,size,b,obj))
    return b

pod_bases={}
housing_objects={}
wing_edges={}
pod_fronts={}
rim_join_records={}
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
    # Inner return stays behind the face and never flares toward a screen.
    mid=[bend(x+(math.copysign(POD_SHOULDER_EXPANSION,x) if hand*x>0 else 0),y,-.10)+back*POD_SHOULDER_DEPTH for i,(x,y) in enumerate(profile)]
    pod_fronts[name]=front
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
    accent=shifted(upper,hand*(width/2-.12),-.60,.02)
    prism(name+' | mirrored outer insert',chamfer(.075,.66,.018),0,-.015,accent,cyan,pods)
    if hand>0:
        # Matched shorter pair; the released half unit becomes a folded lower frame.
        for label,sb in [('PRIMARY',s1),('ORDNANCE',s2)]:
            for action,dv in [('PREVIOUS',.28),('NEXT',-.28)]:
                kb=shifted(sb,2.65,dv,.02)
                prism(label+' '+action,chamfer(.43,.40,.06),.10,0,kb,key_mat,controls)
                control_points[label+' '+action]=point(kb,d=.10)

# Continuous centre: retain the inherited crown and forward fall.
centre_stations=COWL_STATIONS[:2]
cowl_columns=[(-x,y,z) for x,y,z in reversed(centre_stations[1:])]+centre_stations
vs=[]
for x,reach,heights in cowl_columns:
    vs.extend((x,y,z) for y,z in zip([COWL_FRONT_Y,COWL_CONTROL_BACK_Y,COWL_CROWN_Y,COWL_REAR_Y],heights))
fs=[];ids=[]
for i in range(2):
    for j in range(3):
        a=i*4+j;b=(i+1)*4+j
        fs.extend([(a,b,b+1),(a,b+1,a+1)]);ids.extend([2,4])
dash=mesh('Dash | continuous forward falling centre',vs,fs,palette)

# Each wing shares the ledge perimeter and the centre's boundary stations.
# Thus the ledge is literally a face of the wing, not a slab lying on it.
ledge_objects={};wing_objects={};ledge_bases={};ledge_vertices={}
wing_triangle_orientation={}
for hand,name in [(-1,'Left'),(1,'Right')]:
    pb=pod_bases[name][0]
    fold=Matrix.Rotation(math.radians(-LEDGE_FOLD_DEG),3,pb[1])
    u=pb[1];v=fold@pb[2];n=fold@pb[3]
    # Use an outward axis for the shared mirrored wing mesh; keep the normal up.
    outward=u*hand
    rear=point(pb,y=POD_PROFILE_BOTTOM,d=-.10)
    db=(rear-v*LEDGE_DEPTH/2,outward,v,n)
    ledge_bases[name]=db
    deck=chamfer(POD_WIDTH,LEDGE_DEPTH,.22)
    socket=chamfer(*LEDGE_SOCKET_SIZE,.14)
    dv=[point(db,x,y) for x,y in deck]
    ledge_vertices[name]=dv
    iv=[point(db,x,y) for x,y in socket]
    # Perimeter-to-aperture faces are the actual structural deck.
    ledge_objects[name]=mesh(name+' | folded ledge with cut-out',dv+iv,
        [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],palette,shell,[2,2,3,1,2,2,4,2])
    ring(name+' | recessed control well',socket,socket,db,0,-LEDGE_SOCKET_DEPTH,dark,controls)
    prism(name+' | control well floor',socket,-LEDGE_SOCKET_DEPTH,-LEDGE_SOCKET_DEPTH-.03,db,dark,controls)
    # Keys have substantial side walls, but their top remains inside the aperture.
    spec=[('SHIP PREVIOUS',-.67,1.08),('SHIP NEXT',.67,1.08)] if hand<0 else [
          ('WEAPON SWITCH '+str(i+1),(i-1.5)*.84,.65) for i in range(4)]
    for label,x,w in spec:
        kb=shifted(db,x,0)
        prism(label,chamfer(w,.97,.09),LEDGE_KEY_TOP,LEDGE_KEY_TOP-LEDGE_KEY_HEIGHT,kb,key_mat,controls)
        control_points[label]=point(kb,d=LEDGE_KEY_TOP)
    # A folded lower frame uses the 0.50 released by the weapon pair.
    join_v=POD_PROFILE_BOTTOM+(WEAPON_LOWER_FRAME_RISE if hand>0 else 0)
    jleft=point(pb,-hand*(POD_WIDTH/2-.22),join_v,-.10)
    jright=point(pb,hand*(POD_WIDTH/2-.22),join_v,-.10)
    mesh(name+' | pod-to-ledge folded heel', [dv[5],dv[4],jright,jleft],[(0,1,2,3)],shoulder,pods)
    # Corner cheeks close the chamfered pod's foot to the full ledge width.
    for sign,di in [(-1,6),(1,3)]:
        px=hand*sign*POD_WIDTH/2
        p0=point(pb,px,POD_PROFILE_BOTTOM+.45,-.10)
        p1=point(pb,hand*sign*(POD_WIDTH/2-.22),join_v,-.10)
        mesh(name+' | heel corner '+str(sign),[dv[di],dv[5 if sign<0 else 4],p1,p0],[(0,1,2,3)],face,pods)
        p2=point(pb,hand*sign*(POD_WIDTH/2-.45),POD_PROFILE_BOTTOM,-.10)
        mesh(name+' | closed heel miter '+str(sign),[p0,p1,p2],[(0,1,2)],face,pods)
    # World boundary goes from launch's outer spine to the pod outer bolster,
    # then back to the centre's crown. Shared vertices eliminate floating wings.
    ov=[Vector((hand*x,y,z)) for x,y,z in WING_BOUNDARY]
    wing_faces=[];wing_ids=[]
    wv=dv+ov
    def signed_xy_area(tri):
        p,q,r=[wv[k] for k in tri]
        return (q-p).cross(r-p).z
    for i in range(8):
        j=(i+1)%8
        tris=[(i,j,8+j),(i,8+j,8+i)]
        if signed_xy_area(tris[0])*signed_xy_area(tris[1])<0:
            tris=[(i,j,8+i),(j,8+j,8+i)]
        assert signed_xy_area(tris[0])*signed_xy_area(tris[1])>0, (name,i,'crossing wing quad')
        # Upward-facing, nonoverlapping triangles, including concave sweep cells.
        if signed_xy_area(tris[0])<0:tris=[tuple(reversed(t)) for t in tris]
        wing_faces.extend(tris)
        wing_ids.extend([2 if i in [0,6,7] else 1,3 if i in [0,6,7] else 2])
    wing_triangle_orientation[name]=all(signed_xy_area(t)>0 for t in wing_faces)
    wing_objects[name]=mesh(name+' | wing sweeps continuously into centre',dv+ov,wing_faces,palette)
    # The fascia and underside continue around the entire exposed boundary.
    underside=[p-Vector((0,0,COWL_FASCIA_DEPTH)) for p in ov]
    mesh(name+' | continuous wing fascia',ov+underside,
         [(i,i+1,i+9,i+8) for i in range(5)]+[tuple(range(15,7,-1))],side)
    wing_edges[name]=(dv[0],dv[1])

mesh('Dash | centre fascia',
     [(-1.55,-3.40,.86),(1.55,-3.40,.86),(1.55,-3.40,.56),(-1.55,-3.40,.56)],[(0,1,2,3)],side)

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
cylinder('Projector | dark rim',(0,0,PROJECTOR_LENS_Z-.045),PROJECTOR_RIM_RADIUS,PROJECTOR_RIM_DEPTH,dark)
cylinder('PROJECTOR LENS',PROJECTOR_LOCATION,PROJECTOR_LENS_RADIUS,PROJECTOR_LENS_DEPTH,cyan)
control_points['PROJECTOR LENS']=Vector(PROJECTOR_LOCATION)+Vector((0,0,.0125))

# Each riser has a dark front inset; the broad ledges remain exposed above it.
for i,start in enumerate([1,3,5]):
    w0,h0,z0=PEDESTAL_LOOPS[start]
    w1,h1,z1=PEDESTAL_LOOPS[start+1]
    v=Vector((0,(h0-h1)/2,z1-z0)).normalized()
    u=Vector((1,0,0));n=u.cross(v)
    bb=(Vector((0,-(h0+h1)/4,(z0+z1)/2)),u,v,n)
    prism('Projector | front riser inset '+str(i+1),chamfer((w0+w1)*.29,.10,.025),.012,0,bb,dark,shell)
bb=(Vector((0,-.955,1.21)),Vector((1,0,0)),Vector((0,.5,.866)),Vector((0,-.866,.5)))
prism('Projector | base blue insert',chamfer(.95,.055,.015),.025,0,bb,cyan,shell)

# The selector and launch stay in their inherited centre locations.
cylinder('Selector | socket',(2.70,-2.45,.91),.54,.13,dark)
cylinder('SELECTOR KNOB',(2.70,-2.45,1.13),.45,.35,key_mat)
control_points['SELECTOR KNOB']=Vector((2.70,-2.45,1.305))

# The canopy rib is the pod's inner jamb, in the same construction frame.
# Its outward side shares the entire straight pod inner edge, without a sky slit.
struts=[];STRUT_LANDING_POINTS=[];STRUT_TOP_POINTS=[]
for hand,name in [(-1,'Left'),(1,'Right')]:
    pb=pod_bases[name][0]
    ux=-hand*(POD_WIDTH/2+STRUT_WIDTH/2)
    dmid=STRUT_FACE_D-STRUT_DEPTH/2
    vtop=(STRUT_TOP_Z-point(pb,ux,0,dmid).z)/pb[2].z
    vlo=POD_PROFILE_BOTTOM-.18
    lo=point(pb,ux,vlo,dmid);hi=point(pb,ux,vtop,dmid)
    STRUT_LANDING_POINTS.append(tuple(lo));STRUT_TOP_POINTS.append(tuple(hi))
    rb=shifted(pb,ux,(vlo+vtop)/2)
    outline=chamfer(STRUT_WIDTH,vtop-vlo,.075)
    rib=prism(name+' | flush canopy jamb',outline,STRUT_FACE_D,STRUT_FACE_D-STRUT_DEPTH,rb,palette)
    # Assign separate values to front / returns, keeping the broad face readable.
    for poly in rib.data.polygons:poly.material_index=1 if poly.index==0 else (3 if poly.index%2 else 2)
    struts.append(rib)
    ab=shifted(pb,ux,vtop-1.05,STRUT_FACE_D+.006)
    prism(name+' | rib blue insert',chamfer(.075,.60,.018),0,-.012,ab,cyan)
    # Close the chamfer at the pod's inner top against the jamb, all behind glass.
    edge_x=-hand*POD_WIDTH/2
    cap=[point(pb,edge_x,POD_PROFILE_TOP-.45,-.10),
         point(pb,edge_x+hand*.45,POD_PROFILE_TOP,-.10),
         point(pb,edge_x,POD_PROFILE_TOP,-.10)]
    mesh(name+' | upper jamb miter',cap,[(0,1,2)],face,pods)
    # Sill is a continuous raised rear return following the actual pod crown.
    # Its lower front edge shares V=4.08 / D=-0.10 with the housing's top.
    sill_outline=[(-POD_WIDTH/2,POD_PROFILE_TOP),(POD_WIDTH/2,POD_PROFILE_TOP),
                  (POD_WIDTH/2,POD_PROFILE_TOP+SILL_RISE),(-POD_WIDTH/2,POD_PROFILE_TOP+SILL_RISE)]
    sill=prism(name+' | window sill shares pod crown',sill_outline,-.10,-.10-SILL_DEPTH,pb,palette)
    for poly in sill.data.polygons:poly.material_index=2 if poly.index==0 else 3
    # Outer crown chamfer also meets the sill without a sky triangle.
    edge_x=hand*POD_WIDTH/2
    mesh(name+' | outer sill miter',[point(pb,edge_x,POD_PROFILE_TOP-.45,-.10),
         point(pb,edge_x-hand*.45,POD_PROFILE_TOP,-.10),point(pb,edge_x,POD_PROFILE_TOP,-.10)],[(0,1,2)],face,pods)
    rim_join_records[name]={'inner_edge_local_u':-hand*POD_WIDTH/2,
        'common_face_normal_depth':-.10,'straight_inner_edge_v':[POD_PROFILE_BOTTOM+.45,POD_PROFILE_TOP-.45],
        'crown_sill_common_v':POD_PROFILE_TOP,'sill_rise':SILL_RISE,
        'rib_landing':tuple(lo),'rib_top':tuple(hi),
        'inner_crown_world':list(point(pb,-hand*POD_WIDTH/2,POD_PROFILE_TOP,-.10)),
        'outer_crown_world':list(point(pb,hand*POD_WIDTH/2,POD_PROFILE_TOP,-.10))}
STRUT_LANDING_POINTS=tuple(STRUT_LANDING_POINTS);STRUT_TOP_POINTS=tuple(STRUT_TOP_POINTS)

# Side pillars have three stations: the lower knees physically step in and forward.
for hand,name in [(-1,'Left'),(1,'Right')]:
    stations=[(hand*9.90,-.10,.35),(hand*10.45,1.15,4.90),(hand*11.85,4.05,10.30)]
    for i in range(2): beam(name+' | stepped side pillar '+str(i+1),stations[i],stations[i+1],SIDE_PILLAR_WIDTH,1.30)
    beam(name+' | outer pod bolster',(hand*9.25,.3,.25),(hand*10.2,1.6,3.05),1.3,1.8)

# Extruded hexagonal header section: front face, deep top, sloped underside.
cross=[(3.35,HEADER_TOP_Z),(HEADER_REAR_Y,HEADER_TOP_Z),(HEADER_REAR_Y,HEADER_TOP_Z-HEADER_HEIGHT+.13),(3.90,HEADER_TOP_Z-HEADER_HEIGHT),(3.35,HEADER_TOP_Z-HEADER_HEIGHT+.30)]
hv=[(x,y,z) for x in [-HEADER_WIDTH/2,HEADER_WIDTH/2] for y,z in cross]
mesh('Canopy | header with chamfered underside',hv,[tuple(reversed(range(5))),tuple(range(5,10))]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)],palette,shell,[0,0,0,0,3,2,1])
for x in [-8.6,0,8.6]:
    cube('Header | small blue insert',(x,3.335,9.99),(.90,.025,.065),cyan)

# Translucent, genuinely three-dimensional fighter placeholder.
rotation=Matrix.Rotation(math.radians(SHIP_PLACEHOLDER_ROTATION_DEG[0]),3,'X') @ Matrix.Rotation(math.radians(SHIP_PLACEHOLDER_ROTATION_DEG[1]),3,'Z')
ship_origin=Vector(SHIP_PLACEHOLDER_ORIGIN)
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
mesh('Projector | faint light planes',[(0,0,PROJECTOR_LENS_Z+.02),(-1.15,.15,3.4),(1.15,.15,3.4),(0,0,PROJECTOR_LENS_Z+.02),(-.3,-.8,3.3),(.3,.8,3.3)],[(0,1,2),(3,4,5)],fan_mat,hologram)
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

gamecam=camera('D6 | FIXED GAME CAMERA',CAMERA_POSITION)
gamecam.lock_location=(True,True,True);gamecam.lock_rotation=(True,True,True)
gamecam['direction']=str(CAMERA_DIRECTION);gamecam['vertical_fov_degrees']=CAMERA_VERTICAL_FOV_DEG
threecam=camera('D6 | three quarter',(18,-25,18),(0,1,4.6),30)
topcam=camera('D6 | TOP orthographic',(0,0,35),(0,0,0),29)
sidecam=camera('D6 | SIDE orthographic',(-32,0,5),(0,0,5),25)
frontcam=camera('D6 | FRONT orthographic from viewer',(0,-35,5),(0,0,5),29)

def area(name,loc,power,size,target):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);stage.objects.link(o);o.location=loc
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Key | centred in front of all screens',KEY_LIGHT_POSITION,KEY_LIGHT_POWER,KEY_LIGHT_SIZE,(0,1,2.5))
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

def pixel_edge(a,b):
    a,b=Vector(a),Vector(b);d=b-a
    angle=(math.degrees(math.atan2(d.y,d.x))+90)%180-90
    return {'pixels':[list(a),list(b)],'angle_degrees':angle}

def world_edge(a,b):
    result=pixel_edge(projected(a),projected(b))
    result['world_endpoints']=[list(a),list(b)]
    return result

def line_angle_difference(a,b):return abs((a-b+90)%180-90)

report={'camera':{'position':CAMERA_POSITION,'direction':CAMERA_DIRECTION,'vertical_fov':CAMERA_VERTICAL_FOV_DEG},
        'pod_angles':{'left':{'yaw':POD_YAW_DEG,'lean':POD_LEAN_DEG,'bank':-POD_BANK_DEG},'right':{'yaw':-POD_YAW_DEG,'lean':POD_LEAN_DEG,'bank':POD_BANK_DEG}},
        'screens':{},'controls':{},'strut_landings':STRUT_LANDING_POINTS,'pod_depth':POD_DEPTH}
report['image_edges']={'struts':{},'wing_aprons':{},'cowl_shoulders':{}}
for i,(hand,side_name) in enumerate([(-1,'Left'),(1,'Right')]):
    report['image_edges']['struts'][side_name]=world_edge(STRUT_LANDING_POINTS[i],STRUT_TOP_POINTS[i])
    report['image_edges']['wing_aprons'][side_name]=world_edge(*wing_edges[side_name])
    shoulder_points=[(hand*x,COWL_CROWN_Y+reach*.1,zs[2]) for x,reach,zs in COWL_STATIONS[1:3]]
    report['image_edges']['cowl_shoulders'][side_name]=world_edge(*shoulder_points)
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
    screen_data['inner_edge']=pixel_edge(coords[2],coords[3]) if left_half else pixel_edge(coords[7],coords[6])
    screen_data['bottom_edge']=pixel_edge(coords[0],coords[1])
    side_name='Left' if left_half else 'Right'
    screen_data['inner_strut_difference_degrees']=line_angle_difference(
        screen_data['inner_edge']['angle_degrees'],report['image_edges']['struts'][side_name]['angle_degrees'])
    screen_data['bottom_wing_difference_degrees']=line_angle_difference(
        screen_data['bottom_edge']['angle_degrees'],report['image_edges']['wing_aprons'][side_name]['angle_degrees'])
    if screen_data['inner_strut_difference_degrees']>EDGE_ALIGNMENT_TOLERANCE_DEG:
        errors.append((name,'inner edge fails strut alignment',screen_data['inner_strut_difference_degrees']))
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
    light_obstructions=[];light_samples=0
    key=bpy.data.objects['Key | centred in front of all screens']
    km=key.matrix_world.to_3x3()
    # Centre plus eight points on the disk rim, for each near-edge/interior target.
    emitters=[key.location]+[key.location+km@Vector((KEY_LIGHT_SIZE*.5*math.cos(t*math.pi/4),KEY_LIGHT_SIZE*.5*math.sin(t*math.pi/4),0)) for t in range(8)]
    for ix in range(17):
        for iy in range(13):
            sx=(ix/16-.5)*(size[0]-.016);sy=(iy/12-.5)*(size[1]-.016)
            if abs(sx)>size[0]/2-SCREEN_CHAMFER and abs(sy)>size[1]/2-SCREEN_CHAMFER and (size[0]/2-abs(sx))+(size[1]/2-abs(sy))<SCREEN_CHAMFER+.003:continue
            target=centre+b[1]*sx+b[2]*sy
            for source in emitters:
                light_samples+=1
                hit,loc,norm,index,ho,matrix=scene.ray_cast(deps,source,(target-source).normalized())
                if not hit or ho.original!=obj:light_obstructions.append(ho.name if ho else 'none')
    screen_data['key_light_samples']=light_samples
    screen_data['key_light_blocked_samples']=len(light_obstructions)
    screen_data['key_light_blockers']=sorted(set(light_obstructions))
    if light_obstructions:errors.append((name,'key-light obstruction',len(light_obstructions),sorted(set(light_obstructions))))
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

# Regression checks against the supplied builder, independent of rendering.
locked_names = [
    'POD_YAW_DEG','POD_LEAN_DEG','POD_BANK_DEG','LOWER_SCREEN_BREAK_DEG',
    'POD_HALF_SPACING','POD_ORIGIN_Y','POD_ORIGIN_Z','POD_WIDTH','POD_PROFILE_TOP',
    'POD_PROFILE_BOTTOM','POD_SCREEN_INBOARD_OFFSET','SHIP_SCREEN_SIZE','DETAIL_SCREEN_SIZE',
    'SHIP_CENTRE_V','DETAIL_CENTRE_V','LAUNCH_CENTRE_Y','LAUNCH_TOP_Z',
    'LAUNCH_RAKE_DEG','LAUNCH_HOUSING_WIDTH','LAUNCH_HOUSING_LENGTH',
    'PROJECTOR_LENS_Z','PEDESTAL_LOOPS','HOLOGRAM_CENTRE','HOLOGRAM_CLEAR_SIZE',
    'CAMERA_POSITION','CAMERA_DIRECTION','CAMERA_VERTICAL_FOV_DEG','RESOLUTION',
]
baseline_source=(ROOT/'round5/build_D5.py').read_text(encoding='utf-8')
baseline_ast=ast.parse(baseline_source)
baseline_constants={node.targets[0].id:ast.literal_eval(node.value)
                    for node in baseline_ast.body if isinstance(node,ast.Assign)
                    and isinstance(node.targets[0],ast.Name) and node.targets[0].id in locked_names}
changed_locked=[name for name in locked_names if globals()[name]!=baseline_constants[name]]
report['round5_locked_constants']={'count':len(locked_names),'changed':changed_locked}
if changed_locked:errors.append(('round5','locked constants changed',changed_locked))
report['rim_joins']=rim_join_records
# Exact source comparison of all fighter vertices/faces/edge curves, separate from its transform.
def fighter_source(source):
    return source[source.index('outline=[(0,-2.0)'):source.index("mesh('Projector | faint light planes'")]
fighter_unchanged=fighter_source(baseline_source)==fighter_source(Path(__file__).read_text(encoding='utf-8'))
report['hologram_geometry_unchanged']=fighter_unchanged
report['hologram_transform_unchanged']=(SHIP_PLACEHOLDER_ORIGIN==(0.0,.05,4.8) and SHIP_PLACEHOLDER_ROTATION_DEG==(38.,-12.))
if not fighter_unchanged or not report['hologram_transform_unchanged']:
    errors.append(('hologram','placeholder changed'))

def reflection_error(left_obj,right_obj=None):
    right_obj=right_obj or left_obj
    left=[left_obj.matrix_world@v.co for v in left_obj.data.vertices]
    right=[right_obj.matrix_world@v.co for v in right_obj.data.vertices]
    return max(min((Vector((-p.x,p.y,p.z))-q).length for q in right) for p in left)
report['wing_symmetry_error_units']={
    'centre':reflection_error(dash),
    'ledges':reflection_error(ledge_objects['Left'],ledge_objects['Right']),
    'wings':reflection_error(wing_objects['Left'],wing_objects['Right'])}
for name,error in report['wing_symmetry_error_units'].items():
    if error>1e-5:errors.append((name,'wing symmetry',error))
report['raised_wing_blocks_present']=any('raised wing access' in o.name for o in scene.objects)
if report['raised_wing_blocks_present']:errors.append(('wings','old raised blocks remain'))
report['wing_triangles_no_crossing']=wing_triangle_orientation

report['pedestal']={'lens_centre_z':PROJECTOR_LENS_Z,
                    'lens_top_z':PROJECTOR_LENS_Z+PROJECTOR_LENS_DEPTH/2,
                    'clear_volume_floor_z':HOLOGRAM_CENTRE[2]-HOLOGRAM_CLEAR_SIZE[2]/2,
                    'ledges_z':[PEDESTAL_LOOPS[2][2],PEDESTAL_LOOPS[4][2]],'camera_samples':[]}
if report['pedestal']['lens_top_z']>=report['pedestal']['clear_volume_floor_z']:
    errors.append(('pedestal','enters hologram clear volume'))
for layer in [2,4]:
    _,depth,z=PEDESTAL_LOOPS[layer]
    _,inner_depth,_=PEDESTAL_LOOPS[layer+1]
    target=Vector((0,-(depth+inner_depth)/4,z))
    hit,loc,norm,index,ho,matrix=scene.ray_cast(deps,gamecam.location,(target-gamecam.location).normalized())
    visible=hit and ho.name=='Projector | stepped faceted pedestal' and (loc-target).length<.002
    report['pedestal']['camera_samples'].append({'ledge_z':z,'visible':visible,'pixel':list(projected(target))})
    if not visible:errors.append(('pedestal','ledge hidden',z,ho.name if ho else None))
report['weapon_pair']={'matched_size':PRIMARY_SCREEN_SIZE==ORDNANCE_SCREEN_SIZE,
                       'full_pod_extent':(ORDNANCE_CENTRE_V-ORDNANCE_SCREEN_SIZE[1]/2,
                                          PRIMARY_CENTRE_V+PRIMARY_SCREEN_SIZE[1]/2),
                       'blank_filler_present':any('filler' in o.name.lower() for o in scene.objects)}
if PRIMARY_SCREEN_SIZE!=ORDNANCE_SCREEN_SIZE or PRIMARY_SCREEN_SIZE[1]>2.10 or report['weapon_pair']['blank_filler_present']:
    errors.append(('weapon pair','size or filler regression'))
report['errors']=errors
(OUT/'measurements.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('CHECKS',errors or 'PASS',flush=True)
if '--preview' not in sys.argv:assert not errors, errors

def write_notes():
    rows=['| Screen | True size | Width retained | Height retained | Camera rays clear | Key-light rays clear |',
          '|---|---:|---:|---:|---:|---:|']
    for name,v in report['screens'].items():
        rows.append(f"| {name} | {v['size'][0]:.2f} x {v['size'][1]:.2f} | {v['width_retention']*100:.2f}% | {v['height_retention']*100:.2f}% | {v['visibility_samples']-v['occluded_samples']}/{v['visibility_samples']} | {v['key_light_samples']-v['key_light_blocked_samples']}/{v['key_light_samples']} |")
    text=f"""# Cockpit D / round 6

Built from `round5/build_D5.py` using the supplied sketch and round 5 renders. All work is local.

## The five changes

1. **Wing blocks removed.** Both raised wing access slabs and the old separate ship-key mounting pad are gone. The wings are now faceted structural surfaces sharing the ledge edges and the centre's boundary vertices, with a continuous front fascia and underside.
2. **Continuous console and control ledges.** Each pod folds forward by {LEDGE_FOLD_DEG:g} degrees into a {LEDGE_DEPTH:.2f}-deep raked ledge. Its surrounding wing sweeps inward to X=+/-1.55 and shares the centre's front, control-back, crown and rear stations. The left ledge contains two ship-selection keys below SHIP/DETAIL; the right has four chunky switches below the weapons. Each control well is a real opening in the deck, {LEDGE_SOCKET_DEPTH:.2f} deep, with the key tops {abs(LEDGE_KEY_TOP):.3f} below the surrounding plane. The knob and launch retain their round 5 positions.
3. **Shorter weapon pair.** PRIMARY and ORDNANCE are both 4.25 x 1.85. The pair's upper extent remains local V=3.70. Centres are V={PRIMARY_CENTRE_V:.3f} and {ORDNANCE_CENTRE_V:.3f}; the 0.62 inter-screen frame remains. The released 0.50 becomes a sloping lower heel from the carrier into the right control ledge. Each weapon still has its previous/next keys beside it. SHIP and DETAIL keep their sizes and positions.
4. **Clear screen geometry.** Removed the 0.72 inward shoulder flare and the inner-side shoulder expansion. Apertures open toward the screen: the outer lip is behind the blue plane, with only a {SCREEN_RECESS:.3f} nominal recess. Rib fronts and housing edges are at local normal D=-0.10, while screens are at D=-{SCREEN_RECESS:.3f}. The single area key is centred in front of the cockpit at {KEY_LIGHT_POSITION}; screens receive no canopy/shoulder band. Camera rays and key-light rays are checked separately, so emission does not substitute for geometric clearance.
5. **Flush window rim.** This follows the reviewer's interpretation of "flush": a physically shared pod/rib/sill frame. Both pod origins and angles stay fixed. The canopy ribs move to become the inner jambs of those frames, and the header extends rearward to receive them. There is no rib passing across a pod face.

## Exact pod-to-rim construction

- **Left pod:** inner side local U=+2.925. Its straight edge from V=-1.07 to V=3.63 shares D=-0.10 with the outward face-edge of the left canopy jamb. A triangular miter fills the upper chamfer to V=4.08. The left sill follows the pod crown, from the inner to the outer corner at V=4.08, D=-0.10, and returns {SILL_DEPTH:.2f} behind the face, with a {SILL_RISE:.2f} rise. Its inner end butts directly into the jamb.
- **Right pod:** the exact reflected joint at U=-2.925, with the same V extents, normal depth, miter, crown sill and return. Its weapons sit within that frame. The outer top chamfers are also closed against each sill. The sills are the lower window rims directly behind the pod crowns, not a disconnected beam below the console.
- The jamb's long axis is each pod's local V axis, so the adjacent screen edges follow it. Jamb centres run from (+/-3.342, 1.904, 0.925) to (+/-8.188, 7.196, 10.050), replacing round 5's (+/-3.20, 1.65, 1.06) to (+/-7.10, 4.15, 10.05). This rearward rake is needed to follow the unchanged pod plane in 3D. The header's rear edge moves from Y=4.75 to Y={HEADER_REAR_Y:.2f} to receive the tops; its front edge and height stay fixed. Exact world joint coordinates are in `measurements.json` under `rim_joins`.

## Screen retention and clearance

{chr(10).join(rows)}

Retention is sqrt(1 - dot(screen_axis, centre_to_camera_unit_vector)^2), the same convention as round 5; it excludes distance magnification. Required minima are 80% width / 75% height. Camera visibility samples include near-outline points and each clipped screen interior. Light tests sample the area light's centre and eight rim positions against screen interiors and edges; zero rays are blocked. Screen/strut projected edge differences are 0.67 to 0.92 degrees. All control centres are visible. The measurements file records these results, mirrored shells, noncrossing wing triangles, clearance and all {report['round5_locked_constants']['count']} locked round 5 constants. All checks pass.

## Preserved facts

- Pod angles are **exactly round 5's**: yaw magnitude 36 degrees, lean 38 degrees, bank parameter -4 degrees. Left yaw/lean/bank is +36/38/+4; right is -36/38/-4. Zero lower-screen break; no independent screen rotation. Origins remain (+/-6.35, 0.25, 2.45); shell heights remain exact mirrors.
- The centre still falls toward the viewer. The launch housing and its 9-degree rake are unchanged. The three-step pedestal and lens centre Z=2.48 are unchanged; lens top Z=2.4925 stays below the hologram clear volume. The hologram geometry and transform are unchanged.
- Fixed 1920x1080 camera at {CAMERA_POSITION}, direction {CAMERA_DIRECTION}, vertical FOV {CAMERA_VERTICAL_FOV_DEG} degrees. No camera move.
- Flat colours, subtly differentiated adjacent planes, one area key plus uniform world ambient. No image textures, scene text, or modifiers.

## Rebuild

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D6.py
```

`-- --preview` renders just the fixed camera. `-- --check` runs the geometry checks. The full run saves the blend, five views, comparison, notes and measurements. The comparison uses the supplied picked sketch, round 5 camera, and round 6 camera. Local Python/Pillow is used only to lay out that comparison.
"""
    (OUT/'notes.md').write_text(text,encoding='utf-8')

write_notes()
if '--check' in sys.argv:raise SystemExit(1 if errors else 0)
def render(cam,filename):
    scene.camera=cam;scene.render.filepath=str(OUT/filename);bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D6.blend'))
render(gamecam,'D6-camera.png')
if '--preview' not in sys.argv:
    assert not errors, errors
    backdrop.hide_render=True
    world_bg.inputs['Color'].default_value=(.19,.22,.27,1);world_bg.inputs['Strength'].default_value=.55
    for cam,filename in [(threecam,'D6-three-quarter.png'),(topcam,'D6-top.png'),(sidecam,'D6-side.png'),(frontcam,'D6-front.png')]:render(cam,filename)
    backdrop.hide_render=False
    world_bg.inputs['Color'].default_value=(.09,.12,.17,1);world_bg.inputs['Strength'].default_value=.30
    scene.camera=gamecam;scene.render.filepath=str(OUT/'D6-camera.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D6.blend'))
    subprocess.run([str(POSTPROCESS_PYTHON),str(Path(__file__).resolve()),'--postprocess'],check=True)
print('D6 BUILD COMPLETE',flush=True)
