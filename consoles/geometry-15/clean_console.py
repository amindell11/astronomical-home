"""Load inputs/owner-saved.blend and repair exposed geometry without moving existing objects.
Blender --background inputs/owner-saved.blend --python clean_console.py -- --name draft-01
Writes NAME.blend and NAME-manifest.json. Existing Blender output fails.
Use render_audit.py separately for the camera, angled and inspection renders.
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
s=bpy.context.scene;s.name='15 - Owner geometry cleanup'
hero=s.camera;camera_matrix=hero.matrix_world.copy();camera_lens=hero.data.lens
col=bpy.data.collections.new('15 - Local geometry repairs');s.collection.children.link(col)
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
    o=bpy.data.objects.new('15 | '+label,None);col.objects.link(o);o.parent=parent;o.location=loc;o.rotation_euler=rot;o.empty_display_size=.18;return o
def mesh(label,verts,faces,parent=None,indices=None,edge=.012):
    d=bpy.data.meshes.new('15 | '+label);d.from_pydata(verts,[],faces);d.update()
    for m in mats:d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
    o=bpy.data.objects.new('15 | '+label,d);col.objects.link(o);o.parent=parent
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
    d=bpy.data.curves.new('15 | '+label,'CURVE');d.dimensions='3D';d.bevel_depth=radius;d.bevel_resolution=4;d.resolution_u=16
    sp=d.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('15 | '+label,d);col.objects.link(o);o.parent=parent;o.data.materials.append(rubber);return o

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

locked={o.name:[list(r)for r in o.matrix_world]for o in s.objects if o.name not in col.objects}
changes=[]
def worldverts(o):return [o.matrix_world@v.co for v in o.data.vertices]
def replace_faces(o,verts,faces,ids):
    reference_normal=o.data.polygons[0].normal.copy()
    d=bpy.data.meshes.new(o.data.name+' repaired');inverse=o.matrix_world.inverted()
    d.from_pydata([inverse@p for p in verts],[],faces)
    for mat in o.data.materials:d.materials.append(mat)
    for p,i in zip(d.polygons,ids):p.material_index=i
    bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.faces.ensure_lookup_table()
    if bm.faces[0].normal.dot(reference_normal)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(d);bm.free();d.update();o.data=d
def triangles(o,face_indices):
    reference_normal=o.data.polygons[0].normal.copy()
    o.data=o.data.copy();bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table()
    bmesh.ops.triangulate(bm,faces=[bm.faces[i]for i in face_indices],quad_method='BEAUTY',ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.faces.ensure_lookup_table()
    if bm.faces[0].normal.dot(reference_normal)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(o.data);bm.free()

apron=obj('14 | Projector formed armor');av=worldverts(apron)

# Rear returns terminate on the existing base rather than hanging below the top panels.
base=obj('07 | Continuous left console body')
from mathutils.bvhtree import BVHTree
dg=bpy.context.evaluated_depsgraph_get();base_eval=base.evaluated_get(dg);bmdata=base_eval.to_mesh()
base_bvh=BVHTree.FromPolygons([base.matrix_world@v.co for v in bmdata.vertices],[list(p.vertices)for p in bmdata.polygons])
base_eval.to_mesh_clear()
def floor_at(p):
    hit,_,_,_=base_bvh.ray_cast(Vector((p.x,p.y,5)),Vector((0,0,-1)),12)
    return hit.z if hit else .65
def rear_return(label,edge):
    lower=[Vector((p.x,p.y,min(p.z-.12,floor_at(p)+.015)))for p in edge]
    return skin(label,edge+lower,[(i,i+1,i+1+len(edge),i+len(edge))for i in range(len(edge)-1)],[3]*(len(edge)-1),.10,.012)
rear_return('Projector rear casing closure',[av[0],av[1]])
f=obj('14 | Forearm fold into Launch and controls');fv=worldverts(f)
rear_return('Control pod rear casing closure',[fv[21],fv[20],fv[19],fv[18],fv[17],fv[16]])
rear_return('Projector to control rear end cap',[av[1],fv[20]])
retire(obj('09 | Right console shoulder'))
changes.append('Dark rear/end closures seat the projector and control pod on the existing base.')
changes.append('Obsolete floating shoulder behind the current casing hidden, not deleted.')

# The tray trims only the base material protruding through its surface.
launch=obj('07 | Launch assembly');lp,ln=assembly_plane(launch,-.07)
lv=[fv[i]for i in range(6,14)]
launch_inverse=launch.matrix_world.inverted()
outline=inset([(launch_inverse@p).xy[:]for p in lv],-.12)
lower=[launch.matrix_world@Vector((x,y,-.07))for x,y in outline]
cutv=lower+[p+Vector((0,0,5))for p in lower];n=len(lower)
cutf=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
cutter=mesh('Launch underside clearance',cutv,cutf,deck,[3]*len(cutf),0)
cutter.hide_render=True;cutter.hide_set(True);cutter.display_type='WIRE'
mod=base.modifiers.new('Clear exposed Launch overlap','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
changes.append('Base trimmed beneath Launch to remove the exposed crossing sliver.')

returns=obj('14 | Launch casing folded returns');returns.data=returns.data.copy()
bm=bmesh.new();bm.from_mesh(returns.data);bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.faces[0]],context='FACES_ONLY');bm.to_mesh(returns.data);bm.free()
mount=obj('14 | Forearm fold into Launch and controls')
mf=[];mi=[]
for p in mount.data.polygons:
    if p.index==7:mf.extend([(14,15,7,6),(14,6,1,0)]);mi.extend([p.material_index,p.material_index])
    else:mf.append(tuple(p.vertices));mi.append(p.material_index)
replace_faces(mount,worldverts(mount),mf,mi)
controls=obj('07 | Projection control assembly')
contact=rect('Control insert contact clearance',4.70,2.47,3.02,controls,(0,0,3),.56,.005,3,3)
contact.hide_render=True;contact.hide_set(True);contact.display_type='WIRE'
for target in [obj('14 | Forearm fold into Launch and controls'),returns]:
    mod=target.modifiers.new('Trim control insert intersection','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=contact
changes.append('Internal return removed and control insert intersections trimmed to eliminate exposed slivers.')

# Locate each penetration on the saved fascia without moving the owner's tube curves.
fascia=obj('13 | Cooling deck folded beneath monitor');vv=worldverts(fascia)
fp=vv[2];fn=(vv[3]-vv[2]).cross(vv[4]-vv[2]).normalized()
if fn.y>0:fn=-fn
def bezier(a,b,c,d,t):return a*(1-t)**3+3*b*t*(1-t)**2+3*c*t*t*(1-t)+d*t**3
entries=[]
for name in ['14 | Front entry hose 0','14 | Front entry hose 1']:
    hose=obj(name);points=hose.data.splines[0].bezier_points;hits=[]
    for a,b in zip(points,points[1:]):
        last=hose.matrix_world@a.co;last_d=(last-fp).dot(fn)
        for j in range(1,101):
            t=j/100;p=hose.matrix_world@bezier(a.co,a.handle_right,b.handle_left,b.co,t);dist=(p-fp).dot(fn)
            if last_d<=0 and dist>0:
                q=last.lerp(p,-last_d/(dist-last_d));hits.append((q,(p-last).normalized()))
            last=p;last_d=dist
    if not hits:raise RuntimeError('No outward fascia crossing: '+name)
    point,tangent=hits[0];entries.append(point)
    holder=root('Seated tube gland '+name[-1],point,parent=deck);holder.rotation_euler=tangent.to_track_quat('Z','Y').to_euler()
    radial('Fascia tube seal '+name[-1],[(-.06,.17),(0,.19),(.055,.16),(.11,.115)],holder,segments=16,top=5,wall=3)
changes.append('New seals sit at the actual tube/fascia crossings; saved tube paths unchanged.')

bpy.context.view_layer.update()
for name,mat in locked.items():
    assert max(abs(obj(name).matrix_world[i][j]-mat[i][j])for i in range(4)for j in range(4))<1e-6,name
for o in col.objects:
    if o.type!='MESH':continue
    center=sum((Vector(v)for v in o.bound_box),Vector())/8
    o.data.transform(Matrix.Translation(-center));o.matrix_basis=o.matrix_basis@Matrix.Translation(center)
guide=bpy.data.texts.new('15 START HERE');guide.write('Local cleanup of the owner-saved scene. Existing object transforms and tube paths are preserved.\nNew rear closures, seated tube seals and the Launch clearance cutter are in 15 - Local geometry repairs.\nThe owner-shaped visible panels are unchanged.\nSource: inputs/owner-saved.blend.\n')
s.camera=hero;bpy.ops.wm.save_as_mainfile(filepath=str(out))
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
(ROOT/(out.stem+'-manifest.json')).write_text(json.dumps({'input':str(source),'inputSha256':source_hash,'lockedMatrices':locked,'changes':changes,'visualApproval':'pending'},indent=2))
