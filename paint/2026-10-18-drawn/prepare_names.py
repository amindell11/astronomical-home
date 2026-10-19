import bpy,json
from pathlib import Path
s=Path(__file__).parent;g=s/'generation'
bpy.ops.wm.open_mainfile(filepath=str(g/'Bulwark.blend'))
renames={m.name:m.name.replace('.001',' Secondary') for m in bpy.data.materials if '.001' in m.name and m.name.startswith('Bulwark ')}
for old,new in renames.items():bpy.data.materials[old].name=new
(s/'material-renames.json').write_text(json.dumps(renames,indent=2))
for filename in ['paint.json','palettes.json']:
 text=(g/filename).read_text()
 for old,new in renames.items():text=text.replace('"'+old+'"','"'+new+'"')
 (g/filename).write_text(text)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
