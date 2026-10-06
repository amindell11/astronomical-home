from pathlib import Path
import bpy,json,sys,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-secondary-rail-01/round-02';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((root/'results/nightshade-secondary-rail-01/round-01/check/ship_check.json').read_text())
assert bpy.context.mode=='OBJECT' and not bpy.data.is_dirty
assert Path(bpy.data.filepath).resolve()==source.resolve()
before=ship_source.fingerprint(bpy.context.scene);assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
names=['Upper canopy glazing','Lower canopy glazing','Cockpit inset secondary rail']
records={n:{'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
    'faces':[list(f.vertices) for f in bpy.data.objects[n].data.polygons],
    'visible':not bpy.data.objects[n].hide_get()} for n in names}
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
upper=bpy.data.objects[names[0]];inv=upper.matrix_world.inverted();moved=[]
for v in upper.data.vertices:
    p=upper.matrix_world@v.co
    longitudinal=smooth((.485-p.y)/.065)*smooth((p.y-.352)/.045)
    cross=1-smooth((p.x-.06)/.07)
    # Support the rear rail without widening the glass or changing its nose.
    raised=longitudinal*cross*(.013+.015*min(1,p.x/.085)**1.2)
    # Leave the concealed equator and lower joint untouched.
    raised*=smooth((p.z+.02)/.06)
    if raised>1e-8:p.z+=raised;v.co=inv@p;moved.append(v.index)
upper.data.update()
for name in names[:2]:
    ob=bpy.data.objects[name];assert not any(m.type=='SUBSURF' for m in ob.modifiers)
    m=ob.modifiers.new('Glass surface refinement','SUBSURF');m.subdivision_type='CATMULL_CLARK';m.levels=2;m.render_levels=2
    ob.modifiers.move(len(ob.modifiers)-1,1)
bpy.context.view_layer.update();dep=bpy.context.evaluated_depsgraph_get()
def evaluated_tree(name):
    eo=bpy.data.objects[name].evaluated_get(dep);m=eo.to_mesh()
    tree=BVHTree.FromPolygons([eo.matrix_world@v.co for v in m.vertices],[list(f.vertices) for f in m.polygons]);eo.to_mesh_clear();return tree
glass=evaluated_tree('Upper canopy glazing');collar=evaluated_tree('Cockpit rear retaining collar')
rail=bpy.data.objects['Cockpit inset secondary rail'];inv=rail.matrix_world.inverted();steps=len(rail.data.vertices)//4
section=[(-.004,.0012),(-.0025,.0045),(.0025,.0045),(.004,.0012)]
for v in rail.data.vertices:
    p=rail.matrix_world@v.co;i,k=divmod(v.index,4);t=12*i/(steps-1);d,h=section[k]
    hit=glass.ray_cast(Vector((p.x,p.y,2)),Vector((0,0,-1)),4)[0];assert hit is not None
    p.z=hit.z+h
    if t>10:
        anchor=collar.ray_cast(Vector((.073+d,.417,2)),Vector((0,0,-1)),4)[0];assert anchor is not None
        blend=smooth((t-10)/2);p.z=p.z*(1-blend)+(anchor.z+h)*blend
    v.co=inv@p
rail.data.update();bpy.context.view_layer.update()
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert set(changed)==set(names) and set(before['parts'])==set(after['parts'])
for name,old in records.items():
    ob=bpy.data.objects[name]
    assert [list(r) for r in ob.matrix_basis]==old['matrix']
    assert [list(f.vertices) for f in ob.data.polygons]==old['faces'] and (not ob.hide_get())==old['visible']
    assert ob.modifiers[0].type=='MIRROR'
(out/'edit-audit.json').write_text(json.dumps({'checkpoint':'762d8401','changed_parts':changed,
    'upper_glass_rear_vertices_adjusted':moved,'source_topology_object_transforms_and_visibility_retained':True,
    'glass_front_control_vertices_unchanged':True,'glass_refinement':'Live Catmull-Clark level 2 after Mirror, before Solidify',
    'secondary_rail':'Reprojected to evaluated glass, with retained collar anchors',
    'all_other_part_fingerprints_unchanged':True},indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(source))
result={'saved':str(source),'changed_parts':changed,'rear_control_vertices':len(moved)}
