"""Refine hardware in a loaded perspective-07 copy without moving its camera or displays.

Blender --background ../perspective-07/perspective.blend --python refine_hardware.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-angle.png and NAME-manifest.json here.
Existing blend output fails. The input file and retained owner scene are not written.
"""
import bpy,bmesh,math,json,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix

ROOT=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1]
out=ROOT/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output name')
source=Path(bpy.data.filepath);source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
s=bpy.context.scene
assert s.name=='07 - Perspective and form review'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
main=bpy.data.objects['07 | Monitor assembly'];deck=bpy.data.objects['07 | Deck assembly']
screen_names=['07 | Main rectangular display','07 | Detail rectangular display']
bpy.context.view_layer.update()
screen_matrices={n:bpy.data.objects[n].matrix_world.copy() for n in screen_names}
ship=bpy.data.objects['07 | Hologram assembly'];ship_matrix=ship.matrix_world.copy()
light_state={o.name:{'matrix':[list(row)for row in o.matrix_world],'energy':o.data.energy,'color':list(o.data.color)}for o in s.objects if o.type=='LIGHT'}
s.name='08 - Hardware forms with locked camera'
col=bpy.data.collections.new('08 - Editable hardware');s.collection.children.link(col)
retired=[]

def retire(o):
    o.hide_render=True;o.hide_set(True);retired.append(o.name)
def retire_named(*labels):
    for label in labels:retire(bpy.data.objects['07 | '+label])
def retire_children(parent):
    for o in list(parent.children_recursive):
        if o.type in {'MESH','CURVE','FONT'}:retire(o)
def material(label,value,rough=.48,metal=.12):
    m=bpy.data.materials.new('08 | '+label);m.diffuse_color=(value,value,value,1);m.use_nodes=True
    p=m.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(value,value,value,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    return m
armor=material('Armor face',.29);facet=material('Broad armor chamfers',.19);side=material('Armor side walls',.12)
chassis=material('Dark structural housing',.045);seam=material('Gasket and recess',.012,.56,0)
rubber=material('Hose jacket',.023,.40,0);control=material('Faceted controls',.036,.44,0)
control_top=material('Control top faces',.065,.42,0);metal=material('Connector alloy',.13,.36,.45)
indicator=material('Neutral lens and pointer',.64,.30,.08);keycap=material('Raised key faces',.39,.42,.15)
fin_face=material('Matte cooling fin faces',.028,.72,0);fin_side=material('Matte cooling fin sides',.016,.65,0)
for m in [fin_face,fin_side]:m.node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.18
mats=[armor,facet,side,chassis,seam,rubber,control,control_top,metal,indicator,keycap,fin_face,fin_side]

def root(label,loc=(0,0,0),rot=(0,0,0),parent=None):
    o=bpy.data.objects.new('08 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('08 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('08 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('08 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('08 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

retire_named('Monitor single front casting','Top trim','Display divider','Power switch socket','Power switch','Status light recess')
for label in ['Continuous left console body','Continuous right console body','Monitor tapered rear enclosure']:
    o=bpy.data.objects['07 | '+label];o.data.materials.clear();o.data.materials.append(chassis)

# The opening dimensions stay fixed; the front section gains broad shoulder faces.
outer=poly(6.30,6.10,.42,0,3.05);inner=poly(4.67,5.36,.20,.51,3.03)
levels=[(inset(outer,.04),inset(inner,-.05),-.30),(outer,inset(inner,-.05),-.10),(inset(outer,.12),inner,.22)]
v=[(x,y,z)for a,b,z in levels for loop in [a,b]for x,y in loop];f=[];ids=[]
for k in range(2):
    for i in range(8):
        j=(i+1)%8
        f.extend([(16*k+i,16*k+j,16*(k+1)+j,16*(k+1)+i),(16*k+8+i,16*(k+1)+8+i,16*(k+1)+8+j,16*k+8+j)])
        ids.extend([2 if k==0 else 1,3])
for i in range(8):
    j=(i+1)%8;f.extend([(32+i,32+j,40+j,40+i),(i,8+i,8+j,j)]);ids.extend([0,2])
mesh('Thick monitor armor frame',v,f,main,ids,.016)
rect('Raised control column',1.06,3.92,.18,main,(-2.47,3.13,.30),.14,.065,0,2)
plate('Upper left bolted armor ear',[(-3.10,5.10),(-2.10,5.10),(-1.94,5.36),(-1.94,5.70),(-2.20,6.05),(-2.75,6.05),(-3.10,5.70)],.22,main,(0,0,.32),.07)
plate('Upper right bolted armor ear',[(2.52,5.76),(2.52,6.05),(2.77,6.05),(3.10,5.72),(3.10,5.15),(2.97,5.15),(2.82,5.47)],.22,main,(0,0,.32),.055)
rect('Raised brow cap',4.35,.46,.28,main,(.02,5.86,.44),.16,.085,8,2)
rect('Brow light socket',.85,.27,.07,main,(1.48,5.86,.46),.085,.04,4,3)
rect('Brow light lens',.62,.13,.035,main,(1.48,5.86,.49),.04,.025,9,8)
rect('Thick cast display divider',4.67,.20,.30,main,(.51,1.51,.22),.035,.035,0,2)
for x,y in [(-2.65,5.56),(2.77,5.59),(-2.64,.72)]:
    radial('Captive bolt socket',[(0,.162),(.035,.162)],main,(x,y,.33),20,4,4)
    radial('Large round captive bolt',[(0,.132),(.045,.132),(.065,.111)],main,(x,y,.365),20,8,2)

selector=bpy.data.objects['07 | Selector'];retire_children(selector);selector.location.z=.32
radial('Selector retaining collar',[(0,.47),(.075,.47),(.10,.43)],selector,top=4,wall=4)
radial('Selector faceted grip',[(.08,.43),(.15,.46),(.31,.43),(.43,.35)],selector,segments=16,top=7,wall=6)
pointer=rect('Selector pointer',.055,.17,.013,selector,(-.14,.20,.444),.008,.008,9,9);pointer.rotation_euler.z=math.radians(-34)
for label,direction in [('Previous',-1),('Next',1)]:
    p=bpy.data.objects['07 | '+label];retire_children(p);p.location.z=.31
    rect(label+' deep socket',.78,.45,.15,p,corner=.075,bevel=.04,top=4,wall=3)
    rect(label+' chunky keycap',.65,.32,.16,p,(0,0,.14),.065,.045,10,2)
    pts=[(-.025*direction,.09),(.035*direction,0),(-.025*direction,-.09)]
    d=bpy.data.curves.new(label+' arrow','CURVE');d.dimensions='3D';d.bevel_depth=.012;d.bevel_resolution=1
    sp=d.splines.new('POLY');sp.points.add(2)
    for v,(x,y) in zip(sp.points,pts):v.co=(x,y,.149,1)
    o=bpy.data.objects.new('08 | '+label+' inlaid chevron',d);col.objects.link(o);o.parent=p;o.data.materials.append(seam)
power=root('Display power switch',(-2.65,4.15,.34),parent=main)
rect('Power switch boot',.35,.50,.12,power,corner=.075,bevel=.04,top=4,wall=3)
rect('Power switch paddle',.23,.31,.20,power,(0,.06,.19),.06,.04,0,2)
rect('Power light socket',.31,.35,.10,main,(-2.19,4.15,.35),.06,.04,4,3)
rect('Power light lens',.19,.22,.045,main,(-2.19,4.15,.39),.035,.025,9,8)

retire_named('Projector raked apron','Projector aperture cutout')
projector=bpy.data.objects['07 | Projector assembly'];retire_children(projector)
projector_new=root('Projector housing',(2.35,.15,1.87),parent=deck)
carrier_sections=[(-3.38,-.85,8.42,.16),(-2.60,-2.60,8.75,.55),(-.60,-2.65,8.75,1.45),(1.78,-2.45,8.58,1.45)]
carrier_loops=[]
for y,left,right,z in carrier_sections:
    carrier_loops.append([(left+.16,y,z-.42),(right-.16,y,z-.42),(right,y,z-.24),(right,y,z-.10),(right-.16,y,z),(left+.16,y,z),(left,y,z-.10),(left,y,z-.24)])
loft('Continuous stepped equipment carrier',carrier_loops,deck,[2]*3,(2,2),.022)
# Each section has a top, two broad shoulders, and a thick lower shell.
sections=[(-1.90,.70,3.50,.87),(-1.60,.42,3.90,1.05),(-.65,.42,3.90,1.84),(1.02,.42,3.90,1.84),(1.32,.70,3.62,1.84)]
loops=[]
for y,left,right,z in sections:
    loops.append([(left+.17,y,z-.30),(right-.17,y,z-.30),(right,y,z-.18),(right,y,z-.12),(right-.17,y,z),(left+.17,y,z),(left,y,z-.12),(left,y,z-.18)])
apron=loft('Faceted projector armored housing',loops,deck,[0]*(len(loops)-1),(2,2),.018)
for p in apron.data.polygons:
    n=p.normal
    p.material_index=0 if n.z>.7 else 1 if n.z>.15 else 2
cut=rect('Projector housing aperture tool',2.28,1.87,2.0,None,(2.35,.15,2.5),.20,.01,4,4);cut.hide_render=True;cut.hide_set(True);cut.display_type='WIRE'
bo=apron.modifiers.new('Editable projector aperture','BOOLEAN');bo.operation='DIFFERENCE';bo.solver='EXACT';bo.object=cut
rect('Projector dark recessed cartridge',2.28,1.87,.43,projector_new,(0,0,.035),.20,.10,3,2)
ring('Projector heavy aperture rim',poly(2.14,1.73,.18),poly(1.83,1.40,.12),.12,projector_new,.10,8,3,.014)
rect('Projector optical well',1.83,1.40,.10,projector_new,(0,0,.005),.12,.05,4,3)
rect('Projector frosted lens',1.68,1.27,.035,projector_new,(0,0,.016),.07,.025,9,8)
for i in range(9):
    rect('Projector lens baffle '+str(i+1),1.57,.025,.017,projector_new,(0,-.51+i*.127,.040),.005,.005,8,8)

forearm=root('Overlapping forearm armor',(3.55,-1.95,1.18),(math.radians(25),0,0),deck)
shield_outline=[(-1.45,-.54),(-1.18,-.72),(.50,-.72),(1.62,.52),(1.62,.72),(-.80,.72),(-1.45,-.35)]
plate('Forearm raked structural step',inset(shield_outline,-.045),.62,forearm,(0,0,-.08),.10,2,2)
plate('Asymmetric forearm shield',shield_outline,.30,forearm,bevel=.14)
rect('Projection to forearm structural bridge',1.60,.60,.62,deck,(4.50,-1.05,1.46),.12,.065,0,2)

bank=bpy.data.objects['07 | Angled cooling bank'];retire_children(bank)
bank_new=root('Cooling assembly',parent=bank);bank_new.location.z=-.19
rect('Recessed dark cooling plinth',3.13,3.36,.29,bank_new,(0,0,.10),.20,.08,3,3)
rect('Cooling bank rear armor bridge',3.35,.62,.30,bank_new,(0,1.65,.24),.18,.12,0,2)
for j,cx in enumerate([-.80,0,.80]):
    loops=[]
    for y,z,width in [(-1.60,.035,.54),(-.95,.62,.64),(1.30,.62,.64),(1.56,.41,.56)]:
        b=min(.075,z*.48);x0=cx-width/2;x1=cx+width/2
        loops.append([(x0,y,-.10),(x1,y,-.10),(x1,y,z-b),(x1-b,y,z),(x0+b,y,z),(x0,y,z-b)])
    rib=loft('Separate canted cooling fin '+str(j+1),loops,bank_new,[12]*3,(12,12),.014)
    for p in rib.data.polygons:p.material_index=11 if p.normal.z>.7 else 12

switches=bpy.data.objects['07 | Projection control assembly'];retire_children(switches)
rect('Projection control dark enclosure',4.90,2.65,.48,switches,corner=.40,bevel=.14,top=3,wall=3)
rect('Projection armored face',4.65,2.42,.25,switches,(0,0,.18),.36,.12,0,2)
for i,x in enumerate([-1.4,-.62]):
    rect('Toggle '+str(i+1)+' mounting shoe',.52,.54,.12,switches,(x,-.25,.25),.09,.05,4,3)
    radial('Toggle '+str(i+1)+' rubber boot',[(0,.205),(.07,.205),(.17,.13),(.21,.10)],switches,(x,-.25,.25),16,6,6)
    lever=root('Toggle '+str(i+1)+' lever',(x,-.25,.44),(math.radians(14),0,0),switches)
    radial('Toggle '+str(i+1)+' shaft',[(0,.078),(.31,.078)],lever,top=8,wall=8)
    radial('Toggle '+str(i+1)+' rounded grip',[(.24,.12),(.29,.165),(.44,.165),(.49,.13)],lever,segments=16,top=7,wall=6)
radial('Projection knob foot',[(0,.49),(.08,.49)],switches,(.55,-.25,.21),16,4,4)
radial('Projection knob faceted grip',[(.06,.45),(.13,.48),(.31,.43),(.42,.36)],switches,(.55,-.25,.21),16,7,6)
rect('Projection knob pointer',.05,.13,.012,switches,(.55,-.02,.642),.008,.008,9,9)
rect('Projection key socket',.58,.57,.12,switches,(1.60,-.25,.24),.09,.05,4,3)
rect('Projection square key',.43,.42,.17,switches,(1.60,-.25,.39),.06,.065,10,2)

launch=bpy.data.objects['07 | Launch assembly'];retire_children(launch)
rect('Launch armored mounting block',3.26,1.76,.69,launch,corner=.30,bevel=.14,top=3,wall=3)
ring('Launch raised socket lip',poly(2.98,1.48,.24),poly(2.75,1.26,.20),.15,launch,.12,8,3,.016)
rect('Launch button skirt',2.72,1.25,.17,launch,(0,0,.20),.20,.07,top=3,wall=3)
rect('Launch broad beveled button',2.65,1.17,.35,launch,(0,0,.48),.22,.16,top=10,wall=1)

old_cables=bpy.data.objects['07 | Cable assembly'];retire_children(old_cables)
hoses=root('Twin service hoses',parent=deck)
ports=root('Hose entry sockets',(-1.72,-2.57,.63),(math.radians(25),0,0),deck)
rect('Twin socket recessed mounting block',1.06,.72,.30,ports,corner=.19,bevel=.09,top=3,wall=3)
for i,x in enumerate([-.25,.25]):
    radial('Hose entry ferrule '+str(i+1),[(0,.20),(.08,.235),(.20,.235),(.27,.18)],ports,(x,0,.08),12,8,3)
    radial('Hose entry rubber boot '+str(i+1),[(.22,.17),(.32,.16),(.40,.13)],ports,(x,0,.08),16,5,5)
tube('Heavy supply hose',[(-1.97,-2.74,.99),(-1.95,-2.99,.78),(-1.72,-3.20,.51),(-1.15,-3.23,.42),(.10,-3.23,.42),(1.65,-3.22,.41),(3.34,-3.17,.44)],.12,hoses)
tube('Upper return hose',[(-1.47,-2.74,.99),(-1.43,-2.98,.85),(-1.13,-3.17,.70),(-.40,-3.20,.68),(.40,-3.20,.68),(1.95,-3.19,.67),(3.34,-3.17,.68)],.085,hoses)
strap=root('Hose retaining saddle',(.08,-3.20,.55),parent=deck)
strap.rotation_euler=Matrix(((0,0,1),(1,0,0),(0,1,0))).to_euler()
ring('Thick wraparound hose clamp',poly(.49,.68,.085),poly(.32,.54,.060),.42,strap,.21,0,1,.025)
connector=root('Right hose coupler',(3.67,-3.17,.56),parent=deck)
connector.rotation_euler=Matrix(((0,0,1),(1,0,0),(0,1,0))).to_euler()
rect('Faceted dual connector housing',.58,.77,.72,connector,(0,0,.36),.13,.08,3,3)
for z,r in [(-.12,.17),(.12,.135)]:
    radial('Coupler inlet collar',[(0,r),(.07,r*1.13),(.16,r*1.13),(.22,r)],connector,(0,z,-.54),12,8,3)
rect('Coupler end cap',.61,.79,.16,connector,(0,0,.45),.14,.065,8,3)
lamp=root('Coupler status lens',(3.67,-3.477,.56),(math.pi/2,0,0),deck)
rect('Coupler lens recess',.43,.20,.06,lamp,corner=.05,bevel=.025,top=4,wall=3)
rect('Coupler status glass',.31,.10,.025,lamp,(0,0,.025),.035,.02,9,8)
tube('Coupler return elbow',[(4.12,-3.17,.56),(4.30,-3.09,.58),(4.30,-2.65,.68),(4.26,-2.34,.65)],.145,hoses)
end=root('Hose termination socket',(4.26,-2.34,.65),(math.pi/2,0,0),deck)
radial('Hose termination collar',[(0,.22),(.08,.22),(.17,.17)],end,segments=12,top=8,wall=3)
route=root('Complete hose loom',(0,0,.25),parent=deck)
for part in [hoses,ports,strap,connector,lamp,end]:part.parent=route

bpy.context.view_layer.update()
for o in [apron,cut]:
    world=o.matrix_world.copy();o.parent=projector_new;o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_basis=projector_new.matrix_world.inverted()@world
bpy.context.view_layer.update()
for o in col.objects:
    if o.type not in {'MESH','CURVE'}:continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
bpy.context.view_layer.update()
assert max(abs(hero.matrix_world[i][j]-camera_matrix[i][j])for i in range(4)for j in range(4))<1e-7
assert hero.data.lens==camera_lens
for n,m in screen_matrices.items():assert max(abs(bpy.data.objects[n].matrix_world[i][j]-m[i][j])for i in range(4)for j in range(4))<1e-7
assert max(abs(ship.matrix_world[i][j]-ship_matrix[i][j])for i in range(4)for j in range(4))<1e-7
assert light_state=={o.name:{'matrix':[list(row)for row in o.matrix_world],'energy':o.data.energy,'color':list(o.data.color)}for o in s.objects if o.type=='LIGHT'}
guide=bpy.data.texts.new('08 START HERE')
guide.write('Hardware shape review. Camera, screens, ship transform and lights are unchanged from perspective-07.\nNew pieces are in 08 - Editable hardware. Prior replaced meshes are hidden, not deleted.\nThe original owner scene remains separate and unchanged.\nArmor shoulder faces, panel seams, controls, and fittings are actual geometry. Hoses remain Bezier curves.\nProjector opening is a live Boolean; its hidden cutout is under 08.\nKeep edits in this saved file; do not rerun this script over your hand edits.\nGrey materials separate armor, structural metal, rubber and controls. Final paint, glow and screen artwork remain for a later pass.\n')
s.camera=hero;s.cycles.samples=64;s.render.resolution_percentage=100;s.render.filepath=str(ROOT/(name+'-camera.png'))
for im in bpy.data.images:
    if im.has_data and not im.packed_file:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
s.camera=bpy.data.objects['07 Camera - construction'];s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
manifest={'input':str(source),'inputSha256':source_hash,'inputUnchanged':True,'cameraUnchanged':True,'screenTransformsUnchanged':True,'shipTransformUnchanged':True,'lightsUnchanged':True,'cameraLens':camera_lens,'newObjects':len(col.objects),'retiredObjects':retired,'blend':out.name,'ownerSceneObjects':len(bpy.data.scenes['SOURCE - owner saved scene'].objects),'lockedState':{'camera':[list(row)for row in camera_matrix],'screens':{n:[list(row)for row in m]for n,m in screen_matrices.items()},'ship':[list(row)for row in ship_matrix],'lights':light_state},'stage':'Grey hardware geometry review; visual approval pending'}
(ROOT/(name+'-manifest.json')).write_text(json.dumps(manifest,indent=2));print(json.dumps(manifest))
