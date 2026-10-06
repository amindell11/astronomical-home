from pathlib import Path
import bpy,bmesh,json,sys

root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-tail-mounts/round-01';out.mkdir(parents=True,exist_ok=True)
assert Path(bpy.data.filepath).resolve()==source.resolve()
assert bpy.context.mode=='OBJECT' and not bpy.data.is_dirty
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
before=ship_source.fingerprint(bpy.context.scene)
(out/'owner-baseline-fingerprint.json').write_text(json.dumps(before,indent=2))
assert 'Tail root mounting blocks' not in bpy.data.objects
rows=[(.245,.095,.220,.078,-.035),(.200,.085,.270,.092,-.032),
      (.120,.080,.316,.097,-.026),(.035,.075,.280,.093,-.012),
      (-.060,.065,.235,.086,.005)]
vertices=[]
for y,inside,outside,top,bottom in rows:
    bevel=.012
    vertices.extend([(inside,y,top-bevel),(inside+bevel,y,top),
                     (outside-bevel,y,top),(outside,y,top-bevel),
                     (outside,y,bottom+bevel),(outside-bevel,y,bottom),
                     (inside+bevel,y,bottom),(inside,y,bottom+bevel)])
faces=[(j*8+k,j*8+(k+1)%8,(j+1)*8+(k+1)%8,(j+1)*8+k)
       for j in range(len(rows)-1) for k in range(8)]
for j in (0,len(rows)-1):
    for k in range(1,7):faces.append((j*8,j*8+k,j*8+k+1))
mesh=bpy.data.meshes.new('Tail root mounting block cage')
mesh.from_pydata(vertices,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
ob=bpy.data.objects.new('Tail root mounting blocks',mesh)
bpy.data.collections['role.hull'].objects.link(ob);ob.parent=bpy.data.objects['SymmetryOrigin']
for name in ('Clean armor slate','Clean armor face','Clean armor bevel'):
    mesh.materials.append(bpy.data.materials[name])
for p in mesh.polygons:
    p.material_index=2 if p.index<32 and p.index%8 in (0,2,4,6) else 0
    p.use_smooth=False
mirror=ob.modifiers.new('Live X symmetry','MIRROR');mirror.mirror_object=ob.parent
mirror.use_clip=True;mirror.merge_threshold=.00001
ob['construction']='Angular load-bearing shoulder blocks overlap the fuselage and the owner-edited straight fork roots'
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D' and area.spaces.active.local_view:
        ob.local_view_set(area.spaces.active,True)
after=ship_source.fingerprint(bpy.context.scene)
assert all(before['parts'][n]==after['parts'][n] for n in before['parts'])
assert set(after['parts'])-set(before['parts'])=={'Tail root mounting blocks'}
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_existing_parts':[],
    'added_parts':['Tail root mounting blocks'],'removed_parts':[],
    'owner_checkpoint':'bb5ab4df','all_existing_owner_parts_unchanged':True,
    'modifiers_applied':False,'live_mirror':True,'mount_vertices':len(vertices),
    'mount_faces':len(faces),'maximum_thickness':.124},indent=2))
result={'saved':str(source),'added':'Tail root mounting blocks','existing_parts_changed':[]}
