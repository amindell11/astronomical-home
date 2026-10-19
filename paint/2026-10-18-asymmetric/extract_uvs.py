import bpy,json
from pathlib import Path
s=Path(__file__).parent;bpy.ops.wm.open_mainfile(filepath=str(s/'generation/Bulwark.blend'))
(s/'armor-uvs.json').write_text(json.dumps({n:[list(l.uv) for l in bpy.data.objects[n].data.uv_layers['PaintUV'].data] for n in ['Armor - Front plate','Armor - Side plates']}))
