"""Sketch D cockpit massing. Run: blender -b --python out/build_D.py

All geometry is rebuilt from an empty scene. No external models or textures.
The reference PNG is used only for the comparison sheet. Pillow in the local
Miniconda Python is used only to label/combine finished renders, not in the model.
Pass -- --preview to render only the game camera during proportion studies.
"""
from pathlib import Path
import math
import sys
import json
import os
import warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)

# Main design dimensions, in Blender units (metres), Z up.
POD_CANT_DEG = 17.0
POD_BACK_TILT_DEG = 16.0
POD_BANK_DEG = 10.0
LEFT_POD_WIDTH = 5.25
RIGHT_POD_WIDTH = 5.70
LEFT_POD_HEIGHT = 5.55
RIGHT_POD_HEIGHT = 4.40
POD_DEPTH = 0.82
LEFT_POD_ORIGIN = (-6.22, 0.55, 0.02)
RIGHT_POD_ORIGIN = (6.22, 0.55, 0.20)
SHIP_SCREEN_SIZE = (4.25, 3.50)
SHIP_USABLE_RECTANGLE = (3.40, 3.00)
DETAIL_SCREEN_SIZE = (3.76, 0.92)
PRIMARY_SCREEN_SIZE = (4.25, 1.60)
ORDNANCE_SCREEN_SIZE = (4.25, 1.60)
SCREEN_CHAMFER = 0.20
SCREEN_RECESS = 0.09
STRUT_LANDING_POINTS = ((-2.90, 3.05, 0.78), (2.90, 3.05, 0.78))
STRUT_TOP_POINTS = ((-6.30, 4.05, 10.05), (6.30, 4.05, 10.05))
STRUT_WIDTH = 0.46
HEADER_WIDTH = 24.60
HEADER_HEIGHT = 0.62
PEDESTAL_HEIGHT = 1.25
PEDESTAL_BASE_WIDTH = 1.95
PROJECTOR_LOCATION = (0.0, 0.0, 1.25)
DESK_DEPTH = 4.50
DESK_WIDTH = 9.90
DESK_BACK_Y = 1.60
DESK_HEIGHT = 0.30
LAUNCH_SIZE = (2.90, 1.20, 0.13)
HOLOGRAM_CENTRE = (0.0, 0.0, 4.50)
HOLOGRAM_CLEAR_SIZE = (5.0, 4.0, 4.0)  # x, y, z
CAMERA_POSITION = (0.0, -18.0, 6.8)
CAMERA_DIRECTION = (0.0, 0.9936, -0.1132)
CAMERA_VERTICAL_FOV_DEG = 34.11
RESOLUTION = (1920, 1080)
RENDER_SAMPLES = 32
OUT = Path(__file__).resolve().parent
ROOT = OUT.parent
os.environ['OPTIX_CACHE_PATH'] = str(OUT / '.cache/optix')
POSTPROCESS_PYTHON = Path(os.environ.get('USERPROFILE', 'C:/Users/amind')) / 'Miniconda3/python.exe'


def postprocess():
    from PIL import Image, ImageDraw, ImageFont
    metrics = json.loads((OUT / 'measurements.json').read_text())
    src = Image.open(OUT / 'D-camera.png').convert('RGB')
    canvas = src.copy()
    draw = ImageDraw.Draw(canvas)
    font_path = 'C:/Windows/Fonts/arial.ttf'
    font = ImageFont.truetype(font_path, 24)
    small = ImageFont.truetype(font_path, 19)
    big = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 33)
    # Labels are an image-only overlay: there is no text in the Blender model.
    boxes = {
        'SHIP': (63, 340), 'DETAIL': (65, 953),
        'PRIMARY': (1523, 394), 'ORDNANCE': (1515, 894),
        'SHIP PREVIOUS': (410, 1023), 'SHIP NEXT': (625, 1023),
        'SELECTOR KNOB': (1180, 1004), 'LAUNCH': (865, 1030),
        'PRIMARY PREVIOUS': (930, 635), 'PRIMARY NEXT': (960, 692),
        'ORDNANCE PREVIOUS': (930, 799), 'ORDNANCE NEXT': (960, 854),
        'PROJECTOR': (734, 809),
    }
    for name, (bx, by) in boxes.items():
        px, py = metrics['labels'][name]
        bounds = draw.textbbox((0, 0), name, font=font)
        bw, bh = bounds[2] + 24, 40
        is_screen = name in ('SHIP', 'DETAIL', 'PRIMARY', 'ORDNANCE')
        if is_screen:
            bx, by = px-bw/2, py-bh/2
        bx = min(bx, src.width - bw - 12)
        by = min(by, src.height - bh - 8)
        ex = max(bx, min(px, bx + bw))
        ey = max(by, min(py, by + bh))
        colour = '#ffc27c' if name == 'LAUNCH' else '#a8dcf2'
        if not is_screen:
            draw.line([(px, py), (ex, ey)], fill=colour, width=2)
            draw.ellipse((px-4, py-4, px+4, py+4), fill=colour)
        draw.rounded_rectangle((bx, by, bx+bw, by+bh), radius=3,
                               fill='#101d2b', outline=colour, width=1)
        draw.text((bx+12, by+6), name, font=font, fill=colour)
    draw.text((42, 34), 'D  /  FIXED CAMERA', font=big, fill='#c8dfec')
    draw.text((42, 77), 'Screen and control locations', font=small, fill='#97acbd')
    canvas.save(OUT / 'D-camera-labelled.png')
    ref = Image.open(ROOT / 'refs/picked-sketch-D.png').convert('RGB')
    ref = ref.resize((1920, 1080), Image.Resampling.LANCZOS)
    sheet = Image.new('RGB', (3888, 1184), '#101925')
    sheet.paste(ref, (16, 86))
    sheet.paste(src, (1952, 86))
    d = ImageDraw.Draw(sheet)
    d.text((32, 21), 'PICKED SKETCH D', font=big, fill='#dce5ed')
    d.text((1968, 21), 'D MASSING  /  FIXED GAME CAMERA', font=big, fill='#dce5ed')
    sheet.save(OUT / 'D-vs-sketch.png')


if '--postprocess' in sys.argv:
    postprocess()
    raise SystemExit

import bpy
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view
import subprocess

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.materials):
    bpy.data.materials.remove(data)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = RENDER_SAMPLES
scene.cycles.use_denoising = True
scene.cycles.device = 'CPU'
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.render.resolution_x, scene.render.resolution_y = RESOLUTION
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
scene.world = bpy.data.worlds.new('D / flat studio world')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.06, 0.085, 0.13, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.50

def material(name, colour, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*colour, 1)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*colour, 1)
    p.inputs['Roughness'].default_value = 1
    p.inputs['Specular IOR Level'].default_value = 0.15
    if emission:
        p.inputs['Emission Color'].default_value = (*colour, 1)
        p.inputs['Emission Strength'].default_value = emission
    return mat

structure = material('Structure / near-black blue grey', (0.012, 0.020, 0.032))
edge = material('Structural planes / blue grey', (0.025, 0.038, 0.056))
recess = material('Recess / near black', (0.007, 0.013, 0.023))
screen_mat = material('Screen / flat cool blue emission', (0.025, 0.135, 0.30), 0.65)
cyan = material('Small blue accents', (0.045, 0.49, 0.80), 2.0)
orange = material('Launch / flat orange', (1.0, 0.22, 0.022), 0.65)
key_mat = material('Keys and knob', (0.055, 0.075, 0.10))
backdrop_mat = material('Window backdrop / dark blue', (0.006, 0.021, 0.052), 0.75)
for mat, colour, strength in [(screen_mat,(.025,.115,.255),.85),
                              (orange,(1.0,.205,.018),1.0),
                              (backdrop_mat,(.006,.016,.038),1.0)]:
    nodes=mat.node_tree.nodes
    nodes.clear()
    e=nodes.new('ShaderNodeEmission'); e.inputs[0].default_value=(*colour,1); e.inputs[1].default_value=strength
    o=nodes.new('ShaderNodeOutputMaterial'); mat.node_tree.links.new(e.outputs[0],o.inputs[0])
holo = bpy.data.materials.new('Hologram / translucent cyan mass')
holo.use_nodes = True
nt = holo.node_tree
nt.nodes.clear()
outnode = nt.nodes.new('ShaderNodeOutputMaterial')
mix = nt.nodes.new('ShaderNodeMixShader')
mix.inputs[0].default_value = 0.30
transparent = nt.nodes.new('ShaderNodeBsdfTransparent')
em = nt.nodes.new('ShaderNodeEmission')
em.inputs[0].default_value = (0.035, 0.40, 0.65, 1)
em.inputs[1].default_value = 1.25
nt.links.new(transparent.outputs[0], mix.inputs[1])
nt.links.new(em.outputs[0], mix.inputs[2])
nt.links.new(mix.outputs[0], outnode.inputs[0])

groups = {}
def collection(name):
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    groups[name] = col
    return col
shell_col = collection('01 | Joined cockpit structure')
screen_col = collection('02 | Recessed screen planes')
control_col = collection('03 | Controls')
holo_col = collection('04 | Hologram placeholder')
stage_col = collection('05 | Cameras lighting backdrop')
guide_col = collection('06 | Clearance guide (not rendered)')

def put(obj, col):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)
    return obj

def mesh(name, verts, faces, mat, col=shell_col):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    obj = bpy.data.objects.new(name, me)
    col.objects.link(obj)
    obj.data.materials.append(mat)
    return obj

def cube(name, loc, scale, mat, col=shell_col):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return put(obj, col)

def chamfer(w, h, c):
    return [(-w/2+c, -h/2), (w/2-c, -h/2), (w/2, -h/2+c),
            (w/2, h/2-c), (w/2-c, h/2), (-w/2+c, h/2),
            (-w/2, h/2-c), (-w/2, -h/2+c)]

def prism(name, points, front, back, basis, mat, col=shell_col):
    o, u, v, n = basis
    verts = [tuple(o + u*x + v*y + n*d) for d in (front, back) for x,y in points]
    k = len(points)
    faces = [tuple(range(k)), tuple(range(2*k-1,k-1,-1))]
    faces += [(i, (i+1)%k, (i+1)%k+k, i+k) for i in range(k)]
    return mesh(name, verts, faces, mat, col)

def ring(name, outer, inner, front, back, basis, mat, col=shell_col):
    o,u,v,n = basis
    verts = [tuple(o+u*x+v*y+n*d) for d in (front,back) for loop in (outer,inner) for x,y in loop]
    k=len(outer)
    faces=[]
    for i in range(k):
        j=(i+1)%k
        faces += [(i,j,k+j,k+i), (2*k+i,3*k+i,3*k+j,2*k+j),
                  (i,2*k+i,2*k+j,j), (k+i,k+j,3*k+j,3*k+i)]
    return mesh(name,verts,faces,mat,col)

def beam(name,a,b,width,depth,mat=structure):
    obj=cube(name,(Vector(a)+Vector(b))/2,(width,depth,(Vector(b)-Vector(a)).length),mat)
    obj.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler()
    return obj

def cylinder(name, loc, radius, depth, mat, vertices=12, col=shell_col):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj=bpy.context.object
    obj.name=name
    obj.data.materials.append(mat)
    return put(obj,col)

def pod_basis(origin, yaw):
    a=math.radians(yaw); p=math.radians(POD_BACK_TILT_DEG)
    u=Vector((math.cos(a),math.sin(a),0))
    v=Vector((-math.sin(a)*math.sin(p),math.cos(a)*math.sin(p),math.cos(p)))
    n=u.cross(v)
    bank=math.radians(-POD_BANK_DEG if yaw>0 else POD_BANK_DEG)
    u,v=u*math.cos(bank)+v*math.sin(bank),-u*math.sin(bank)+v*math.cos(bank)
    return Vector(origin),u,v,n

def offset_basis(b,u=0,v=0,n=0):
    return (b[0]+u*b[1]+v*b[2]+n*b[3],*b[1:])

labels = {}
screens = []
def add_screen(name, size, b, v, u=0):
    w,h=size
    sb=offset_basis(b,u=u,v=v)
    outer=chamfer(w+0.38,h+0.32,SCREEN_CHAMFER+0.10)
    inner=chamfer(w,h,SCREEN_CHAMFER)
    ring(name+' / structural aperture',outer,inner,0.025,-0.18,sb,edge)
    ring(name+' / black reveal',chamfer(w+.035,h+.035,SCREEN_CHAMFER),
         chamfer(w-.055,h-.055,SCREEN_CHAMFER),0.028,-SCREEN_RECESS-.01,sb,recess)
    obj=mesh(name+' / emissive screen',
        [tuple(sb[0]+sb[1]*x+sb[2]*y-sb[3]*SCREEN_RECESS) for x,y in inner],
        [tuple(range(8))],screen_mat,screen_col)
    obj['true_width']=w; obj['true_height']=h
    obj['inset_depth']=SCREEN_RECESS
    labels[name]=sb[0]-sb[3]*SCREEN_RECESS
    screens.append((name,size,sb,obj))
    return sb

horizontal=(Vector((0,0,DESK_HEIGHT)),Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
desk_points=[(-DESK_WIDTH/2+.6,DESK_BACK_Y-DESK_DEPTH),
             (DESK_WIDTH/2-.6,DESK_BACK_Y-DESK_DEPTH),
             (DESK_WIDTH/2,DESK_BACK_Y-DESK_DEPTH+.6),
             (DESK_WIDTH/2-.35,DESK_BACK_Y-.3),
             (DESK_WIDTH/2-.8,DESK_BACK_Y),(-DESK_WIDTH/2+.8,DESK_BACK_Y),
             (-DESK_WIDTH/2+.35,DESK_BACK_Y-.3),(-DESK_WIDTH/2,DESK_BACK_Y-DESK_DEPTH+.6)]
prism('Low centre desk / single flat deck',desk_points,0,-.58,horizontal,edge)
cube('Continuous rear dash / joins pods pedestal and strut feet',(0,2.85,.53),(22.0,1.50,.62),structure)
cube('Centre deck / rear bridge into sill',(0,1.95,.10),(7.1,1.50,.40),structure)

left=pod_basis(LEFT_POD_ORIGIN,POD_CANT_DEG)
right=pod_basis(RIGHT_POD_ORIGIN,-POD_CANT_DEG)
for name,b,w,h in [('Left',left,LEFT_POD_WIDTH,LEFT_POD_HEIGHT),('Right',right,RIGHT_POD_WIDTH,RIGHT_POD_HEIGHT)]:
    outline=[(x,y+h/2) for x,y in chamfer(w,h,.60)]
    prism(name+' pod / solid faceted back',outline,-.17,-POD_DEPTH,b,structure)
    inner=[(x,y+h/2) for x,y in chamfer(w-.26,h-.26,.50)]
    ring(name+' pod / perimeter flange',outline,inner,.04,-.20,b,edge)
    # The pod is seated in a real block overlapping both its lower body and the dash.
    cube(name+' pod / dash saddle',(b[0].x,2.03,-.05),(w-1.0,1.50,.70),structure)
    ab=offset_basis(b,u=(-w/2+.14 if name=='Left' else w/2-.14),v=h*.47,n=.055)
    prism(name+' pod / blue edge strip',chamfer(.065,.69,.018),0,-.045,ab,cyan)

add_screen('SHIP',SHIP_SCREEN_SIZE,left,3.46)
add_screen('DETAIL',DETAIL_SCREEN_SIZE,left,.86)
primary=add_screen('PRIMARY',PRIMARY_SCREEN_SIZE,right,3.18,.32)
ordnance=add_screen('ORDNANCE',ORDNANCE_SCREEN_SIZE,right,1.16,.32)

for name,sb in [('PRIMARY',primary),('ORDNANCE',ordnance)]:
    for action,dv in [('PREVIOUS',.29),('NEXT',-.29)]:
        kb=offset_basis(sb,u=-2.73,v=dv,n=.05)
        prism(name+' '+action,chamfer(.37,.40,.055),.10,-.16,kb,key_mat,control_col)
        labels[name+' '+action]=kb[0]+kb[3]*.10

# Broad feet and a low tapered centre pedestal share the deck and rear dash.
base=chamfer(PEDESTAL_BASE_WIDTH,1.70,.25)
top=chamfer(1.14,.94,.16)
verts=[(x,y,DESK_HEIGHT-.03) for x,y in base]+[(x,y,PEDESTAL_HEIGHT-.12) for x,y in top]
mesh('Centre pedestal / low tapered mass',verts,
     [tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],edge)
cylinder('Projector / dark rim',(0,0,PEDESTAL_HEIGHT-.085),.55,.19,recess)
cylinder('Projector / lens',PROJECTOR_LOCATION,.43,.035,cyan,col=control_col)
labels['PROJECTOR']=Vector(PROJECTOR_LOCATION)+Vector((0,0,.0175))
cube('Pedestal / blue strip',(0,-.70,.58),(.64,.035,.055),cyan)

launch_basis=offset_basis(horizontal,v=-1.83,n=.05)
prism('Launch / socket',chamfer(LAUNCH_SIZE[0]+.36,LAUNCH_SIZE[1]+.28,.26),.06,-.07,launch_basis,recess,control_col)
prism('LAUNCH',chamfer(LAUNCH_SIZE[0],LAUNCH_SIZE[1],.22),LAUNCH_SIZE[2],.01,launch_basis,orange,control_col)
labels['LAUNCH']=launch_basis[0]+Vector((0,0,LAUNCH_SIZE[2]))
for name,x in [('SHIP PREVIOUS',-3.48),('SHIP NEXT',-2.67)]:
    kb=offset_basis(horizontal,u=x,v=-1.72,n=.05)
    prism(name,chamfer(.60,1.00,.08),.18,-.06,kb,key_mat,control_col)
    labels[name]=kb[0]+Vector((0,0,.18))
cylinder('Selector / socket',(2.72,-1.60,.37),.54,.14,recess,col=control_col)
cylinder('SELECTOR KNOB',(2.72,-1.60,.61),.45,.40,key_mat,col=control_col)
labels['SELECTOR KNOB']=Vector((2.72,-1.60,.83))

struts=[]
for side,lo,hi in zip(('Left','Right'),STRUT_LANDING_POINTS,STRUT_TOP_POINTS):
    struts.append(beam(side+' canopy / main swept strut',lo,hi,STRUT_WIDTH,.62))
    # Small face-mounted blue dash and canopy inserts, not large luminous ribs.
    low=Vector(lo); high=Vector(hi); delta=high-low
    for t,l in [(.82,.58)]:
        centre=low+delta*t+Vector((0,-.300,0))
        beam(side+' canopy / blue accent',centre-delta.normalized()*l/2,centre+delta.normalized()*l/2,.075,.035,cyan)
    cube(side+' dash / accent',(lo[0]+(-.35 if side=='Left' else .35),2.095,.72),(.64,.04,.055),cyan)
    s=-1 if side=='Left' else 1
    beam(side+' canopy / outer return',(s*10.75,2.85,.55),(s*12.00,4.05,10.05),.56,.66)
cube('Canopy / continuous header',(0,4.05,10.05),(HEADER_WIDTH,.88,HEADER_HEIGHT),structure)
cube('Canopy / header lower face',(0,3.57,9.84),(HEADER_WIDTH-.4,.08,.12),edge)
for x in (-8.12,0,8.12):
    cube('Header / small blue insert',(x,3.60,9.96),(.90,.035,.065),cyan)

# A simple angular translucent fighter. Its long axis is tipped toward the viewer
# so it reads as a ship-shaped volume, not a billboard or a finished ship asset.
ship_outline=[(0,-2.05),(.47,-1.50),(.57,-.42),(1.97,.08),(2.12,.64),
              (.73,.50),(.62,1.78),(.31,1.28),(0,1.57),(-.31,1.28),
              (-.62,1.78),(-.73,.50),(-2.12,.64),(-1.97,.08),(-.57,-.42),(-.47,-1.50)]
rotation=Matrix.Rotation(math.radians(43),4,'X') @ Matrix.Rotation(math.radians(-12),4,'Z')
ship_origin=Vector((0,0,5.10))
def ship_point(p):
    return tuple(ship_origin+rotation.to_3x3()@Vector(p))
ship_verts=[ship_point((x,y,z)) for z in (-.12,.12) for x,y in ship_outline]
k=len(ship_outline)
ship_faces=[tuple(reversed(range(k))),tuple(range(k,2*k))]+[(i,(i+1)%k,(i+1)%k+k,i+k) for i in range(k)]
ship=mesh('Hologram / translucent fighter envelope',ship_verts,ship_faces,holo,holo_col)
line_mat=material('Hologram / pale blue edges',(.055,.54,.90),1.5)
def line(name,points,mat,radius=.009,col=holo_col):
    data=bpy.data.curves.new(name,'CURVE'); data.dimensions='3D'; data.bevel_depth=radius; data.bevel_resolution=0; data.resolution_u=1
    sp=data.splines.new('POLY'); sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points): p.co=(*co,1)
    obj=bpy.data.objects.new(name,data); col.objects.link(obj); obj.data.materials.append(mat)
    return obj
line('Hologram / perimeter',[ship_point((x,y,.13)) for x,y in ship_outline+[ship_outline[0]]],line_mat)
for x in (-.40,.40):
    line('Hologram / fuselage edge',[ship_point((0,-1.93,.14)),ship_point((x,-.82,.29)),ship_point((x,.90,.20)),ship_point((0,1.50,.14))],line_mat)
# Thin translucent triangular projection fans, deliberately simple flat shapes.
fan_mat=holo.copy(); fan_mat.name='Projection fan / transparent blue'; fan_mat.node_tree.nodes.get('Mix Shader').inputs[0].default_value=.035
mesh('Projector / two faint flat light fans',[(0,0,1.27),(-1.35,.1,3.40),(1.35,.1,3.40),
     (0,0,1.27),(-.4,-1.1,3.35),(.4,1.1,3.35)],[(0,1,2),(3,4,5)],fan_mat,holo_col)

clear=cube('Reserved hologram volume / 5w x 4d x 4h',HOLOGRAM_CENTRE,HOLOGRAM_CLEAR_SIZE,edge,guide_col)
clear.hide_render=True
clear.display_type='WIRE'
clear.hide_set(True)
backdrop=cube('Window / uniform dark blue space',(0,6.0,6.0),(100,.10,60),backdrop_mat,stage_col)

def camera(name,loc,target=None,ortho=None):
    data=bpy.data.cameras.new(name)
    obj=bpy.data.objects.new(name,data); stage_col.objects.link(obj)
    obj.location=loc
    direction=Vector(target)-Vector(loc) if target else Vector(CAMERA_DIRECTION)
    obj.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
    if ortho:
        data.type='ORTHO'; data.ortho_scale=ortho
    else:
        data.sensor_fit='VERTICAL'; data.sensor_height=24
        data.lens=data.sensor_height/(2*math.tan(math.radians(CAMERA_VERTICAL_FOV_DEG)/2))
    data.clip_end=200
    return obj
gamecam=camera('D / FIXED GAME CAMERA',CAMERA_POSITION)
gamecam['locked_position']=str(CAMERA_POSITION)
gamecam['locked_direction']=str(CAMERA_DIRECTION)
gamecam['vertical_fov_degrees']=CAMERA_VERTICAL_FOV_DEG
gamecam.lock_location=(True,True,True); gamecam.lock_rotation=(True,True,True)
scene.camera=gamecam
threecam=camera('D / three quarter',(17,-23,17),(0,1,4.6),ortho=29.0)
topcam=camera('D / top orthographic',(0,0,34),(0,0,0),ortho=27.0)
sidecam=camera('D / side orthographic',(30,-.6,5),(0,-.6,5),ortho=21.0)

def area(name,loc,power,size,target):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    obj=bpy.data.objects.new(name,data); stage_col.objects.link(obj); obj.location=loc
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
area('Key / broad frontal',(0,-9,12),1500,11,(0,0,3))
area('Fill / left',(-10,-3,7),600,8,(0,1,3))
area('Rim / above',(3,4,13),1000,9,(0,0,3))

bpy.context.view_layer.update()
# Fit only the three inspection cameras; never alter the fixed game camera.
# Use the actual model bounds, with 6% margins on all four sides.
subject_points=[obj.matrix_world@Vector(corner)
    for col in (shell_col,screen_col,control_col,holo_col)
    for obj in col.objects for corner in obj.bound_box]
for inspection_cam in (threecam,topcam,sidecam):
    inv=inspection_cam.matrix_world.inverted()
    local=[inv@p for p in subject_points]
    xmin,xmax=min(p.x for p in local),max(p.x for p in local)
    ymin,ymax=min(p.y for p in local),max(p.y for p in local)
    rot=inspection_cam.matrix_world.to_3x3()
    inspection_cam.location+=rot@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0))
    inspection_cam.data.ortho_scale=max((xmax-xmin)/.88,(ymax-ymin)*(16/9)/.88)
bpy.context.view_layer.update()
def projected(p,cam=gamecam):
    c=world_to_camera_view(scene,cam,Vector(p))
    return (round(c.x*RESOLUTION[0],2),round((1-c.y)*RESOLUTION[1],2))
report={'camera':{'position':CAMERA_POSITION,'direction':CAMERA_DIRECTION,'vertical_fov':CAMERA_VERTICAL_FOV_DEG},
        'labels':{name:projected(p) for name,p in labels.items()},'screens':{}}
focal_pixels=RESOLUTION[1]/(2*math.tan(math.radians(CAMERA_VERTICAL_FOV_DEG)/2))
for name,size,b,obj in screens:
    centre=b[0]-SCREEN_RECESS*b[3]
    eye=(gamecam.location-centre).normalized()
    width_ratio=math.sqrt(1-b[1].dot(eye)**2)
    local_view_normal=abs(b[3].dot(eye))
    p0=projected(centre-b[1]*size[0]/2); p1=projected(centre+b[1]*size[0]/2)
    horizontal_px=abs(p1[0]-p0[0])
    coords=[projected(v.co) for v in obj.data.vertices]
    # Perspective width relative to a camera-parallel equal-width segment at its midpoint.
    forward=Vector(CAMERA_DIRECTION).normalized()
    z=(centre-gamecam.location).dot(forward)
    camera_width_ratio=horizontal_px/(focal_pixels*size[0]/z)
    report['screens'][name]={'width':size[0],'height':size[1],
      'width_retained_local_view':width_ratio,'horizontal_width_retained_camera':camera_width_ratio,
      'face_cosine':local_view_normal,'projected_width_pixels':horizontal_px,
      'projected_corners':coords,'centre':list(centre)}
    assert width_ratio>=.70 and camera_width_ratio>=.70, (name,'too oblique')
    assert all(0<x<RESOLUTION[0] and 0<y<RESOLUTION[1] for x,y in coords), (name,'clipped')
    occluded=[]
    deps=bpy.context.evaluated_depsgraph_get()
    for ix in range(9):
        for iy in range(7):
            sx=(ix/8-.5)*(size[0]-2*SCREEN_CHAMFER-.10)
            sy=(iy/6-.5)*(size[1]-2*SCREEN_CHAMFER-.10)
            target=centre+b[1]*sx+b[2]*sy
            hit,loc,normal,index,hit_obj,matrix=scene.ray_cast(deps,gamecam.location,(target-gamecam.location).normalized())
            if not hit or hit_obj.original != obj:
                occluded.append(hit_obj.name if hit_obj else 'no hit')
    report['screens'][name]['visibility_samples']=63
    report['screens'][name]['occluded_samples']=len(occluded)
    if occluded:
        print('SCREEN OCCLUSION:',name,len(occluded),sorted(set(occluded)),flush=True)
    assert not occluded,(name,'screen is obscured',occluded)
assert SHIP_SCREEN_SIZE[0]-2*SCREEN_CHAMFER>=SHIP_USABLE_RECTANGLE[0]
assert SHIP_SCREEN_SIZE[1]-2*SCREEN_CHAMFER>=SHIP_USABLE_RECTANGLE[1]
assert PRIMARY_SCREEN_SIZE[0]>=4.13 and PRIMARY_SCREEN_SIZE[1]>=1.45
assert ORDNANCE_SCREEN_SIZE==PRIMARY_SCREEN_SIZE
for obj in struts:
    verts=[obj.matrix_world@v.co for v in obj.data.vertices]
    # Entire struts are outboard of the reserved x slab, a stronger check than
    # intersection at any one elevation or a single sample of the silhouette.
    assert min(abs(v.x) for v in verts)>HOLOGRAM_CLEAR_SIZE[0]/2
report['hologram_clearance']='Both canopy struts remain entirely outside x = +/-2.5.'
report['projector']=PROJECTOR_LOCATION
report['controls']={}
for name,point in labels.items():
    if name in report['screens']:
        continue
    px,py=projected(point)
    assert 0<px<1920 and 0<py<1080,(name,'control outside frame')
    hit,loc,normal,index,obj,matrix=scene.ray_cast(bpy.context.evaluated_depsgraph_get(),gamecam.location,(point-gamecam.location).normalized())
    expected='Projector / lens' if name=='PROJECTOR' else name
    assert hit and obj.name.startswith(expected),(name,'control obscured',obj.name if obj else None)
    report['controls'][name]='Visible from fixed camera; ray hits '+obj.name
for p in ship_verts:
    for axis in range(3):
        assert abs(p[axis]-HOLOGRAM_CENTRE[axis])<HOLOGRAM_CLEAR_SIZE[axis]/2,(p,'hologram outside clearance')
report['hologram_placeholder_bounds']={
    'min':[min(v[a] for v in ship_verts) for a in range(3)],
    'max':[max(v[a] for v in ship_verts) for a in range(3)]}
(OUT/'measurements.json').write_text(json.dumps(report,indent=2))

notes=[]
notes.append('This is a rough camera-led massing of picked sketch D, with the larger SHIP-screen hierarchy from E. All dimensions are Blender units, Z up. There are no textures, model text, bevel modifiers, or finished ship details. The labelled image is an overlay on the unlabelled render.')
notes.append('The game camera remains at (0, -18, 6.8), points along the normalized (0, 0.9936, -0.1132) direction, and has a vertical field of view of 34.11 degrees. Its render is 1920 by 1080.')
for name,data in report['screens'].items():
    retain=100*data['width_retained_local_view']
    notes.append(f"The {name} screen is {data['width']:.2f} wide by {data['height']:.2f} high in its own plane, before its 0.20-unit corner cuts. It retains {retain:.2f}% of its true width relative to the camera's local sightline, a width loss of {100-retain:.2f}%. Its projected centreline width is {data['projected_width_pixels']:.1f} pixels. All 63 interior visibility rays reach the screen without obstruction.")
notes.append('The SHIP plane contains an unobstructed 3.40 by 3.00 rectangle. Its bounding area is 14.875 square units, compared with 6.80 for each matched weapon screen and 3.4592 for DETAIL. PRIMARY and ORDNANCE are each larger than the required 4.13 by 1.45. All four screens are recessed 0.09 behind their frames.')
notes.append('Width retention is the length of the unit screen-width vector projected perpendicular to the sightline from that screen centre to the camera: sqrt(1 - dot(width_axis, sightline)^2). This separates foreshortening from perspective scale. The separate horizontal image-space ratios in measurements.json are slightly over 100% because the off-axis pods turn toward the eye; that does not mean the physical screens grew. Both measures exceed 70%.')
notes.append(f'The pods cant inward by {POD_CANT_DEG:g} degrees, tilt back by {POD_BACK_TILT_DEG:g} degrees and bank outward by {POD_BANK_DEG:g} degrees. Left pod width is {LEFT_POD_WIDTH:.2f}; right pod width is {RIGHT_POD_WIDTH:.2f}. The centre desk is {DESK_DEPTH:.2f} deep. The projector lens is centred at (0, 0, {PEDESTAL_HEIGHT:.2f}).')
notes.append('A hidden wire guide reserves x = -2.5 to 2.5, y = -2 to 2, z = 2.5 to 6.5 for the hologram. The translucent fighter placeholder fits inside that box, in its upper portion. Both main canopy struts remain wholly outside the box in X and sit behind it in Y. Their landing centres are (-2.90, 3.05, 0.78) and (2.90, 3.05, 0.78). The header and outer returns complete three window openings.')
notes.append('The pods seat into rear saddles and the continuous sill. The sill meets the strut feet and the centre deck bridge, and the pedestal overlaps the centre deck. Keys, knob, launch socket and projector meet their supports. These are editable overlapping masses, not a final watertight production mesh. The hologram and its light fans are the only deliberately suspended display elements.')
notes.append('The main departure from D is the taller left pod and nearly square SHIP area, required by the usable 3.40 by 3.00 rectangle and the owner’s instruction to make that screen bigger. Its shallow DETAIL strip preserves the hierarchy shown in E. The right pod is lower, keeping its two equal weapon screens rather than stretching them to match the taller left pod.')
notes.append('The specified camera and lens position place the projector at about 81% of image height, lower than in the drawing. The centre desk consequently sits near the bottom edge and is cropped in front. The camera and lens were not moved to force a traced silhouette. The canopy and sill sit farther behind the pods to prevent struts or dash faces covering the screen rectangles. The two navigation keys on each weapon screen take a small inboard rail absent from D’s blank screen study.')
notes.append('The background is deliberately uniform dark blue, the glazing is represented by open panes, and the projected fighter is only a translucent angular placeholder. Star fields, surface texture, UI content and glow polish are omitted to keep the review about mass and framing.')
notes.append('I would ask the owner whether the taller left pod gives SHIP enough priority; whether the right pod should rise to align its top with SHIP despite leaving more space around its smaller screens; and whether the launch button feels sufficiently prominent at the fixed camera’s low foreground position.')
notes.append('Rebuild all deliverables by running the supplied Blender executable with -b --python out/build_D.py from the project folder. The script clears the scene, rebuilds and saves D.blend, renders all four views, writes these notes and measurements, then uses the installed local Miniconda Python with Pillow to create the labels and reference comparison. It reads refs/picked-sketch-D.png for the comparison only and uses no network. Add -- --preview for just the game-camera render.')
(OUT/'notes.md').write_text('\n\n'.join(notes)+'\n',encoding='utf-8')

def render(cam,filename):
    scene.camera=cam
    scene.render.filepath=str(OUT/filename)
    bpy.ops.render.render(write_still=True)

scene.camera=gamecam
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D.blend'))
render(gamecam,'D-camera.png')
if '--preview' not in sys.argv:
    backdrop.hide_render=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.18,.21,.26,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
    for cam,name in [(threecam,'D-three-quarter.png'),(topcam,'D-top.png'),(sidecam,'D-side.png')]:
        render(cam,name)
    backdrop.hide_render=False
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.06,.085,.13,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.50
    scene.camera=gamecam
    scene.render.filepath=str(OUT/'D-camera.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'D.blend'))
    subprocess.run([str(POSTPROCESS_PYTHON),str(Path(__file__).resolve()),'--postprocess'],check=True)
print('D BUILD COMPLETE',flush=True)
