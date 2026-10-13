import bpy,bmesh,sys,json,hashlib,math
from pathlib import Path
import numpy as np
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/hinged-flaps')
source=root/'refined/Bulwark.blend'
sha=hashlib.sha256(source.read_bytes()).hexdigest()
src=ship_source.open_source(source)
audit=json.loads((root/'refined/hinge-audit.json').read_text())
new_mesh_names=[n for n in audit['added_parts'] if bpy.data.objects[n].type=='MESH']
ev=ship_source.Evaluation(src)
stats={}
for name in new_mesh_names:
    mesh=ev.mesh(name)
    bm=bmesh.new();bm.from_mesh(mesh)
    stats[name]={'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'zero_area_faces':sum(f.calc_area()<1e-9 for f in bm.faces),'vertices':len(bm.verts),'faces':len(bm.faces),'volume':bm.calc_volume(signed=True)}
    bm.free();bpy.data.meshes.remove(mesh)
    assert stats[name]['nonmanifold_edges']==0 and stats[name]['zero_area_faces']==0 and stats[name]['volume']>0,stats[name]
old=json.loads((root/'owner/check/ship_check.json').read_text())
new=json.loads((root/'refined/check/ship_check.json').read_text())
oldrules={(f['rule'],f['part']) for f in old['findings']}
newrules={(f['rule'],f['part']) for f in new['findings']}
assert not newrules-oldrules,newrules-oldrules
allowed={'10 Tail fins','10 Tail fins.001'}
assert all(new['fingerprint']['parts'][n]==p for n,p in old['fingerprint']['parts'].items() if n not in allowed)

src=ship_source.open_source(source)
base=ship_source.fingerprint(src)
motion=[]
for hinge,angle in zip(audit['hinges'],[-12,12]):
    control=bpy.data.objects[hinge['control']]
    flap=bpy.data.objects[hinge['flap']]
    fixed=bpy.data.objects[hinge['fixed']]
    start=[flap.matrix_world@v.co for v in flap.data.vertices]
    stationary=fixed.matrix_world.copy()
    control.rotation_euler.x=math.radians(angle)
    bpy.context.view_layer.update()
    posed=[flap.matrix_world@v.co for v in flap.data.vertices]
    assert (posed[2]-start[2]).length>.15
    assert fixed.matrix_world==stationary
    lengths=[abs((posed[i]-posed[j]).length-(start[i]-start[j]).length) for i in range(len(start)) for j in range(i)]
    assert max(lengths)<1e-5
    bevels=[m for m in flap.modifiers if m.type=='BEVEL' and m.show_viewport]
    for m in bevels:m.show_viewport=False
    bpy.context.view_layer.update()
    evaluated=flap.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh=evaluated.to_mesh()
    w=evaluated.matrix_world
    points=np.array([list(w@v.co) for v in mesh.vertices])
    reflected=points*np.array([-1,1,1])
    symmetry=max(float(np.linalg.norm(points-p,axis=1).min()) for p in reflected)
    evaluated.to_mesh_clear()
    assert symmetry<1e-5,symmetry
    for m in bevels:m.show_viewport=True
    control.rotation_euler.x=0
    bpy.context.view_layer.update()
    reset=max((flap.matrix_world@v.co-start[i]).length for i,v in enumerate(flap.data.vertices))
    assert reset<1e-6
    motion.append({'flap':flap.name,'tested_degrees':angle,'max_rigidity_error':max(lengths),'mirrored_cage_symmetry_error':symmetry,'restoration_error':reset,'stationary_brackets_unchanged':True})
assert ship_source.fingerprint(src)==base
(root/'refined/validation-delta.json').write_text(json.dumps({'source_sha256':sha,'hinge_meshes':stats,'motion':motion,'findings_before':len(old['findings']),'findings_after':len(new['findings']),'new_findings':[],'resolved_findings':list(oldrules-newrules),'only_flap_parenting_changed':True},indent=2))

for kind in ['sheet','scale','compare']:
    args=['--ship',str(root/'refined/ship.json'),'--out',str(root/'refined'/kind),'--set',kind,'--size','512']
    if kind=='compare':args+=['--before',str(root/'owner/Bulwark.blend')]
    ship_render.main(args)

def pose_review(deployed):
    src=ship_source.open_source(source)
    if deployed:
        bpy.data.objects['Upper flap deployment'].rotation_euler.x=math.radians(-12)
        bpy.data.objects['Lower flap deployment'].rotation_euler.x=math.radians(12)
        bpy.context.view_layer.update()
    names=[o.name for o in src.objects if o.type=='MESH' and not o.hide_render]
    ev=ship_source.Evaluation(src);ev.show_only(names)
    scene=ev.scene
    ship_source.apply_review_preset(scene,1200)
    shading=scene.display.shading
    shading.light='STUDIO';shading.studiolight_rotate_z=.3
    shading.show_shadows=True;shading.show_cavity=True;shading.cavity_type='BOTH'
    shading.curvature_ridge_factor=1.1;shading.curvature_valley_factor=1
    shading.show_object_outline=False;shading.background_type='WORLD'
    scene.world.color=(.11,.12,.14);scene.display.render_aa='32'
    camera=bpy.data.objects.new('Hinge review camera',bpy.data.cameras.new('Hinge review camera'))
    scene.collection.objects.link(camera);scene.camera=camera
    camera.data.type='ORTHO';camera.data.clip_start=.01;camera.data.clip_end=100
    camera.matrix_world=ship_render.camera_matrix((-1,0,0),(0,0,1),(0,-2.4,2.5),30)
    camera.data.ortho_scale=5.1
    scene.render.resolution_x=1400;scene.render.resolution_y=850
    scene.render.filepath=str(root/'refined/studio'/('deployment-12deg.png' if deployed else 'hinge-closeup.png'))
    bpy.ops.render.render(write_still=True)
pose_review(False)
pose_review(True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print(json.dumps({'sha256':sha,'motion':motion,'findings':[len(old['findings']),len(new['findings'])]}))
