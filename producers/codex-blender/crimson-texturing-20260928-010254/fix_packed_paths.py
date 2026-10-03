import bpy,json
from pathlib import Path
base=Path('D:/amind/git/agent-7/art/ships/crimson')
bpy.ops.wm.open_mainfile(filepath=str(base/'Crimson.blend'))
for i in bpy.data.images:
 path=Path(bpy.path.abspath(i.filepath));assert path.is_file(),str(path)
 for packed in i.packed_files:packed.filepath=str(path)
 assert len(i.packed_files)>0
bpy.ops.wm.save_as_mainfile(filepath=str(base/'Crimson.blend'),relative_remap=False)
print(json.dumps({'packed_images':[(i.name,list(i.size)) for i in bpy.data.images],'geometry_parts':sum(o.type=='MESH' for o in bpy.context.scene.objects)}))
