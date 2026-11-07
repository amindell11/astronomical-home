"""Load hardware-08/hardware.blend, fit editable assemblies to the approved reference.
Blender --background ../hardware-08/hardware.blend --python fit_geometry.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-angle.png, NAME-fit.json. Existing output fails.
The loaded file is read-only. Fits rigid poses and rectangular dimensions, never camera-space mesh warps.
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
s=bpy.context.scene;s.name='09 - Reference geometry fit'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('09 - Reference fitted hardware');s.collection.children.link(col)
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
    o=bpy.data.objects.new('09 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('09 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('09 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('09 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('09 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

fits={}
original_monitor=main.matrix_world.copy()
def project(mat,points):
    result=[]
    for p in points:
        q=world_to_camera_view(s,hero,mat@Vector(p));result.extend([q.x*1672,(1-q.y)*941])
    return np.array(result)
def fit_pose(obj,points,targets,resize=False):
    bpy.context.view_layer.update()
    parent=obj.parent.matrix_world.copy() if obj.parent else Matrix.Identity(4)
    initial=np.array([*obj.location,*obj.rotation_euler,1.,1.])
    params=initial.copy();target=np.array(targets).flatten()
    def matrix(v):return parent@Matrix.Translation(Vector(v[:3]))@Euler(v[3:6]).to_matrix().to_4x4()@Matrix.Diagonal(Vector((v[6],v[7],1,1)))
    def error(v):return np.r_[project(matrix(v),points)-target,(v[6:8]-1)*10,(v[1]-initial[1])*250,(v[3:6]-initial[3:6])*3]
    before=project(matrix(params),points);cost=float(error(params)@error(params));damp=.1
    count=8 if resize else 6
    for _ in range(200):
        e=error(params);J=np.empty((len(e),count))
        for i in range(count):
            v=params.copy();v[i]+=.001;J[:,i]=(error(v)-e)/.001
        delta=np.linalg.solve(J.T@J+damp*np.eye(count),-J.T@e)
        if np.max(np.abs(delta))>.20:delta*=.20/np.max(np.abs(delta))
        trial=params.copy();trial[:count]+=delta
        score=float(error(trial)@error(trial))
        if score<cost:params=trial;cost=score;damp=max(.001,damp*.5)
        else:damp*=4
        if np.linalg.norm(delta)<1e-7:break
    obj.location=params[:3];obj.rotation_euler=params[3:6];obj.scale=(params[6],params[7],1)
    bpy.context.view_layer.update();after=project(obj.matrix_world,points)
    if obj==main:before=project(original_monitor,points)
    fits[obj.name]={'localPoints':points,'targetPixels':targets,'beforePixels':before.reshape(-1,2).tolist(),'afterPixels':after.reshape(-1,2).tolist(),'beforeRmsPixels':float(np.sqrt(np.mean((before-target)**2))),'afterRmsPixels':float(np.sqrt(np.mean((after-target)**2))),'pose':params.tolist()}
    print(obj.name, fits[obj.name]['beforeRmsPixels'],fits[obj.name]['afterRmsPixels'])

# Pose changes preserve physical rectangles and their local control layout.
main.rotation_euler=(math.radians(73),0,math.radians(-10))
fit_pose(main,[(-3.15,6.1,.22),(3.15,6.1,.22),(-3.15,0,.22),(3.15,0,.22)],[(32,443),(516,443),(16,938),(567,938)],True)
main.rotation_euler.y=-.01
bpy.data.objects['07 | Continuous left console body'].location.z=-.60
hide('Raised control column','Upper right bolted armor ear','Upper left bolted armor ear','Thick cast display divider')
plate('Left shoulder armor',[(-3.12,5.12),(-2.10,5.12),(-1.91,5.35),(-1.91,5.78),(-2.16,6.08),(-2.77,6.08),(-3.12,5.72)],.15,main,(0,0,.25),.055)
plate('Right shoulder armor',[(2.35,5.78),(2.45,6.08),(2.78,6.08),(3.12,5.72),(3.12,5.06),(2.85,5.45)],.14,main,(0,0,.25),.055)
rect('Slim screen separation casting',4.67,.14,.14,main,(.51,1.51,.13),.035,.025,0,2)
old('Raised brow cap').scale.z=.65
old('Raised brow cap').scale.y=1.28
old('Raised brow cap').scale.x=.96
for label in ['Selector','Previous','Next']:
    o=bpy.data.objects['07 | '+label];o.location.z=.20
    if label=='Selector':o.scale=(.88,.88,.88)
bpy.data.objects['07 | Selector'].location.y+=.24
for label in ['Display power switch','Power light socket','Power light lens']:old(label).location.y+=.22
for o in s.objects:
    if o.name.startswith('08 | Large round captive bolt'):o.scale=(.87,.87,.55)
    if o.name.startswith(('08 | Large round captive bolt','08 | Captive bolt socket')) and o.location.y<1.0:o.location.y+=.22
for o in s.objects:
    if 'Monitor trunnion support' in o.name or 'Monitor hinge ' in o.name:
        hide_tree(o)
for x in [-2.05,2.05]:rect('Monitor pedestal leg',.64,1.12,.86,main,(x,-.51,-.40),.10,.055,3,3)
rect('Bolted monitor pedestal',6.10,.18,1.24,main,(0,-1.10,-.28),.12,.045,3,3)

# A broad wedge with a shallow nose replaces the tall box around the projector.
hide('Faceted projector armored housing')
projector=old('Projector housing')
hide_tree(projector)
projector_new=root('Projector optical assembly',(2.35,.03,1.58),(math.radians(23),0,0),deck)
rect('Recessed projector cartridge',2.32,1.30,.20,projector_new,corner=.18,bevel=.08,top=3,wall=3)
ring('Projector rounded rim',poly(2.27,1.27,.16),poly(1.98,1.03,.10),.12,projector_new,.10,8,3,.014)
rect('Projector optical well',1.99,1.04,.07,projector_new,(0,0,.025),.10,.035,4,3)
rect('Projector lens',1.85,.90,.025,projector_new,(0,0,.05),.07,.018,9,8)
for i in range(8):rect('Projector lens rib '+str(i),1.76,.018,.012,projector_new,(0,-.37+i*.105,.07),.004,.004,8,8)
sections=[(-2.12,.06,3.82,1.00),(-1.84,-.07,3.90,1.22),(-.52,.39,3.60,1.40),(.85,.47,3.52,1.92)]
loops=[]
for y,left,right,z in sections:
    loops.append([(left+.12,y,z-.22),(right-.12,y,z-.22),(right,y,z-.14),(right,y,z-.10),(right-.16,y,z),(left+.16,y,z),(left,y,z-.10),(left,y,z-.14)])
apron=loft('Broad low projector ramp',loops,deck,[0]*3,(2,2),.015)
cut=rect('Projector aperture cutter',2.33,1.31,1.2,projector_new,(0,0,.25),.18,.005,4,4);cut.hide_render=True;cut.hide_set(True);cut.display_type='WIRE'
bo=apron.modifiers.new('Recess for optical cartridge','BOOLEAN');bo.operation='DIFFERENCE';bo.solver='EXACT';bo.object=cut

forearm=old('Overlapping forearm armor');hide_tree(forearm)
forearm.location=(3.67,-2.10,.95);forearm.rotation_euler.x=math.radians(36)
outline=[(-1.60,-.59),(-1.30,-.82),(.43,-.82),(1.62,.65),(1.62,.83),(-.88,.83),(-1.60,-.42)]
plate('Forearm broad slope',outline,.18,forearm,bevel=.065)
plate('Forearm structural underside',inset(outline,.03),.33,forearm,(0,0,-.12),.045,2,2)
hide('Projection to forearm structural bridge')

switches=bpy.data.objects['07 | Projection control assembly']
fit_pose(switches,[(-2.325,1.21,.18),(2.325,1.21,.18),(-2.325,-1.21,.18),(2.325,-1.21,.18)],[(1140,668),(1515,668),(1155,780),(1570,780)],True)
old('Projection control dark enclosure').scale.z=.60
old('Projection armored face').scale.z=.72
for o in s.objects:
    if o.name.startswith('08 | Toggle ') and 'lever' in o.name:
        o.scale=(.90,.90,.83)
for o in switches.children:
    if o.name.startswith(('08 | Toggle ','08 | Projection knob')):o.location.y-=.20

bank=bpy.data.objects['07 | Angled cooling bank']
bank.location.x=-.80;bank.location.y=.12;bank.rotation_euler.z=0
bank.location.z=.20
for o in old('Cooling assembly').children:
    if 'Separate canted cooling fin' in o.name:retire(o)
for j,cx in enumerate([-.80,0,.80]):
    loops=[]
    for y,z,width,lean in [(-.96,.035,.54,-.22),(-.44,.40,.64,-.02),(1.20,.40,.64,.44),(1.42,.26,.56,.46)]:
        b=min(.06,z*.48);x0=cx+lean-width/2;x1=cx+lean+width/2
        loops.append([(x0,y,-.09),(x1,y,-.09),(x1,y,z-b),(x1-b,y,z),(x0+b,y,z),(x0,y,z-b)])
    rib=loft('Slanted cooling fin '+str(j+1),loops,old('Cooling assembly'),[12]*3,(12,12),.012)
    for p in rib.data.polygons:p.material_index=11 if p.normal.z>.7 else 12
old('Recessed dark cooling plinth').hide_render=True
rect('Cooling bank supporting deck',3.65,2.75,.50,old('Cooling assembly'),(.14,.25,-.10),.16,.055,3,3)
old('Cooling bank rear armor bridge').scale.z=.72
old('Cooling bank rear armor bridge').location.y=1.43

launch=bpy.data.objects['07 | Launch assembly']
fit_pose(launch,[(-1.325,.585,.48),(1.325,.585,.48),(-1.325,-.585,.48),(1.325,-.585,.48)],[(1320,789),(1542,789),(1337,868),(1586,868)],True)
old('Launch broad beveled button').scale.z=.85

# Trim the carrier to the equipment underside instead of cutting across the faceplates.
hide('Continuous stepped equipment carrier')
bpy.data.objects['07 | Continuous right console body'].location.z=-.60
support_sections=[(-3.38,-.85,8.42,.12),(-2.60,-2.60,8.75,.35),(-.60,-2.65,8.75,.95),(1.78,-2.45,8.58,1.22)]
support_loops=[]
for y,left,right,z in support_sections:
    support_loops.append([(left+.16,y,z-.42),(right-.16,y,z-.42),(right,y,z-.24),(right,y,z-.10),(right-.16,y,z),(left+.16,y,z),(left,y,z-.10),(left,y,z-.24)])
support=loft('Joined equipment understructure',support_loops,deck,[3]*3,(3,3),.02);support.location.z=-.20
rightcap=root('Right armor end wing',(8.50,.25,1.24),(math.radians(14),0,0),deck)
plate('Right console shoulder',poly(1.15,2.75,.25),.23,rightcap,bevel=.10)

# Reduce fittings around the same twin hose route; the reference has a compact inline coupler.
for o in [old('Right hose coupler'),old('Coupler status lens')]:
    o.scale=(.72,.72,.72)
hide('Coupler return elbow')
tube('Compact coupler return',[(3.98,-3.17,.56),(4.19,-3.15,.57),(4.21,-2.70,.65),(4.19,-2.34,.65)],.10,old('Twin service hoses'))
old('Hose termination socket').location.x=4.19
old('Hose retaining saddle').scale=(.80,.85,1)
old('Complete hose loom').location.z=.50
old('Heavy supply hose').data.bevel_depth=.10
old('Upper return hose').data.bevel_depth=.08
old('Right hose coupler').location.x-=.45
old('Coupler status lens').location.x-=.45
for label in ['Heavy supply hose','Upper return hose']:
    o=old(label)
    for p in o.data.splines[0].bezier_points:
        if p.co.x>1.5:p.co.x-=.45
for o in col.objects:
    if 'Compact coupler return' in o.name:o.location.x-=.45
old('Hose termination socket').location.x-=.45
for o in old('Hose entry sockets').children:
    o.scale*=.85
old('Hose entry sockets').location.z-=.33
old('Hose entry sockets').location.y-=.12
for label in ['Heavy supply hose','Upper return hose']:
    o=old(label)
    for i,p in enumerate(o.data.splines[0].bezier_points):
        if i<3:p.co.z-=[.33,.18,.06][i]
hide_tree(old('Hose termination socket'))
for o in col.objects:
    if 'Compact coupler return' in o.name:
        o.data.splines[0].bezier_points[-1].co.z-=.40
        o.data.splines[0].bezier_points[-2].co.z-=.26

bpy.context.view_layer.update()
assert max(abs(hero.matrix_world[i][j]-camera_matrix[i][j])for i in range(4)for j in range(4))<1e-7
for label,fit in fits.items():
    after=project(bpy.data.objects[label].matrix_world,fit['localPoints']);target=np.array(fit['targetPixels']).flatten()
    fit['afterPixels']=after.reshape(-1,2).tolist();fit['afterRmsPixels']=float(np.sqrt(np.mean((after-target)**2)))
    fit['finalMatrix']=[list(row)for row in bpy.data.objects[label].matrix_world]
guide=bpy.data.texts.new('09 START HERE')
guide.write('Geometry matched against interactive-console-05. Hero camera and ship are unchanged.\nMonitor, projection controls, and Launch use rigid poses and planar local rectangles.\nEdited source scene retained separately as SOURCE - owner saved scene.\nNew parts are in 09 - Reference fitted hardware. Earlier replaced geometry is hidden, not deleted.\nThe hoses remain Bezier curves. Armor bevels and the projector aperture Boolean remain editable.\nScreen artwork, paint and glow are not part of this geometry pass.\n')
s.camera=hero;s.cycles.samples=64 if name=='console' else 32;s.render.resolution_percentage=100;s.render.filepath=str(ROOT/(name+'-camera.png'))
for im in bpy.data.images:
    if im.has_data and not im.packed_file:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
s.camera=bpy.data.objects['07 Camera - construction'];s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(name+'-fit.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'cameraUnchanged':True,'cameraLens':camera_lens,'fits':fits,'visualApproval':'pending'},indent=2))
