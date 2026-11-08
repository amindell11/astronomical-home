"""Load geometry-11/console.blend and reshape folded console skins.
Blender --background ../geometry-11/console.blend --python fold_console.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-angle.png, NAME-manifest.json. Existing output fails.
The name console also writes console-structure.png with the projection cue hidden.
The loaded file is read-only. The camera and monitor pose remain fixed.
"""
import bpy,bmesh,math,json,hashlib,sys,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix,Euler
from bpy_extras.object_utils import world_to_camera_view
ROOT=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1]
out=ROOT/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
source=Path(bpy.data.filepath);source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
s=bpy.context.scene;s.name='13 - Folded console skins'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('13 - Editable folded skins');s.collection.children.link(col)
main=bpy.data.objects['07 | Monitor assembly'];deck=bpy.data.objects['07 | Deck assembly']
mats=[bpy.data.materials['08 | '+n] for n in ['Armor face','Broad armor chamfers','Armor side walls','Dark structural housing','Gasket and recess','Hose jacket','Faceted controls','Control top faces','Connector alloy','Neutral lens and pointer','Raised key faces','Matte cooling fin faces','Matte cooling fin sides']]
rubber=mats[5]
def retire(o):o.hide_render=True;o.hide_set(True)
def old(label):return bpy.data.objects['08 | '+label]
def hide(*labels):
 for label in labels:retire(old(label))
def hide_tree(o):
 for child in o.children_recursive:
  if child.type in {'MESH','CURVE','FONT'}:retire(child)
def root(label,loc=(0,0,0),rot=(0,0,0),parent=None):
    o=bpy.data.objects.new('13 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('13 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('13 | '+label,d);col.objects.link(o);o.parent=parent
    if edge:
        b=o.modifiers.new('Small softened edge','BEVEL');b.width=edge;b.segments=2
        b=o.modifiers.new('Weighted broad faces','WEIGHTED_NORMAL');b.keep_sharp=True
    return o
def ccw(points):
    if sum(p[0]*q[1]-q[0]*p[1]for p,q in zip(points,points[1:]+points[:1]))<0:points=list(reversed(points))
    return points
def poly(w,h,c,cx=0,cy=0):
    return [(cx-w/2+c,cy-h/2),(cx+w/2-c,cy-h/2),(cx+w/2,cy-h/2+c),(cx+w/2,cy+h/2-c),(cx+w/2-c,cy+h/2),(cx-w/2+c,cy+h/2),(cx-w/2,cy+h/2-c),(cx-w/2,cy-h/2+c)]
def inset(points,d):
    points=ccw(points);result=[]
    for i,p in enumerate(points):
        p=Vector(p);a=(p-Vector(points[i-1])).normalized();b=(Vector(points[(i+1)%len(points)])-p).normalized()
        n1=Vector((-a.y,a.x));n2=Vector((-b.y,b.x));result.append(tuple(p+(n1+n2)*(d/(1+n1.dot(n2)))))
    return result
def loft(label,loops,parent,band_mats,cap_mats=(2,0),edge=.012):
    n=len(loops[0]);verts=[p for loop in loops for p in loop];faces=[tuple(reversed(range(n)))];ids=[cap_mats[0]]
    for k in range(len(loops)-1):
        for i in range(n):
            faces.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i));ids.append(band_mats[k])
    faces.append(tuple(range((len(loops)-1)*n,len(loops)*n)));ids.append(cap_mats[1])
    return mesh(label,verts,faces,parent,ids,edge)
def plate(label,outline,depth,parent,loc=(0,0,0),bevel=.10,top=0,wall=2):
    outline=ccw(outline);low=min(.035,depth*.18)
    loops=[[(x,y,z)for x,y in pts]for pts,z in [(inset(outline,low),-depth),(outline,-depth+low),(outline,-bevel*.8),(inset(outline,bevel),0)]]
    o=loft(label,loops,parent,[wall,wall,1 if top==0 else wall],(wall,top));o.location=loc;return o
def rect(label,w,h,depth,parent,loc=(0,0,0),corner=.10,bevel=.08,top=0,wall=2):
    return plate(label,poly(w,h,corner),depth,parent,loc,bevel,top,wall)
def ring(label,outer,inner,depth,parent,z=0,top=0,wall=2,edge=.012):
    outer=ccw(outer);inner=ccw(inner);n=len(outer)
    v=[(x,y,zz)for zz in [z-depth,z]for loop in [outer,inner]for x,y in loop];f=[];ids=[]
    for i in range(n):
        j=(i+1)%n
        f.extend([(i,j,j+2*n,i+2*n),(i+n,i+3*n,j+3*n,j+n),(i+2*n,j+2*n,j+3*n,i+3*n),(i,i+n,j+n,j)])
        ids.extend([wall,wall,top,wall])
    return mesh(label,v,f,parent,ids,edge)
def radial(label,profile,parent,loc=(0,0,0),segments=16,top=7,wall=6):
    loops=[[(math.cos(i*math.tau/segments)*r,math.sin(i*math.tau/segments)*r,z)for i in range(segments)]for z,r in profile]
    o=loft(label,loops,parent,[wall]*(len(loops)-1),(wall,top),.008);o.location=loc;return o
def tube(label,points,radius,parent):
    d=bpy.data.curves.new('13 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('13 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

def obj(label):return bpy.data.objects[label]
monitor_matrix=main.matrix_world.copy()
projection=hero.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(),x=1672,y=941)
inv=(projection@hero.matrix_world.inverted()).inverted()
def on_plane(x,y,point,normal):
    h=inv@Vector((2*x/1672-1,1-2*y/941,0,1));v=Vector(h[:3])/h.w
    d=(v-hero.location).normalized()
    return hero.location+d*(normal.dot(point-hero.location)/normal.dot(d))
def plane_pixels(points,point,normal):return [on_plane(x,y,point,normal)for x,y in points]
def skin(label,verts,faces,ids,thickness=.12,edge=.025):
    o=mesh(label,verts,faces,deck,ids,0)
    mod=o.modifiers.new('Editable sheet thickness','SOLIDIFY');mod.thickness=thickness;mod.offset=-1
    mod=o.modifiers.new('Fold edge softness','BEVEL');mod.width=edge;mod.segments=2
    mod=o.modifiers.new('Broad flat faces','WEIGHTED_NORMAL');mod.keep_sharp=True
    return o
def assembly_plane(assembly,z=0):
    return assembly.matrix_world@Vector((0,0,z)),assembly.matrix_world.to_quaternion()@Vector((0,0,1))
for label in ['11 | Continuous faceted console body','11 | Projector to controls armored knuckle','09 | Right armor end wing']:
    retire(obj(label))
for o in s.objects:
    if o.name.startswith(('09 | Monitor pedestal leg','09 | Bolted monitor pedestal')):retire(o)
monitor_base=obj('07 | Continuous left console body');monitor_base.data=monitor_base.data.copy()
for v in monitor_base.data.vertices:
    w=monitor_base.matrix_world@v.co
    w.x=max(w.x,-9.0)
    if w.y<-3.9:
        w.y=-3.9
        if w.z> -1.3:w.z=.02
    w.z=max(w.z,-.65)
    v.co=monitor_base.matrix_world.inverted()@w

# The cooling deck turns over behind the tubes and continues beneath the monitor.
top_point=Vector((0,-.95,1.00));top_normal=Vector((0,-.12,1)).normalized()
upper=plane_pixels([(420,720),(1105,720),(1105,817),(430,817)],top_point,top_normal)
front_normal=Vector((0,-math.sin(math.radians(73)),math.cos(math.radians(73))))
lower=plane_pixels([(1105,899),(430,899)],upper[2],front_normal)
bottom=[lower[0]+Vector((0,.22,-.12)),lower[1]+Vector((0,.22,-.12))]
leftskin=skin('Cooling deck folded beneath monitor',upper+lower+bottom,[(0,1,2,3),(3,2,4,5),(5,4,6,7)],[3,3,2],.16,.024)

# A narrow skin follows the rear silhouette and seats both upper panels.
switches=obj('07 | Projection control assembly');launch=obj('07 | Launch assembly')
cp,cn=assembly_plane(switches,-.02)
lp,ln=assembly_plane(launch,-.045)
control_outer=[switches.matrix_world@Vector((x,y,.015))for x,y in poly(4.85,2.62,.59)]

# Launch and controls share an edge; the side turns down past the frame crop.
launch_top=plane_pixels([(1297,789),(1563,789),(1611,790),(1680,937),(1635,968),(1219,968),(1225,916)],lp,ln)
verts=control_outer+launch_top
faces=[tuple(range(8)),tuple(range(8,15)),(0,1,9,8)]
ids=[3,3,3]
perimeter=[0,7,6,5,4,3,2,1,9,10,11,12,13,14,8]
for i in perimeter:verts.append(verts[i]+Vector((0,.08,-.58)))
for j,i in enumerate(perimeter):faces.append((i,perimeter[(j+1)%15],15+(j+1)%15,15+j));ids.append(2)
faces.append(tuple(range(15,30)));ids.append(3)
mesh('Control and Launch folded chassis',verts,faces,deck,ids,.035)
wing=plane_pixels([(1547,723),(1608,732),(1634,762),(1566,762)],cp,cn)
skin('Outer shoulder armor',wing,[(0,1,2,3)],[0],.14,.02)
skin('Outer shoulder folded side',[wing[1],wing[2],launch_top[2],launch_top[3],control_outer[2]],[(0,1,2,4),(1,3,2)],[3,2],.14,.02)

# Preserve the projector ramp but shorten its heavy front lip.
apron=obj('11 | Projector wraparound armor');apron.data=apron.data.copy()
for v in apron.data.vertices:
    w=apron.matrix_world@v.co
    if w.y<-2.0:w.y+=.20;w.z+=.21
    v.co=apron.matrix_world.inverted()@w
av=[apron.matrix_world@v.co for v in apron.data.vertices]
apron_edge=[av[i]for i in [24,16,8,0,1,9,17,25]]
skirt=apron_edge+[p+Vector((0,.04,-.48))for p in apron_edge]
skin('Projector recessed support return',skirt,[(i,i+1,i+9,i+8)for i in range(7)],[3]*7,.12,.012)

# The shoulder follows the projector edge instead of crossing over its face.
pv=[(3.52,.85,1.78),(3.52,.85,1.58),(3.60,-.52,1.17),(3.60,-.52,1.26)]
left=plane_pixels([(1131,692),(1134,704),(1159,781),(1167,779)],cp,cn)
skin('Projector to control narrow folded seam',[Vector(v)for v in pv]+left,[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],[2,2,2,0],.12,.016)

# The forearm return meets the Launch face beneath the existing armor.
forearm=obj('10 | Forearm bent armor shell')
fv=[forearm.matrix_world@v.co for v in forearm.data.vertices]
skin('Forearm right return into Launch',[p+Vector((0,0,-.035))for p in [fv[20],fv[12],fv[4],launch_top[6],launch_top[0]]],[(0,1,2,3,4)],[3],.12,.018)

bpy.context.view_layer.update()
for o in col.objects:
    if o.type not in {'MESH','CURVE'}:continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
bpy.context.view_layer.update()
assert max(abs(main.matrix_world[i][j]-monitor_matrix[i][j])for i in range(4)for j in range(4))<1e-7
cone=obj('11 | Neutral projector frustum');cone.hide_render=False
s.camera=hero;s.cycles.samples=64 if name=='console' else 24;s.render.filepath=str(ROOT/(name+'-camera.png'))
guide=bpy.data.texts.new('13 START HERE');guide.write('Geometry-11 base with folded skins and narrow joining edges.\nCamera, monitor, ship, controls and tubes retain their transforms.\nEdit individual mesh vertices, Solidify thickness and Bevel modifiers in 13 - Editable folded skins.\nNo casing unions. Replaced surfaces are hidden.\n')
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
if name=='console':
    cone.hide_render=True;s.render.filepath=str(ROOT/(name+'-structure.png'));bpy.ops.render.render(write_still=True)
s.camera=obj('07 Camera - construction');cone.hide_render=True;s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'monitorMatrix':[list(r)for r in monitor_matrix],'cameraMatrix':[list(r)for r in camera_matrix],'visualApproval':'pending'},indent=2))
