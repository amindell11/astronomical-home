"""Brighten the loaded nebula-26 glass etchings; write console.blend and its manifest, refusing overwrite."""
import bpy,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent;out=root/'console.blend'
if out.exists():raise RuntimeError('Output already exists')
source=Path(bpy.data.filepath);s=bpy.context.scene;s.name='27 - Brighter window edges'
node=bpy.data.materials['25 | Etched glass perimeter'].node_tree.nodes['Etching visibility']
old=node.inputs[1].default_value;node.inputs[1].default_value=.12
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/'console-manifest.json').write_text(json.dumps({'input':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'etchingVisibilityBefore':old,'etchingVisibilityAfter':.12,'otherMaterialsLightingGeometryAndCamera':'unchanged','inspection':'Unchanged angled view reused from nebula-26; that view hides the glass.'},indent=2))
