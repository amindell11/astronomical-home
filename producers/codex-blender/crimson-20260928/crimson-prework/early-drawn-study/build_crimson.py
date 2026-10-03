import bpy, bmesh, json, math, hashlib
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
ASSETS = ROOT / 'src/Asteroids3D/Assets/Visuals/Ships/Crimson/DrawnStudy'
DONOR = ROOT / 'src/Asteroids3D/Assets/Visuals/Ships/Ship2'
EVIDENCE = ROOT / 'results/crimson-study'
EVIDENCE.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = 'Crimson - Editable construction'
reference = bpy.data.collections.new('01 Reference - AI geometry is a rough guide')
parts = bpy.data.collections.new('02 Clean separate ship parts')
studio = bpy.data.collections.new('03 Inspection cameras and lights')
for col in (reference, parts, studio): scene.collection.children.link(col)


def move(obj, collection):
    for col in list(obj.users_collection): col.objects.unlink(obj)
    collection.objects.link(obj)


def material(name, rgb, metallic=0, roughness=.62, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*rgb, 1)
    node.inputs['Metallic'].default_value = metallic
    node.inputs['Roughness'].default_value = roughness
    node.inputs['Emission Color'].default_value = (*rgb, 1)
    node.inputs['Emission Strength'].default_value = emission
    return mat


palette = {
    'Silver armor': material('Silver armor', (.56,.61,.65), .22),
    'Crimson inset': material('Crimson inset', (.55,.009,.019), .08),
    'Graphite frame': material('Graphite frame', (.027,.034,.045), .18),
    'Canopy glass': material('Canopy glass', (.009,.019,.030), .3, .24),
    'Vent slats': material('Vent slats', (.13,.17,.20), .25),
    'Engine core': material('Engine core', (.08,.64,1), 0, .4, 5),
    'Wear graphite': material('Wear graphite', (.09,.11,.12)),
    'Exposed alloy': material('Exposed alloy', (.72,.73,.68), .25),
}
obj_path = DONOR / 'CrimsonVortex0613023558TextureObj/crimsonVortex0613023558Texture.obj'
bpy.ops.wm.obj_import(filepath=str(obj_path))
donor = next(o for o in scene.objects if o.type == 'MESH')
donor.name = 'REF Crimson original AI mesh - do not export'
bpy.context.view_layer.objects.active = donor
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
donor.rotation_euler.z = -math.pi/2
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
move(donor, reference)
donor.data.materials.clear()
donor_mat = material('REF original painted texture', (.5,.5,.5))
tex = donor_mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(str(DONOR/'CrimsonVortex0613023558TextureObj/crimsonVortex0613023558Texture.png'))
tex.image.pack()
donor_mat.node_tree.links.new(tex.outputs['Color'], donor_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
donor.data.materials.append(donor_mat)
donor['usage'] = 'Rough volume reference only. Engine profile and panel topology corrected from the painted reference.'
for name, filename in [('REF approved top drawing','shipUpdateEnginesOff.png')]:
    ref = bpy.data.objects.new(name, None)
    reference.objects.link(ref)
    ref.empty_display_type = 'IMAGE'
    ref.data = bpy.data.images.load(str(DONOR/filename)); ref.data.pack()
    ref.empty_display_size = 2.327
    ref.rotation_euler.z = math.pi/2
    ref.location = (0,-.109,-.35)
    ref.color[3] = .45


def mesh(name, vertices, faces, mat, mirror=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces); data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(data); bm.free()
    data.materials.append(palette[mat])
    obj = bpy.data.objects.new(name, data); parts.objects.link(obj)
    if mirror:
        mod = obj.modifiers.new('Opposite side - editable symmetry', 'MIRROR')
        mod.use_clip = True; mod.use_mirror_merge = True
    return obj


def xy(px, py):
    return ((512-py)/440, (px-560)/440)


def plate(name, pixels, top, depth, mat, bevel=.016, mirror=True):
    points = [Vector((*xy(*p), top)) for p in pixels]
    center = sum(points, Vector())/len(points)
    count = len(points)
    verts = [tuple(Vector((p.x,p.y,top-depth))) for p in points]
    verts += [tuple(p-Vector((0,0,bevel))) for p in points]
    verts += [tuple(p+(center-p).normalized()*bevel) for p in points]
    faces = [tuple(reversed(range(count))), tuple(range(count*2,count*3))]
    for row in range(2):
        for i in range(count):
            j = (i+1)%count
            faces.append((row*count+i,row*count+j,(row+1)*count+j,(row+1)*count+i))
    return mesh(name, verts, faces, mat, mirror)


plate('Hull - central keel', [(207,480),(314,423),(505,412),(740,436),(841,469),(860,512),(350,512),(312,491),(207,491)], -.005,.075,'Graphite frame',mirror=True)
plate('Wing - structural deck', [(274,336),(410,217),(681,177),(819,223),(930,251),(977,283),(868,321),(727,316),(638,398),(475,424),(309,403)], .002,.08,'Graphite frame')
plate('Prow - continuous crimson channel', [(378,345),(509,278),(686,204),(844,255),(986,282),(878,314),(727,310),(642,396),(560,401)], .062,.064,'Crimson inset')
plate('Prow - forward silver blade', [(655,235),(727,253),(825,237),(928,249),(988,280),(875,300),(725,289),(681,285)], .119,.063,'Silver armor')
plate('Prow - inner silver sweep', [(383,331),(583,248),(648,333),(582,366),(559,354),(391,365)], .14,.10,'Silver armor')
plate('Shoulder - aft silver armor', [(338,244),(407,184),(461,226),(514,230),(530,250),(376,318)], .147,.075,'Silver armor',bevel=.025)
plate('Shoulder - forward silver armor', [(592,224),(626,219),(654,233),(650,248),(676,285),(648,329),(590,253)], .147,.065,'Silver armor')
plate('Outer rail - crimson inset', [(538,242),(596,207),(690,185),(775,216),(751,230),(667,228),(651,212),(619,212),(548,253)], .094,.07,'Crimson inset')
plate('Outer rail - silver spine', [(531,220),(570,184),(696,159),(793,192),(810,211),(779,207),(696,178),(588,199),(550,233)], .137,.05,'Silver armor')
plate('Fin - aft swept spar', [(218,152),(389,204),(367,232)], .011,.025,'Graphite frame',bevel=.003)
plate('Fin - aft crimson face', [(238,159),(376,206),(364,220)], .015,.007,'Crimson inset',bevel=.002)
plate('Fin - outer swept spar', [(511,40),(595,146),(553,161)], -.004,.025,'Graphite frame',bevel=.002)
plate('Fin - outer crimson face', [(525,61),(580,142),(557,149)], .002,.007,'Crimson inset',bevel=.001)
plate('Fin - outer root', [(550,151),(594,136),(625,170),(567,195)], -.023,.024,'Graphite frame',bevel=.004)
plate('Fin - forward winglet', [(810,151),(827,148),(894,227),(830,221)], .009,.028,'Graphite frame',bevel=.003)
plate('Fin - forward crimson face', [(821,157),(830,157),(881,220),(839,214)], .014,.006,'Crimson inset',bevel=.002)
plate('Engine - shoulder bridge', [(191,430),(198,411),(333,353),(379,369),(305,424)], .092,.105,'Graphite frame')
plate('Engine - silver saddle', [(259,310),(350,325),(361,355),(294,393),(258,377)], .054,.088,'Silver armor')
plate('Engine - saddle crimson cover', [(288,326),(331,337),(342,355),(292,380)], .062,.018,'Crimson inset')
plate('Service - rear collar', [(350,447),(392,419),(487,417),(516,449),(516,493),(488,502),(363,477)], .12,.07,'Silver armor')
plate('Service - recessed cooling well', [(369,450),(403,432),(476,433),(497,451),(493,483),(388,473)], .129,.009,'Graphite frame',bevel=.003)


def loft(name, stations, mat):
    verts = []
    for y,w,z,halfheight in stations:
        verts.extend([(0,y,z+halfheight),(w*.75,y,z+halfheight*.82),(w,y,z),
                      (w*.78,y,z-halfheight*.70),(0,y,z-halfheight)])
    faces=[]
    for i in range(len(stations)-1):
        for j in range(4): faces.append((i*5+j,i*5+j+1,(i+1)*5+j+1,(i+1)*5+j))
    faces.extend([tuple(reversed(range(5))),tuple(range((len(stations)-1)*5,len(stations)*5))])
    return mesh(name,verts,faces,mat,True)


loft('Cockpit - silver fairing', [(-.13,.10,.10,.055),(-.03,.163,.10,.075),(.20,.17,.11,.074),(.49,.125,.095,.055),(.62,.055,.073,.032),(.66,.018,.06,.015)],'Silver armor')
loft('Canopy - crimson seal', [(.025,.077,.181,.006),(.16,.122,.192,.025),(.41,.094,.17,.022),(.535,.035,.128,.013)],'Crimson inset')
glass=loft('Canopy - dark glass', [(.051,.057,.192,.008),(.17,.100,.209,.034),(.39,.081,.19,.032),(.509,.026,.144,.010)],'Canopy glass')
for p in glass.data.polygons: p.use_smooth=True
loft('Canopy - central arch', [(.263,.095,.204,.044),(.277,.094,.203,.044)], 'Crimson inset')


def engine(name, profile, mat, closed=False):
    n=16; cx=.317; z=.006
    vertices=[(cx+r*math.cos(i*math.tau/n),y,z+r*math.sin(i*math.tau/n)) for y,r in profile for i in range(n)]
    faces=[]
    for j in range(len(profile)-1):
        for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    if closed: faces.extend([tuple(reversed(range(n))),tuple(range((len(profile)-1)*n,len(profile)*n))])
    return mesh(name,vertices,faces,mat,True)


engine('Engine - axial casing', [(-.64,.083),(-.74,.096),(-.87,.117),(-.95,.118),(-.964,.105),(-.94,.087),(-.84,.065)],'Graphite frame')
engine('Engine - machined collar', [(-.66,.09),(-.67,.1),(-.704,.1),(-.711,.09)],'Silver armor')
engine('Engine - nozzle rim', [(-.911,.116),(-.935,.121),(-.954,.116),(-.958,.104)],'Vent slats')
engine('Engine - recessed luminous throat', [(-.878,.078),(-.881,.073)],'Engine core',True)
for i in range(5):
    x=403+i*14
    plate('Vent - fitted slat %02d'%i,[(x,438),(x+5,439),(x+3,475),(x-2,474)],.138,.012,'Vent slats',bevel=.002)

# Localized glancing impacts follow the armor's leading edges.
for i,(poly,level) in enumerate([
    ([(885,260),(891,262),(870,279),(877,265)],.1195),
    ([(898,260),(900,262),(888,273)],.1197),
    ([(420,210),(424,213),(408,227),(413,218)],.1475),
    ([(431,217),(433,218),(423,229)],.1475),
]):
    plate('Wear - directional chip %02d'%i,poly,level,.0002,'Wear graphite',bevel=.0001,mirror=False)
plate('Wear - exposed prow lip',[(883,260),(886,260),(873,271)],.120,.0002,'Exposed alloy',bevel=.0001,mirror=False)

for obj in parts.objects:
    obj['construction'] = 'Clean authored polygons over aligned reference; modifiers retained in Blender.'
    uv=obj.data.uv_layers.new(name='Hull planar paint')
    for loop in obj.data.loops:
        p=obj.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv=((p.x+1.1)/2.2,(p.y+1.1)/2.2)

scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1440; scene.render.resolution_y=1440; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world=bpy.data.worlds.new('Quiet navy studio'); scene.world.color=(.045,.055,.075)
scene.view_settings.view_transform='AgX'
for name,loc,power,size in [('Key',(-3,1,5),380,4),('Rim',(3,-2,3),260,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc); light=bpy.context.object
    light.name=name; light.data.energy=power; light.data.size=size
    light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler();move(light,studio)
cameras={}
for name,loc in [('Top',(0,0,4)),('Side',(4,0,0)),('Hero',(2.4,-3.2,4.7)),('Underside',(2,-3,-4))]:
    bpy.ops.object.camera_add(location=loc);cam=bpy.context.object;cam.name=name
    cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=2.5;move(cam,studio);cameras[name]=cam
for view in ('Top','Side'):
    parts.hide_render=True;reference.hide_render=False;scene.camera=cameras[view]
    scene.render.filepath=str(EVIDENCE/('reference-'+view.lower()+'.png'));bpy.ops.render.render(write_still=True)
parts.hide_render=False;reference.hide_render=True
reference.hide_viewport=True
for view in ('Top','Side','Hero','Underside'):
    scene.camera=cameras[view];scene.render.filepath=str(EVIDENCE/('clean-'+view.lower()+'.png'))
    bpy.ops.render.render(write_still=True)
scene.camera=cameras['Hero']
bpy.ops.object.select_all(action='DESELECT')
for obj in parts.objects: obj.select_set(True)
bpy.context.view_layer.objects.active=glass
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3
            area.spaces.active.region_3d.view_rotation=cameras['Hero'].rotation_euler.to_quaternion()
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CrimsonStudy.blend'))
manifest={'source_obj':str(obj_path.relative_to(ROOT)), 'source_sha256':hashlib.sha256(obj_path.read_bytes()).hexdigest(),
          'reference_transform':'OBJ default import then world Z -90 degrees; Blender +Y nose, +Z visible hull',
          'parts':[{'name':o.name,'control_vertices':len(o.data.vertices),'modifiers':[m.type for m in o.modifiers]} for o in parts.objects],
          'palette':{n:list(m.diffuse_color) for n,m in palette.items()}}
(OUT/'construction-manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'blend':str(OUT/'CrimsonStudy.blend'),'evidence':str(EVIDENCE),'parts':len(parts.objects)}))
