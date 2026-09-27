"""Blender --background --python build_top_mesh.py; rebuilds this revision only.

Writes CrimsonTopMatch.blend, CrimsonTopMatch.fbx and mesh-manifest.json beside
this script; inspection renders go to results/crimson-top-match. Exit 0 means
all authored meshes passed manifold and face-area checks. Never reads or saves
the earlier CrimsonMesh.blend. Rebuilding replaces this revision's hand edits.
"""
import bpy
import bmesh
import hashlib
import json
import math
import numpy as np
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
OUTPUT = ROOT / 'results/crimson-top-match'
OUTPUT.mkdir(parents=True, exist_ok=True)
REFERENCE = HERE / 'reference/original-top.png'
SCALE = 440.0
AXIS = 460.0
PITCH = math.tan(math.radians(5.5))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = 'Crimson - original top constrained'
collections = {}
for name in ('01 Hull and cockpit', '02 Upper wings', '03 Lower wings',
             '04 Fins and spars', '05 Engines', '90 References', '99 Cameras'):
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    collections[name] = col
parts = []
reference_image = bpy.data.images.load(str(REFERENCE))
pixels = np.asarray(reference_image.pixels[:]).reshape(1024,1024,4)[::-1]
mask = pixels[:,:,3] > .5
interior = mask.copy()
for axis in (0,1):
    interior &= np.roll(mask,1,axis) & np.roll(mask,-1,axis)
edge_v, edge_u = np.where(mask & ~interior)
reference_edge = np.column_stack((edge_u,edge_v))


def fit_outline(outline):
    fitted = []
    for u, v in outline:
        candidates = []
        for mirrored in (False,True):
            target = np.array((u,2*AXIS-v if mirrored else v))
            distances = ((reference_edge-target)**2).sum(axis=1)
            i = distances.argmin()
            if distances[i] <= 18**2:
                x,y = reference_edge[i]
                candidates.append((float(x),float(2*AXIS-y if mirrored else y)))
        fitted.append(tuple(np.mean(candidates,axis=0)) if len(candidates)==2 else (u,v))
    return fitted


def point(u, v, height):
    y = (u - 550) / SCALE
    return ((v - AXIS) / SCALE, y, height - PITCH * y)


def hull_height(u, v):
    crest = np.interp(u, [120,250,360,480,530,590,700,840,1000],
                        [.04,.09,.19,.335,.34,.265,.17,.085,.016])
    lateral = abs(v-AXIS)/440
    return float(crest * max(.26, 1-.67*lateral))


def mesh(name, vertices, faces, collection, mirror=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collections[collection].objects.link(obj)
    if mirror:
        mod = obj.modifiers.new('Port and starboard symmetry', 'MIRROR')
        mod.use_clip = True
    parts.append(obj)
    return obj


def shell(name, outline, low, high, collection, mirror=True):
    if ('fin' in name.lower() or 'foundation' in name or 'rail' in name
            or name in ('Structural center web','Cockpit surround','Central hull',
                        'Engine support frame','Outer rail mounting block','Shoulder service pod')):
        outline = fit_outline(outline)
    n = len(outline)
    center_u = sum(p[0] for p in outline)/n
    center_v = sum(p[1] for p in outline)/n
    wing = collection in ('02 Upper wings', '03 Lower wings')
    fin = 'fin' in name.lower()
    bottom_inset = .78 if name == 'Central hull' else .955
    rings = []
    for inset, fraction in [(bottom_inset,0),(1,.28),(1,.70),(.94,1)]:
        ring = []
        for u, v in outline:
            pu = center_u + (u-center_u)*inset
            pv = center_v + (v-center_v)*inset
            height = low + (high-low)*fraction
            if wing:
                height *= hull_height(pu,pv)/.166
            elif name in ('Central hull','Cockpit surround','Rear cockpit yoke',
                           'Service channel floor','Dorsal service spine'):
                height *= hull_height(pu,pv)/.20
            elif name in ('Wing root beam','Shoulder underside link',
                          'Outer rail mounting block','Shoulder service pod','Service pod cap'):
                height *= hull_height(pu,pv)/.15
            if fin:
                sign = 1 if name.startswith('Upper') else -1
                if 'Aft' in name:
                    base = hull_height(pu,pv)*.50 + (365-pu)*.00038
                elif 'Outer' in name:
                    base = hull_height(pu,pv)*.40 + (150-pv)*.00075
                else:
                    base = hull_height(pu,pv)*.50 + (220-pv)*.0009
                height = sign*(base + .018*(fraction if sign == 1 else 1-fraction))
            ring.append(point(pu,pv,height))
        rings.append(ring)
    return loft(name, rings, collection, mirror)


def paired_shell(name, outline, low, high):
    shell('Upper ' + name, outline, low, high, '02 Upper wings')
    shell('Lower ' + name, outline, -high, -low, '03 Lower wings')


def symmetrical_outline(upper):
    return upper + [(u, 2*AXIS-v) for u, v in reversed(upper) if v != AXIS]


def loft(name, rings, collection, mirror=False):
    n = len(rings[0])
    vertices = [v for ring in rings for v in ring]
    faces = [(r*n+i, r*n+(i+1) % n, (r+1)*n+(i+1) % n, (r+1)*n+i)
             for r in range(len(rings)-1) for i in range(n)]
    faces += [tuple(reversed(range(n))), tuple(range((len(rings)-1)*n, len(rings)*n))]
    return mesh(name, vertices, faces, collection, mirror)


web = symmetrical_outline([(255,460),(255,441),(178,443),(178,423),
    (228,401),(229,397),(237,276),(280,288),(280,299),(355,301),
    (332,238),(409,174),(438,199),(471,138),(515,141),(540,176),
    (560,167),(623,120),(657,150),(697,151),(809,193),(805,146),
    (828,143),(891,220),(944,238),(998,281),(893,325),(720,324),
    (663,386),(705,390),(752,395),(795,405),(840,420),(862,432),
    (869,445),(869,460)])
shell('Structural center web', web, -.023, .023, '01 Hull and cockpit', False)
hull = symmetrical_outline([(253,460),(260,423),(329,409),(353,405),
    (421,387),(489,384),(529,406),(591,391),(638,389),(709,399),
    (804,416),(851,435),(865,454),(865,460)])
shell('Central hull', hull, -.105, .035, '01 Hull and cockpit', False)
fairing = symmetrical_outline([(522,460),(522,420),(551,398),(592,385),
    (637,384),(688,386),(733,392),(791,403),(835,416),(854,427),
    (865,435),(869,450),(869,460)])
shell('Cockpit surround', fairing, .027, .105, '01 Hull and cockpit', False)
canopy_rings = []
for u, width, rise in [(588,14,.035),(602,22,.068),(636,28,.095),
                        (674,38,.123),(719,43,.142),(763,40,.126),
                        (804,30,.089),(835,18,.040),(841,7,.011)]:
    canopy_rings.append([point(u, AXIS-width*math.cos(i*math.tau/16),
        hull_height(u,AXIS)*.515+rise*.80*math.sin(i*math.tau/16)) for i in range(16)])
canopy = loft('Canopy', canopy_rings, '01 Hull and cockpit')
for face in canopy.data.polygons:
    face.use_smooth = True
spine = symmetrical_outline([(342,460),(343,439),(352,420),(395,405),
    (455,390),(496,395),(521,414),(513,429),(490,421),(453,423),
    (411,439),(369,440),(367,460)])
shell('Rear cockpit yoke', spine, .025, .101, '01 Hull and cockpit', False)
shell('Service channel floor', symmetrical_outline([(361,460),(368,442),
    (409,439),(451,424),(489,424),(507,434),(508,460)]), .012, .042,
    '01 Hull and cockpit', False)
shell('Dorsal service spine', symmetrical_outline([(389,460),(435,449),
    (456,441),(484,442),(508,452),(508,460)]), .047, .098,
    '01 Hull and cockpit', False)

carrier = [(333,239),(409,174),(461,217),(528,228),(568,207),
    (590,188),(697,174),(754,197),(779,216),(827,220),(881,224),
    (944,238),(998,281),(894,325),(718,324),(663,386),(570,399),
    (559,383),(388,384),(375,355)]
paired_shell('wing foundation', carrier, .025, .095)
paired_shell('aft shoulder armor', [(337,239),(409,181),(459,222),
    (528,234),(549,250),(378,327)], .095, .166)
paired_shell('inner shoulder armor', [(382,335),(579,244),(649,317),
    (586,364),(558,346),(387,361)], .095, .159)
paired_shell('prow armor', [(585,242),(620,224),(620,215),(649,211),
    (667,216),(658,226),(749,231),(773,218),(825,223),(823,300),
    (688,291),(656,314)], .095, .139)
paired_shell('prow tip', [(831,223),(880,227),(942,240),(996,280),
    (882,301),(830,299)], .077, .137)
paired_shell('outer armor rail', [(532,229),(559,209),(562,181),
    (623,164),(697,151),(759,176),(809,192),(810,208),(799,216),
    (756,205),(697,178),(594,194),(591,206),(545,241)], .045, .113)
shell('Wing root beam', [(179,423),(369,352),(383,383),(297,434),
    (181,443)], -.026, .049, '04 Fins and spars')
shell('Shoulder underside link', [(382,375),(433,361),(548,355),
    (578,374),(560,384),(430,389)], -.025, .062, '04 Fins and spars')
shell('Outer rail mounting block', [(532,163),(622,120),(655,148),
    (561,184)], -.065, .069, '04 Fins and spars')
shell('Shoulder service pod', [(436,198),(471,138),(512,141),
    (541,189),(508,212),(465,210)], -.055, .114, '04 Fins and spars')
shell('Service pod cap', [(477,144),(507,146),(505,173),(480,170)],
    .114, .131, '04 Fins and spars')

for name, outline, low, high in [
    ('Aft swept fin', [(207,140),(391,187),(358,228)], .008, .045),
    ('Outer swept fin', [(503,18),(596,137),(554,153)], -.005, .046),
    ('Forward fin', [(805,147),(827,143),(891,220),(826,218)], .012, .057)]:
    shell('Upper ' + name, outline, low+.04, high+.04, '04 Fins and spars')
    shell('Lower ' + name, outline, -high-.04, -low-.04, '04 Fins and spars')

shell('Engine support frame', [(237,276),(280,288),(276,301),
    (358,302),(375,336),(372,363),(278,388),(269,405),(229,397)],
    -.103, .101, '05 Engines')
shell('Engine forward cowling', [(278,306),(357,310),(370,337),
    (368,355),(282,376)], -.067, .110, '05 Engines')


def engine(name, profile):
    rings = [[point(u, 344-radius*math.cos(i*math.tau/24),
                    radius/SCALE*math.sin(i*math.tau/24))
              for i in range(24)] for u, radius in profile]
    return loft(name, rings, '05 Engines', True)


engine('Engine housing', [(192,36),(226,38),(251,42),(284,40),(346,32)])
engine('Engine neck', [(190,36),(196,40),(224,39),(230,37)])
engine('Engine nozzle', [(198,44),(181,49),(151,53),(130,54),(120,43),
    (120,35),(134,40),(158,35),(179,29)])
engine('Engine throat', [(178,29),(181,27)])

ref = bpy.data.objects.new('REF TOP - authoritative outline - Num7', None)
collections['90 References'].objects.link(ref)
ref.empty_display_type = 'IMAGE'
ref.data = reference_image
ref.data.pack()
ref.empty_display_size = 1024/SCALE
ref.location = point(512,512,-.36)
ref.rotation_euler.z = math.pi/2
ref.color[3] = .65
ref.empty_image_depth = 'BACK'
ref.hide_set(True)
profile_path = HERE.parent / 'reference/approved-profile.png'
ref2 = bpy.data.objects.new('REF PROFILE - depth guide only', None)
collections['90 References'].objects.link(ref2)
ref2.empty_display_type = 'IMAGE'
ref2.data = bpy.data.images.load(str(profile_path))
ref2.data.pack()
ref2.empty_display_size = 2.6
ref2.location = (1.4,0,0)
ref2.rotation_euler = (math.pi/2,0,math.pi/2)
ref2.hide_set(True)
collections['90 References'].hide_render = True

scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 1400
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
shading = scene.display.shading
shading.light = 'STUDIO'
shading.color_type = 'SINGLE'
shading.single_color = (.61,.64,.68)
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = 'BOTH'
shading.curvature_ridge_factor = 1.3
shading.curvature_valley_factor = 1.2
shading.show_object_outline = True
shading.object_outline_color = (.065,.075,.09)
shading.background_type = 'WORLD'
scene.world = bpy.data.worlds.new('Clay inspection background')
scene.world.color = (.08,.09,.11)
scene.view_settings.view_transform = 'Standard'
cameras = {}
for name, location in [('Top',(0,0,5)),('Side',(5,0,0)),('Front',(0,5,0)),
                       ('Hero',(2.8,3.6,3.1)),('Underside',(2.8,3.6,-3.1))]:
    data = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new('View - '+name, data)
    collections['99 Cameras'].objects.link(cam)
    cam.location = location
    cam.rotation_euler = (-cam.location).to_track_quat('-Z','Y').to_euler()
    data.type = 'ORTHO'
    data.ortho_scale = 2.8
    if name == 'Top':
        cam.location = point(512,512,5)
        cam.rotation_euler = (0,0,math.pi/2)
        data.ortho_scale = 1024/SCALE
        scene.render.resolution_x = scene.render.resolution_y = 1024
    else:
        scene.render.resolution_x = 1400
        scene.render.resolution_y = 1100
    cameras[name] = cam
    scene.camera = cam
    scene.render.filepath = str(OUTPUT/(name.lower()+'.png'))
    bpy.ops.render.render(write_still=True)
    if name == 'Top':
        scene.render.film_transparent = True
        scene.render.image_settings.color_mode = 'RGBA'
        scene.render.filepath = str(OUTPUT/'top-alpha.png')
        bpy.ops.render.render(write_still=True)
        scene.render.film_transparent = False

report = {'reference_sha256': hashlib.sha256(REFERENCE.read_bytes()).hexdigest(),
    'projection': 'Original image pixels traced at 440 pixels/unit; +Y nose, X symmetry at image row 460.',
    'depth': 'Paired wing shells crest behind the cockpit, falling toward nose, tail and sides; 5.5 degree nose-down slope.',
    'textures': False, 'parts': []}
graph = bpy.context.evaluated_depsgraph_get()
exported = []
bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    evaluated = obj.evaluated_get(graph)
    data = bpy.data.meshes.new_from_object(evaluated)
    data.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(data)
    nonmanifold = sum(not edge.is_manifold for edge in bm.edges)
    degenerate = sum(face.calc_area() < 1e-10 for face in bm.faces)
    bm.free()
    if nonmanifold or degenerate:
        raise ValueError(f'{obj.name}: {nonmanifold} nonmanifold, {degenerate} degenerate')
    report['parts'].append({'name':obj.name, 'control_vertices':len(obj.data.vertices),
        'triangles':len(data.loop_triangles), 'modifiers':[m.type for m in obj.modifiers]})
    temporary = bpy.data.objects.new(obj.name+' export', data)
    scene.collection.objects.link(temporary)
    temporary.select_set(True)
    exported.append(temporary)
bpy.ops.export_scene.fbx(filepath=str(HERE/'CrimsonTopMatch.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_anim=False,
    add_leaf_bones=False, path_mode='STRIP')
for obj in exported:
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(data)
scene.camera = cameras['Hero']
active = bpy.data.objects['Upper inner shoulder armor']
active.select_set(True)
bpy.context.view_layer.objects.active = active
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.region_3d.view_rotation = cameras['Hero'].rotation_euler.to_quaternion()
            space.region_3d.view_distance = 3.2
            space.region_3d.view_perspective = 'ORTHO'
            space.shading.type = 'SOLID'
            space.shading.show_cavity = True
            space.overlay.show_floor = False
            space.overlay.show_axis_x = False
            space.overlay.show_axis_y = False
for cam in cameras.values():
    cam.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'CrimsonTopMatch.blend'))
(HERE/'mesh-manifest.json').write_text(json.dumps(report, indent=2))
print(json.dumps({'blend':str(HERE/'CrimsonTopMatch.blend'), 'parts':len(parts),
                  'triangles':sum(p['triangles'] for p in report['parts']), 'renders':str(OUTPUT)}))
