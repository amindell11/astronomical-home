import bpy, json
from pathlib import Path
from mathutils import Vector
out=Path('D:/amind/git/agent-2/results/valis-crimson-retry/v02')
exec((out/'verify_native.py').read_text(encoding='utf-8').split("a=snapshot(out/")[0])
a=snapshot(out/'Valis-Crimson-study.blend')
b=snapshot(out/'Valis-asymmetric-tuned.blend')
assert a==b
s=json.loads((out/'selected-settings.json').read_text(encoding='utf-8-sig'))
checks={}
for m in bpy.data.materials:
 if not m.use_nodes: continue
 group=m.node_tree.nodes.get('PAINT — independent layers')
 if group is None: continue
 vals={name.lower()+'_strength':group.inputs[name+' strength'].default_value for name in ['Shadow','Light','Ink']}
 assert all(abs(vals[k]-s[k])<1e-6 for k in vals)
 checks[m.name]=vals
assert checks
b['selected_settings']=s
b['material_controls_verified']=len(checks)
b['geometry_and_packed_paint_unchanged']=True
(out/'selected-settings-validation.json').write_text(json.dumps(b,indent=2))
scene=bpy.context.scene
scene.render.filepath=str(out/'tuned-quarter.png')
bpy.ops.render.render(write_still=True)
proj=json.loads((out/'projection.json').read_text())
cam=scene.camera
cam.location=Vector(proj['center'])+Vector((0,0,30))
cam.rotation_euler=(0,0,0)
cam.data.ortho_scale=proj['span']
scene.render.filepath=str(out/'tuned-top.png')
bpy.ops.render.render(write_still=True)
print('SELECTED_SETTINGS_VERIFIED '+json.dumps(b))
