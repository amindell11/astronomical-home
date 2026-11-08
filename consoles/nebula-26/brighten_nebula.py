"""Raise only the visible nebula in loaded console-finish-25; write console.blend and a manifest, refusing overwrite."""
import bpy,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent;out=root/'console.blend'
if out.exists():raise RuntimeError('Output already exists')
source=Path(bpy.data.filepath);s=bpy.context.scene;s.name='26 - Brighter nebula'
m=bpy.data.materials['19 | Distant nebula'];n=m.node_tree.nodes;L=m.node_tree.links
emission=next(a for a in n if a.type=='EMISSION');old=emission.inputs['Strength'].default_value
path=n.new('ShaderNodeLightPath');path.name='Keep console illumination unchanged'
visible=n.new('ShaderNodeValue');visible.name=visible.label='Visible nebula brightness';visible.outputs[0].default_value=.45
difference=n.new('ShaderNodeMath');difference.operation='SUBTRACT';difference.inputs[1].default_value=old;L.new(visible.outputs[0],difference.inputs[0])
strength=n.new('ShaderNodeMath');strength.operation='MULTIPLY_ADD';strength.name='Camera brightness with original ambient contribution';strength.inputs[2].default_value=old;L.new(path.outputs['Is Camera Ray'],strength.inputs[0]);L.new(difference.outputs[0],strength.inputs[1]);L.new(strength.outputs[0],emission.inputs['Strength'])
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/'console-manifest.json').write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'visibleNebulaBefore':old,'visibleNebulaAfter':.45,'consoleIllumination':'unchanged; non-camera rays retain the previous nebula emission','geometryAndCamera':'unchanged','inspection':'Unchanged angled view reused from console-finish-25; that view hides the nebula.'},indent=2))
