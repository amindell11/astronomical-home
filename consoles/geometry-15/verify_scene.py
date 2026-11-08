"""Check the loaded cleanup against owner-baseline.json; write NAME-verification.json without saving the scene."""
import bpy,bmesh,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent;stem=Path(bpy.data.filepath).stem
baseline=json.loads((root/'owner-baseline.json').read_text());manifest=json.loads((root/(stem+'-manifest.json')).read_text())
bpy.context.view_layer.update();s=bpy.context.scene
assert s.name=='15 - Owner geometry cleanup'
assert s.camera.name==baseline['camera']
assert hashlib.sha256(Path(manifest['input']).read_bytes()).hexdigest()==baseline['sha256']==manifest['inputSha256']
changed={'14 | Launch casing folded returns','14 | Forearm fold into Launch and controls'}
max_error=0;curve_count=0
for name,prior in baseline['objects'].items():
    o=bpy.data.objects[name]
    error=max(abs(o.matrix_world[i][j]-prior['matrix'][i][j])for i in range(4)for j in range(4));max_error=max(error,max_error)
    assert error<1e-6,name
    if o.type=='MESH':
        assert len(o.data.vertices)>=len(prior['vertices']),name
        assert max((abs(v.co[j]-xyz[j])for v,xyz in zip(o.data.vertices,prior['vertices'])for j in range(3)),default=0)<1e-5,name
        if name not in changed:assert [list(p.vertices)for p in o.data.polygons]==prior['faces'],name
    if o.type=='CURVE':
        curves=[[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points]for sp in o.data.splines]
        assert curves==prior['curves'],name;curve_count+=1
    if name!='09 | Right console shoulder':assert o.hide_render==prior['hideRender'],name
checks={};dg=bpy.context.evaluated_depsgraph_get()
names=changed|{'07 | Continuous left console body'}|{o.name for o in bpy.data.collections['15 - Local geometry repairs'].objects if o.type=='MESH'}
for name in names:
    o=bpy.data.objects[name];ev=o.evaluated_get(dg);me=ev.to_mesh();bm=bmesh.new();bm.from_mesh(me)
    bad=sum(not e.is_manifold for e in bm.edges);assert bad==0,(name,bad)
    checks[name]=bad;bm.free();ev.to_mesh_clear()
report={'result':'pass','sourceSha256':baseline['sha256'],'existingObjectsPreserved':len(baseline['objects']),'maxTransformError':max_error,'originalMeshVerticesPreserved':True,'curvePathsPreserved':curve_count,'evaluatedNonManifoldEdges':checks,'visualApproval':'pending'}
(root/(stem+'-verification.json')).write_text(json.dumps(report,indent=2));print(json.dumps(report))
