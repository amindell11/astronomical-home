"""Load geometry-13/console.blend and reshape folded console skins.
Blender --background ../geometry-13/console.blend --python join_folds.py -- --name draft-01
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
s=bpy.context.scene;s.name='14 - Joined folds and front hose entry'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('14 - Editable armor and hose entry');s.collection.children.link(col)
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
    o=bpy.data.objects.new('14 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('14 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('14 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('14 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('14 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

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

for label in ['11 | Projector wraparound armor','13 | Projector recessed support return','13 | Projector to control narrow folded seam','10 | Forearm bent armor shell','10 | Forearm structural backing','13 | Forearm right return into Launch','13 | Control and Launch folded chassis','13 | Outer shoulder armor','13 | Outer shoulder folded side','08 | Launch raised socket lip','08 | Launch button skirt','10 | Projection panel single enclosure','11 | Thick projection panel armor']:
    retire(obj(label))

optics=obj('09 | Projector optical assembly')
op,on=assembly_plane(optics,-.055)
seam=on_plane(902,758,op,on)
an=Vector((0,-math.sin(math.radians(8)),math.cos(math.radians(8))))
ap=lambda x,y:on_plane(x,y,seam,an)
upper=plane_pixels([(888,687),(1117,687),(1141,758),(902,758),(887,715)],op,on)
front=[upper[3],upper[2],ap(1160,782),ap(1082,782),ap(1045,821),ap(867,821)]
outer_left=ap(831,815)-Vector((0,0,.04))
nose_normal=Vector((0,-math.sin(math.radians(72)),math.cos(math.radians(72))))
nose_left=on_plane(861,845,front[5],nose_normal)
nose_right=on_plane(1024,845,front[5],nose_normal)
verts=upper+front[2:]+[outer_left,nose_left,nose_right]
# Shared vertices carry the upper slope, broad apron and folded nose.
faces=[(0,1,2,3,4),(3,2,5,6,7,8),(4,3,8,9),(9,8,10),(8,7,11,10)]
apron=skin('Projector formed armor',verts,faces,[0,0,1,1,1],.12,.016)
cut=obj('09 | Projector aperture cutter')
mod=apron.modifiers.new('Optical cartridge opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut

up=Vector((3.67,-2.70,.95));un=Vector((0,-math.sin(math.radians(36)),math.cos(math.radians(36))))
ft=plane_pixels([(1082,783),(1300,783),(1263,843),(1023,843)],up,un)
nn=Vector((0,-math.sin(math.radians(65)),math.cos(math.radians(65))))
fb=plane_pixels([(1223,914),(1089,914)],ft[3],nn)
q=ft[0].lerp(ft[3],38/60)
for i,p in [(6,ft[0]),(7,q),(11,ft[3])]:apron.data.vertices[i].co=p+Vector((-.018,.012,-.018))
bm=bmesh.new();bm.from_mesh(apron.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(apron.data);bm.free()
av=[v.co.copy()for v in apron.data.vertices]
edge=[av[i]for i in [0,4,9,10,11]]
support=edge+[p+Vector((.025,.06,-.36))for p in edge]
skin('Projector recessed folded sidewalls',support,[(i,i+1,i+6,i+5)for i in range(4)],[3]*4,.10,.01)
launch=obj('07 | Launch assembly');lp,ln=assembly_plane(launch,.08)
switches=obj('07 | Projection control assembly');cp,cn=assembly_plane(switches,.005)
launch_edge=plane_pixels([(1313,790),(1575,790),(1628,766),(1690,938),(1654,968),(1219,968),(1238,917),(1277,845)],lp,ln)
controls=[switches.matrix_world@Vector((x,y,.005))for x,y in poly(4.78,2.55,.57)]
rect('Thin fitted control face',4.65,2.42,.12,switches,(0,0,.145),.55,.065,0,2)
bridge=[av[1],av[2],controls[7],controls[6],ft[0],controls[0]]
skin('Projector to control folded shoulder',bridge,[(0,1,2,3),(1,4,5,2)],[0,0],.10,.014)
fv=ft+fb+launch_edge+controls
ff=[(0,1,2,3),(3,2,4,5),(1,6,13,2),(2,13,12,4),(4,12,11),(6,7,8,9,10,11,12,13),tuple(range(14,22)),(14,15,7,6,1,0)]
fm=[0,1,3,3,3,3,3,3]
fore=skin('Forearm fold into Launch and controls',fv,ff,fm,.12,.018)
edge=[ft[1],launch_edge[0],launch_edge[1],launch_edge[2],launch_edge[3],launch_edge[4],launch_edge[5],launch_edge[6],fb[0]]
returns=edge+[p+Vector((0,.10,-.42))for p in edge]
skin('Launch casing folded returns',returns,[(i,i+1,i+10,i+9)for i in range(8)],[3]*8,.12,.02)
# The key remains raised; its mounting gasket sits in the shared sloping face.
ring('Flush Launch gasket',poly(2.97,1.48,.24),poly(2.71,1.25,.20),.055,launch,.085,4,4,.01)
rect('Recessed Launch key boot',2.64,1.16,.16,launch,(0,0,.23),.20,.035,4,4)

wing=plane_pixels([(1543,722),(1607,732),(1634,763),(1566,763)],cp,cn)
skin('Right shoulder into Launch edge',wing+[launch_edge[2],launch_edge[3]],[(0,1,2,3),(3,2,4),(2,5,4)],[0,3,3],.12,.018)
channel=[av[10],av[11],ft[3],fb[1],av[10]+Vector((0,.08,-.40))]
skin('Dark support beneath apron fold',channel,[(0,1,2,3,4)],[3],.10,.01)

# The hose socket mounts on the fascia, with both outlets normal to that face.
entry=obj('10 | Paired tube entry');hide_tree(entry)
for label in ['10 | Lower curved tube','10 | Upper curved tube']:retire(obj(label))
fascia=obj('13 | Cooling deck folded beneath monitor')
fverts=[fascia.matrix_world@v.co for v in fascia.data.vertices]
fp=fverts[2];fn=Vector((0,-math.sin(math.radians(73)),math.cos(math.radians(73))))
mount=on_plane(659,861,fp,fn)
entry_new=root('Recessed front hose socket',mount,(math.radians(73),0,0),deck)
bpy.context.view_layer.update()
rect('Hose socket inset',.82,.84,.055,entry_new,(0,0,.013),.20,.035,4,4)
ring('Socket perimeter',poly(.85,.87,.20),poly(.68,.70,.16),.045,entry_new,.03,3,3,.012)
for i,local in enumerate([(-.045,-.13,.02),(.045,.13,.02)]):
    radial('Flush hose gland '+str(i),[(0,.135),(.035,.14),(.075,.125),(.11,.112)],entry_new,local,12,5,3)
    start=entry_new.matrix_world@Vector((local[0],local[1],local[2]+.085))
    leave=start+fn*.18
    if i==0:
        pts=[start,leave,Vector((-1.78,-3.31,.96)),Vector((-1.28,-3.49,.92)),Vector((-.65,-3.50,.92)),Vector((.1,-3.50,.92)),Vector((1.8,-3.50,.92)),Vector((2.63,-3.50,.92))];rad=.105
    else:
        pts=[start,leave,Vector((-1.48,-3.22,1.13)),Vector((-.94,-3.45,1.11)),Vector((-.45,-3.47,1.11)),Vector((.25,-3.47,1.11)),Vector((1.9,-3.47,1.11)),Vector((2.63,-3.47,1.11))];rad=.095
    hose=tube('Front entry hose '+str(i),pts,rad,deck)
    for p in hose.data.splines[0].bezier_points[:2]:p.handle_left_type='VECTOR';p.handle_right_type='VECTOR'

bpy.context.view_layer.update()
for o in col.objects:
    if o.type not in {'MESH','CURVE'}:continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
bpy.context.view_layer.update()
assert max(abs(main.matrix_world[i][j]-monitor_matrix[i][j])for i in range(4)for j in range(4))<1e-7
cone=obj('11 | Neutral projector frustum');cone.hide_render=False
s.camera=hero;s.cycles.samples=64 if name=='console' else 24;s.render.filepath=str(ROOT/(name+'-camera.png'))
guide=bpy.data.texts.new('14 START HERE');guide.write('Shared folded surfaces around the projector and Launch.\nCamera, monitor and control poses preserved from geometry-13.\nThe front hose socket and its glands are grouped; tube curves remain separately editable.\nArmor thickness is .12; no casing unions or thickened bases.\nOriginal owner scene preserved.\n')
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
if name=='console':
    cone.hide_render=True;s.render.filepath=str(ROOT/(name+'-structure.png'));bpy.ops.render.render(write_still=True)
s.camera=obj('07 Camera - construction');cone.hide_render=True;s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'monitorMatrix':[list(r)for r in monitor_matrix],'cameraMatrix':[list(r)for r in camera_matrix],'visualApproval':'pending'},indent=2))
