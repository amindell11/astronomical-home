"""Audit deck.blend and render six hardware silhouettes without saving the scene.

Run Blender -b --python verify_deck.py [-- --massing] from any directory.
All evidence is written into previews. --massing renders only the D13 reference mask.
"""
from pathlib import Path
import json
import sys
import math
import bpy
import bmesh
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import VIEWS, game_projection
OUT = ROOT / 'previews'
OUT.mkdir(exist_ok=True)
MASSING = '--massing' in sys.argv
bpy.ops.wm.open_mainfile(filepath=str(ROOT / ('massing/D13.blend' if MASSING else 'deck.blend')))
scene = bpy.context.scene
report = {'objects': [], 'parts': [], 'controls': [], 'screens': [], 'total_triangles': 0}
excluded = ('07 reuse | Vanguard', '19 | Vanguard', '19 | Distant space',
            '11 | Neutral projector frustum', '25 | Viewport glass')
hardware = []
for obj in scene.objects:
    is_hardware = obj.type == 'MESH' and not obj.hide_render and not obj.name.startswith(excluded)
    if MASSING:
        is_hardware = is_hardware and not any('Hologram' in c.name or 'Clearance' in c.name for c in obj.users_collection)
        is_hardware = is_hardware and not obj.name.startswith('Window |')
    if is_hardware:
        hardware.append(obj)
    elif obj.type in {'MESH', 'CURVE', 'FONT'}:
        obj.hide_render = True

for obj in hardware:
    mesh = obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    item = {'name': obj.name, 'vertices':len(mesh.vertices), 'triangles':len(mesh.loop_triangles),
            'closed_manifold': all(e.is_manifold for e in bm.edges),
            'boundary_edges':sum(e.is_boundary for e in bm.edges),
            'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),
            'loose_vertices':sum(not v.link_faces for v in bm.verts),
            'consistent_winding': all(e.is_contiguous for e in bm.edges if e.is_manifold),
            'degenerate_faces':sum(f.calc_area() == 0 for f in bm.faces),
            'tiny_faces_below_1e10':sum(0 < f.calc_area() < 1e-10 for f in bm.faces),
            'minimum_face_area':min((f.calc_area() for f in bm.faces),default=0),
            'signed_volume':bm.calc_volume(signed=True), 'modifiers':[m.type for m in obj.modifiers],
            'role':obj.get('export_role','ConsoleBody')}
    bm.free()
    report['objects'].append(item)
    report['total_triangles'] += item['triangles']
    centre = obj.matrix_world @ (sum((Vector(c) for c in obj.bound_box),Vector()) / 8)
    p = world_to_camera_view(scene,scene.camera,centre)
    report['parts'].append({'name':obj.name,'role':item['role'],'pixel':[p.x*1920,(1-p.y)*1080]})
    if item['role'].endswith('Button') or item['role'] == 'SelectorKnob':
        caps = [0.,0.,0.]
        for tri in mesh.loop_triangles:
            a,b,c = (mesh.vertices[i].co for i in tri.vertices)
            cross = (b-a).cross(c-a)
            if cross.length < 1e-12:
                continue
            normal = cross.normalized()
            for axis in range(3):
                if abs(normal[axis]) > .99:
                    caps[axis] += cross.length
        report['controls'].append({'name':obj.name,'role':item['role'],'cap_area_twice_xyz':caps,
            'passes_authored_up_after_export':caps[2] > caps[0] and caps[2] > caps[1],
            'position':list(obj.location),'local_frame_columns':[list(obj.matrix_world.to_3x3().col[i]) for i in range(3)]})
    if item['role'].endswith('ScreenGlass'):
        coordinates = [list(v.co) for v in mesh.vertices]
        report['screens'].append({'name':obj.name,'role':item['role'],'vertices':coordinates,
            'position':list(obj.location),'local_frame_columns':[list(obj.matrix_world.to_3x3().col[i]) for i in range(3)],
            'polygons':len(mesh.polygons),'uv_layers':[uv.name for uv in mesh.uv_layers],
            'local_normals':[list(p.normal) for p in mesh.polygons]})

report['camera'] = {'position':list(scene.camera.location),'lens':scene.camera.data.lens,
    'sensor_width':scene.camera.data.sensor_width,'sensor_height':scene.camera.data.sensor_height,
    'sensor_fit':scene.camera.data.sensor_fit,'forward':list(-scene.camera.matrix_world.to_3x3().col[2])}
report['lamp_names'] = [o.name for o in hardware if o.name.endswith(' lens') and o.name != '09 | Projector lens']
if not MASSING:
    report['preview_objects'] = []
    deps = bpy.context.evaluated_depsgraph_get()
    for obj in scene.objects:
        if obj.type not in {'MESH','CURVE','FONT'} or obj in hardware:
            continue
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        bm=bmesh.new()
        bm.from_mesh(mesh)
        report['preview_objects'].append({'name':obj.name,'triangles':len(mesh.loop_triangles),
            'closed_manifold':all(e.is_manifold for e in bm.edges)})
        bm.free()
        evaluated.to_mesh_clear()
    report['scene_total_triangles'] = report['total_triangles'] + sum(o['triangles'] for o in report['preview_objects'])
    report['positive_volume_intersections'] = []
    report['pressed_intersections'] = []
    report['press_travel'] = []
    report['intersection_pairs_checked'] = 0
    solids = [o for o in hardware if not o.get('export_role','').endswith('ScreenGlass')]
    boxes = {}
    for obj in solids:
        corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        boxes[obj] = ([min(p[k] for p in corners) for k in range(3)],
                      [max(p[k] for p in corners) for k in range(3)])
    for i,a in enumerate(solids):
        for b in solids[i+1:]:
            amin,amax=boxes[a]
            bmin,bmax=boxes[b]
            if any(min(amax[k],bmax[k])-max(amin[k],bmin[k]) <= 1e-7 for k in range(3)):
                continue
            duplicate = a.copy()
            duplicate.data = a.data.copy()
            scene.collection.objects.link(duplicate)
            duplicate.hide_render = True
            bpy.context.view_layer.objects.active = duplicate
            mod = duplicate.modifiers.new('Audit volume intersection','BOOLEAN')
            mod.operation='INTERSECT'
            mod.solver='EXACT'
            mod.object=b
            bpy.ops.object.modifier_apply(modifier=mod.name)
            bm=bmesh.new()
            bm.from_mesh(duplicate.data)
            volume=abs(bm.calc_volume(signed=True))
            bm.free()
            report['intersection_pairs_checked'] += 1
            if volume > 1e-7:
                report['positive_volume_intersections'].append({'a':a.name,'b':b.name,'volume':volume})
            data=duplicate.data
            bpy.data.objects.remove(duplicate,do_unlink=True)
            bpy.data.meshes.remove(data)
    body=next(o for o in hardware if o.name=='Deck | joined closed structure')
    for obj in solids:
        role=obj.get('export_role','')
        if not (role.endswith('Button') or role=='SelectorKnob'):
            continue
        share=.2 if role=='SelectorKnob' else (.35 if role=='LaunchButton' else .28)
        travel=(max(v.co.z for v in obj.data.vertices)-min(v.co.z for v in obj.data.vertices))*share
        report['press_travel'].append({'role':role,'travel':travel,'share':share})
        carried=[obj]+[o for o in solids if o.get('export_role','')==role.replace('Button','Legend') and o!=obj]
        for fraction in (.5,1.):
            for source in carried:
                duplicate=source.copy()
                duplicate.data=source.data.copy()
                scene.collection.objects.link(duplicate)
                duplicate.hide_render=True
                duplicate.location-=obj.matrix_world.to_3x3().col[2]*travel*fraction
                bpy.context.view_layer.objects.active=duplicate
                mod=duplicate.modifiers.new('Audit pressed clearance','BOOLEAN')
                mod.operation='INTERSECT'
                mod.solver='EXACT'
                mod.object=body
                bpy.ops.object.modifier_apply(modifier=mod.name)
                bm=bmesh.new()
                bm.from_mesh(duplicate.data)
                volume=abs(bm.calc_volume(signed=True))
                bm.free()
                if volume>1e-7:
                    report['pressed_intersections'].append({'role':role,'object':source.name,
                        'fraction':fraction,'volume':volume})
                data=duplicate.data
                bpy.data.objects.remove(duplicate,do_unlink=True)
                bpy.data.meshes.remove(data)
    (OUT/'mesh-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')

white = bpy.data.materials.new('Silhouette evidence white')
white.use_nodes = True
nodes = white.node_tree.nodes
nodes.clear()
emission = nodes.new('ShaderNodeEmission')
emission.inputs[0].default_value=(1,1,1,1)
output = nodes.new('ShaderNodeOutputMaterial')
white.node_tree.links.new(emission.outputs[0],output.inputs[0])
bpy.context.view_layer.material_override = white
scene.render.engine = 'CYCLES'
scene.cycles.samples = 1
scene.cycles.use_denoising = False
scene.render.use_freestyle = False
scene.render.film_transparent = True
scene.render.resolution_x,scene.render.resolution_y = 1920,1080
scene.render.resolution_percentage = 100
scene.render.image_settings.color_mode = 'RGBA'
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(OUT/('massing-mask.png' if MASSING else 'deck-mask.png'))
bpy.ops.render.render(write_still=True)
if not MASSING:
    for suffix, width, height in VIEWS:
        if suffix == '16x9':
            continue
        game_projection(scene, scene.camera, width, height)
        scene.render.filepath = str(OUT / ('deck-mask-' + suffix + '.png'))
        bpy.ops.render.render(write_still=True)
    game_projection(scene, scene.camera, 1920, 1080)
    roles = ['ConsoleBody','MainScreenGlass','DetailScreenGlass','PrimaryScreenGlass','OrdnanceScreenGlass',
             'SelectorKnob','PreviousButton','PreviousLegend','NextButton','NextLegend',
             'LaunchButton','LaunchLegend','ProjectorLens','PrimaryPreviousButton','PrimaryPreviousLegend',
             'PrimaryNextButton','PrimaryNextLegend','OrdnancePreviousButton','OrdnancePreviousLegend',
             'OrdnanceNextButton','OrdnanceNextLegend']
    colors = ['596574','3aa9ff','2966d4','a17cfc','7347d1','60d4a1','ffb951','fff2bd','f29344','ffe0a6',
              'ff701c','fff7d7','38e9f2','ff647c','ffdbe2','d85378','ffbed5','71d66e','deffd9','3dba6a','c1ffde']
    materials = {}
    for role,color in zip(roles,colors):
        material = bpy.data.materials.new('Parts evidence '+role)
        material.use_nodes=True
        nodes=material.node_tree.nodes
        nodes.clear()
        emission=nodes.new('ShaderNodeEmission')
        emission.inputs[0].default_value=tuple((int(color[i:i+2],16)/255)**2.2 for i in (0,2,4))+(1,)
        output=nodes.new('ShaderNodeOutputMaterial')
        material.node_tree.links.new(emission.outputs[0],output.inputs[0])
        materials[role]=material
    bpy.context.view_layer.material_override=None
    for obj in hardware:
        obj.data.materials.clear()
        obj.data.materials.append(materials[obj.get('export_role','ConsoleBody')])
        for poly in obj.data.polygons:
            poly.material_index=0
    scene.view_settings.view_transform='Standard'
    scene.view_settings.look='None'
    scene.render.filepath=str(OUT/'deck-parts-raw.png')
    bpy.ops.render.render(write_still=True)
    (OUT/'part-colors.json').write_text(json.dumps(dict(zip(roles,colors)),indent=2))
print(json.dumps({'meshes':len(hardware),'triangles':report['total_triangles']}))
