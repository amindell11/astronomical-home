from pathlib import Path
import bpy,json,math,sys,hashlib
from mathutils import Vector
root=Path('D:/amind/git/agent-1')
scratch=Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba')
source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-rounded-canopy/round-01';out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
baseline=json.loads((root/'results/nightshade-frame-fit/round-02/check/ship_check.json').read_text())
assert Path(bpy.data.filepath).resolve()==source.resolve() and bpy.context.mode=='OBJECT'
before=ship_source.fingerprint(bpy.context.scene)
assert before==baseline['fingerprint']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline['source_sha256']
original=json.loads((scratch/'canopy-round-before.json').read_text())
names=['Upper canopy glazing','Lower canopy glazing']
audit={}
for name in names:
    ob=bpy.data.objects[name];inv=ob.matrix_world.inverted()
    points=[ob.matrix_world@v.co for v in ob.data.vertices]
    matrix=[list(r) for r in ob.matrix_basis]
    mods=[(m.name,m.type) for m in ob.modifiers]
    mesh=ob.data.as_pointer();visible=not ob.hide_get()
    # Preserve each cross-section's angle while rounding its longitudinal outline.
    rows={}
    for p in points:rows.setdefault(round(p.y,6),[]).append(p)
    envelope=sorted((sum(p.y for p in ps)/len(ps),max(abs(p.x) for p in ps)) for y,ps in rows.items() if len(ps)>=7 or len(ps)==1)
    def width(y):
        for (a,wa),(b,wb) in zip(envelope,envelope[1:]):
            if a<=y<=b:return wa+(wb-wa)*(y-a)/(b-a)
        return 0
    edits=[]
    for v,p in zip(ob.data.vertices,points):
        if p.y<=.707:continue
        q=p.copy()
        if p.y>=.82299:
            q.y=.817
        else:
            t=max(0,min(1,(p.y-.707)/(.744-.707)))
            blend=t*t*(3-2*t)
            axial=max(0,(p.y-.744)/(.823-.744))
            desired=.103*math.sqrt(max(0,1-axial*axial))
            w=width(p.y)
            assert w>0,(name,v.index,p.y)
            q.x=p.x*((1-blend)+blend*desired/w)
            q.y=p.y-.006*axial
        if (q-p).length>1e-8:
            v.co=inv@q;edits.append(v.index)
    ob.data.update()
    assert [list(r) for r in ob.matrix_basis]==matrix
    assert [(m.name,m.type) for m in ob.modifiers]==mods and any(m.type=='MIRROR' for m in ob.modifiers)
    assert mesh==ob.data.as_pointer() and visible==(not ob.hide_get())
    assert [list(p.vertices) for p in ob.data.polygons]==original[name]['faces']
    for i,p in enumerate(original[name]['vertices']):
        if i not in edits:assert tuple(ob.data.vertices[i].co)==tuple(p)
    audit[name]={'changed_vertices':edits,'vertices':len(ob.data.vertices),'faces':len(ob.data.polygons)}
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]
assert set(changed)==set(names) and set(before['parts'])==set(after['parts'])
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'edit-audit.json').write_text(json.dumps({'source_checkpoint':'73ca2f1d','changed_parts':changed,
    'parts_added':[],'parts_removed':[],'all_unclaimed_parts_unchanged':True,
    'hull_rims_fins_and_transforms_unchanged':True,'topology_and_live_modifiers_retained':True,
    'canopy_aft_of_y_0707_unchanged':True,'edits':audit},indent=2))
result={'saved':str(source),'changed_parts':changed,'vertex_changes':{n:len(a['changed_vertices']) for n,a in audit.items()}}
