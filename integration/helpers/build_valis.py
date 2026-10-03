import bpy, math, json, bmesh, sys
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

OUT = Path('D:/amind/git/agent-2/results/valis-geometry')
SOURCE = Path('D:/amind/git/agent-2/art/ships/valis/Valis.blend')
UPPER_WING_SPAN_SLOPE = .17
LOWER_WING_HEIGHT = -0.20
FIN_DROP = -0.64
PLATE_THICKNESS = 0.115

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.name = 'Valis geometry'
parts = bpy.data.collections.new('Valis - editable parts')
scene.collection.children.link(parts)
root = bpy.data.objects.new('Valis symmetry origin', None)
parts.objects.link(root)
root.empty_display_type = 'PLAIN_AXES'
root.empty_display_size = .5
root['forward'] = '-Y; Z up; X bilateral symmetry'
root['milestone'] = 'Geometry review; temporary flat color materials; no runtime integration'
materials = {}
for name, rgb in {'Ivory':(.84,.82,.72), 'Cool gray':(.48,.50,.54), 'Graphite':(.065,.077,.105), 'Canopy':(.032,.039,.055), 'Lavender':(.40,.34,.77), 'Edge':(.16,.18,.23), 'Engine':(.60,.61,.63)}.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb,1)
    materials[name] = m

def mesh(name, verts, faces, color, mirror=True, solid=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    if not solid: bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    obj = bpy.data.objects.new(name, data)
    parts.objects.link(obj)
    obj.parent = root
    obj.data.materials.append(materials[color])
    obj.color = materials[color].diffuse_color
    if mirror:
        mod = obj.modifiers.new('Live bilateral symmetry', 'MIRROR')
        mod.mirror_object = root
        mod.use_clip = True
        mod.use_mirror_merge = True
        mod.merge_threshold = .0001
    if solid:
        mod = obj.modifiers.new('Editable plate thickness', 'SOLIDIFY')
        mod.thickness = solid
        mod.offset = -1
    return obj

def plate(name, points, height, color, thickness=PLATE_THICKNESS):
    verts = [((x-764)/100,(500-y)/100,height((x-764)/100,(500-y)/100)) for x,y in points]
    vectors = [Vector(v) for v in verts]
    tris = tessellate_polygon([vectors])
    faces = [tuple(t) if (vectors[t[1]]-vectors[t[0]]).cross(vectors[t[2]]-vectors[t[0]]).z > 0 else tuple(reversed(t)) for t in tris]
    return mesh(name, verts, faces, color, solid=thickness)

def loft(name, rings, color):
    verts=[]
    for y,w,top,bottom in rings:
        verts.extend([(0,y,top),(w*.70,y,top-.035),(w,y,(top+bottom)*.5),(w*.77,y,bottom),(0,y,bottom)])
    faces=[]
    for k in range(len(rings)-1):
        for j in range(4):
            a=k*5+j; faces.append((a,a+1,a+6,a+5))
    faces += [tuple(range(4,-1,-1)),tuple((len(rings)-1)*5+j for j in range(5))]
    return mesh(name,verts,faces,color)

HULL_PROFILE = [(-4.02,.28,-.30,-.49),(-3.52,.70,.045,-.51),(-2.85,1.10,.16,-.55),(-2.45,1.10,.28,-.56),(-1.38,1.09,.47,-.53),(-.70,1.0,.61,-.42),(1.3,.72,.88,-.36),(2.5,.45,.79,-.25),(3.34,.30,.40,-.18)]
def hull_profile(y):
    for a,b in zip(HULL_PROFILE,HULL_PROFILE[1:]):
        if a[0] <= y <= b[0]:
            t=(y-a[0])/(b[0]-a[0])
            return tuple(a[i]+t*(b[i]-a[i]) for i in range(1,4))
    raise ValueError('Armor exceeds the fuselage profile')
def hull_roof(x,y):
    w,top,bottom=hull_profile(y)
    t=(x/w-.7)/.3
    return top-.035 + t*((top+bottom)*.5-top+.035)+.012
def hull_belly(x,y):
    return hull_profile(y)[2]+.008
loft('01 central fuselage',HULL_PROFILE,'Graphite')
loft('02 black canopy', [(-3.51,.27,.02,-.10),(-3.24,.43,.16,-.03),(-2.27,.61,.45,.13),(-1.51,.88,.58,.25),(-1.27,.74,.62,.30)], 'Canopy')
cheek_vertices=[]
for y,w,top,bottom in HULL_PROFILE[1:5]:
    for t in [.08,.92]:
        cheek_vertices.append((w*(.70+.30*t),y,top-.035+t*((top+bottom)*.5-top+.035)+.03))
mesh('03 canopy side cheeks',cheek_vertices,[(i,i+1,i+3,i+2) for i in range(0,6,2)],'Lavender',solid=.045)
plate('04 dorsal armor', [(764,296),(804,302),(808,535),(825,559),(820,615),(764,622)], lambda x,y:.63+.145*(y+.9), 'Ivory',.14)
plate('05 dorsal shoulder bevel',[(804,302),(844,431),(854,597),(820,619),(825,559),(808,535)],lambda x,y:.61+.145*(y+.9)-.8*(x-.55), 'Cool gray',.13)
plate('06 dorsal lavender rails',[(844,435),(869,638),(837,610)],lambda x,y:.40+.14*(y+.8),'Lavender',.065)
plate('07 rear crest',[(764,274),(808,273),(811,305),(783,373),(764,378)],lambda x,y:1.06+.08*(y-1.6),'Lavender',.085)
belly_vertices=[]
for y,w,top,bottom in HULL_PROFILE:
    belly_vertices.extend([(0,y,bottom-.014),(.70*w,y,bottom-.014)])
mesh('08 ventral armor',belly_vertices,[(i,i+1,i+3,i+2) for i in range(0,len(belly_vertices)-2,2)],'Ivory',solid=.025)

upper=[(901,549),(949,536),(991,512),(1025,481),(1054,435),(1075,394),(1320,215),(1299,315),(1218,423),(1129,545),(1090,546),(1022,627),(953,695),(911,702),(882,646)]
def upper_z(x,y): return .30 + UPPER_WING_SPAN_SLOPE*(x-1.3) + .23*(y+.5)
plate('10 upper swept wings',upper,upper_z,'Ivory',.14)
plate('11 upper wing gray planes',[(911,554),(968,541),(1020,504),(1108,410),(1276,288),(1252,361),(1218,423),(1129,545),(1090,546),(1022,627),(953,677),(916,683)],lambda x,y:upper_z(x,y)+.015,'Cool gray',.025)
plate('12 upper wing lavender tips',[(1320,215),(1276,243),(1263,318),(1221,376),(1218,423),(1299,315)],lambda x,y:upper_z(x,y)+.025,'Lavender',.045)
plate('13 charcoal trailing spars',[(1090,546),(1129,545),(1072,630),(991,715),(954,741),(917,702),(953,678),(1022,627)],lambda x,y:upper_z(x,y)-.10,'Graphite',.23)

lower=[(939,632),(974,580),(1027,506),(1097,461),(1345,406),(1312,477),(1222,544),(1094,629),(995,706),(954,724)]
def lower_z(x,y): return LOWER_WING_HEIGHT+.045*(x-1.8)+.16*(y+.9)
plate('20 lower swept blades',lower,lower_z,'Cool gray',.13)
plate('21 lower blade lavender tips',[(1345,406),(1304,424),(1285,467),(1225,513),(1210,551),(1312,477)],lambda x,y:lower_z(x,y)+.015,'Lavender',.04)
plate('22 lower blade dark inner facets',[(954,724),(994,706),(1094,629),(1222,544),(1123,588),(1014,634),(962,661)],lambda x,y:lower_z(x,y)+.016,'Edge',.035)

fin=[(1077,637),(1110,640),(1268,764),(1265,793),(1085,774),(1034,714),(1049,681)]
def fin_z(x,y): return -.29 + FIN_DROP*max(0,(x-2.8)/2.3)+.22*(y+1.8)
plate('30 forward lower fins',fin,fin_z,'Ivory',.10)
plate('31 forward fin lavender edges',[(1070,766),(1106,750),(1225,774),(1243,794),(1085,774)],lambda x,y:fin_z(x,y)+.02,'Lavender',.035)

plate('40 forward outriggers',[(878,608),(927,602),(963,579),(978,584),(989,600),(975,813),(920,877),(884,911),(889,820),(910,778),(911,682),(899,660),(873,651)],lambda x,y:.45+.24*(y+.9),'Ivory',.24)
plate('41 outrigger lavender toes',[(910,778),(932,800),(920,877),(884,911),(889,820)],lambda x,y:.47+.24*(y+.9),'Lavender',.08)
plate('42 outrigger inner sockets',[(878,661),(899,666),(911,682),(910,778),(883,799)],lambda x,y:.48+.24*(y+.9),'Edge',.15)

plate('50 aft split prongs',[(815,24),(820,24),(873,120),(858,140),(858,160),(936,233),(933,280),(908,293),(885,324),(887,486),(913,516),(914,548),(875,559),(857,398),(834,341),(834,294),(852,268),(850,240),(815,151)],lambda x,y:.35+.14*y,'Ivory',.15)
plate('51 midship auxiliary vanes',[(901,374),(908,373),(938,416),(944,446),(940,488),(919,509),(899,465)],lambda x,y:.01+.13*(x-1.3),'Lavender',.12)
plate('53 auxiliary vane roots',[(830,461),(912,461),(929,523),(840,523)],lambda x,y:-.08,'Graphite',.20)
plate('52 auxiliary vane feet',[(899,445),(919,463),(940,488),(931,523),(900,536)],lambda x,y:-.01,'Graphite',.14)

loft('60 engine spine',[(2.21,.33,.51,-.30),(2.9,.33,.52,-.30),(3.27,.26,.43,-.24),(3.42,.23,.31,-.19)],'Engine')
plate('61 engine side fairings',[(801,209),(817,209),(824,247),(826,305),(811,329),(807,275)],lambda x,y:.26,'Lavender',.29)
for x in [773]:
    plate('62 engine raised ribs',[(x,166),(x+8,166),(x+8,256),(x,256)],lambda x,y:.60,'Ivory',.11)
stripe_vertices=[]
for y in [1.28,1.3,2.0,2.21,2.5,2.86]:
    h=hull_profile(y)[2]
    if y>=2.21:h=min(h,-.30)
    stripe_vertices.extend([(0,y,h-.023),(.16,y,h-.023)])
mesh('63 ventral engine stripe',stripe_vertices,[(i,i+1,i+3,i+2) for i in range(0,len(stripe_vertices)-2,2)],'Lavender',solid=.025)

def disc(name,cx,cy,z,rad,depth,color):
    verts=[]; N=12
    for h in [z-depth,z]:
        verts += [(cx+rad*math.cos(i*2*math.pi/N),cy+rad*math.sin(i*2*math.pi/N),h) for i in range(N)]
    faces=[tuple(range(N-1,-1,-1)),tuple(range(N,2*N))]
    faces += [(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
    return mesh(name,verts,faces,color)
for name,cx,cy,z,r in [('shoulder',1.43,-.73,.58,.37),('forward fin',2.94,-1.94,-.17,.23)]:
    disc('70 '+name+' pivot housing',cx,cy,z,r+.07,.18,'Graphite')
    disc('71 '+name+' lavender cap',cx,cy,z+.025,r,.035,'Lavender')

verts=[]; faces=[]; N=8
for y,rx,rz in [(3.25,.31,.34),(3.43,.31,.34),(3.44,.23,.25),(3.35,.23,.25)]:
    verts.extend([(rx*math.cos(-math.pi/2+i*math.pi/N),y,rz*math.sin(-math.pi/2+i*math.pi/N)) for i in range(N+1)])
for k in range(3):
    for i in range(N):
        a=k*(N+1)+i;faces.append((a,a+1,a+N+2,a+N+1))
mesh('64 exhaust rim',verts,faces,'Edge')
loft('65 exhaust recess',[(3.34,.22,.23,-.23),(3.35,.22,.23,-.23)],'Graphite')

for name, skin, base_thickness in [('12 upper wing lavender tips',.025,.14),('21 lower blade lavender tips',.015,.13),('31 forward fin lavender edges',.02,.10)]:
    original=bpy.data.objects[name]
    underside=original.copy();underside.data=original.data.copy()
    underside.name=name+' underside';parts.objects.link(underside)
    normal=original.data.polygons[0].normal.copy()
    for v in underside.data.vertices:
        v.co-=Vector((0,0,skin))+normal*(base_thickness-.008)
    underside.modifiers['Editable plate thickness'].thickness=.025

scene.render.engine = 'BLENDER_WORKBENCH'
shade=scene.display.shading
shade.light='STUDIO'; shade.studio_light='paint.sl'; shade.studiolight_rotate_z=.35; shade.studiolight_intensity=1.2
shade.color_type='MATERIAL';shade.show_shadows=True
shade.show_cavity=True; shade.cavity_type='BOTH'
shade.curvature_ridge_factor=.35; shade.curvature_valley_factor=.65
shade.show_object_outline=True; shade.object_outline_color=(.014,.018,.028)
shade.background_type='WORLD'
scene.world=bpy.data.worlds.new('Review slate');scene.world.color=(.047,.057,.080)
scene.view_settings.view_transform='Standard'
scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.display.render_aa='32'
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.shading.color_type='MATERIAL'
        a.spaces.active.shading.studio_light='paint.sl'
        a.spaces.active.region_3d.view_rotation=Vector((13,-18,17)).to_track_quat('Z','Y')
        a.spaces.active.region_3d.view_distance=16
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))

review=bpy.data.collections.new('Review cameras');scene.collection.children.link(review)
camdata=bpy.data.cameras.new('Review orthographic');cam=bpy.data.objects.new('Review orthographic',camdata);review.objects.link(cam);scene.camera=cam
camdata.type='ORTHO';camdata.ortho_scale=13.6

def camera(position):
    cam.location=position
    direction=Vector((0,0,0))-cam.location
    cam.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()

def render(name,position):
    camera(position);scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)

views={'top':(0,0,25),'bottom':(0,0,-25),'front':(0,-25,0),'rear':(0,25,0),'left':(25,0,0),'right':(-25,0,0),'front-quarter':(13,-18,17),'rear-quarter':(-13,18,17)}
for name,position in views.items():render(name,position)
wing_objects=[o for o in parts.objects if o.type=='MESH' and int(o.name[:2]) in list(range(10,32))+[70,71]]
for o in wing_objects:o.hide_render=True
render('fuselage-side',(25,0,0))
for o in wing_objects:o.hide_render=False
scene.render.resolution_x=640;scene.render.resolution_y=540;scene.display.render_aa='16'
for i in range(72):
    a=2*math.pi*i/72
    render('turn-%03d'%i,(20*math.sin(a),-20*math.cos(a),15))
print('VALIS_OUTPUT='+str(SOURCE))








