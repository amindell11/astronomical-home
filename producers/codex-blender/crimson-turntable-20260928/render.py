import bpy
from pathlib import Path
work=Path('C:/Users/amind/.codex/artifact-archives/crimson-turntable-20260928')
bpy.ops.wm.open_mainfile(filepath=str(work/'Comparison.blend'))
bpy.context.scene.render.filepath=str(work/'frames'/'frame-')
bpy.ops.render.render(animation=True)
