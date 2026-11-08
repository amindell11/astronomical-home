"""Repair three annotated joins in the loaded geometry-15 copy; write --name .blend and JSON. Existing outputs fail."""
import bpy,bmesh,sys,json,hashlib
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:];name=args[args.index('--name')+1];output=root/(name+'.blend')
if output.exists():raise RuntimeError('Output exists')
s=bpy.context.scene;s.name='16 - Annotated join repairs';source=Path(bpy.data.filepath)
locked={o.name:[list(r)for r in o.matrix_world]for o in s.objects}
col=bpy.data.collections.new('16 - Fitted joins');s.collection.children.link(col)
def verts(o):return [o.matrix_world@v.co for v in o.data.vertices]
def rebuild(o,v,f,ids=None):
 old=o.data;d=bpy.data.meshes.new(old.name+' fitted');inv=o.matrix_world.inverted();d.from_pydata([inv@p for p in v],[],f)
 for m in old.materials:d.materials.append(m)
 for p,i in zip(d.polygons,ids or [0]*len(f)):p.material_index=i
 bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.faces.ensure_lookup_table()
 if bm.faces[0].normal.dot(old.polygons[0].normal)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
 bm.to_mesh(d);bm.free();d.update();o.data=d
 return o
O=bpy.data.objects
apron=O['14 | Projector formed armor'];a=verts(apron)
mount=O['14 | Forearm fold into Launch and controls'];v=verts(mount)
returns=O['14 | Launch casing folded returns'];r=verts(returns)
shoulder=O['14 | Projector to control folded shoulder'];h=verts(shoulder)
# Fit a single shared crease instead of the overlapping triangular tips.
a[6].z=1.3331;v[0]=a[6].copy();h[4]=a[6].copy()
normal=(v[2]-v[3]).cross(v[0]-v[3]);v[1].z=v[3].z-(normal.x*(v[1].x-v[3].x)+normal.y*(v[1].y-v[3].y))/normal.z
af=[list(p.vertices)for p in apron.data.polygons];af[1]=[3,8,7,6,2]
rebuild(apron,a,af,[p.material_index for p in apron.data.polygons])
rebuild(shoulder,h,[list(p.vertices)for p in shoulder.data.polygons],[p.material_index for p in shoulder.data.polygons])
# The tray meets the existing front rail rather than dipping below it.
v[11]=Vector((v[11].x,-3.9,.576));v[12]=v[4].copy()
f=[list(p.vertices)for p in mount.data.polygons];ids=[p.material_index for p in mount.data.polygons]
ids[8]=0
f[4]=[];f[3]=[2,4,13];f[5]=[6,13,4,11,10,9,8,7]
f.append([15,7,8,16]);ids.append(2)
fids=[(face,i)for face,i in zip(f,ids)if face]
rebuild(mount,v,[x[0]for x in fids],[x[1]for x in fids])
r[6]=v[11].copy();r[7]=v[4].copy();r[8]=v[4].copy()
r[15]=Vector((r[6].x,r[6].y,-.094));r[16]=Vector((r[7].x,r[7].y,-.094));r[17]=r[16].copy()
rf=[list(p.vertices)for p in returns.data.polygons][:-1]
rebuild(returns,r,rf,[p.material_index for p in returns.data.polygons][:len(rf)])
# Replace the overly wide clearance with the actual fitted tray footprint.
cut=O['15 | Launch underside clearance'];cv=verts(cut)
outline=[v[i]for i in [6,7,8,9,10,11,4,13]]
lo=[p-Vector((0,0,.10))for p in outline];hi=[p+Vector((0,0,5))for p in lo];n=len(lo)
cf=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
rebuild(cut,lo+hi,cf,[3]*len(cf))
bpy.context.view_layer.update()
errors={name:max(abs(O[name].matrix_world[i][j]-mat[i][j])for i in range(4)for j in range(4))for name,mat in locked.items()}
assert max(errors.values())<1e-6
s.camera=O['07 Camera - perspective'];bpy.ops.wm.save_as_mainfile(filepath=str(output))
(root/(name+'-checks.json')).write_text(json.dumps({'source':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'existingTransformsPreserved':len(locked),'maxTransformError':max(errors.values()),'modifiedMeshes':[apron.name,mount.name,returns.name,shoulder.name,cut.name],'addedMeshes':[],'visualApproval':'pending'},indent=2))
