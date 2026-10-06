from pathlib import Path
import bpy,json,sys,math,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-secondary-rail-01/round-01';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((root/'results/nightshade-cockpit-frame-01/round-01/check/ship_check.json').read_text())
assert bpy.context.mode=='OBJECT' and not bpy.data.is_dirty and Path(bpy.data.filepath).resolve()==source.resolve()
before=ship_source.fingerprint(bpy.context.scene);assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
name='Cockpit inset secondary rail';assert name not in bpy.data.objects
def tree(ob):
    return BVHTree.FromPolygons([ob.matrix_world@v.co for v in ob.data.vertices],[list(p.vertices) for p in ob.data.polygons])
glass=tree(bpy.data.objects['Upper canopy glazing']);collar=tree(bpy.data.objects['Cockpit rear retaining collar'])
path=[Vector((x,y,0)) for x,y in [(0,.790),(.024,.789),(.047,.781),(.070,.762),
    (.094,.722),(.118,.668),(.135,.609),(.141,.551),(.137,.501),(.122,.467),(.100,.443),(.080,.428),(.073,.417)]]
end=len(path)-1
def curve(t):
    i=min(int(t),end-1);u=t-i;a=path[i-1] if i else path[0]*2-path[1]
    b,c=path[i:i+2];d=path[i+2] if i+2<=end else path[-1]*2-path[-2]
    return (2*b+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)*.5
def sample(t,distance,height):
    p=curve(t);tan=curve(min(end,t+.01))-curve(max(0,t-.01));tan.normalize()
    p+=Vector((-tan.y,tan.x,0))*distance
    if t==0:p.x=0
    hit=glass.ray_cast(Vector((p.x,p.y,2)),Vector((0,0,-1)),4)[0];assert hit is not None,tuple(p)
    p.z=hit.z+height
    if t>end-2:
        anchor=collar.ray_cast(Vector((.073+distance,.417,2)),Vector((0,0,-1)),4)[0];assert anchor is not None
        blend=(t-(end-2))/2;blend=blend*blend*(3-2*blend)
        p.z=p.z*(1-blend)+(anchor.z+height)*blend
    return tuple(p)
steps=97;section=[(-.004,.001),(-.0025,.0045),(.0025,.0045),(.004,.001)]
vs=[sample(end*i/(steps-1),d,h) for i in range(steps) for d,h in section]
fs=[];mi=[]
for i in range(steps-1):
    for k in range(3):fs.append((i*4+k,(i+1)*4+k,(i+1)*4+k+1,i*4+k+1));mi.append(0 if k==1 else 1)
mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update();mesh.uv_layers.new(name='PaintUV')
for n in ['Clean armor bevel','Detail machined armor']:mesh.materials.append(bpy.data.materials[n])
for f,i in zip(mesh.polygons,mi):f.material_index=i
ob=bpy.data.objects.new(name,mesh);bpy.data.collections['role.hull'].objects.link(ob);ob.parent=bpy.data.objects['SymmetryOrigin']
mod=ob.modifiers.new('Live X symmetry','MIRROR');mod.mirror_object=bpy.data.objects['SymmetryOrigin'];mod.use_clip=True
mod=ob.modifiers.new('Editable secondary rail thickness','SOLIDIFY');mod.thickness=.002;mod.offset=-1
bpy.context.view_layer.update();after=ship_source.fingerprint(bpy.context.scene)
assert all(before['parts'][n]==after['parts'][n] for n in before['parts'])
assert set(after['parts'])-set(before['parts'])=={name}
(out/'edit-audit.json').write_text(json.dumps({'checkpoint':'2a7f4cf57787e1a0f9274c5a87c11c61d57d9116',
    'new_parts':[name],'existing_parts_changed':[],'all_previous_part_fingerprints_unchanged':True,
    'reference':'Approved three-quarter concept: inset U rail with a glass band outside it',
    'attachment':'Ends rise onto the existing rear collar'},indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
result={'saved':str(source),'new_part':name}
