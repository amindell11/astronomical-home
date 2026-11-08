"""Load geometry-11/console.blend and build a wrapped housing with recessed control seats.
Blender --background ../geometry-11/console.blend --python wrap_housing.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-structure.png, NAME-angle.png, NAME-manifest.json. Existing output fails.
The loaded file is read-only. Camera, monitor pose and viewport remain fixed.
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
s=bpy.context.scene;s.name='12 - Wrapped console housing'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('12 - Editable housing construction');s.collection.children.link(col)
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
    o=bpy.data.objects.new('12 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('12 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('12 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('12 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('12 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

def obj(label):return bpy.data.objects[label]
monitor_matrix=main.matrix_world.copy()
mats.append(bpy.data.materials['11 | Continuous chassis grey'])
for label in ['11 | Continuous faceted console body','07 | Continuous left console body','11 | Projector wraparound armor','11 | Projector to controls armored knuckle','10 | Projection panel single enclosure','10 | Forearm bent armor shell','10 | Forearm structural backing','08 | Launch raised socket lip']:
    retire(obj(label))
for o in s.objects:
    if o.name.startswith(('09 | Monitor pedestal leg','09 | Bolted monitor pedestal')):retire(o)
parts=[]
def column(label,outline,bottom=-1.15,topmat=13):
    n=len(outline);v=[(x,y,bottom)for x,y,z in outline]+[tuple(p)for p in outline]
    f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
    o=mesh(label,v,f,deck,[13,topmat]+[13]*n,.015);parts.append(o);return o
foundation=rect('Housing foundation',20,7.8,.45,deck,(.1,-1.15,-.72),.4,.06,13,13)

# The cooling bay and monitor share a deep wall, rather than resting on a tabletop.
sections=[(-3.7,.15),(-3.55,.62),(-2.25,.95),(-1.,1.05),(.85,1.32),(2.15,1.40)]
loops=[]
for y,z in sections:loops.append([(-9.0,y,-1.15),(1.50,y,-1.15),(1.50,y,z),(-9.0,y,z)])
bay=loft('Monitor to cooling bay housing',loops,deck,[13]*5,(13,13),.025);parts.append(bay)
plinth_loops=[]
for depth,w,lo,hi in [(-.14,6.22,-1.28,.025),(-.50,6.60,-1.35,.12),(-2.15,6.90,-1.30,.78)]:
    plinth_loops.append([(x,y,depth)for x,y in poly(w,hi-lo,.22,0,(hi+lo)/2)])
plinth=loft('Under-screen armored housing',plinth_loops,main,[0,13],(0,13),.04);parts.append(plinth)

# Projector armor continues down into the same housing, with broad shoulder faces.
sections=[(-2.18,.02,3.84,1.30,.25),(-1.84,-.10,3.94,1.40,.45),(-.52,.34,3.66,1.40,.25),(.92,.43,3.65,1.94,.20)]
loops=[]
for y,left,right,z,shoulder in sections:
    loops.append([(left+.12,y,-1.15),(right-.12,y,-1.15),(right,y,z-.30),(right,y,z-.14),(right-shoulder,y,z),(left+shoulder,y,z),(left,y,z-.14),(left,y,z-.30)])
for i in [0,1]:x,y,z=loops[0][i];loops[0][i]=(x,-2.44,z)
for i in [2,7]:x,y,z=loops[0][i];loops[0][i]=(x,-2.44,1.08)
for i in [3,6]:x,y,z=loops[0][i];loops[0][i]=(x,-2.321818,1.18)
projector=loft('Projector full-depth casing',loops,deck,[0]*3,(13,13),.02)
for p in projector.data.polygons:p.material_index=0 if p.normal.z>.7 else 1 if p.normal.z>.1 else 13
bm=bmesh.new();bm.from_mesh(projector.data);bm.faces.ensure_lookup_table();bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.faces[0]],context='FACES_ONLY')
bm.faces.new([bm.verts[i]for i in [2,3,4,5,6,7]]).material_index=1
bm.faces.new([bm.verts[i]for i in [0,1,2,7]]).material_index=13
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(projector.data);bm.free()
parts.append(projector)

projection=hero.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(),x=1672,y=941)
inv=(projection@hero.matrix_world.inverted()).inverted()
def plane_outline(pixels,assembly,height):
    mat=assembly.matrix_world;normal=mat.to_quaternion()@Vector((0,0,1));point=mat@Vector((0,0,height));local=mat.inverted();result=[]
    for x,y in pixels:
        h=inv@Vector((2*x/1672-1,1-2*y/941,0,1));v=Vector(h[:3])/h.w;d=(v-hero.location).normalized()
        world=hero.location+d*(normal.dot(point-hero.location)/normal.dot(d));p=local@world;result.append((p.x,p.y))
    return ccw(result)
switches=obj('07 | Projection control assembly');bpy.context.view_layer.update()
control_outline=plane_outline([(1116,708),(1182,650),(1518,650),(1555,688),(1607,731),(1635,769),(1574,802),(1152,802)],switches,.09)
controls=plate('Wrapped control housing shoulder',control_outline,.36,switches,(0,0,.09),.10,13,13);parts.append(controls)
column('Control housing wall',[switches.matrix_world@Vector((x,y,-.12))for x,y in control_outline])
launch=obj('07 | Launch assembly')
launch_outline=plane_outline([(1300,783),(1560,783),(1603,746),(1642,777),(1710,963),(1210,963),(1224,915)],launch,.10)
launch_case=plate('Launch surrounding armor face',launch_outline,.36,launch,(0,0,.10),.10,13,13);parts.append(launch_case)
column('Launch housing wall',[launch.matrix_world@Vector((x,y,-.10))for x,y in launch_outline])
column('Joined projector and control core',[(3.15,-2.35,.62),(4.75,-2.35,.65),(4.90,1.40,1.65),(3.10,1.40,1.60)])

# Preserve the approved fold while extending its side walls to the shared foundation.
old_forearm=obj('10 | Forearm bent armor shell');bpy.context.view_layer.update()
loops=[]
for k in range(3):
    world=[old_forearm.matrix_world@v.co for v in old_forearm.data.vertices[k*8:(k+1)*8]]
    a,b=world[5],world[4];left=min(p.x for p in world);right=max(p.x for p in world);y=a.y;z=a.z
    loops.append([(left+.05,y,-1.15),(right-.05,y,-1.15),(right,y,z-.10),(right,y,z-.045),(b.x,y,z),(a.x,y,z),(left,y,z-.045),(left,y,z-.10)])
forearm=loft('Folded armor joining the launch cheek',loops,deck,[1,0],(13,13),.02);parts.append(forearm)
for p in forearm.data.polygons:
    if abs(p.normal.x)>.5:p.material_index=13
cheek_outline=plane_outline([(1300,783),(1263,843),(1223,914),(1240,946),(1280,946),(1345,786)],launch,.10)
column('Launch left return beneath folded armor',[launch.matrix_world@Vector((x,y,.10))for x,y in cheek_outline])

# All casing volumes unite into one body; named operands remain editable.
bpy.context.view_layer.update()
for part in parts:
    mod=foundation.modifiers.new('Join '+part.name.removeprefix('12 | '),'BOOLEAN');mod.operation='UNION';mod.solver='EXACT';mod.object=part
    part.hide_render=True;part.hide_set(True);part.display_type='WIRE'
foundation.name='12 | Complete console housing'
cuts=[]
guard=rect('Monitor face clearance',6.38,6.22,8,main,(0,3.21,7.90),.20,.01,13,13);cuts.append(guard)
control_cut=rect('Recessed control insert',4.69,2.46,.55,switches,(0,0,.50),.55,.008,4,4);cuts.append(control_cut)
launch_cut=rect('Recessed Launch seat',2.95,1.49,.94,launch,(0,0,.60),.24,.008,4,4);cuts.append(launch_cut)
cuts.append(obj('09 | Projector aperture cutter'))
for cut in cuts:
    mod=foundation.modifiers.new('Pocket '+cut.name,'BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
    cut.hide_render=True;cut.hide_set(True);cut.display_type='WIRE'
ring('Launch recessed gasket',poly(2.92,1.46,.23),poly(2.74,1.28,.20),.12,launch,.145,4,4,.012)

bpy.context.view_layer.update()
for o in col.objects:
    if o.type not in {'MESH','CURVE'}:continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
bpy.context.view_layer.update()
assert max(abs(main.matrix_world[i][j]-monitor_matrix[i][j])for i in range(4)for j in range(4))<1e-7
guide=bpy.data.texts.new('12 START HERE')
guide.write('Shaped console housing with recessed control and Launch seats.\n12 | Complete console housing carries live unions of the named hidden casing operands.\nSelect an operand in the Outliner and unhide it to edit its shape; leave its render visibility off.\nMonitor pose, camera, ship, tubes and viewport are unchanged.\nOriginal owner scene and input files are preserved.\n')
s.camera=hero;s.cycles.samples=64 if name=='console' else 24;s.render.filepath=str(ROOT/(name+'-camera.png'))
cone=obj('11 | Neutral projector frustum');cone.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
cone.hide_render=True;s.render.filepath=str(ROOT/(name+'-structure.png'));bpy.ops.render.render(write_still=True)
s.camera=obj('07 Camera - construction');s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'monitorMatrix':[list(row)for row in monitor_matrix],'cameraMatrix':[list(row)for row in camera_matrix],'housingOperands':[o.name for o in parts],'pockets':[o.name for o in cuts],'visualApproval':'pending'},indent=2))
