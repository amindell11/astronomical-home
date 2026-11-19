"""D12: C's continuous band with compact, grown-in B control ramps.
Run: blender -b --python out/build_D12.py -- [--preview | --check]
Self-contained local geometry; no textures, text, modifiers, or external assets.
The inherited scene is generated verbatim, then only console parts are replaced.
"""
from pathlib import Path
import math
import sys
import os
import json
import ast
import warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)

# D12 CONSOLE DESIGN -- Blender units; rake is measured above horizontal.
BAND_PATH = [(-8.85,-2.50,.55),(-7.3,-1.60,.78),(-3.25,-2.10,1.32),
             (-1.62,-3.42,1.43),(1.62,-3.42,1.43),(3.25,-2.10,1.32),
             (7.3,-1.60,.78),(8.85,-2.50,.55)]
BAND_SECTION = [(-1.0,-.91),(-1.0,-.60),(-.80,-.39),
                (.72,.38),(.95,.34),(1.35,-.24)]
BAND_SILL_Y = 2.35
BAND_SILL_Z = .86
BAND_RAKE_DEG = 26.866
RAISED_PANEL_RAKE_DEG = 37.0
RAISED_PANEL_HALF_SPACING = 4.00
SELECTOR_PANEL_X = 3.65
RAISED_PANEL_LIFT = .19       # centre displacement along the band's normal
RAISED_PANEL_SHOULDER = .27   # fold spreads out into the parent band
SHIP_PANEL_SIZE = (1.93, 1.12)
KNOB_PANEL_SIZE = (1.55, 1.24)
RAISED_PANEL_BEZEL = .16
RAISED_PANEL_WELL_DEPTH = .075
SHIP_KEY_SIZE = (.54, .49)
SHIP_KEY_HALF_SPACING = .37
SWITCH_SIZE = (.34, .49)
SWITCH_PITCH = .52
SWITCH_PANEL_X = 5.85
SWITCH_PANEL_SIZE = (2.63, 1.20)
KNOB_RADIUS = .34
KNOB_HEIGHT = .24
KNOB_COLLAR_RADIUS = .44
LAUNCH_D12_CENTRE = (0, -3.44, 1.50)
LAUNCH_D12_RAKE_DEG = 27.0
LAUNCH_D12_SIZE = (2.60, 1.08)
SPINE_STATIONS = [(-2.80,1.40,1.80),(-2.43,1.28,1.71),
                  (-1.22,1.12,1.28),(-.94,1.10,1.27)]  # y, half-width, z
STATUS_CENTRE_X = -6.25
STATUS_SIZE = (.13, .055)
STATUS_PITCH = .32
FINAL_SAMPLES = 48
PREVIEW_SAMPLES = 16

# DESIGN CONSTANTS -- Blender units, Z up. Angles are degrees.
# One pod definition, reflected in world X. Width is the face's local width.
# Inherited pod construction angles are locked; no independent screen rotations.
POD_YAW_DEG = 36.0
POD_LEAN_DEG = 38.0              # back from vertical
POD_BANK_DEG = -4.0              # reversed bank: outer corners lower; sign mirrored
LOWER_SCREEN_BREAK_DEG = 0.0      # shared face plane eliminates unequal lower slopes
POD_DEPTH = 1.25                 # horizontal rearward depth in the yaw direction
POD_SHOULDER_DEPTH = 0.42
POD_SHOULDER_EXPANSION = 0.20
POD_INNER_SHOULDER_FLARE = 0.0
BASELINE_POD_HALF_SPACING = 5.77  # round 9 C origin
POD_OUTWARD_MOVE = 1.22          # additional outward X translation from round 9 C
POD_HALF_SPACING = BASELINE_POD_HALF_SPACING + POD_OUTWARD_MOVE
POD_ORIGIN_Y = 0.25
POD_ORIGIN_Z = 2.45
POD_WIDTH = 5.85
POD_PROFILE_TOP = 2.88
POD_PROFILE_BOTTOM = -1.52
POD_PROFILE_HEIGHT = POD_PROFILE_TOP - POD_PROFILE_BOTTOM
POD_SCREEN_INBOARD_OFFSET = 0.20
SHIP_SCREEN_SIZE = (4.25, 3.00)
DETAIL_SCREEN_SIZE = (3.76, 0.80)
PRIMARY_SCREEN_SIZE = (4.25, 1.60)
ORDNANCE_SCREEN_SIZE = (4.25, 1.60)
SHIP_CENTRE_V = 1.20
DETAIL_CENTRE_V = -0.96
PRIMARY_CENTRE_V = 1.90
ORDNANCE_CENTRE_V = 0.00
SCREEN_CHAMFER = 0.20
POD_INNER_TOP_CHAMFER = (1.35, 0.95)  # local U and V setbacks; mirrored
SCREEN_INNER_TOP_CHAMFER = (0.85, 0.60)
SCREEN_RECESS = 0.008
SCREEN_MIN_FRAME_MARGIN_PX = 40.0
WEAPON_STEP_LOCAL_U = -2.65       # inboard housing frame, same size keys
WEAPON_STEP_PRIMARY_V = 1.35      # below the inner top chamfer
WEAPON_STEP_ORDNANCE_V = 0.00
WEAPON_STEP_PAIR_SPACING = 0.56
WEAPON_STEP_SIZE = (0.43, 0.40)
ASPECT_RESOLUTIONS = {'16x9': (1920,1080), '16x10': (1920,1200), '21x9': (2560,1080)}
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
WING_BOUNDARY = ((1.55,-3.40,.86),(8.50+POD_OUTWARD_MOVE,-4.00,.28),(9.00+POD_OUTWARD_MOVE,-3.60,.28),
                 (9.00+POD_OUTWARD_MOVE,.00,.65),(8.70+POD_OUTWARD_MOVE,2.35,.90),(1.55,2.35,1.30),
                 (1.55,.60,1.32),(1.55,-1.35,1.10))
STRUT_WIDTH = 0.38
STRUT_DEPTH = 0.30
STRUT_CAMERA_OFFSET_PX = 50.0    # centreward offset from the straight pod inner edge
STRUT_FOOT_Y = 2.65             # canopy behind the pods and clear hologram box
STRUT_FOOT_Z = 1.20
STRUT_TOP_Y = 6.95
STRUT_TOP_Z = 10.05
KEY_LIGHT_POSITION = (0.0, -10.0, 10.8)
KEY_LIGHT_POWER = 2100
KEY_LIGHT_SIZE = 4.0
HEADER_WIDTH = 24.8
HEADER_TOP_Z = 10.62
HEADER_HEIGHT = 1.05            # inherited nominal section; edge varies by station
HEADER_FRONT_Y = 3.35
HEADER_EDGE_Y = 3.90
HEADER_JUNCTION_Z = 9.02         # lower step corner / strut junction
HEADER_REAR_EDGE_RISE = 0.60     # exposes the front lower edge as the window outline
HEADER_FACE_EDGE_RISE = 0.30
HEADER_ACCENT_EDGE_OFFSET = 0.43
HEADER_ACCENT_LENGTH = 0.95
HEADER_ACCENT_WIDTH = 0.065
HEADER_C_CENTRE_Z = 9.80
HEADER_C_STEP_WIDTH = 1.05
HEADER_C_OUTER_KINK_X = 10.05
HEADER_C_END_Z = 8.40
STRUT_ACCENT_DROP = 2.05
SELECTOR_CENTRE = (2.15, -1.20, 1.285)
SELECTOR_RADIUS = 0.45
SELECTOR_SOCKET_RADIUS = 0.54
SELECTOR_MOUNT_FLOOR_INSET = 0.025
HEADER_REAR_Y = 7.65            # receives the raked jamb tops at Y=7.20
SIDE_PILLAR_WIDTH = 0.32
SIDE_PILLAR_DEPTH = 0.38
SIDE_PILLAR_FOOT = (10.85, 1.50, 0.45)
SIDE_PILLAR_TOP = (12.25, 4.05, 10.30)
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
CLEAR_CENTRE_SCANLINE_PX = 680
OUT = Path(__file__).resolve().parent
ROOT = OUT.parent
os.environ['OPTIX_CACHE_PATH'] = str(OUT / '.cache/optix')


STEM = 'D10'

import bpy
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

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
scene.world = bpy.data.worlds.new('D10 | quiet blue ambient')
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

def inner_top_clip(outline, hand, size):
    result=list(outline);du,dv=size
    if hand<0:
        xmax=result[3][0];ymax=result[4][1]
        result[3]=(xmax,ymax-dv);result[4]=(xmax-du,ymax)
    else:
        xmin=result[6][0];ymax=result[5][1]
        result[5]=(xmin+du,ymax);result[6]=(xmin,ymax-dv)
    return result

def inside_polygon(x,y,outline):
    # All construction outlines are convex and counterclockwise.
    return all((b[0]-a[0])*(y-a[1])-(b[1]-a[1])*(x-a[0])>=1e-6
               for a,b in zip(outline,outline[1:]+outline[:1]))

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

def add_screen(name,size,b,x,y,inner_hand=None):
    w,h=size; b=shifted(b,x,y)
    inner=chamfer(w,h,SCREEN_CHAMFER)
    if inner_hand:inner=inner_top_clip(inner,inner_hand,SCREEN_INNER_TOP_CHAMFER)
    def aperture(w,h,c):
        p=chamfer(w,h,c)
        return inner_top_clip(p,inner_hand,SCREEN_INNER_TOP_CHAMFER) if inner_hand else p
    # Bounding screen size stays fixed; the upper inner corner follows the housing.
    ring(name+' | aperture slope',aperture(w+.26,h+.26,.26),inner,b,-.025,-SCREEN_RECESS,face)
    ring(name+' | thin dark reveal',aperture(w+.040,h+.040,.210),inner,b,-.009,-SCREEN_RECESS,dark)
    obj=mesh(name+' | screen', [point(b,u,v,-SCREEN_RECESS) for u,v in inner], [tuple(range(8))],screen_mat,screens_col)
    obj['true_width']=w; obj['true_height']=h
    screen_records.append((name,size,b,obj,inner))
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
    profile=inner_top_clip(profile,hand,POD_INNER_TOP_CHAMFER)
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
    s1=add_screen('SHIP' if hand<0 else 'PRIMARY',upper_size,upper,xshift,v1,hand)
    s2=add_screen('DETAIL' if hand<0 else 'ORDNANCE',lower_size,upper,xshift,v2)
    accent=shifted(upper,hand*(width/2-.12),-.60,.02)
    prism(name+' | mirrored outer insert',chamfer(.075,.66,.018),0,-.015,accent,cyan,pods)
    if hand>0:
        # Matched shorter pair; the released half unit becomes a folded lower frame.
        for label,sb in [('PRIMARY',s1),('ORDNANCE',s2)]:
            for action,dv in [('PREVIOUS',WEAPON_STEP_PAIR_SPACING/2),('NEXT',-WEAPON_STEP_PAIR_SPACING/2)]:
                centre_v=WEAPON_STEP_PRIMARY_V if label=='PRIMARY' else WEAPON_STEP_ORDNANCE_V
                kb=shifted(upper,WEAPON_STEP_LOCAL_U,centre_v+dv,.02)
                prism(label+' '+action,chamfer(*WEAPON_STEP_SIZE,.06),.10,0,kb,key_mat,controls)
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
        assert signed_xy_area(tris[0])*signed_xy_area(tris[1])>0, (name,i,'crossing wing quad', [tuple(p) for p in wv])
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

# Move the selector inboard and slightly rearward onto the inner wing.
# A fitted lower collar closes its mount against the existing faceted wing.
mount_upper=[];mount_lower=[]
for i in range(12):
    t=i*math.pi/6
    x=SELECTOR_CENTRE[0]+SELECTOR_SOCKET_RADIUS*math.cos(t)
    y=SELECTOR_CENTRE[1]+SELECTOR_SOCKET_RADIUS*math.sin(t)
    hit,loc,normal,index=wing_objects['Right'].ray_cast(Vector((x,y,5)),Vector((0,0,-1)))
    assert hit, ('selector mount outside wing',x,y)
    mount_upper.append((x,y,SELECTOR_CENTRE[2]-.285))
    mount_lower.append((x,y,loc.z-SELECTOR_MOUNT_FLOOR_INSET))
mesh('Selector | fitted lower collar',mount_lower+mount_upper,
     [(i,(i+1)%12,(i+1)%12+12,i+12) for i in range(12)],dark,controls)
cylinder('Selector | socket',(SELECTOR_CENTRE[0],SELECTOR_CENTRE[1],SELECTOR_CENTRE[2]-.22),SELECTOR_SOCKET_RADIUS,.13,dark)
cylinder('SELECTOR KNOB',SELECTOR_CENTRE,SELECTOR_RADIUS,.35,key_mat)
control_points['SELECTOR KNOB']=Vector(SELECTOR_CENTRE)+Vector((0,0,.175))

# Canopy geometry is independent of the pod shells. Place the struts in a
# rearward plane and align their projected centre lines with the screen edges.
cam_forward=Vector(CAMERA_DIRECTION).normalized()
cam_up=Vector((0,-cam_forward.z,cam_forward.y))
cam_eye=Vector(CAMERA_POSITION)
focal_px=RESOLUTION[1]/(2*math.tan(math.radians(CAMERA_VERTICAL_FOV_DEG)/2))
def design_project(p):
    q=Vector(p)-cam_eye;depth=q.dot(cam_forward)
    return Vector((960+focal_px*q.x/depth,540-focal_px*q.dot(cam_up)/depth))
struts=[];STRUT_LANDING_POINTS=[];STRUT_TOP_POINTS=[]
for hand,name in [(-1,'Left'),(1,'Right')]:
    pb=pod_bases[name][0]
    a=design_project(point(pb,-hand*POD_WIDTH/2,POD_PROFILE_BOTTOM,-.10))
    b=design_project(point(pb,-hand*POD_WIDTH/2,POD_PROFILE_TOP,-.10))
    def rib_station(y,z):
        p=Vector((0,y,z));py=design_project(p).y
        px=a.x+(py-a.y)*(b.x-a.x)/(b.y-a.y)-hand*STRUT_CAMERA_OFFSET_PX
        depth=(p-cam_eye).dot(cam_forward)
        p.x=(px-960)*depth/focal_px
        return p
    lo=rib_station(STRUT_FOOT_Y,STRUT_FOOT_Z)
    hi=rib_station(STRUT_TOP_Y,STRUT_TOP_Z)
    STRUT_LANDING_POINTS.append(tuple(lo));STRUT_TOP_POINTS.append(tuple(hi))
    rib=beam(name+' | independent slim canopy strut',lo,hi,STRUT_WIDTH,STRUT_DEPTH)
    struts.append(rib)
    along=(hi-lo).normalized();u=Vector((1,0,0));u=(u-along*u.dot(along)).normalized();n=u.cross(along)
    ab=(hi-along*STRUT_ACCENT_DROP+n*(STRUT_DEPTH/2+.008),u,along,n)
    prism(name+' | strut blue insert',chamfer(.045,.42,.012),0,-.008,ab,cyan)
    # Low rear foot: a short dash return, never a pod crown closure.
    beam(name+' | strut foot to rear dash',(lo.x,2.25,1.10),lo,.42,.30)
STRUT_LANDING_POINTS=tuple(STRUT_LANDING_POINTS);STRUT_TOP_POINTS=tuple(STRUT_TOP_POINTS)

# Single continuous edge posts avoid the old detached dark end-cap slivers.
# Only a low dash-level bolster connects to the side structure.
for hand,name in [(-1,'Left'),(1,'Right')]:
    lo=(hand*SIDE_PILLAR_FOOT[0],*SIDE_PILLAR_FOOT[1:])
    hi=(hand*SIDE_PILLAR_TOP[0],*SIDE_PILLAR_TOP[1:])
    beam(name+' | slim continuous outer pillar',lo,hi,SIDE_PILLAR_WIDTH,SIDE_PILLAR_DEPTH)
    beam(name+' | low dash side bolster',(hand*8.65,1.50,.75),(hand*SIDE_PILLAR_FOOT[0],1.50,.75),.45,.55)

# Faceted header: one closed mesh with a constant upper datum and a bent lower
# perimeter. Junction X is solved in the fixed camera so every visible strut
# lands at a real edge corner, rather than appearing to hit the middle of a facet.
left_lo,left_hi=map(Vector,(STRUT_LANDING_POINTS[0],STRUT_TOP_POINTS[0]))
lo_px,hi_px=design_project(left_lo),design_project(left_hi)
edge_centre=Vector((0,HEADER_EDGE_Y,HEADER_JUNCTION_Z))
join_py=design_project(edge_centre).y
join_px=lo_px.x+(join_py-lo_px.y)*(hi_px.x-lo_px.x)/(hi_px.y-lo_px.y)
HEADER_JUNCTION_X=abs((join_px-960)*(edge_centre-cam_eye).dot(cam_forward)/focal_px)
j=HEADER_JUNCTION_X
positive=[(0,HEADER_C_CENTRE_Z),(j-HEADER_C_STEP_WIDTH,HEADER_C_CENTRE_Z),
          (j,HEADER_JUNCTION_Z),(HEADER_C_OUTER_KINK_X,HEADER_JUNCTION_Z),(HEADER_WIDTH/2,HEADER_C_END_Z)]
header_description='Raised flat centre shelf, short diagonal step-downs and level outer shoulders with clipped ends; each strut meets the lower corner of a step.'
HEADER_STATIONS=[(-x,z) for x,z in reversed(positive[1:])]+positive
def junction_x_at(y,z):
    p=Vector((0,y,z));py=design_project(p).y
    px=lo_px.x+(py-lo_px.y)*(hi_px.x-lo_px.x)/(hi_px.y-lo_px.y)
    return abs((px-960)*(p-cam_eye).dot(cam_forward)/focal_px)

hv=[]
for x,z in HEADER_STATIONS:
    cross=[(HEADER_FRONT_Y,HEADER_TOP_Z),(HEADER_REAR_Y,HEADER_TOP_Z),
           (HEADER_REAR_Y,z+HEADER_REAR_EDGE_RISE),(HEADER_EDGE_Y,z),
           (HEADER_FRONT_Y,z+HEADER_FACE_EDGE_RISE)]
    # The junction seam follows the plane containing the strut and the camera.
    # This also makes the physical strut/header contact fall on that seam.
    is_junction=abs(abs(x)-j)<1e-5
    hv.extend([(math.copysign(junction_x_at(y,zz),x) if is_junction else x,y,zz)
               for y,zz in cross])
hf=[tuple(reversed(range(5)))];mi=[0]
for k in range(len(HEADER_STATIONS)-1):
    for i in range(5):
        hf.append((k*5+i,k*5+(i+1)%5,(k+1)*5+(i+1)%5,(k+1)*5+i))
        # Alternate adjoining fascia values very slightly, retaining rough massing.
        mi.append([0,0,3,2,1][i])
hf.append(tuple(range(len(hv)-5,len(hv))));mi.append(0)
header=mesh('Canopy | C stepped faceted brow',hv,hf,palette,shell,mi)

def brow_z(x):
    for (x0,z0),(x1,z1) in zip(HEADER_STATIONS,HEADER_STATIONS[1:]):
        if x0-1e-6<=x<=x1+1e-6:
            return z0+(z1-z0)*(x-x0)/(x1-x0)
    raise ValueError(x)

# Short inlays follow the exact polyline slope, with the retained C stepped outline.
for centre_x in [-8.6,0,8.6]:
    lo=centre_x-HEADER_ACCENT_LENGTH/2;hi=centre_x+HEADER_ACCENT_LENGTH/2
    cuts=[lo]+[x for x,z in HEADER_STATIONS if lo<x<hi]+[hi]
    for k,(x0,x1) in enumerate(zip(cuts,cuts[1:])):
        z0=brow_z(x0)+HEADER_ACCENT_EDGE_OFFSET;z1=brow_z(x1)+HEADER_ACCENT_EDGE_OFFSET
        y=HEADER_FRONT_Y-.015;w=HEADER_ACCENT_WIDTH/2
        mesh('Header | blue inlay '+str(centre_x)+' '+str(k),
             [(x0,y,z0-w),(x1,y,z1-w),(x1,y,z1+w),(x0,y,z0+w)],[(0,1,2,3)],cyan)

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

gamecam=camera('D10 | FIXED GAME CAMERA',CAMERA_POSITION)
gamecam.lock_location=(True,True,True);gamecam.lock_rotation=(True,True,True)
gamecam['direction']=str(CAMERA_DIRECTION);gamecam['vertical_fov_degrees']=CAMERA_VERTICAL_FOV_DEG
threecam=camera('D10 | three quarter',(18,-25,18),(0,1,4.6),30)
topcam=camera('D10 | TOP orthographic',(0,0,35),(0,0,0),29)
sidecam=camera('D10 | SIDE orthographic',(-32,0,5),(0,0,5),25)
frontcam=camera('D10 | FRONT orthographic from viewer',(0,-35,5),(0,0,5),29)

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
    c=world_to_camera_view(scene,gamecam,Vector(p))
    return Vector((c.x*scene.render.resolution_x,(1-c.y)*scene.render.resolution_y))

def pixel_edge(a,b):
    a,b=Vector(a),Vector(b);d=b-a
    angle=(math.degrees(math.atan2(d.y,d.x))+90)%180-90
    return {'pixels':[list(a),list(b)],'angle_degrees':angle}

def world_edge(a,b):
    result=pixel_edge(projected(a),projected(b))
    result['world_endpoints']=[list(a),list(b)]
    return result

def line_angle_difference(a,b):return abs((a-b+90)%180-90)


# D11 console construction appended to the frozen D10 scene generator.
import hashlib
import argparse

parser=argparse.ArgumentParser()

parser.add_argument('--preview', action='store_true')
parser.add_argument('--check', action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.variant='D12'
STEM='D12'

def signature(obj):
    data={'type':obj.type,'matrix':[list(r) for r in obj.matrix_world],
          'materials':[m.name for m in obj.data.materials] if hasattr(obj.data,'materials') else []}
    if obj.type=='MESH':
        data['vertices']=[list(v.co) for v in obj.data.vertices]
        data['polygons']=[list(p.vertices) for p in obj.data.polygons]
        data['indices']=[p.material_index for p in obj.data.polygons]
    if obj.type=='CURVE':
        data['splines']=[[list(p.co) for p in s.points] for s in obj.data.splines]
        data['radius']=obj.data.bevel_depth
    if obj.type=='CAMERA':
        data['camera']=[obj.data.lens,obj.data.sensor_height,obj.data.ortho_scale,obj.data.sensor_fit]
    if obj.type=='LIGHT':data['light']=[obj.data.energy,obj.data.size]
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()

# These are exclusively the D10 console. In particular the four weapon step
# keys, pod shells, full carriers, apertures, all canopy parts and hologram stay.
remove_prefixes=('Dash |','Launch |','Spine |','Selector |','SELECTOR KNOB',
    'SHIP PREVIOUS','SHIP NEXT','WEAPON SWITCH ','LAUNCH',
    'Projector | stepped','Projector | front riser','Projector | base blue')
remove_fragments=('folded ledge with cut-out','recessed control well','control well floor',
    'wing sweeps','continuous wing fascia','pod-to-ledge folded heel',
    'heel corner','closed heel miter')
def is_console(o):
    return o.name.startswith(remove_prefixes) or any(s in o.name for s in remove_fragments)
locked={o.name:signature(o) for o in scene.objects if not is_console(o)}
for obj in list(scene.objects):
    if is_console(obj):bpy.data.objects.remove(obj,do_unlink=True)
control_points={n:p for n,p in control_points.items() if n.startswith(('PRIMARY ','ORDNANCE ','PROJECTOR LENS'))}

console_col=collection('08 | D12 continuous console')
# Share the actual pod materials, not just similar RGB values.
steel=face
steel_hi=side
steel_mid=face
steel_dark=body
wellmat=dark
keytop=material('D12 | small control blocks',(.23,.29,.34))
cp=[steel,steel_hi,steel_mid,steel_dark,dark]

def cm(name,vertices,faces,ids=None,mat=None):
    return mesh('Console '+args.variant+' | '+name,vertices,faces,mat or cp,console_col,ids)

def rake(origin,angle=28,yaw=0):
    a,t=map(math.radians,(angle,yaw))
    u=Vector((math.cos(t),math.sin(t),0))
    v=Vector((-math.sin(t)*math.cos(a),math.cos(t)*math.cos(a),math.sin(a)))
    return (Vector(origin),u,v,u.cross(v))

apertures=[]
panel_objects=set()
def panel(name,b,w,h,edge=.18,depth=.22,floor=-.075):
    """A real open aperture, sloped bezel, reveal walls, recessed floor and skirt."""
    outer=chamfer(w,h,min(.22,w*.12,h*.15))
    mouth=chamfer(w-edge*2,h-edge*2,min(.16,h*.1))
    vs=[point(b,x,y,d) for loop,d in [(outer,0),(mouth,.055),(mouth,floor),(outer,-depth)] for x,y in loop]
    fs=[];mi=[]
    for i in range(8):
        j=(i+1)%8
        fs.extend([(i,j,j+8,i+8),(i+8,j+8,j+16,i+16),(i,i+24,j+24,j)])
        mi.extend(([0,1,2,1,0,1,2,1][i],4,3))
    fs.append(tuple(range(16,24)));mi.append(4)
    obj=cm(name+' bezel and recessed well',vs,fs,mi)
    panel_objects.add(obj.name)
    apertures.append((b,chamfer(w-.07,h-.07,min(.20,w*.12,h*.15)),depth))
    return obj, b

control_faces={}
def key(name,b,x,y,w=SHIP_KEY_SIZE[0],h=SHIP_KEY_SIZE[1]):
    kb=shifted(b,x,y)
    o=prism(name,chamfer(w,h,.055),.015,-.07,kb,keytop,controls)
    control_points[name]=point(kb,d=.015)
    control_faces[name]=(o,[point(kb,u,v,.015) for u,v in chamfer(w,h,.055)])
    return o

def shipkeys(b):
    key('SHIP PREVIOUS',b,-SHIP_KEY_HALF_SPACING,0)
    key('SHIP NEXT',b,SHIP_KEY_HALF_SPACING,0)

def switches(b,count=4):
    for i in range(count):key('WEAPON SWITCH '+str(i+1),b,(i-(count-1)/2)*SWITCH_PITCH,0,*SWITCH_SIZE)

def knob(b,x=0,y=0):
    kb=shifted(b,x,y)
    for label,r,d,z,mat in [('Selector | recessed collar',KNOB_COLLAR_RADIUS,.07,-.05,dark),('SELECTOR KNOB',KNOB_RADIUS,KNOB_HEIGHT,.08,keytop)]:
        o=cylinder(label,point(kb,d=z),r,d,mat,controls,12)
        o.rotation_euler=kb[3].to_track_quat('Z','Y').to_euler()
    control_points['SELECTOR KNOB']=point(kb,d=.20)
    bpy.context.view_layer.update()
    cap=[o.matrix_world@v.co for v in o.data.vertices if v.co.z>.119]
    control_faces['SELECTOR KNOB']=(o,cap)

def launch(b,w=2.65,h=1.04):
    panel('launch',b,w+.55,h+.48,.19,.24)
    o=prism('LAUNCH',chamfer(w,h,.17),.015,-.06,b,orange,controls)
    control_points['LAUNCH']=point(b,d=.015)
    control_faces['LAUNCH']=(o,[point(b,u,v,.015) for u,v in chamfer(w,h,.17)])

def skin(name,columns):
    # Columns run left to right; each has corresponding front-to-rear stations.
    nv=len(columns[0]);vs=[p for col in columns for p in col];fs=[];mi=[]
    for i in range(len(columns)-1):
        for j in range(nv-1):
            a=i*nv+j;b=(i+1)*nv+j
            fs.append((a,b,b+1,a+1));mi.append([3,1,0,2,3,2][j%6])
    return cm(name,vs,fs,mi)

def pod_landings(inner,variant):
    # The top of each transition is copied from the locked pod's foot, using
    # exactly the D10 heel join. Only the fold below this edge is redesigned.
    for hand,name in [(-1,'Left'),(1,'Right')]:
        pb=pod_bases[name][0]
        join_v=POD_PROFILE_BOTTOM+(WEAPON_LOWER_FRAME_RISE if hand>0 else 0)
        top=[point(pb,hand*u,join_v,-.10) for u in [-POD_WIDTH/2+.22,POD_WIDTH/2-.22]]
        edge=[Vector((hand*x,y,z)) for x,y,z in inner]
        # inner and outer endpoints, two declining tiers, then the main desk.
        middle=[p*.55+q*.45+Vector((0,0,.06)) for p,q in zip(top,edge)]
        cm(name+' ledge cascades into '+variant,top+middle+edge,
           [(0,1,3,2),(2,3,5,4)],[0,2])
        for i in [0,1]:
            p,q,r=top[i],middle[i],edge[i]
            cm(name+' ledge side '+str(i),[p,q,r,r-Vector((0,0,.22)),p-Vector((0,0,.22))],[(0,1,2,3,4)],[3])

def pedestal(loops):
    obj=loft('Console '+args.variant+' | integrated projector cowl',loops,cp,console_col)
    return obj

# C's exact continuous horseshoe path and folded cross-section.
path=BAND_PATH
cols=[]
for x,y,z in path:
    cols.append([(x,y+dy,z+dz) for dy,dz in BAND_SECTION]+[(x,BAND_SILL_Y,BAND_SILL_Z)])
ribbon=skin('continuous horseshoe control ribbon',cols)
for side_index in [0,-1]:
    cap=cols[side_index]
    cm('ribbon end return '+str(side_index),cap,[tuple(range(len(cap)))],[3])

def band_origin(x):
    for p,q in zip(path,path[1:]):
        if p[0]<=x<=q[0]:
            t=(x-p[0])/(q[0]-p[0])
            return Vector(p).lerp(Vector(q),t),(Vector(q)-Vector(p)).normalized()
    raise ValueError(x)

def band_basis(x,offset=.08):
    origin,u=band_origin(x)
    v=Vector((0,1,.77/1.52));n=u.cross(v).normalized();v=n.cross(u)
    return (origin+n*offset,u,v,n)

def band_surface_z(x,y):
    origin,_=band_origin(x)
    section=[(origin.y+dy,origin.z+dz) for dy,dz in BAND_SECTION]
    section.append((BAND_SILL_Y,BAND_SILL_Z))
    for (ya,za),(yb,zb) in zip(section,section[1:]):
        if ya<=y<=yb and yb-ya>1e-6:
            return za+(zb-za)*(y-ya)/(yb-ya)
    raise ValueError(('outside ribbon',x,y))

support_cuts=[]
raised_records={}
def grown_panel(name,x,size):
    base=band_basis(x,RAISED_PANEL_LIFT)
    amplitude=math.hypot(base[2].z,base[3].z)
    turn=math.asin(math.sin(math.radians(RAISED_PANEL_RAKE_DEG))/amplitude)-math.atan2(base[2].z,base[3].z)
    rotation=Matrix.Rotation(turn,3,base[1])
    b=(base[0],base[1],rotation@base[2],rotation@base[3])
    w,h=size
    outer=chamfer(w,h,min(.22,w*.12,h*.15))
    top=[point(b,u,v) for u,v in outer]
    # The support's lower edge lies exactly on C's skin, at every corner.
    # Side folds descend to this edge; there is no separate block or plinth.
    footprint=[]
    for u,v in outer:
        p=point(b,u*(w+2*RAISED_PANEL_SHOULDER)/w,v*(h+2*RAISED_PANEL_SHOULDER)/h)
        footprint.append(Vector((p.x,p.y,band_surface_z(p.x,p.y))))
    shoulder=cm(name+' folds grown from ribbon',footprint+top,
                [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],
                [0,0,2,2,0,2,2,0])
    support_cuts.append((horizontal_basis((0,0,3.0)),[(p.x,p.y) for p in footprint],3.0))
    panel(name,b,w,h,RAISED_PANEL_BEZEL,.18,-RAISED_PANEL_WELL_DEPTH)
    raised_records[name]={'size':size,'rake_degrees':math.degrees(math.asin(b[2].z)),
        'centre':list(b[0]),'centre_lift_normal':RAISED_PANEL_LIFT,
        'face_vertical_extent':max(p.z for p in top)-min(p.z for p in top),
        'rear_top_z':max(p.z for p in top),'fold_spread':RAISED_PANEL_SHOULDER}
    return b

kb=grown_panel('ship-key ramp',-RAISED_PANEL_HALF_SPACING,SHIP_PANEL_SIZE)
shipkeys(kb)
nb=grown_panel('selector ramp',SELECTOR_PANEL_X,KNOB_PANEL_SIZE)
knob(nb)
sb=band_basis(SWITCH_PANEL_X)
panel('low four-switch bank',sb,*SWITCH_PANEL_SIZE)
switches(sb)
launch(rake(LAUNCH_D12_CENTRE,LAUNCH_D12_RAKE_DEG),*LAUNCH_D12_SIZE)

# Short, solid centre saddle: a shared shoulder at LAUNCH tapers into the tower.
vs=[]
for y,w,z in SPINE_STATIONS:
    vs.extend([(-w-.16,y,z-.20),(-w,y,z),(w,y,z),(w+.16,y,z-.20)])
fs=[];mi=[]
for j in range(len(SPINE_STATIONS)-1):
    for i in range(3):
        a=j*4+i;fs.append((a,a+1,a+5,a+4));mi.append([2,0,2][i])
cm('launch-to-projector short centre spine',vs,fs,mi)

# Restore D10's exact tier geometry, at its original coordinates and lens height.
tower=loft('Console D12 | three-step faceted pedestal',[(w,h,z,0) for w,h,z in PEDESTAL_LOOPS],
           [face,side,face,body,side],console_col)
for i,start in enumerate([1,3,5]):
    w0,h0,z0=PEDESTAL_LOOPS[start]
    w1,h1,z1=PEDESTAL_LOOPS[start+1]
    v=Vector((0,(h0-h1)/2,z1-z0)).normalized()
    u=Vector((1,0,0));n=u.cross(v)
    bb=(Vector((0,-(h0+h1)/4,(z0+z1)/2)),u,v,n)
    prism('Console D12 | pedestal dark slot '+str(i+1),chamfer((w0+w1)*.29,.10,.025),.012,0,bb,dark,console_col)

# Two small status inserts replace the empty outboard bezel. No extra panel.
vb=band_basis(STATUS_CENTRE_X,.035)
for i in range(2):
    b=shifted(vb,(i-.5)*STATUS_PITCH,0)
    prism('Console D12 | status socket '+str(i),chamfer(.21,.13,.025),.006,-.018,b,dark,console_col)
    prism('Console D12 | status insert '+str(i),chamfer(*STATUS_SIZE,.012),.007,-.005,b,
          cyan if i==0 else screen_mat,console_col)

pod_landings([(4.35,-.05,1.24),(8.6,-2.55,.74)],'continuous band')


# Subtract convex panel footprints from the structural skin, directly as mesh
# polygons. The retained bezel skirts close every opening. No modifiers are
# used, and no underlying deck polygon can pass through a recessed control.
def split_poly(poly,dist):
    inside=[];outside=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da,db=dist(a),dist(b)
        (inside if da>=-1e-7 else outside).append(a)
        if (da>1e-7 and db< -1e-7) or (da< -1e-7 and db>1e-7):
            p=a+(b-a)*(da/(da-db));inside.append(p);outside.append(p)
    return inside,outside

for obj in list(console_col.objects):
    if obj != ribbon and 'short centre spine' not in obj.name:continue
    pieces=[([v.co.copy() for v in [obj.data.vertices[i] for i in f.vertices]],f.material_index) for f in obj.data.polygons]
    for b,outline,depth in apertures + (support_cuts if obj == ribbon else []):
        # Local aperture is CCW. Each edge's left side is inside the cutter.
        planes=[]
        for a,c in zip(outline,outline[1:]+outline[:1]):
            dx,dy=c[0]-a[0],c[1]-a[1]
            planes.append(lambda p,a=a,dx=dx,dy=dy,b=b: dx*((p-b[0]).dot(b[2])-a[1])-dy*((p-b[0]).dot(b[1])-a[0]))
        planes.extend([lambda p,b=b,depth=depth:(p-b[0]).dot(b[3])+max(.65,depth),
                       lambda p,b=b:.056-(p-b[0]).dot(b[3])])
        new=[]
        for poly,idx in pieces:
            if any(all(d(p)<-1e-7 for p in poly) for d in planes):
                new.append((poly,idx));continue
            current=poly
            for d in planes:
                current,exterior=split_poly(current,d)
                if len(exterior)>=3:new.append((exterior,idx))
                if len(current)<3:break
        pieces=new
    vs=[];fs=[];mi=[]
    for poly,idx in pieces:
        fs.append(tuple(range(len(vs),len(vs)+len(poly))));vs.extend(poly);mi.append(idx)
    data=bpy.data.meshes.new(obj.name+' aperture-cut mesh');data.from_pydata(vs,[],fs);data.update()
    for m in obj.data.materials:data.materials.append(m)
    for f,i in zip(data.polygons,mi):f.material_index=i
    obj.data=data

bpy.context.view_layer.update()
scene.camera=gamecam
scene.cycles.samples=PREVIEW_SAMPLES if args.preview else FINAL_SAMPLES
scene.render.resolution_x,scene.render.resolution_y=1920,1080

# Regression and clearance validation is evidence attached to each deliverable.
report={'variant':args.variant,'locked_object_count':len(locked),
        'changed_locked_objects':[n for n,s in locked.items() if n not in bpy.data.objects or signature(bpy.data.objects[n])!=s],
        'locked_signatures':locked,'lens_centre':list(bpy.data.objects['PROJECTOR LENS'].location),
        'camera':{'position':list(gamecam.location),'vertical_fov_degrees':CAMERA_VERTICAL_FOV_DEG},
        'controls':{},'screens':{},'errors':[]}
errors=report['errors']
if report['changed_locked_objects']:errors.append('locked geometry changed')
if any(o.modifiers for o in scene.objects):errors.append('modifiers present')
if sum(o.type=='LIGHT' for o in scene.objects)!=1:errors.append('wrong key light count')
deps=bpy.context.evaluated_depsgraph_get()

def visible_sample(obj,p,source):
    hit,loc,norm,index,ho,matrix=scene.ray_cast(deps,source,(p-source).normalized())
    return hit and ho.original==obj

def blocker_name(obj,p,source):
    hit,loc,norm,index,ho,matrix=scene.ray_cast(deps,source,(p-source).normalized())
    return None if hit and ho.original==obj else (ho.name if hit else 'none')

for n,(obj,verts) in control_faces.items():
    centre=sum(verts,Vector())/len(verts)
    points=[centre]+[centre+(p-centre)*.97 for p in verts]
    pp=[projected(obj.matrix_world@v.co) for v in obj.data.vertices]
    margin=min(min(p.x,1920-p.x,p.y,1080-p.y) for p in pp)
    blockers=[blocker_name(obj,p,gamecam.location) for p in points]
    blocked=sum(n is not None for n in blockers)
    report['controls'][n]={'margin_pixels':margin,'blocked_samples':blocked,'blockers':sorted(set(n for n in blockers if n)),
        'bounds':[min(p.x for p in pp),min(p.y for p in pp),max(p.x for p in pp),max(p.y for p in pp)]}
    if margin<8 or blocked:errors.append([n,'control frame or occlusion',margin,blocked])
keylight=bpy.data.objects['Key | centred in front of all screens']
km=keylight.matrix_world.to_3x3()
emitters=[keylight.location]+[keylight.location+km@Vector((2*math.cos(t*math.pi/4),2*math.sin(t*math.pi/4),0)) for t in range(8)]
for n,size,b,obj,poly in screen_records:
    pts=[point(b,(i/20-.5)*(size[0]-.012),(j/16-.5)*(size[1]-.012),-SCREEN_RECESS)
         for i in range(21) for j in range(17)
         if inside_polygon((i/20-.5)*(size[0]-.012),(j/16-.5)*(size[1]-.012),poly)]
    camera_block=sum(not visible_sample(obj,p,gamecam.location) for p in pts)
    light_block=sum(not visible_sample(obj,p,s) for p in pts[::3] for s in emitters)
    report['screens'][n]={'camera_blocked':camera_block,'light_blocked':light_block,'camera_samples':len(pts),'light_samples':len(pts[::3])*9}
    if camera_block or light_block:errors.append([n,'screen blocked',camera_block,light_block])
intrusions=[]
for o in console_col.objects:
    for v in o.data.vertices:
        p=o.matrix_world@v.co
        if abs(p.x)<2.5 and abs(p.y)<2 and p.z>=2.5:intrusions.append(o.name);break
report['raised_panels']=raised_records
report['console_intrudes_hologram']=intrusions
if intrusions:errors.append('hologram clearance')
(OUT/(STEM+'-checks.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
print('D12 CHECKS',errors or 'PASS',flush=True)
if args.check:raise SystemExit(1 if errors else 0)
assert not errors, errors

def render_d11(cam,suffix):
    scene.camera=cam;scene.render.filepath=str(OUT/(STEM+'-'+suffix+'.png'))
    bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(STEM+'.blend')))
render_d11(gamecam,'camera')
if not args.preview:
    backdrop.hide_render=True
    world_bg.inputs['Color'].default_value=(.19,.22,.27,1);world_bg.inputs['Strength'].default_value=.55
    for inspection_cam,suffix in [(threecam,'three-quarter'),(topcam,'top'),(sidecam,'side'),(frontcam,'front')]:
        render_d11(inspection_cam,suffix)
    backdrop.hide_render=False
    world_bg.inputs['Color'].default_value=(.09,.12,.17,1);world_bg.inputs['Strength'].default_value=.30
scene.camera=gamecam;scene.render.filepath=str(OUT/(STEM+'-camera.png'))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(STEM+'.blend')))
print(STEM+' COMPLETE',flush=True)
