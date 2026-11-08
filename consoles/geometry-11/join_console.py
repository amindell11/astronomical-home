"""Load geometry-10/console.blend, join the armor, rebuild the header and add a neutral projection cue.
Blender --background ../geometry-10/console.blend --python join_console.py -- --name draft-01
Writes NAME.blend, NAME-camera.png, NAME-structure.png, NAME-angle.png, NAME-manifest.json. Existing output fails.
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
s=bpy.context.scene;s.name='11 - Joined console and viewport'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('11 - Joined console structure');s.collection.children.link(col)
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
    o=bpy.data.objects.new('11 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('11 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('11 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('11 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('11 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

monitor_matrix=main.matrix_world.copy()
def obj(label):return bpy.data.objects[label]
for label in ['07 | Continuous right console body','09 | Joined equipment understructure','09 | Cooling bank supporting deck','08 | Launch armored mounting block']:
    retire(obj(label))
mat=bpy.data.materials.new('11 | Continuous chassis grey');mat.diffuse_color=(.035,.035,.035,1);mat.use_nodes=True
p=mat.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(.035,.035,.035,1);p.inputs['Roughness'].default_value=.8;p.inputs['Metallic'].default_value=.08;p.inputs['Specular IOR Level'].default_value=.18
mats.append(mat)
xs=[-2.65,0,1.85,2.3,4.6,5.1,8.4,11.5]
ys=[-5.0,-3.5,-2.8,-2.1,-1.0,.85,2.0,4.5]
left=[-.40,.60,.75,.90,.98,1.22,1.35,1.0]
middle=[-.60,-.30,.12,.70,1.0,1.4,1.35,1.0]
launch=obj('07 | Launch assembly');normal=launch.matrix_world.to_quaternion()@Vector((0,0,1));point=launch.matrix_world.translation
def launch_height(x,y):return point.z-(normal.x*(x-point.x)+normal.y*(y-point.y))/normal.z-.02
verts=[]
for j,y in enumerate(ys):
    for x in xs:
        if x<=1.85:z=left[j]
        elif x<=4.6:z=middle[j]
        elif y<=-1.:z=launch_height(x,y)
        else:z=left[j]
        verts.append((x,y,z))
nx=len(xs);ny=len(ys);faces=[]
for j in range(ny-1):
    for i in range(nx-1):faces.append((j*nx+i,j*nx+i+1,(j+1)*nx+i+1,(j+1)*nx+i))
boundary=list(range(nx))+[j*nx+nx-1 for j in range(1,ny)]+list(range((ny-1)*nx+nx-2,(ny-1)*nx-1,-1))+[j*nx for j in range(ny-2,0,-1)]
bottom=[]
for idx in boundary:bottom.append(len(verts));v=verts[idx];verts.append((v[0],v[1],-1.25))
for i,idx in enumerate(boundary):faces.append((idx,boundary[(i+1)%len(boundary)],bottom[(i+1)%len(bottom)],bottom[i]))
faces.append(tuple(reversed(bottom)))
body=mesh('Continuous faceted console body',verts,faces,deck,[13]*len(faces),.025)

def profile_prism(label,x0,x1,yz,material=0):
    n=len(yz);v=[(x,y,z)for x in [x0,x1]for y,z in yz]
    f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
    return mesh(label,v,f,deck,[material]*len(f),.035)
knuckle=profile_prism('Projector to controls armored knuckle',3.43,4.24,[(-1.02,.79),(-1.02,1.02),(.85,1.83),(1.12,1.83),(1.12,.79)],0)
knuckle.modifiers[0].width=.075;knuckle.modifiers[0].segments=1
for p in knuckle.data.polygons:p.material_index=0 if p.normal.z>.7 else 1 if p.normal.z>.1 else 2
retire(obj('09 | Broad low projector ramp'))
sections=[(-2.12,.06,3.82,1.00,.25),(-1.84,-.07,3.90,1.22,.45),(-.52,.39,3.60,1.40,.25),(.85,.47,3.52,1.92,.20)]
loops=[]
for y,left,right,z,shoulder in sections:
    loops.append([(left+.12,y,z-.34),(right-.12,y,z-.34),(right,y,z-.23),(right,y,z-.14),(right-shoulder,y,z),(left+shoulder,y,z),(left,y,z-.14),(left,y,z-.23)])
apron=loft('Projector wraparound armor',loops,deck,[0]*3,(2,2),.015)
for p in apron.data.polygons:p.material_index=0 if p.normal.z>.7 else 1 if p.normal.z>.1 else 2
cut=obj('09 | Projector aperture cutter');mod=apron.modifiers.new('Recessed optical cartridge','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
retire(obj('10 | Projection panel chamfered face'))
rect('Thick projection panel armor',4.65,2.42,.26,obj('07 | Projection control assembly'),(0,0,.145),.55,.10,0,2)

# Roof corners are chamfered volumes. The side supports sit outside the final camera.
for label in ['07 | Viewport upper lintel','07 | Viewport inner lip']:retire(obj(label))
posts=[o for o in s.objects if o.name.startswith('07 | Viewport side post')]
for o in posts:o.location.x=math.copysign(16.0,o.location.x)
projection=hero.calc_matrix_camera(bpy.context.evaluated_depsgraph_get(),x=1672,y=941)
inverse=(projection@hero.matrix_world.inverted()).inverted()
def at_y(x,y,depth):
    h=inverse@Vector((2*x/1672-1,1-2*y/941,0,1));v=Vector(h[:3])/h.w
    d=(v-hero.location).normalized();return hero.location+d*((depth-hero.location.y)/d.y)
roof=root('Beveled viewport header',(0,3.9,0),(math.pi/2,0,0))
def roof_plate(label,points,depth,bevel,top):
    outline=[]
    for x,y in points:
        p=at_y(x,y,3.9);outline.append((p.x,p.z))
    return plate(label,outline,depth,roof,bevel=bevel,top=top,wall=2)
roof_plate('Long chamfered header',[(-30,-35),(1702,-35),(1610,36),(62,36)],.95,.12,3)
roof_plate('Left beveled roof corner',[(-30,-35),(84,-35),(64,36),(54,43),(-30,8)],1.25,.11,2)
roof_plate('Right beveled roof corner',[(1588,-35),(1702,-35),(1702,8),(1618,43),(1608,36)],1.25,.11,2)
roof_plate('Inset viewport lip',[(65,25),(1607,25),(1605,35),(67,35)],.15,.035,2)

# A neutral volume restores the visual connection between the projector and hologram.
beam_mat=bpy.data.materials.new('11 | Neutral projection volume');beam_mat.use_nodes=True
nodes=beam_mat.node_tree.nodes;nodes.clear();out_node=nodes.new('ShaderNodeOutputMaterial');volume=nodes.new('ShaderNodeVolumePrincipled')
volume.inputs['Density'].default_value=.018;volume.inputs['Color'].default_value=(.72,.72,.72,1)
volume.inputs['Emission Color'].default_value=(.65,.65,.65,1);volume.inputs['Emission Strength'].default_value=.055
position=nodes.new('ShaderNodeNewGeometry');separate=nodes.new('ShaderNodeSeparateXYZ')
beam_mat.node_tree.links.new(position.outputs['Position'],separate.inputs[0])
fade=nodes.new('ShaderNodeMapRange');fade.clamp=True;fade.inputs['From Min'].default_value=3.7;fade.inputs['From Max'].default_value=5.05;fade.inputs['To Min'].default_value=1;fade.inputs['To Max'].default_value=0
beam_mat.node_tree.links.new(separate.outputs['Z'],fade.inputs['Value'])
for socket,value in [('Density',.018),('Emission Strength',.055)]:
    strength=nodes.new('ShaderNodeMath');strength.operation='MULTIPLY';strength.inputs[1].default_value=value
    beam_mat.node_tree.links.new(fade.outputs['Result'],strength.inputs[0]);beam_mat.node_tree.links.new(strength.outputs[0],volume.inputs[socket])
beam_mat.node_tree.links.new(volume.outputs['Volume'],out_node.inputs['Volume'])
projector=obj('09 | Projector optical assembly');origin=projector.matrix_world@Vector((0,0,.10))
beam=root('Projection volume',parent=deck)
loops=[]
for z,rx,ry in [(origin.z,.82,.33),(4.2,3.85,.95),(5.05,4.55,1.25)]:
    loops.append([(origin.x+math.cos(i*math.tau/32)*rx,origin.y+math.sin(i*math.tau/32)*ry,z)for i in range(32)])
cone=loft('Neutral projector frustum',loops,beam,[3,3],(3,3),0);cone.data.materials.clear();cone.data.materials.append(beam_mat)
for p in cone.data.polygons:p.material_index=0

bpy.context.view_layer.update()
for o in col.objects:
    if o.type not in {'MESH','CURVE'}:continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
bpy.context.view_layer.update()
assert max(abs(hero.matrix_world[i][j]-camera_matrix[i][j])for i in range(4)for j in range(4))<1e-7
assert max(abs(main.matrix_world[i][j]-monitor_matrix[i][j])for i in range(4)for j in range(4))<1e-7
guide=bpy.data.texts.new('11 START HERE')
guide.write('Joined console structure, chamfered viewport header, neutral projection volume.\nMonitor pose, camera, ship and light objects are unchanged from geometry-10.\nNew body supports the editable armor panels. Old replaced body pieces are hidden.\nSide posts remain in the scene outside the final camera.\nHide 11 | Neutral projector frustum to inspect the geometry without the projection cue.\nOriginal owner scene remains separate and source files are unchanged.\n')
s.camera=hero;s.cycles.samples=64 if name=='console' else 32;s.render.filepath=str(ROOT/(name+'-camera.png'))
for im in bpy.data.images:
    if im.has_data and not im.packed_file:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out));bpy.ops.render.render(write_still=True)
cone.hide_render=True;s.render.filepath=str(ROOT/(name+'-structure.png'));bpy.ops.render.render(write_still=True)
s.camera=obj('07 Camera - construction');s.render.filepath=str(ROOT/(name+'-angle.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(name+'-manifest.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'monitorMatrix':[list(row)for row in monitor_matrix],'cameraUnchanged':True,'monitorUnchanged':True,'sidePostNames':[o.name for o in posts],'visualApproval':'pending'},indent=2))
