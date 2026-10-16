import bpy,json,sys
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source
scene=bpy.context.scene
before=ship_source.fingerprint(scene)
assert before['components']['geometry']=='9361716870ed5ffc3f74db05c65b22ff161c1de5aecc095612bb7cf4823dac8c', 'Source is not the approved pre-correction geometry'
lifts={'Pod cooling hatch':.004,'Cockpit cooling vent':.0004}
for name,lift in lifts.items():
    for vertex in scene.objects[name].data.vertices:
        if vertex.index>=16:vertex.co.z+=lift
bpy.context.view_layer.update()
after=ship_source.fingerprint(scene)
changed=sorted(name for name in before['parts'] if before['parts'][name]['geometry']!=after['parts'][name]['geometry'])
assert changed==sorted(lifts),changed
assert before['components']['uv']==after['components']['uv'],'Correction changed stored UVs'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
Path(__file__).with_name('applied-fit-correction.json').write_text(json.dumps({'owner_approved':True,'changed_parts':changed,'lifts':lifts,'other_geometry_unchanged':True,'uv_coordinates_unchanged':True},indent=2)+'\n')
