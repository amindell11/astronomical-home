from pathlib import Path
import bpy, bmesh, json, sys
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
base=root/'results/nightshade-straight-fork';out=base/'round-01'
out.mkdir(parents=True,exist_ok=True)
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source
before=ship_source.fingerprint(bpy.context.scene)
assert before==json.loads((base/'precheck/ship_check.json').read_text())['fingerprint']
names=['Rebuilt pitched hull','Aft spine armor','Aft engine collar','Rear swept wing pair']
original={n:{'xyz':np.array([v.co[:] for v in bpy.data.objects[n].data.vertices]),
             'faces':[tuple(p.vertices) for p in bpy.data.objects[n].data.polygons],
             'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers]} for n in names}
body=bpy.data.objects[names[0]];co=original[body.name]['xyz'].copy()
assert co.shape==(444,3)
old_tree=BVHTree.FromPolygons([Vector(v) for v in co],original[body.name]['faces'])
targets={14:(.147,-.155),15:(.143,-.141),16:(.139,-.122),17:(.135,-.102),
         18:(.131,-.081),19:(.128,-.044),20:(.127,-.007),21:(.129,.031),
         22:(.132,.066),23:(.134,.084),24:(.134,.098),25:(.132,.102)}
for j,(hi,lo) in targets.items():
    ring=co[j*17:(j+1)*17];old_hi,old_lo=ring[0,2],ring[16,2]
    ring[:,2]=lo+(ring[:,2]-old_lo)/(old_hi-old_lo)*(hi-lo)
co[443,2]=.117
assert np.array_equal(co[:14*17],original[body.name]['xyz'][:14*17])
assert np.array_equal(co[:,:2],original[body.name]['xyz'][:,:2])
for v,p in zip(body.data.vertices,co):v.co=p
body.data.update()
new_tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],original[body.name]['faces'])
armor=bpy.data.objects['Aft spine armor']
for v in armor.data.vertices:
    ray=Vector((v.co.x,v.co.y,.6));direction=Vector((0,0,-1))
    old_hit=old_tree.ray_cast(ray,direction,1.2)[0]
    new_hit=new_tree.ray_cast(ray,direction,1.2)[0]
    assert old_hit is not None and new_hit is not None,v.index
    v.co.z+=new_hit.z-old_hit.z
armor.data.update()
old_rows=original[body.name]['xyz'][:442].reshape(26,17,3)
new_rows=co[:442].reshape(26,17,3)
axis_y=old_rows[:,:,1].mean(axis=1)[::-1]
old_depth=(old_rows[:,0,2]-old_rows[:,16,2])[::-1]
new_depth=(new_rows[:,0,2]-new_rows[:,16,2])[::-1]
new_center=((new_rows[:,0,2]+new_rows[:,16,2])/2)[::-1]
collar=bpy.data.objects['Aft engine collar']
collar_co=original[collar.name]['xyz'].copy().reshape(4,9,3)
for row in collar_co:
    center_z=(row[:,2].min()+row[:,2].max())/2;y=float(row[:,1].mean())
    factor=float(np.interp(y,axis_y,new_depth)/np.interp(y,axis_y,old_depth))
    center=float(np.interp(y,axis_y,new_center))
    row[:,2]=center+(row[:,2]-center_z)*factor
for v,p in zip(collar.data.vertices,collar_co.reshape(-1,3)):v.co=p
collar.data.update()

guide=json.loads((base/'fork-guide.json').read_text());rows=guide['rows']
wing=bpy.data.objects['Rear swept wing pair'];mesh=wing.data
assert len(mesh.vertices)==156 and len(rows)==61
bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table()
width=bm.verts.layers.float.new('temporary_fork_section')
for v in bm.verts:v[width]=v.index%12
edges=[e for e in bm.edges if abs(e.verts[0].index-e.verts[1].index)==12]
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=4,use_grid_fill=True)
groups={}
for v in bm.verts:groups.setdefault(round(v.co.y,5),[]).append(v)
assert len(groups)==61,(len(groups),len(bm.verts))
for (_,vs),row in zip(sorted(groups.items(),reverse=True),rows):
    vs=sorted(vs,key=lambda v:v[width])
    assert len(vs)==12 and all(abs(v[width]-k)<1e-4 for k,v in enumerate(vs))
    for v,p in zip(vs,row):v.co=p
bm.verts.layers.float.remove(width)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(mesh);bm.free();mesh.update()
for p in mesh.polygons:p.use_smooth=False
wing['construction']='Existing cage subdivided and fitted to original native straight fork outline and upper/lower surfaces; no legacy faces or UVs imported'

audit={}
for name,old in original.items():
    ob=bpy.data.objects[name]
    assert old['mods']==[(m.name,m.type) for m in ob.modifiers]
    if name!='Rear swept wing pair':assert old['faces']==[tuple(p.vertices) for p in ob.data.polygons]
    audit[name]={'topology_retained':name!='Rear swept wing pair','mesh_datablock_retained':True,
                 'vertices_before':len(old['xyz']),'vertices_after':len(ob.data.vertices),
                 'live_modifiers_retained':True}
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names),changed
assert set(after['parts'])==set(before['parts'])
bpy.context.scene['review']='Restored gradual aft taper; existing rear pair fitted to the original native straight fork'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'added_parts':[],'removed_parts':[],
    'parts':audit,'modifiers_applied':False,'owner_edits_outside_claimed_parts_unchanged':True,
    'guide_tolerance':guide['silhouette_tolerance'],'reference_scaled':False,
    'legacy_topology_or_uv_imported':False,'hidden_root_start_y':.16,
    'baseline':'6859548d; owner explicitly chose saved file after two-session mismatch'},indent=2))
result={'saved':str(source),'changed':changed,'wing_vertices':len(mesh.vertices),'wing_faces':len(mesh.polygons)}
