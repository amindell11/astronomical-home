import bpy,bmesh,sys,json,hashlib
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/interfaces')
for label in ['owner','refined']:
 source=root/label/'Bulwark.blend';sha=hashlib.sha256(source.read_bytes()).hexdigest()
 src=ship_source.open_source(source);names=[o.name for o in src.objects if o.type=='MESH' and not o.hide_render]
 ev=ship_source.Evaluation(src);ev.show_only(names);scene=ev.scene
 ship_source.apply_review_preset(scene,1000);s=scene.display.shading;s.light='STUDIO';s.show_shadows=True;s.show_cavity=True;s.cavity_type='BOTH';s.show_object_outline=False;s.background_type='WORLD';scene.world.color=(.11,.12,.14);scene.display.render_aa='32'
 camera=bpy.data.objects.new('Interface review camera',bpy.data.cameras.new('Interface review camera'));scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO';camera.data.clip_start=.01;camera.data.clip_end=100
 shots=[('tail-join',(-1,0,0),(0,0,1),(0,-2.0,2.3),5.4,1200,600),('cockpit-seat',(7,14,10),(0,0,1),(0,2.9,2.35),4.3,1000,750),('circular-inset',(.999592,.01863,.02163),(0,0,1),(2.92,1.96,1.44),2.3,850,850)]
 for name,direction,up,center,scale,w,h in shots:
  camera.matrix_world=ship_render.camera_matrix(direction,up,center,30);camera.data.ortho_scale=scale;scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(root/label/'studio'/f'{name}.png');bpy.ops.render.render(write_still=True)
 if label=='refined':
  stats={}
  for name in ['04 Tail booms','06 Cockpit surround','07 Side insets']:
   mesh=ev.mesh(name);bm=bmesh.new();bm.from_mesh(mesh)
   stats[name]={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'vertices':len(bm.verts),'faces':len(bm.faces)}
   bm.free();bpy.data.meshes.remove(mesh)
  a=json.loads((root/'owner/check/ship_check.json').read_text());b=json.loads((root/'refined/check/ship_check.json').read_text())
  old={(f['rule'],f['part']) for f in a['findings']};new={(f['rule'],f['part']) for f in b['findings']}
  assert not new-old,new-old
  assert all(s['nonmanifold_edges']==0 for s in stats.values()),stats
  report={'source_sha256':sha,'parts':stats,'findings_before':len(a['findings']),'findings_after':len(b['findings']),'new_findings':list(new-old),'removed_findings':list(old-new)}
  (root/'refined/validation-delta.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
 assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
