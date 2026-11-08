import bpy,sys
from pathlib import Path
s=bpy.context.scene;s.cycles.samples=24;s.camera=bpy.data.objects['07 Camera - construction'];bpy.data.objects['11 | Neutral projector frustum'].hide_render=True
s.render.filepath=str(Path(bpy.data.filepath).with_name(Path(bpy.data.filepath).stem+'-angle.png'));bpy.ops.render.render(write_still=True)
