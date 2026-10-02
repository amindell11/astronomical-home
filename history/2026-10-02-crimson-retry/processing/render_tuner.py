import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('D:/amind/git/agent-2/results/valis-crimson-retry/v02')
bpy.ops.wm.open_mainfile(filepath=str(out/'Valis-Crimson-study.blend'))
scene=bpy.context.scene;cam=scene.camera;proj=json.loads((out/'projection.json').read_text());center=Vector(proj['center'])
materials=sorted({s.material.name for o in scene.objects if o.type=='MESH' for s in o.material_slots});(out/'material-ids.json').write_text(json.dumps(materials))
scene.render.resolution_x=1024;scene.render.resolution_y=1024
for view in ['top','quarter']:
 if view=='top':cam.location=center+Vector((0,0,30));cam.rotation_euler=(0,0,0);cam.data.ortho_scale=proj['span']
 else:cam.location=center+Vector((9,-12,15));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=proj['span']*1.06
 for mode in ['mask','id']:
  for m in bpy.data.materials:
   if m.name not in materials:continue
   n=m.node_tree.nodes;l=m.node_tree.links;n.clear();e=n.new('ShaderNodeEmission');end=n.new('ShaderNodeOutputMaterial');l.new(e.outputs[0],end.inputs['Surface'])
   if mode=='id':e.inputs['Color'].default_value=((materials.index(m.name)+1)/8,0,0,1)
   else:
    combine=n.new('ShaderNodeCombineColor');l.new(combine.outputs[0],e.inputs['Color'])
    for i,name in enumerate(['Shadow','Light','Ink']):
     t=n.new('ShaderNodeTexImage');t.image=bpy.data.images.get('Valis '+name+' — editable');l.new(t.outputs['Color'],combine.inputs[i])
  scene.render.filepath=str(out/f'tuner-{view}-{mode}.png');bpy.ops.render.render(write_still=True)
print('MASK_PASSES_COMPLETE')

