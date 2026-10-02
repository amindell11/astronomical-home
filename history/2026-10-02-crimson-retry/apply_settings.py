import argparse,json,sys
from pathlib import Path
import bpy
p=argparse.ArgumentParser(description='Apply paint controls to an existing artist-edited blend, saving a new file without regenerating textures.')
p.add_argument('--source',required=True,type=Path);p.add_argument('--settings',required=True,type=Path);p.add_argument('--output',required=True,type=Path)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
if a.output.exists():p.error('Output already exists; choose a new revision filename.')
if not a.source.is_file() or not a.settings.is_file():p.error('Source blend and settings file must exist.')
s=json.loads(a.settings.read_text(encoding='utf-8-sig'))
for key in ['shadow_strength','light_strength','ink_strength']:
 if not isinstance(s.get(key),(int,float)) or not 0<=s[key]<=2:p.error(key+' must be a number between 0 and 2.')
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
palettes=json.loads(bpy.context.scene['palettes_json'])['palettes']
palette=s.get('palette','jade-iris')
if palette not in palettes:p.error('Unknown palette: '+str(palette))
for m in bpy.data.materials:
 if m.name not in palettes[palette]:continue
 color=palettes[palette][m.name];m.diffuse_color=color
 m.node_tree.nodes['BASE COAT — recolor here'].outputs[0].default_value=color
 group=m.node_tree.nodes['PAINT — independent layers']
 for name in ['Shadow','Light','Ink']:group.inputs[name+' strength'].default_value=s[name.lower()+'_strength']
g=bpy.data.node_groups['Valis painted finish']
for key,node in [('light_color_linear','Painted lights'),('ink_color_linear','Panel ink')]:
 if key in s:
  if not isinstance(s[key],list) or len(s[key])!=4 or any(not isinstance(v,(int,float)) or not 0<=v<=1 for v in s[key]):p.error(key+' must contain four color components between 0 and 1.')
  g.nodes[node].inputs[2].default_value=s[key]
a.output.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(a.output.resolve()))
print('SAVED_NEW_REVISION '+str(a.output.resolve()))
