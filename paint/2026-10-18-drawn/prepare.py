import bpy,json,sys
from pathlib import Path
stage=Path(__file__).parent
sys.path.insert(0,'D:/amind/git/astronomical-home/art/tools/ship')
import ship_source,ship_contract
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
bpy.ops.wm.open_mainfile(filepath=str(source))
roles=ship_source.Roles(bpy.context.scene)
names=sorted({n for role in ship_contract.VISUAL_ROLES for n in roles.members.get(role,[]) if bpy.data.objects[n].type=='MESH'})
mats={m.name:m for name in names for m in bpy.data.objects[name].data.materials}
colors={};details={}
for name,m in mats.items():
 nodes=m.node_tree.nodes
 base=nodes.get('Alternate base coat') or nodes.get('Base coat')
 rgb=next((n for n in nodes if n.type=='RGB'),None)
 bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
 color=list(base.inputs[1].default_value if base else rgb.outputs[0].default_value if rgb else bs.inputs['Base Color'].default_value)
 colors[name]=color
 details[name]={'base':color,'alternate':list(nodes['Alternate base coat'].inputs[2].default_value) if nodes.get('Alternate base coat') else None}
(stage/'palettes.json').write_text(json.dumps({'color_space':'linear-sRGB','default':'plum-bone','palettes':{'plum-bone':colors}},indent=2))
(stage/'source-map.json').write_text(json.dumps({'roles_errors':roles.errors,'parts':names,'materials':details},indent=2))
