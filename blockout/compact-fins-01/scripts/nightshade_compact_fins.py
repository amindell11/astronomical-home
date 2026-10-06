from pathlib import Path
import bpy,json,sys,hashlib
from mathutils import Vector

root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
base=root/'results/nightshade-sharp-fins';out=base/'round-02';out.mkdir(parents=True,exist_ok=True)
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
previous=json.loads((base/'round-01/check/ship_check.json').read_text())
before=ship_source.fingerprint(bpy.context.scene)
assert before==previous['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==previous['source_sha256']
names=['Upper small swept fins','Lower small swept fins','Upper fin armor joint','Lower fin armor joint']
original={n:{'faces':[tuple(p.vertices) for p in bpy.data.objects[n].data.polygons],
             'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
             'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers]} for n in names}
factor=.62;anchor=.24
def compact(p):return Vector((p.x*factor,anchor+(p.y-anchor)*factor,p.z))
for prefix in ('Upper','Lower'):
    fin=bpy.data.objects[prefix+' small swept fins']
    for v in fin.data.vertices:v.co=compact(v.co)
    fin.data.update()
    seam=bpy.data.objects[prefix+' fin armor joint']
    to_fin=fin.matrix_world.inverted()@seam.matrix_world
    to_seam=seam.matrix_world.inverted()@fin.matrix_world
    for v in seam.data.vertices:v.co=to_seam@compact(to_fin@v.co)
    seam.data.update()
for name,old in original.items():
    ob=bpy.data.objects[name]
    assert old['faces']==[tuple(p.vertices) for p in ob.data.polygons]
    assert old['matrix']==[list(r) for r in ob.matrix_basis]
    assert old['mods']==[(m.name,m.type) for m in ob.modifiers]
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names) and set(before['parts'])==set(after['parts'])
bpy.context.scene['review']='Compact sharp upper and lower fins; reduced projection, defined shoulder and clipped tips'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'topology_retained':True,'object_transforms_retained':True,
    'modifiers_applied':False,'live_mirrors_retained':True,'local_fin_z_unchanged':True,
    'xy_factor_from_sharpened_pass':factor,'y_anchor':anchor,'owner_checkpoint':'e77f6110',
    'intermediate_checkpoint':'9060885a','all_unclaimed_parts_unchanged':True},indent=2))
result={'saved':str(source),'changed':changed,'xy_factor':factor}
