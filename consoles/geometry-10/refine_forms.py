"""Load geometry-09/console.blend, refine the monitor rake, panel profiles and tube assembly.
Blender --background ../geometry-09/console.blend --python refine_forms.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-angle.png, NAME-manifest.json. Existing output fails.
The loaded file is read-only. Monitor angles stay fixed while fitting position and rectangular dimensions.
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
s=bpy.context.scene;s.name='10 - Raked monitor and formed panels'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('10 - Editable form refinement');s.collection.children.link(col)
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
    o=bpy.data.objects.new('10 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('10 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('10 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('10 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('10 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

def previous(label):return bpy.data.objects['09 | '+label]
def pixel(v):
    p=world_to_camera_view(s,hero,v);return np.array([p.x*1672,(1-p.y)*941])

# Fix the rake before fitting placement; corner error cannot straighten the screen.
monitor_before=[list(row)for row in main.matrix_world]
main.rotation_euler=(math.radians(56),math.radians(2),math.radians(18))
params=np.array([main.location.x,main.location.z,main.scale.x,1.03])
points=[(-3.15,6.1,.22),(3.15,6.1,.22),(-3.15,0,.22),(3.15,0,.22)]
targets=np.array([(32,443),(516,443),(16,938),(567,938)]).flatten()
def monitor_error(p):
    mat=Matrix.Translation(Vector((p[0],main.location.y,p[1])))@main.rotation_euler.to_matrix().to_4x4()@Matrix.Diagonal(Vector((p[2],p[3],1,1)))
    result=np.concatenate([pixel(mat@Vector(v))for v in points])
    return result-targets
for _ in range(30):
    e=monitor_error(params);j=np.empty((8,4))
    for i in range(4):
        p=params.copy();p[i]+=.001;j[:,i]=(monitor_error(p)-e)/.001
    step=np.linalg.solve(j.T@j+.01*np.eye(4),-j.T@e)
    params+=step
    if np.linalg.norm(step)<1e-6:break
main.location.x=params[0];main.location.z=params[1];main.scale=(params[2],params[3],1)

# The forearm shell has a shallow upper face and a steeper folded nose.
hide_tree(old('Overlapping forearm armor'))
bpy.context.view_layer.update()
camera_inverse=hero.matrix_world.inverted()
projection=hero.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(),x=1672,y=941)
inv=(projection@camera_inverse).inverted()
def on_plane(x,y,point,normal):
    h=inv@Vector((2*x/1672-1,1-2*y/941,0,1));world=Vector(h[:3])/h.w
    direction=(world-hero.location).normalized()
    return hero.location+direction*(normal.dot(point-hero.location)/normal.dot(direction))
upper_point=Vector((3.67,-2.70,.95));upper_normal=Vector((0,-math.sin(math.radians(36)),math.cos(math.radians(36))))
top=[on_plane(x,783,upper_point,upper_normal)for x in [1082,1300]]
fold=[on_plane(x,843,upper_point,upper_normal)for x in [1023,1263]]
lower_normal=Vector((0,-math.sin(math.radians(65)),math.cos(math.radians(65))))
bottom=[on_plane(x,914,fold[0],lower_normal)for x in [1089,1223]]
loops=[]
for a,b in [bottom,fold,top]:
    width=b.x-a.x;ch=.045
    loops.append([(a.x+ch,a.y,a.z-.16),(b.x-ch,b.y,b.z-.16),(b.x,b.y,b.z-.10),(b.x,b.y,b.z-.045),(b.x-ch,b.y,b.z),(a.x+ch,a.y,a.z),(a.x,a.y,a.z-.045),(a.x,a.y,a.z-.10)])
forearm=root('Folded forearm assembly',parent=deck)
shell=loft('Forearm bent armor shell',loops,forearm,[1,0],(2,2),.018)
for p in shell.data.polygons:
    if abs(p.normal.x)>.5:p.material_index=2
support_loops=[[(x,y,z-.16)for x,y,z in loop]for loop in loops]
loft('Forearm structural backing',support_loops,forearm,[3,3],(3,3),.01)

# Open elbows lead into a paired horizontal run, held by a small wraparound clamp.
hide_tree(old('Complete hose loom'))
loom=root('Complete tube assembly',parent=deck)
ports=root('Paired tube entry',(-1.72,-2.75,.90),(math.radians(35),0,0),loom)
rect('Entry socket plate',1.05,.78,.22,ports,corner=.16,bevel=.06,top=3,wall=3)
for i,x in enumerate([-.25,.25]):
    radial('Entry collar '+str(i),[(0,.19),(.04,.22),(.13,.22),(.20,.17)],ports,(x,0,.06),12,8,3)
    radial('Entry rubber boot '+str(i),[(.18,.16),(.28,.145),(.34,.12)],ports,(x,0,.06),16,5,5)
tube('Lower curved tube',[(-1.97,-2.96,1.19),(-1.98,-3.16,1.12),(-1.60,-3.44,.95),(-.95,-3.50,.92),(.1,-3.50,.92),(1.8,-3.50,.92),(2.63,-3.50,.92)],.105,loom)
tube('Upper curved tube',[(-1.47,-2.96,1.19),(-1.43,-3.12,1.21),(-1.10,-3.38,1.11),(-.45,-3.47,1.11),(.25,-3.47,1.11),(1.9,-3.47,1.11),(2.63,-3.47,1.11)],.095,loom)
clamp=root('Tube saddle',(-.15,-3.50,1.015),parent=loom)
clamp.rotation_euler=Matrix(((0,0,1),(1,0,0),(0,1,0))).to_euler()
ring('Wraparound retaining strap',poly(.34,.45,.065),poly(.24,.39,.045),.37,clamp,.185,0,1,.014)
coupler=root('Low inline connector',(2.85,-3.68,1.015),(math.pi/2,0,0),loom)
rect('Inline connector jacket',.86,.39,.41,coupler,corner=.10,bevel=.07,top=3,wall=3)
rect('Inline lens recess',.61,.25,.04,coupler,(0,0,.027),.065,.025,4,3)
rect('Inline status lens',.46,.14,.025,coupler,(0,0,.052),.04,.02,9,8)
for i,z in enumerate([.92,1.11]):
    collar=root('Connector inlet '+str(i),(2.43,-3.49,z),(0,math.pi/2,0),loom)
    radial('Tube ferrule '+str(i),[(0,.13),(.10,.145),(.18,.13)],collar,segments=12,top=8,wall=3)
tube('Connector return elbow',[(3.27,-3.48,1.015),(3.41,-3.41,1.015),(3.41,-3.13,1.015),(3.27,-3.00,.995)],.105,loom)

# The fin surround follows the broad shouldered plate in the reference.
retire(old('Cooling bank rear armor bridge'))
bank=bpy.data.objects['07 | Angled cooling bank']
rim=root('Cooling bank armor surround',(.5,0,0),parent=bank)
outline=[(-2.05,.35),(-1.55,1.95),(.84,1.95),(1.32,1.04),(1.14,.78),(.72,1.45),(-1.28,1.45),(-1.67,.35)]
plate('Cooling bank angular shoulder',outline,.17,rim,(0,0,.23),.055)
plate('Cooling shoulder structural backing',outline,.46,rim,(0,0,.06),.035,3,3)

hide('Projection control dark enclosure','Projection armored face')
switches=bpy.data.objects['07 | Projection control assembly']
rect('Projection panel single enclosure',4.80,2.57,.32,switches,(0,0,.01),.58,.06,3,3)
rect('Projection panel chamfered face',4.65,2.42,.18,switches,(0,0,.145),.55,.075,0,2)
wing=previous('Right armor end wing');wing.scale.y=.55;wing.location.y=1.0

bpy.context.view_layer.update()
for o in col.objects:
    if o.type not in {'MESH','CURVE'}:continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
bpy.context.view_layer.update()
assert max(abs(hero.matrix_world[i][j]-camera_matrix[i][j])for i in range(4)for j in range(4))<1e-7
guide=bpy.data.texts.new('10 START HERE')
guide.write('Fixed 56-degree monitor face angle, equivalent to 34 degrees back from vertical.\nBoth screens remain local rectangles. New folded forearm shell and tube assembly are separate editable parts.\nHoses are Bezier curves, panel edges have live bevels. Older replaced objects are hidden.\nOriginal owner scene remains separate. Camera, ship, lights and screen artwork are unchanged.\n')
s.camera=hero;s.cycles.samples=64 if name=='console' else 32;s.render.filepath=str(ROOT/(name+'-camera.png'))
for im in bpy.data.images:
    if im.has_data and not im.packed_file:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
s.camera=bpy.data.objects['07 Camera - construction'];s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'monitorBefore':monitor_before,'monitorAfter':[list(row)for row in main.matrix_world],'monitorBackRakeDegrees':34,'cameraUnchanged':True,'cameraLens':camera_lens,'visualApproval':'pending'},indent=2))
