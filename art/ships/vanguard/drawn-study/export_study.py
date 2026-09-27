import bpy, json, hashlib, sys
from pathlib import Path

root = Path(__file__).resolve().parents[4]
out = Path(__file__).resolve().parent
source = Path(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else out/'VanguardStudy.blend'
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.data.scenes['Vanguard - Texture MVP']
bpy.context.window.scene = scene
meshes = [o for o in scene.objects if o.type == 'MESH']
images = []
for im in bpy.data.images:
    if im.source != 'FILE': continue
    path = Path(bpy.path.abspath(im.filepath))
    images.append({'name': im.name, 'path': str(path), 'packed': bool(im.packed_file), 'exists': path.is_file()})
    if not im.packed_file and path.is_file(): im.pack()
paint = bpy.data.images['Vanguard MVP - Base Color']
assets = root/'src/Asteroids3D/Assets/Visuals/Ships/Vanguard/DrawnStudy'
assets.mkdir(parents=True, exist_ok=True)
paint.filepath_raw = str(assets/'VanguardBaseColor.png')
paint.file_format = 'PNG'
paint.save()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'VanguardStudy.blend'))
report = {'source': str(source), 'source_sha256': source_hash,
          'scene': scene.name, 'images': images, 'objects': []}
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:
    report['objects'].append({'name':obj.name, 'materials':[m.name for m in obj.data.materials],
        'uv_layers':[u.name for u in obj.data.uv_layers]})
    if 'MVP_Atlas' in obj.data.uv_layers:
        for uv in list(obj.data.uv_layers):
            if uv.name != 'MVP_Atlas': obj.data.uv_layers.remove(uv)
        obj.data.uv_layers.active_index = 0
        obj.data.uv_layers[0].active_render = True
    obj.select_set(True)
    mesh = obj.data
    mesh.calc_loop_triangles()
    triangles = list(mesh.loop_triangles)
    normals = [mesh.corner_normals[loop].vector[:] for tri in triangles for loop in tri.loops]
    baked = bpy.data.meshes.new(mesh.name + ' render triangles')
    baked.from_pydata([v.co[:] for v in mesh.vertices], [], [tuple(t.vertices) for t in triangles])
    for material in mesh.materials: baked.materials.append(material)
    for layer in mesh.uv_layers:
        uv = baked.uv_layers.new(name=layer.name)
        for target, source_loop in zip(uv.data, [i for t in triangles for i in t.loops]):
            target.uv = layer.data[source_loop].uv
    for face, tri in zip(baked.polygons, triangles):
        face.material_index = mesh.polygons[tri.polygon_index].material_index
        face.use_smooth = mesh.polygons[tri.polygon_index].use_smooth
    baked.normals_split_custom_set(normals)
    obj.data = baked
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.export_scene.fbx(filepath=str(assets/'VanguardStudy.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
    bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
sys.path.insert(0, str(out))
from build_structure import build
drawing = build(scene, out)
drawing.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'VanguardStructure.blend'))
bpy.ops.export_scene.fbx(filepath=str(assets/'VanguardStructure.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
    bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
(out/'export-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'baseline':str(assets/'VanguardStudy.fbx'),'drawn':str(assets/'VanguardStructure.fbx')}))
