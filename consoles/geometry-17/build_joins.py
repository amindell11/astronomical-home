"""Load geometry-15 and replace only connecting faces, preserving every original vertex. --name writes a new .blend and checks JSON; existing output fails."""
import bpy,bmesh,json,sys,hashlib
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent;args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];out=root/(name+'.blend')
if out.exists():raise RuntimeError('Choose a new output')
s=bpy.context.scene;source=Path(bpy.data.filepath);s.name='17 - Locked armor with fitted joins';O=bpy.data.objects
col=bpy.data.collections.new('17 - Planar connecting panels');s.collection.children.link(col)
locked={o.name:([list(r)for r in o.matrix_world],[list(v.co)for v in o.data.vertices]if o.type=='MESH'else None)for o in s.objects}
def wverts(o):return[o.matrix_world@v.co for v in o.data.vertices]
mount=O['14 | Forearm fold into Launch and controls'];v=wverts(mount)
apron=O['14 | Projector formed armor'];a=wverts(apron)
shoulder=O['14 | Projector to control folded shoulder']
def remove_faces(o,indices):
 old=o.data;new=bpy.data.meshes.new(old.name+' joins removed');faces=[p for p in old.polygons if p.index not in indices];new.from_pydata([v.co for v in old.vertices],[],[list(p.vertices)for p in faces])
 for mat in old.materials:new.materials.append(mat)
 for p,oldp in zip(new.polygons,faces):p.material_index=oldp.material_index
 new.update();o.data=new
remove_faces(mount,{6,7,8});remove_faces(shoulder,{1})
mount.modifiers.remove(mount.modifiers['Trim control insert intersection'])
errors={}
def panel(label,points,mat=0,thickness=.07):
 normal=(points[1]-points[0]).cross(points[2]-points[0]).normalized()
 error=max(abs((p-points[0]).dot(normal))for p in points);errors[label]=error
 assert error<1e-5,(label,error)
 if normal.z<0:points=list(reversed(points))
 d=bpy.data.meshes.new('17 | '+label);d.from_pydata(points,[],[tuple(range(len(points)))]);d.materials.append(bpy.data.materials[['08 | Armor face','08 | Armor side walls','08 | Dark structural housing'][mat]])
 o=bpy.data.objects.new(d.name,d);col.objects.link(o)
 mod=o.modifiers.new('Inward panel thickness','SOLIDIFY');mod.thickness=thickness;mod.offset=-1
 mod=o.modifiers.new('Small edge bevel','BEVEL');mod.width=.008;mod.segments=2
 return o
# This plane is defined by the existing armor ridge and the existing tray edge.
normal=(v[1]-v[0]).cross(v[6]-v[0]);t=normal.dot(v[0]-v[14])/normal.dot(v[21]-v[14]);j=v[14].lerp(v[21],t)
panel('Ridge to control transition',[v[0],v[1],v[6],j])
# Each connector follows the existing apron boundary instead of cutting across it.
panel('Apron upper connection',[a[2],a[5],v[21]])
panel('Apron control connection',[a[5],j,v[21]])
panel('Apron lower connection',[a[5],a[6],j])
panel('Existing armor seam closure',[a[6],v[0],j])
panel('Control front left return',[j,v[6],v[14]],1)
panel('Control front middle return',[v[14],v[6],v[15]],1)
panel('Control front right return',[v[6],v[7],v[15]],1)
panel('Control corner inner closure',[v[15],v[7],v[16]],1)
panel('Control corner outer closure',[v[16],v[7],v[8]],1)
# The narrow flange joins the lower tray edge to the unmodified base surface.
p=Vector((3.6054,-3.6237,.4944));q=Vector((4.0171,-2.807,.2532))
panel('Lower join flange front',[v[11],p,v[4]],1)
panel('Lower join flange rear',[p,q,v[4]],1)
panel('Lower join end return',[v[11],p,p-Vector((0,0,.15)),v[11]-Vector((0,0,.15))],2)
bpy.context.view_layer.update()
for name,(mat,vs)in locked.items():
 o=O[name];assert [list(r)for r in o.matrix_world]==mat,name
 if vs is not None:assert [list(v.co)for v in o.data.vertices]==vs,name
s.camera=O['07 Camera - perspective'];bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/(out.stem+'-checks.json')).write_text(json.dumps({'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'existingTransformsAndVerticesPreserved':len(locked),'removedJoinFaces':{mount.name:[6,7,8],shoulder.name:[1]},'panelPlanarityError':errors,'visualApproval':'pending'},indent=2))
