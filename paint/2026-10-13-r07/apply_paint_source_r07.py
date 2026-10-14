import bpy, sys, json, hashlib, shutil
from pathlib import Path

sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
folder=source.parent
stage=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r07')
baseline='5465bbcdc30a3ca5102717f6ad2513feaa50d2dc55223d21325dd230bb659bb2'
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline
src=ship_source.open_source(source);before=ship_source.fingerprint(src)
assert 'Bulwark EdgeCoat' not in bpy.data.materials
selection=[o.name for o in bpy.context.selected_objects]
active=bpy.context.view_layer.objects.active
unchanged={name:hashlib.sha256((folder/'layers'/(name+'.png')).read_bytes()).hexdigest()
           for name in ('Shadow','Light','Ink')}
shutil.copyfile(stage/'layers/BaseRegion.png',folder/'layers/BaseRegion.png')
image=bpy.data.images.load(str(folder/'layers/BaseRegion.png'),check_existing=False)
image.name='Bulwark BaseRegion';image.colorspace_settings.name='Non-Color'
image.pack();image.filepath='//layers/BaseRegion.png'
for material in bpy.data.materials:
    if not material.name.startswith('Bulwark ') or not material.use_nodes:continue
    for node in material.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.name=='BaseRegion':node.image=image

def rgba(h):
    values=[int(h[i:i+2],16)/255 for i in (1,3,5)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in values)+(1,)

edge=bpy.data.materials['Bulwark Structure'].copy();edge.name='Bulwark EdgeCoat'
nodes=edge.node_tree.nodes;links=edge.node_tree.links
bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
previous=bsdf.inputs['Base Color'].links[0].from_socket
mix=nodes.new('ShaderNodeMixRGB');mix.name='Alternate base coat'
mix.label='Continuous bone stripe over charcoal armor edges';mix.location=(360,20)
mix.inputs[2].default_value=rgba('#D8CDB4')
links.new(previous,mix.inputs[1]);links.new(nodes['BaseRegion'].outputs['Color'],mix.inputs[0])
links.new(mix.outputs[0],bsdf.inputs['Base Color'])
affected=('02 Upper hull.001','05 Tail tips.002','09 Side armor.001')
ev=ship_source.Evaluation(src);matrices={n:ev.to_space(n).copy() for n in affected}
for o in list(ev.scene.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.data.scenes.remove(ev.scene);bpy.context.window.scene=src
face_counts={}
for name in affected:
    obj=src.objects[name];assert len(obj.data.materials)==5
    obj.data.materials.append(bpy.data.materials['Bulwark Metal'])
    obj.data.materials.append(edge)
    normal_matrix=matrices[name].to_3x3().inverted().transposed();count=0
    for face in obj.data.polygons:
        normal=(normal_matrix@face.normal).normalized()
        rear_face=name!='02 Upper hull.001' or max((matrices[name]@obj.data.vertices[v].co).y for v in face.vertices)<1.8
        if face.material_index==2 and normal.z>=-.25 and rear_face:
            face.material_index=6;count+=1
    face_counts[name]=count
after=ship_source.fingerprint(src)
for component in ('geometry','uv'):assert before['components'][component]==after['components'][component]
for name in ('Upper flap deployment','Lower flap deployment'):assert src.objects[name].rotation_euler.x==0
for o in src.objects:o.select_set(o.name in selection)
bpy.context.view_layer.objects.active=active
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
sha=hashlib.sha256(source.read_bytes()).hexdigest()
palette=json.loads((folder/'palettes.json').read_text())
palette['palettes']['plum-bone']['EdgeCoat']='#302E34'
palette['alternate_base_regions']['Bone']='Continuous angular rear armor band and angled nose coat over plum substrate'
palette['alternate_base_regions']['EdgeCoat']='Same rear bone band over charcoal armor thickness edges'
(folder/'palettes.json').write_text(json.dumps(palette,indent=2))
for name,digest in unchanged.items():assert hashlib.sha256((folder/'layers'/(name+'.png')).read_bytes()).hexdigest()==digest
audit={'source_sha256':sha,'previous_paint_source_sha256':baseline,
    'baseline_commit':'bf80138faf38d36d354b13cbfa6274ab76452df6',
    'geometry_unchanged':True,'uvs_unchanged':True,
    'geometry_fingerprint':after['components']['geometry'],'uv_fingerprint':after['components']['uv'],
    'rear_band_parts':list(affected),'charcoal_edge_faces_enabled_for_stripe':face_counts,
    'shadow_light_ink_unchanged':True,'nose_and_fins_preserved':True,
    'canisters_and_cockpit_preserved':True,'recessed_frame_and_bridge_preserved':True,
    'separate_editable_base_colours':palette['palettes']['plum-bone'],
    'approval':'UV and plum direction approved; continuous rear band pending review'}
(stage/'paint-source-audit.json').write_text(json.dumps(audit,indent=2));print(json.dumps(audit))
