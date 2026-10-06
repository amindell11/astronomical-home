from pathlib import Path
import math
import sys

import bpy
import bmesh
from mathutils import Quaternion

ROOT = Path('D:/amind/git/agent-1')
SHIP = ROOT / 'art/ships/nightshade'
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
origin = bpy.data.objects['SymmetryOrigin']
assert not [o for o in scene.objects if o.type == 'MESH']

def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    return mat

hull = material('Blockout slate', (.30, .34, .43))
glass = material('Blockout canopy', (.30, .26, .66))
accent = material('Blockout tip region', (.70, .12, .67))

def part(name, verts, faces, role='hull', mat=hull, subdivision=0):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.uv_layers.new(name='PaintUV')
    ob = bpy.data.objects.new(name, mesh)
    bpy.data.collections['role.' + role].objects.link(ob)
    ob.parent = origin
    mesh.materials.append(mat)
    mirror = ob.modifiers.new('Live bilateral symmetry', 'MIRROR')
    mirror.mirror_object = origin
    mirror.use_clip = True
    mirror.merge_threshold = .0001
    if subdivision:
        sub = ob.modifiers.new('Editable surface', 'SUBSURF')
        sub.levels = subdivision
        sub.render_levels = subdivision
    return ob

def half_body(name, stations, mat=hull, role='hull', subdivision=1):
    verts, faces = [], []
    angles = [0, 25, 50, 75, 90, 110, 140, 165, 180]
    for y, width, top, bottom in stations:
        for deg in angles:
            t = math.radians(deg)
            z = top * math.cos(t) if deg <= 90 else -bottom * math.cos(t)
            verts.append((width * math.sin(t), y, z))
    n = len(angles)
    for j in range(len(stations) - 1):
        for i in range(n - 1):
            a = j * n + i
            faces.append((a, a + 1, a + n + 1, a + n))
    for end in (0, len(stations)-1):
        c = len(verts)
        verts.append((0, stations[end][0], 0))
        for i in range(n-1):
            faces.append((c, end*n+i, end*n+i+1))
    return part(name, verts, faces, role, mat, subdivision)

body = half_body('Fuselage', [
    (2.00,.05,.055,-.055),(1.96,.20,.08,-.08),(1.83,.35,.13,-.12),
    (1.60,.48,.21,-.16),(1.32,.55,.24,-.18),(1.03,.57,.23,-.18),
    (.72,.52,.21,-.15),(.36,.46,.19,-.12),(.02,.37,.14,-.09),
    (-.30,.26,.10,-.07),(-.58,.15,.065,-.035),(-.67,.035,.018,-.012)])

def canopy(name, stations, lower=False):
    verts, faces = [], []
    for y, width, edge_z, peak_z in stations:
        for a in (0, 22.5, 45, 67.5, 90):
            t = math.radians(a)
            z = edge_z + (peak_z-edge_z)*math.cos(t)
            verts.append((width*math.sin(t),y,z))
    for j in range(len(stations)-1):
        for i in range(4):
            a=j*5+i
            faces.append((a,a+1,a+6,a+5))
    ob=part(name,verts,faces,'canopy',glass,1)
    solid=ob.modifiers.new('Canopy thickness','SOLIDIFY')
    solid.thickness=.018
    return ob

canopy('Upper canopy',[
    (1.94,.075,.075,.095),(1.87,.21,.105,.18),(1.68,.36,.14,.30),
    (1.42,.43,.14,.39),(1.17,.455,.125,.415),(.99,.445,.11,.38),
    (.94,.43,.11,.35)])
canopy('Lower canopy',[
    (1.93,.07,-.076,-.09),(1.83,.23,-.09,-.15),(1.60,.37,-.11,-.22),
    (1.34,.435,-.11,-.235),(1.08,.45,-.10,-.22),(.96,.43,-.095,-.20)],True)

def px(x,y):
    return ((x-765)/200,(540-y)/200)

def plate(name, outline, center, z_func, thickness, tip_material=False):
    verts, faces = [], []
    xy=[px(*p) for p in outline]
    cx,cy=px(*center)
    for x,y in xy:
        verts.append((x,y,z_func(x,y)))
    ridge=len(verts)
    verts.append((cx,cy,z_func(cx,cy)+thickness*.65))
    for x,y in xy:
        verts.append((x,y,z_func(x,y)-thickness))
    bottom=len(verts)
    verts.append((cx,cy,z_func(cx,cy)-thickness*1.15))
    n=len(xy)
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,ridge),(n+1+j,n+1+i,bottom),(i,n+1+i,n+1+j,j)])
    ob=part(name,verts,faces)
    if tip_material:
        ob.data.materials.append(accent)
        for poly in ob.data.polygons:
            if sum(ob.data.vertices[v].co.y for v in poly.vertices)/len(poly.vertices)<-1.19:
                poly.material_index=1
    bevel=ob.modifiers.new('Soft blockout edges','BEVEL')
    bevel.width=.022
    bevel.segments=2
    return ob

plate('Outer wing shoulders',[
    (918,119),(946,146),(982,172),(1020,210),(1052,249),(1080,285),
    (1106,329),(1136,415),(1169,514),(1090,517),(1070,504),
    (1050,473),(1041,431),(1018,384),(989,349),(952,323),
    (906,317),(891,298),(898,269),(921,241),(907,196),(919,178)],
    (1014,302),lambda x,y:.025 + .015*y,.07)
plate('Outer wing blades',[
    (1090,520),(1177,518),(1207,619),(1229,668),(1255,722),
    (1281,748),(1285,788),(1278,830),(1243,781),(1204,734),
    (1145,664),(1085,589),(1041,533)],
    (1172,661),lambda x,y:.01-.022*y,.065,True)
plate('Inner swept fins',[
    (908,320),(930,373),(950,450),(972,521),(960,540),
    (939,549),(901,533),(873,486),(862,448),(880,392)],
    (911,448),lambda x,y:.13-.13*y,.085)

tail_stations=[
    # image y, inner x, outer x, vertical center, vertical half-thickness
    (460,857,898,.065,.055),(502,868,916,.050,.060),
    (548,887,927,.055,.075),(585,894,941,.080,.085),
    (627,884,947,.135,.085),(666,866,934,.235,.080),
    (706,841,910,.345,.070),(750,826,884,.425,.060),
    (801,789,859,.465,.048),(848,785,833,.488,.035),
    (902,785,811,.510,.023),(958,790,796,.535,.006)]
verts,faces=[],[]
for py,inner,outer,z,h in tail_stations:
    x0,y=px(inner,py)
    x1,_=px(outer,py)
    w=x1-x0
    for x,dz in [(x0,0),(x0+w*.16,h*.75),(x0+w*.60,h),
                 (x1,h*.15),(x1-w*.16,-h*.75),(x0+w*.43,-h)]:
        verts.append((x,y,z+dz))
for j in range(len(tail_stations)-1):
    for i in range(6):
        a=j*6+i
        faces.append((a,j*6+(i+1)%6,(j+1)*6+(i+1)%6,a+6))
for j in (0,len(tail_stations)-1):
    for i in range(1,5):
        faces.append((j*6,j*6+i,j*6+i+1))
tail=part('Rising tail blades',verts,faces)
bevel=tail.modifiers.new('Sculpted edge softness','BEVEL')
bevel.width=.018
bevel.segments=2

top=bpy.data.objects['reference.top']
top.empty_display_size=1531/200
top.location=(0, (540-1027/2)/200, -.32)
side=bpy.data.objects['reference.side']
side.empty_display_size=1536/320
side.location=(3.0,-.10, -.10)
side.hide_set(True)
top.color[3]=.40
side.color[3]=.45
for img in bpy.data.images:
    if img.source=='FILE':
        img.pack()
        img.filepath='//concepts/'+Path(img.filepath).name

for name in ('camera.top','camera.side','camera.front'):
    bpy.data.objects[name].data.ortho_scale=6
scene.camera=bpy.data.objects['camera.top']
bpy.ops.object.select_all(action='DESELECT')
tail.select_set(True)
bpy.context.view_layer.objects.active=tail
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=7
            area.spaces.active.region_3d.view_location=(0,0,.15)
            area.spaces.active.region_3d.view_rotation=Quaternion((.880,.280,.116,.365))
            area.spaces.active.shading.type='SOLID'
            area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(SHIP/'Nightshade.blend'))
