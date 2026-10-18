import bpy,json,sys,numpy as np,hashlib
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-4/art/tools/ship')
import ship_source
out=[]
for path in [r'D:/amind/git/astronomical-home/art/ships/vanguard/drawn-study/VanguardPainted.blend',r'D:/amind/git/agent-4/art/ships/vanguard/Vanguard.blend']:
 bpy.ops.wm.open_mainfile(filepath=path)
 scene=bpy.data.scenes['Vanguard - Texture MVP'] if 'VanguardPainted' in path else bpy.context.scene
 bpy.context.window.scene=scene
 records=[]
 for obj in scene.objects:
  if obj.type!='MESH' or obj.hide_render:continue
  mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
  vertices=np.array([obj.matrix_world@v.co for v in mesh.vertices],np.float32)
  records.append({'name':obj.name,'vertices':len(vertices),'world_sha':hashlib.sha256(vertices.tobytes()).hexdigest(),'materials':[m.name if m else None for m in mesh.materials],'mods':[(m.name,m.type) for m in obj.modifiers]})
 out.append({'path':path,'records':records})
Path('D:/amind/git/agent-4/results/vanguard-redo/authored-regions/preservation-geometry.json').write_text(json.dumps(out,indent=2))
print(json.dumps(out))
