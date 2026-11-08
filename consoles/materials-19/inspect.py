import bpy,json
from pathlib import Path
s=bpy.context.scene;out={'materials':{},'images':{},'objects':{}}
for m in bpy.data.materials:
 if m.users:out['materials'][m.name]={'users':m.users,'nodes':[(n.name,n.type)for n in m.node_tree.nodes]if m.use_nodes else []}
for i in bpy.data.images:out['images'][i.name]={'path':i.filepath,'packed':bool(i.packed_file),'size':list(i.size)}
for o in s.objects:
 if not o.hide_render:out['objects'][o.name]={'type':o.type,'mats':[m.name if m else '' for m in o.data.materials]if hasattr(o.data,'materials')else [],'location':list(o.matrix_world.translation),'bounds':[list(o.matrix_world@__import__('mathutils').Vector(x))for x in o.bound_box]}
Path(__file__).with_name('inventory.json').write_text(json.dumps(out,indent=2))
