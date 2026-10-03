import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('D:/amind/git/agent-4/art/ships/crimson')
WORK=Path('D:/amind/git/agent-4/results/crimson-uv')
bpy.ops.wm.open_mainfile(filepath=str(BASE/'Crimson.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
dg=bpy.context.evaluated_depsgraph_get()
def evaluated_uvs(o):
    ev=o.evaluated_get(dg); m=ev.to_mesh()
    result=[list(d.uv) for d in m.uv_layers.active.data]; ev.to_mesh_clear(); return result
old_uv={o.name:evaluated_uvs(o) for o in objects}
def geometry(o):
    return {'matrix':[list(r) for r in o.matrix_world], 'vertices':[list(v.co) for v in o.data.vertices], 'faces':[list(p.vertices) for p in o.data.polygons], 'mods':[(m.name,m.type) for m in o.modifiers]}
original={o.name:geometry(o) for o in objects}
bpy.ops.object.select_all(action='DESELECT')
copies=[]
for o in objects:
    mesh=o.data.copy(); mesh.transform(o.matrix_world)
    copy=bpy.data.objects.new('UV COPY '+o.name,mesh); bpy.context.scene.collection.objects.link(copy)
    copy.select_set(True); copies.append(copy)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=False,margin_method='FRACTION',margin=.008)
bpy.ops.object.mode_set(mode='OBJECT')
for o,copy in zip(objects,copies):
    assert len(o.data.loops)==len(copy.data.loops)
    for dst,src in zip(o.data.uv_layers.active.data,copy.data.uv_layers.active.data):dst.uv=src.uv
    mesh=copy.data; bpy.data.objects.remove(copy,do_unlink=True);bpy.data.meshes.remove(mesh)
bpy.context.view_layer.update()
correspondence=[]
for o in objects:
    new=evaluated_uvs(o);assert len(new)==len(old_uv[o.name])
    correspondence.extend(zip(old_uv[o.name],new))
(WORK/'uv-correspondence.json').write_text(json.dumps(correspondence))
brush=bpy.data.images.load(str(BASE/'textures/painted-brush-source.png'));brush.colorspace_settings.name='Non-Color'
recipe=Path('C:/Users/amind/.codex/artifact-archives/crimson-texturing-20260928-010254/texture_pass.py').read_text()
exec(recipe[recipe.index('PALETTE='):recipe.index('materials=[create_material')])
final_material=objects[0].data.materials[0]
materials=[create_material(o) for o in objects]
image=bpy.data.images.get('Crimson painted base color')
for mat in materials:
    n=mat.node_tree.nodes.new('ShaderNodeTexImage');n.image=image;mat.node_tree.nodes.active=n
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.cycles.use_denoising=False
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in objects:
    mesh=bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg);mesh.transform(o.matrix_world)
    copy=bpy.data.objects.new('BAKE COPY '+o.name,mesh);scene.collection.objects.link(copy);copy.select_set(True);copies.append(copy)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();baker=bpy.context.object
print('BAKE_START',flush=True)
bpy.ops.object.bake(type='EMIT',use_clear=True,margin=12)
bpy.data.objects.remove(baker,do_unlink=True)
image.filepath_raw=str(BASE/'textures/Crimson_BaseColor.png');image.file_format='PNG';image.save();image.pack();image.filepath='//textures/Crimson_BaseColor.png'
for p in image.packed_files:p.filepath=image.filepath
for o in objects:
    o.data.materials.clear();o.data.materials.append(final_material)
    assert geometry(o)==original[o.name],o.name
for mat in materials:bpy.data.materials.remove(mat)
bpy.data.images.remove(brush)
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
scene.render.engine='BLENDER_WORKBENCH'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Crimson.blend'))
print('BAKE_SAVED geometry preserved',flush=True)
