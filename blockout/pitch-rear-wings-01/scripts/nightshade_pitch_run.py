from pathlib import Path
import bpy,runpy,json
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-1/art/ships/nightshade/Nightshade.blend')
result=runpy.run_path(str(Path(__file__).with_name('nightshade_pitch_wings.py')))['result']
print(json.dumps(result))
