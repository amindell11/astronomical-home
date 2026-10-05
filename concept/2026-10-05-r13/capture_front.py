import bpy, json, hashlib
from pathlib import Path
from mathutils import Vector
root = Path('D:/amind/git/agent-2')
reference = root/'results/nightshade-concept/concept/2026-10-05-r01/references/native/Nightshade-reference.blend'
out = root/'results/nightshade-concept/concept/2026-10-05-r13/references'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(reference))
scene = bpy.context.scene
meshes = [o for o in scene.objects if o.type=='MESH']
points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
center = (lo+hi)/2
camera=scene.camera
camera.location=center+Vector((0,-1,0))*max(hi-lo)*3
camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'
camera.data.ortho_scale=(hi.x-lo.x)*1.15
scene.render.resolution_x=1200
scene.render.resolution_y=500
scene.render.resolution_percentage=100
scene.render.filepath=str(out/'mesh-front.png')
bpy.ops.render.render(write_still=True)
fbx=root/'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/cruiserUpdate1.fbx'
metadata=dict(source=str(fbx),source_sha256=hashlib.sha256(fbx.read_bytes()).hexdigest(),reference_blend=str(reference),reference_sha256=hashlib.sha256(reference.read_bytes()).hexdigest(),view='orthographic front, native proportions',camera_direction=[0,-1,0],note='Render only; no geometry or source file changes. Native FBX proportions; lighting differs from Unity.')
(out/'capture.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
