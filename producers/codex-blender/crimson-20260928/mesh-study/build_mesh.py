"""Blender --background --python build_mesh.py; writes editable source and clay inspection images."""
import bpy
import bmesh
import hashlib
import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
DONOR = ROOT / 'src/Asteroids3D/Assets/Visuals/Ships/Ship2'
OUTPUT = ROOT / 'results/crimson-mesh'
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = 'Crimson - mesh only'
collections = {}
for name in ('00 Symmetry controls', '01 Hull and cockpit', '02 Upper wings', '03 Lower wings', '04 Fins and spars',
             '05 Engines', '90 References - toggle to compare', '99 Inspection cameras'):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    collections[name] = collection
mirror_center = bpy.data.objects.new('SHIP CENTER - mirror plane', None)
collections['00 Symmetry controls'].objects.link(mirror_center)
mirror_center.empty_display_type = 'PLAIN_AXES'
mirror_center.empty_display_size = .15
mirror_center.lock_location = (True, True, True)
mirror_center.lock_rotation = (True, True, True)
mirror_center.lock_scale = (True, True, True)
mirror_center.hide_select = True
mirror_center.hide_render = True
parts = []
pitch = math.radians(-5.5)


def move(obj, collection):
    for previous in list(obj.users_collection):
        previous.objects.unlink(obj)
    collections[collection].objects.link(obj)


def mesh(name, vertices, faces, collection, mirror=False, smooth=False):
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
    obj.rotation_euler.x = pitch
    obj.color = (.58, .62, .66, 1)
    if mirror:
        modifier = obj.modifiers.new('Opposite wing', 'MIRROR')
        modifier.mirror_object = mirror_center
        modifier.use_clip = True
        modifier.use_mirror_merge = True
    for face in data.polygons:
        face.use_smooth = smooth
    parts.append(obj)
    return obj


def closed_loft(name, rings, collection, mirror=False, smooth=False):
    count = len(rings[0])
    vertices = [tuple(point) for ring in rings for point in ring]
    faces = []
    for row in range(len(rings)-1):
        for column in range(count):
            next_column = (column+1) % count
            faces.append((row*count+column, row*count+next_column,
                          (row+1)*count+next_column, (row+1)*count+column))
    faces += [tuple(reversed(range(count))), tuple(range((len(rings)-1)*count,len(rings)*count))]
    return mesh(name, vertices, faces, collection, mirror, smooth)


def wing(name, stations, collection, sign=1):
    rings = []
    for y, inner, outer, bottom, top in stations:
        bevel = min(.022, (outer-inner)*.19, (top-bottom)*.22)
        rings.append([(inner,y,sign*(bottom+bevel)), (inner,y,sign*(top-bevel)),
                      (inner+bevel,y,sign*top), (outer-bevel,y,sign*(top-.012)),
                      (outer,y,sign*(top-bevel-.012)), (outer,y,sign*(bottom+bevel)),
                      (outer-bevel,y,sign*bottom), (inner+bevel,y,sign*bottom)])
    return closed_loft(name,rings,collection,True)


def body(name, stations, collection):
    rings=[]
    for y,width,center,height in stations:
        rings.append([(width*math.cos(angle),y,center+height*math.sin(angle))
                      for angle in [math.tau*(i+.5)/8 for i in range(8)]])
    return closed_loft(name,rings,collection)


def plate(name, polygon, thickness, collection, mirror=True):
    count=len(polygon)
    vertices=[(x,y,z-thickness/2) for x,y,z in polygon]
    vertices += [(x,y,z+thickness/2) for x,y,z in polygon]
    faces=[tuple(reversed(range(count))),tuple(range(count,count*2))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    return mesh(name,vertices,faces,collection,mirror)


body('Fuselage', [(-.79,.075,-.018,.050),(-.50,.15,-.018,.088),(-.22,.205,-.005,.105),
                  (.05,.192,0,.095),(.32,.16,-.012,.080),(.57,.095,-.018,.055),
                  (.64,.035,-.020,.026)], '01 Hull and cockpit')
body('Cockpit fairing', [(-.15,.110,.065,.057),(-.04,.172,.070,.095),(.17,.183,.077,.110),
                        (.36,.159,.060,.099),(.52,.117,.029,.074),(.64,.055,-.008,.036),
                        (.675,.016,-.014,.015)], '01 Hull and cockpit')
canopy=body('Canopy', [(-.01,.072,.156,.015),(.06,.097,.169,.033),(.19,.113,.176,.045),
                     (.34,.094,.158,.045),(.47,.065,.124,.036),(.55,.026,.087,.018),
                     (.565,.010,.071,.009)], '01 Hull and cockpit')
for face in canopy.data.polygons:
    face.use_smooth=True
subdivision=canopy.modifiers.new('Canopy smoothing - editable cage','SUBSURF')
subdivision.levels=1
subdivision.render_levels=1
body('Dorsal service housing', [(-.51,.07,.105,.025),(-.43,.13,.11,.040),(-.20,.127,.11,.042),
                              (-.14,.10,.103,.030)], '01 Hull and cockpit')

# Shared stations retain the paired wing volumes without baking symmetry into dense triangles.
wing_stations=[(-.55,.30,.66,.035,.105),(-.41,.225,.72,.025,.167),(-.22,.215,.64,.027,.180),
               (-.03,.27,.60,.030,.155),(.20,.38,.64,.033,.133),(.47,.415,.685,.036,.112),
               (.77,.43,.655,.039,.093),(.995,.548,.559,.041,.060)]
wing('Upper wing body',wing_stations,'02 Upper wings')
wing('Lower wing body',wing_stations,'03 Lower wings',-1)

shoulder_stations=[(-.49,.28,.665,.110,.143),(-.375,.256,.72,.162,.197),
                   (-.16,.246,.535,.173,.202),(-.07,.285,.475,.148,.174)]
wing('Upper shoulder armor',shoulder_stations,'02 Upper wings')
wing('Lower shoulder armor',shoulder_stations,'03 Lower wings',-1)

rail_stations=[(-.10,.60,.725,.055,.098),(.04,.695,.79,.054,.105),
               (.27,.75,.82,.048,.105),(.48,.705,.78,.044,.085),(.58,.69,.708,.044,.062)]
wing('Upper outer armor rail',rail_stations,'02 Upper wings')
wing('Lower outer armor rail',rail_stations,'03 Lower wings',-1)

wing('Wing root spar',[(-.59,.135,.43,-.036,.038),(-.38,.14,.66,-.026,.043),
                      (-.17,.175,.59,-.026,.043),(.03,.22,.41,-.025,.035)],'04 Fins and spars')
wing('Outer rail root',[(-.15,.54,.70,-.027,.035),(.17,.58,.795,-.027,.035),
                       (.43,.59,.735,-.027,.035)],'04 Fins and spars')

plate('Upper swept tail fin',[(.46,-.36,.095),(.78,-.725,.25),(.68,-.48,.125),
                             (.63,-.215,.088)],.019,'04 Fins and spars')
plate('Lower swept tail fin',[(.46,-.36,-.095),(.75,-.70,-.225),(.68,-.48,-.125),
                             (.63,-.215,-.088)],.019,'04 Fins and spars')
plate('Lateral shoulder fin',[(.62,-.30,.005),(.925,-.26,.009),(.715,.035,.012),
                             (.64,.05,.012)],.019,'04 Fins and spars')
plate('Upper prow fin',[(.60,.43,.064),(.70,.65,.18),(.735,.42,.075)],.016,'04 Fins and spars')
plate('Lower prow fin',[(.60,.43,-.064),(.70,.65,-.18),(.735,.42,-.075)],.016,'04 Fins and spars')

wing('Engine saddle',[(-.72,.235,.47,-.077,.080),(-.57,.23,.50,-.065,.082),
                     (-.40,.25,.45,-.045,.070)],'05 Engines')


def engine(name,profile):
    rings=[[(.355+radius*math.cos(i*math.tau/16),y,radius*math.sin(i*math.tau/16))
            for i in range(16)] for y,radius in profile]
    return closed_loft(name,rings,'05 Engines',True)


engine('Engine housing',[(-.61,.078),(-.65,.091),(-.74,.094),(-.84,.102),(-.89,.109)])
engine('Engine neck collar',[(-.735,.094),(-.743,.100),(-.773,.100),(-.782,.097)])
engine('Engine nozzle',[(-.826,.102),(-.838,.115),(-.914,.119),(-.933,.110),
                        (-.933,.090),(-.900,.082),(-.864,.073)])
engine('Engine throat',[(-.862,.072),(-.865,.071)])

source_path=DONOR/'CrimsonVortex0613023558TextureObj/crimsonVortex0613023558Texture.obj'
bpy.ops.wm.obj_import(filepath=str(source_path))
donor=next(obj for obj in bpy.context.selected_objects if obj.type=='MESH')
donor.name='REF original AI mesh - rough proportions only'
bpy.context.view_layer.objects.active=donor
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
donor.rotation_euler.z=-math.pi/2
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
donor.data.materials.clear()
donor.display_type='WIRE'
move(donor,'90 References - toggle to compare')
for name,path,size,position,rotation in [
    ('REF original top',DONOR/'shipUpdateEnginesOff.png',2.327,(0,-.109,-.40),(0,0,math.pi/2)),
    ('REF selected profile - shorter spikes and wider wings requested',HERE/'reference/approved-profile.png',2.6,
     (1.3,0,.1),(math.pi/2,0,math.pi/2))]:
    ref=bpy.data.objects.new(name,None)
    collections['90 References - toggle to compare'].objects.link(ref)
    ref.empty_display_type='IMAGE'
    ref.data=bpy.data.images.load(str(path));ref.data.pack()
    ref.empty_display_size=size
    ref.location=position
    ref.rotation_euler=rotation
    ref.color[3]=.55
    ref.empty_image_depth='BACK'
collections['90 References - toggle to compare'].hide_render=True
collections['90 References - toggle to compare'].hide_viewport=True

scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=1600
scene.render.resolution_y=1200
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
shading=scene.display.shading
shading.light='STUDIO'
shading.studiolight_rotate_z=.35
shading.color_type='SINGLE'
shading.single_color=(.62,.65,.69)
shading.show_shadows=True
shading.show_cavity=True
shading.cavity_type='BOTH'
shading.curvature_ridge_factor=1.25
shading.curvature_valley_factor=1.15
shading.cavity_ridge_factor=1.0
shading.cavity_valley_factor=1.0
shading.show_object_outline=True
shading.object_outline_color=(.07,.085,.1)
shading.background_type='WORLD'
scene.world=bpy.data.worlds.new('Inspection background')
scene.world.color=(.075,.085,.10)
scene.view_settings.view_transform='Standard'
cameras={}
for name,location in [('Top',(0,0,5)),('Side',(5,0,0)),('Front',(0,5,0)),
                     ('Hero',(2.7,3.5,3.0)),('Underside',(2.7,3.5,-3.0))]:
    bpy.ops.object.camera_add(location=location)
    camera=bpy.context.object
    camera.name='View - '+name
    camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler()
    if name=='Top':
        camera.rotation_euler=(0,0,0)
    camera.data.type='ORTHO'
    camera.data.ortho_scale=3.1 if name=='Top' else 2.7 if name in ('Hero','Underside') else 2.30
    move(camera,'99 Inspection cameras')
    cameras[name]=camera
    scene.camera=camera
    scene.render.filepath=str(OUTPUT/(name.lower()+'.png'))
    bpy.ops.render.render(write_still=True)

scene.camera=cameras['Hero']
bpy.ops.object.select_all(action='DESELECT')
active=next(obj for obj in parts if obj.name=='Upper wing body')
active.select_set(True)
bpy.context.view_layer.objects.active=active
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type!='VIEW_3D':
            continue
        space=area.spaces.active
        space.region_3d.view_rotation=cameras['Hero'].rotation_euler.to_quaternion()
        space.region_3d.view_distance=3.1
        space.region_3d.view_perspective='ORTHO'
        space.shading.type='SOLID'
        space.shading.light='STUDIO'
        space.shading.show_cavity=True
        space.shading.cavity_type='BOTH'
        space.overlay.show_floor=False
        space.overlay.show_axis_x=False
        space.overlay.show_axis_y=False
collections['99 Inspection cameras'].hide_viewport=True

report={'source_obj':str(source_path.relative_to(ROOT)),
        'source_sha256':hashlib.sha256(source_path.read_bytes()).hexdigest(),
        'orientation':'Blender +Y nose, +Z upper hull; 5.5 degree authored nose-down pitch',
        'textures':False,'parts':[]}
depsgraph=bpy.context.evaluated_depsgraph_get()
for obj in parts:
    evaluated=obj.evaluated_get(depsgraph).to_mesh()
    evaluated.calc_loop_triangles()
    bm=bmesh.new();bm.from_mesh(evaluated)
    bad_edges=sum(not edge.is_manifold for edge in bm.edges)
    zero_faces=sum(face.calc_area()<1e-10 for face in bm.faces)
    if bad_edges or zero_faces:
        raise ValueError(f'{obj.name}: {bad_edges} nonmanifold edges, {zero_faces} zero-area faces')
    report['parts'].append({'name':obj.name,'control_vertices':len(obj.data.vertices),
                            'triangles':len(evaluated.loop_triangles),'modifiers':[m.type for m in obj.modifiers]})
    bm.free()
    obj.evaluated_get(depsgraph).to_mesh_clear()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'CrimsonMesh.blend'))
(HERE/'mesh-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'blend':str(HERE/'CrimsonMesh.blend'),'parts':len(parts),'evidence':str(OUTPUT)}))
