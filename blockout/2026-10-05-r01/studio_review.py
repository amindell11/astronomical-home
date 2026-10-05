from pathlib import Path
import sys,hashlib,json
import bpy,numpy as np
pool=Path('D:/amind/git/agent-2')
sys.path.insert(0,str(pool/'art/tools/ship'))
import ship_render,ship_source
source=pool/'art/ships/nightshade/Nightshade.blend'
out=pool/'results/nightshade-blockout/blockout/2026-10-05-r01/studio'
out.mkdir(parents=True,exist_ok=True)
digest=hashlib.sha256(source.read_bytes()).hexdigest()
evaluation,bounds=ship_render.prepare(source)
scene=evaluation.scene
ship_source.apply_review_preset(scene,1000)
scene.display.shading.light='STUDIO'
scene.display.shading.studio_light='paint.sl'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.cavity_type='BOTH'
camera=bpy.data.objects.new('Studio review camera',bpy.data.cameras.new('Studio review camera'))
camera.data.type='ORTHO'
scene.collection.objects.link(camera)
scene.camera=camera
lo,hi=bounds
center=(lo+hi)/2
radius=float(np.linalg.norm(hi-lo))/2
corners=np.array([[x,y,z] for x in (lo[0],hi[0]) for y in (lo[1],hi[1]) for z in (lo[2],hi[2])])
images={}
for name,direction,up in [('top',(0,0,1),(0,1,0)),('side',(-1,0,0),(0,0,1)),('quarter',(1,1,1),(0,0,1))]:
    matrix=ship_render.camera_matrix(direction,up,center,radius*3)
    camera.matrix_world=evaluation.space.inverted()@matrix
    extent=(corners-center)@np.array(matrix.to_3x3(),np.float64)
    camera.data.ortho_scale=float(max(np.ptp(extent[:,0]),np.ptp(extent[:,1])))*1.12
    scene.render.filepath=str(out/f'{name}.png')
    bpy.ops.render.render(write_still=True)
    images[name]=scene.render.filepath
if hashlib.sha256(source.read_bytes()).hexdigest()!=digest:
    raise RuntimeError('Review changed source.')
(out/'capture.json').write_text(json.dumps(dict(source=str(source),source_sha256=digest,blender_version=bpy.app.version_string,views=images,note='Read-only studio-lit review of blockout; no source save.'),indent=2),encoding='utf-8')
