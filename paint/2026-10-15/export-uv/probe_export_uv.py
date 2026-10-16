import bpy,json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='D:/amind/git/agent-2/results/vanguard-redo/export-preflight/Vanguard.fbx')
order={o.name:[u.name for u in o.data.uv_layers] for o in bpy.context.scene.objects if o.type=='MESH'}
print(json.dumps(order))
assert all(layers and layers[0]=='PaintUV' for layers in order.values()), 'The drawn shader samples TEXCOORD0, so PaintUV must be first: '+repr(order)
