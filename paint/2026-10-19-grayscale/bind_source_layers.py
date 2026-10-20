import bpy, json, sys
from pathlib import Path
sys.path.insert(0, 'D:/amind/git/agent-5/art/tools/ship')
import ship_source
from ship_paint import Graph
root=Path('D:/amind/git/agent-5')
folder=root/'art/ships/crimson'
bpy.ops.wm.open_mainfile(filepath=str(folder/'Crimson.blend'),load_ui=False)
before=ship_source.fingerprint(bpy.context.scene)['components']
palette=json.loads((folder/'palettes.json').read_text())['palettes']['crimson']
settings=json.loads((folder/'paint.json').read_text())['material']
group=bpy.data.node_groups.new('Crimson paint layers','ShaderNodeTree')
group.interface.new_socket(name='Color',in_out='OUTPUT',socket_type='NodeSocketColor')
class Host:
    use_nodes=True
    node_tree=group
g=Graph(Host())
uv=g.node('ShaderNodeUVMap',uv_map='PaintUV')
def texture(name):
    image=bpy.data.images.load(str(folder/'layers'/f'{name}.png'),check_existing=True)
    image.colorspace_settings.name='Non-Color'
    image.filepath=f'//layers/{name}.png'
    image.pack()
    node=g.node('ShaderNodeTexImage',image=image,name=name,label=name,extension='EXTEND')
    g.feed(node.inputs['Vector'],uv.outputs['UV'])
    return node.outputs['Color']
base=None
weight_sum=None
for name,color in sorted(palette.items()):
    weight=texture('Region_'+name)
    weight_sum=weight if weight_sum is None else g.math('ADD',weight_sum,weight)
    pigment=g.node('ShaderNodeRGB',name='Palette: '+name,label=name)
    pigment.outputs[0].default_value=tuple(color)
    term=g.vector('SCALE',pigment.outputs[0],scale=weight)
    base=term if base is None else g.vector('ADD',base,term)
base=g.vector('SCALE',base,scale=g.math('DIVIDE',1.,g.math('MAXIMUM',weight_sum,.0001)))
light_color=g.mix_colour(settings['light_lift'],base,(1.,1.,1.))
shadow=g.math('MULTIPLY',texture('Shadow'),settings['shadow_strength'])
light=g.math('MINIMUM',g.math('MULTIPLY',texture('Light'),settings['light_strength']),1.)
ink=g.math('MINIMUM',g.math('MULTIPLY',texture('Ink'),settings['ink_strength']),1.)
color=g.vector('SCALE',base,scale=g.math('SUBTRACT',1.,shadow))
color=g.mix_colour(light,color,light_color)
color=g.mix_colour(ink,color,settings['ink_color'])
out=g.node('NodeGroupOutput')
g.feed(out.inputs['Color'],color)
for i,node in enumerate(group.nodes):
    node.location=((i%6)*230,-(i//6)*180)
materials={m for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='Canopy' for m in o.data.materials}
for material in materials:
    graph=Graph(material)
    paint=graph.node('ShaderNodeGroup',node_tree=group,name='Crimson paint layers')
    paint.location=(-240,0)
    emission=graph.node('ShaderNodeEmission')
    graph.feed(emission.inputs['Color'],paint.outputs['Color'])
    output=graph.node('ShaderNodeOutputMaterial')
    output.location=(220,0)
    graph.feed(output.inputs['Surface'],emission.outputs[0])
    image=graph.node('ShaderNodeTexImage',image=bpy.data.images.get('Shadow.png'),name='Paintable Shadow mask')
    image.location=(-240,-200)
    material.node_tree.nodes.active=image
bpy.context.scene.display.shading.color_type='MATERIAL'
after=ship_source.fingerprint(bpy.context.scene)['components']
assert before['geometry']==after['geometry'] and before['uv']==after['uv']
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_mainfile()
(root/'scratch/crimson-paint/source-authoring.json').write_text(json.dumps({'before':before,'after':after,'packed_layers':sorted(i.name for i in bpy.data.images if i.packed_file and i.filepath.startswith('//layers/'))},indent=2))
print('AUTHORING_BOUND',after)
