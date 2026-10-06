from pathlib import Path
import bpy,json,sys,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-secondary-rail-01/round-03';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((root/'results/nightshade-secondary-rail-01/round-02/check/ship_check.json').read_text())
assert bpy.context.mode=='OBJECT' and not bpy.data.is_dirty
assert Path(bpy.data.filepath).resolve()==source.resolve()
before=ship_source.fingerprint(bpy.context.scene);assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
rail=bpy.data.objects['Cockpit inset secondary rail'];steps=len(rail.data.vertices)//4
assert steps==97 and rail.data.users==1
matrix=[list(r) for r in rail.matrix_basis];faces=[list(f.vertices) for f in rail.data.polygons]
visibility=not rail.hide_get()
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
centers=[]
for i in range(steps):
    p=sum((rail.matrix_world@rail.data.vertices[4*i+k].co for k in range(4)),Vector())/4
    t=12*i/(steps-1)
    p.x-=.013*smooth(t/4)*(1-smooth((t-9)/3))
    centers.append(p)
dep=bpy.context.evaluated_depsgraph_get()
def tree(name):
    eo=bpy.data.objects[name].evaluated_get(dep);m=eo.to_mesh()
    result=BVHTree.FromPolygons([eo.matrix_world@v.co for v in m.vertices],[list(f.vertices) for f in m.polygons])
    eo.to_mesh_clear();return result
glass=tree('Upper canopy glazing');collar=tree('Cockpit rear retaining collar')
inv=rail.matrix_world.inverted();samples=[]
for i,center in enumerate(centers):
    t=12*i/(steps-1);growth=1+.42*smooth((t-2)/9)
    tan=centers[min(steps-1,i+1)]-centers[max(0,i-1)];tan.z=0;tan.normalize()
    normal=Vector((-tan.y,tan.x,0))
    section=[(-.004,.0012),(-.0025,.0045),(.0025,.0045),(.004,.0012)]
    for k,(distance,height) in enumerate(section):
        distance*=growth
        height*=1+.20*smooth((t-2)/9)
        p=center+normal*distance
        if i==0:p.x=0
        hit=glass.ray_cast(Vector((p.x,p.y,2)),Vector((0,0,-1)),4)[0];assert hit is not None
        p.z=hit.z+height
        if t>10:
            anchor=collar.ray_cast(Vector((.073+distance,.417,2)),Vector((0,0,-1)),4)[0];assert anchor is not None
            blend=smooth((t-10)/2);p.z=p.z*(1-blend)+(anchor.z+height)*blend
        rail.data.vertices[4*i+k].co=inv@p
    if i%8==0:samples.append({'station':t,'width_xy':.008*growth,'inward_shift':.013*smooth(t/4)*(1-smooth((t-9)/3))})
rail.data.update();bpy.context.view_layer.update();after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert changed==['Cockpit inset secondary rail'] and set(before['parts'])==set(after['parts'])
assert [list(r) for r in rail.matrix_basis]==matrix
assert [list(f.vertices) for f in rail.data.polygons]==faces and (not rail.hide_get())==visibility
assert rail.modifiers[0].type=='MIRROR'
(out/'edit-audit.json').write_text(json.dumps({'baseline':'round-02','changed_parts':changed,
    'purpose':'Pane divider raised on glass; cross-section thickens gradually toward rear collar',
    'source_topology_object_transforms_and_visibility_retained':True,
    'all_other_part_fingerprints_unchanged':True,'stations':samples},indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
result={'saved':str(source),'changed_parts':changed,'rail_width_front':.008,'rail_width_rear':.01136}
