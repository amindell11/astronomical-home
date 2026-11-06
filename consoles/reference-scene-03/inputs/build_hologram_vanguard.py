import bpy, math, json, hashlib, array
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parent
SOURCE = Path('D:/amind/git/astronomical-home/art/ships/vanguard/Vanguard.blend')
SOURCE_HASH = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
source_scene = bpy.context.scene
parts = sorted({o for c in bpy.data.collections if c.name in ('role.hull','role.canopy','role.cores','role.ink') for o in c.all_objects if o.type == 'MESH'}, key=lambda o:o.name)
deps = bpy.context.evaluated_depsgraph_get()
points = [o.matrix_world @ v.co for o in parts for v in o.evaluated_get(deps).data.vertices]
low = Vector(tuple(min(v[i] for v in points) for i in range(3)))
high = Vector(tuple(max(v[i] for v in points) for i in range(3)))
center = (low + high) / 2
scale = 6.0 / (high.y-low.y)
conversion = Matrix.Scale(scale,4) @ Matrix.Rotation(-math.pi/2,4,'Z') @ Matrix.Translation(-center)
copied = []
for source in parts:
    mesh = bpy.data.meshes.new_from_object(source.evaluated_get(deps), preserve_all_data_layers=True, depsgraph=deps)
    mesh.name = 'Vanguard mesh / '+source.name.removeprefix('MVP ')
    mesh.transform(conversion @ source.matrix_world)
    obj = bpy.data.objects.new('Vanguard / '+source.name.removeprefix('MVP '),mesh)
    obj['source_part'] = source.name
    obj['source_file'] = str(SOURCE)
    obj['source_geometry'] = 'Evaluated source mesh, UVs retained; source file unchanged'
    copied.append(obj)

scene = bpy.data.scenes.new('Vanguard hologram review')
bpy.context.window.scene = scene
collection = bpy.data.collections.new('HologramVanguard')
scene.collection.children.link(collection)
root = bpy.data.objects.new('HologramVanguardRoot',None)
root.empty_display_type='PLAIN_AXES'
root.empty_display_size=0.5
collection.objects.link(root)
root['source_sha256'] = SOURCE_HASH
root['nose_axis']='+X'
root['length']=6.0
root['suggested_bank_degrees']=34.0
root['instructions']='Append collection HologramVanguard. Place root; rotate X 34 degrees for console hero camera.'
for obj in copied:
    collection.objects.link(obj)
    obj.parent=root
for old in list(bpy.data.scenes):
    if old != scene: bpy.data.scenes.remove(old)

palette = {
    'HullPrimary':(0.56,0.59,0.61), 'HullAccent':(0.48,0.105,0.087),
    'Structure':(0.065,0.095,0.14), 'Underside':(0.10,0.145,0.20),
    'Linework':(0.20,0.25,0.29), 'WarningMark':(0.38,0.12,0.10),
    'Indicator':(0.28,0.62,0.67), 'CanopyGlass':(0.035,0.080,0.13),
    'CanopyFrame':(0.40,0.12,0.105), 'CoreGlow':(0.10,0.65,0.80),
    'Contour':(0.35,0.47,0.53), 'HatchFrame':(0.11,0.15,0.19),
    'PanelGray':(0.27,0.32,0.35), 'PanelEdge':(0.48,0.52,0.54),
    'HullInset':(0.13,0.20,0.27), 'AmberIndex':(0.61,0.38,0.16),
}
size=2048
pixels=np.zeros((size*size,4),dtype=np.float32)
coverage=np.zeros(size*size,dtype=np.float32)
for region,color in palette.items():
    path=SOURCE.parent/'layers'/('Region_'+region+'.png')
    image=bpy.data.images.load(str(path),check_existing=False)
    image.colorspace_settings.name='Non-Color'
    image.scale(size,size)
    values=np.empty(size*size*4,dtype=np.float32)
    image.pixels.foreach_get(values)
    weight=values.reshape((-1,4))[:,0]
    pixels[:,:3] += weight[:,None]*np.array(color,dtype=np.float32)
    coverage += weight
    bpy.data.images.remove(image)
covered=coverage>0.0001
pixels[covered,:3]/=coverage[covered,None]
pixels[~covered,:3]=palette['HullPrimary']
pixels[:,3]=1.0
atlas=bpy.data.images.new('Vanguard muted hologram palette',width=size,height=size,alpha=True,float_buffer=True)
atlas.colorspace_settings.name='Non-Color'
atlas.pixels.foreach_set(pixels.ravel())
atlas.pack()

def node(tree,type,name,x,y):
    n=tree.nodes.new(type); n.name=name; n.label=name; n.location=(x,y); return n

def make_surface(name,color=None,opacity=0.83,emission=0.65):
    mat=bpy.data.materials.new(name); mat.use_nodes=True
    mat.surface_render_method='DITHERED'
    mat.diffuse_color=(*(color or palette['HullPrimary']),opacity)
    nt=mat.node_tree; nt.nodes.clear(); links=nt.links
    out=node(nt,'ShaderNodeOutputMaterial','Hologram output',950,40)
    mix=node(nt,'ShaderNodeMixShader','Visible projected surface',710,40)
    transparent=node(nt,'ShaderNodeBsdfTransparent','Space visible through hull',470,-100)
    shader=node(nt,'ShaderNodeBsdfPrincipled','Muted painted projection',450,190)
    shader.inputs['Roughness'].default_value=0.7
    shader.inputs['Metallic'].default_value=0.0
    shader.inputs['Specular IOR Level'].default_value=0.1
    shader.inputs['Emission Strength'].default_value=emission
    if color:
        shader.inputs['Base Color'].default_value=(*color,1)
        shader.inputs['Emission Color'].default_value=(*color,1)
    else:
        uv=node(nt,'ShaderNodeUVMap','Authored PaintUV',-900,500); uv.uv_map='PaintUV'
        tex=node(nt,'ShaderNodeTexImage','Packed muted paint',-680,500); tex.image=atlas
        links.new(uv.outputs['UV'],tex.inputs['Vector'])
        links.new(tex.outputs['Color'],shader.inputs['Base Color'])
        links.new(tex.outputs['Color'],shader.inputs['Emission Color'])
    coords=node(nt,'ShaderNodeTexCoord','Ship coordinates',-1100,-50); coords.object=root
    wave=node(nt,'ShaderNodeTexWave','Understated horizontal scanlines',-880,-50)
    wave.wave_type='BANDS'; wave.bands_direction='Z'; wave.wave_profile='SIN'
    wave.inputs['Scale'].default_value=42.0
    links.new(coords.outputs['Object'],wave.inputs['Vector'])
    ramp=node(nt,'ShaderNodeMapRange','Scanline contrast',-620,-30)
    ramp.inputs['From Min'].default_value=0; ramp.inputs['From Max'].default_value=1
    ramp.inputs['To Min'].default_value=0.94; ramp.inputs['To Max'].default_value=1.0
    links.new(wave.outputs['Fac'],ramp.inputs['Value'])
    geometry=node(nt,'ShaderNodeNewGeometry','Front and back surfaces',-1100,-340)
    side=node(nt,'ShaderNodeMapRange','Front opacity / back opacity',-860,-340)
    side.inputs['From Min'].default_value=0; side.inputs['From Max'].default_value=1
    side.inputs['To Min'].default_value=opacity; side.inputs['To Max'].default_value=0.04 if color is None else opacity*0.22
    links.new(geometry.outputs['Backfacing'],side.inputs['Value'])
    opacity_node=node(nt,'ShaderNodeMath','Surface opacity',-380,-60); opacity_node.operation='MULTIPLY'
    links.new(ramp.outputs['Result'],opacity_node.inputs[0]); links.new(side.outputs['Result'],opacity_node.inputs[1])
    rim=node(nt,'ShaderNodeLayerWeight','Grazing surface rim',-850,200); rim.inputs['Blend'].default_value=0.33
    inv=node(nt,'ShaderNodeMath','Rim presence',-610,200); inv.operation='SUBTRACT'; inv.inputs[0].default_value=1
    links.new(rim.outputs['Facing'],inv.inputs[1])
    rim_gain=node(nt,'ShaderNodeMath','Rim gain',-380,200); rim_gain.operation='MULTIPLY'; rim_gain.inputs[1].default_value=0.04
    links.new(inv.outputs[0],rim_gain.inputs[0])
    total=node(nt,'ShaderNodeMath','Projected opacity',-120,20); total.operation='ADD'; total.use_clamp=True
    links.new(opacity_node.outputs[0],total.inputs[0]); links.new(rim_gain.outputs[0],total.inputs[1])
    links.new(total.outputs[0],mix.inputs[0]); links.new(transparent.outputs[0],mix.inputs[1]); links.new(shader.outputs[0],mix.inputs[2]); links.new(mix.outputs[0],out.inputs['Surface'])
    return mat

surface=make_surface('HOLO / muted red and grey panels')
glass=make_surface('HOLO / translucent smoke canopy',palette['CanopyGlass'],0.22,0.8)
core=make_surface('HOLO / cyan nacelle cores',palette['CoreGlow'],0.56,2.0)
ink=make_surface('HOLO / original structural panel seams',(0.38,0.55,0.62),0.66,1.1)
frame=make_surface('HOLO / muted red canopy frame',palette['CanopyFrame'],0.48,0.75)
for obj in copied:
    for i,old in enumerate(list(obj.data.materials)):
        obj.data.materials[i] = glass if old and old.name=='CanopyGlass' else core if old and old.name=='CoreGlow' else ink if old and old.name=='Contour' else frame if old and old.name=='CanopyFrame' else surface

edge_mat=bpy.data.materials.new('HOLO / bright contour'); edge_mat.use_nodes=True
edge_mat.diffuse_color=(0.63,0.83,0.88,1)
nt=edge_mat.node_tree; nt.nodes.clear()
emit=node(nt,'ShaderNodeEmission','Contour brightness',0,0); emit.inputs['Color'].default_value=(0.63,0.83,0.88,1); emit.inputs['Strength'].default_value=2.2
out=node(nt,'ShaderNodeOutputMaterial','Contour output',230,0); nt.links.new(emit.outputs[0],out.inputs['Surface'])
edge_count=0
for obj in copied:
    if 'structural ink' in obj.name or 'Core' in obj.name: continue
    mesh=obj.data
    edge_faces={}
    for face in mesh.polygons:
        for key in face.edge_keys: edge_faces.setdefault(tuple(sorted(key)),[]).append(face)
    chosen=[]
    for edge in mesh.edges:
        faces=edge_faces.get(tuple(sorted(edge.vertices)),[])
        visible=len(faces)!=2
        if len(faces)==2:
            visible=faces[0].normal.dot(faces[1].normal)<math.cos(math.radians(27.0)) or faces[0].material_index!=faces[1].material_index
        if visible: chosen.append(edge)
    if not chosen: continue
    curve=bpy.data.curves.new(obj.name+' contours','CURVE'); curve.dimensions='3D'; curve.resolution_u=1
    curve.bevel_depth=0.0048; curve.bevel_resolution=1
    for edge in chosen:
        spline=curve.splines.new('POLY'); spline.points.add(1)
        for i,index in enumerate(edge.vertices): spline.points[i].co=(*mesh.vertices[index].co,1)
    outline=bpy.data.objects.new(obj.name+' / bright contours',curve)
    collection.objects.link(outline); outline.parent=root; curve.materials.append(edge_mat)
    edge_count+=len(chosen)

for obj in list(bpy.data.objects):
    if obj.name not in collection.all_objects: bpy.data.objects.remove(obj,do_unlink=True)
for unused in list(bpy.data.collections):
    if unused != collection: bpy.data.collections.remove(unused)
for mat in list(bpy.data.materials):
    if mat not in (surface,glass,core,ink,frame,edge_mat): bpy.data.materials.remove(mat)
for image in list(bpy.data.images):
    if image != atlas: bpy.data.images.remove(image)
bpy.ops.outliner.orphans_purge(do_local_ids=True,do_linked_ids=True,do_recursive=True)
world=bpy.data.worlds.new('Hologram review dark blue'); world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(0.018,0.030,0.060,1)
world.node_tree.nodes['Background'].inputs[1].default_value=0.3; scene.world=world
review=bpy.data.collections.new('Review only'); scene.collection.children.link(review)
camera_data=bpy.data.cameras.new('Review camera'); camera=bpy.data.objects.new('Review camera',camera_data); review.objects.link(camera)
camera.location=(0,-9,10.3); camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler(); camera_data.type='ORTHO'; camera_data.ortho_scale=8.1; scene.camera=camera
for name,loc,power,color in [('Cool key',(0,-4,7),350,(0.60,0.80,1.0)),('Soft fill',(1,3,4),160,(0.72,0.80,1.0))]:
    d=bpy.data.lights.new(name,'AREA'); d.energy=power; d.shape='DISK'; d.size=7; d.color=color
    o=bpy.data.objects.new(name,d); review.objects.link(o); o.location=loc; o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'; scene.cycles.samples=48; scene.cycles.use_denoising=True
scene.render.resolution_x=1500; scene.render.resolution_y=850; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'; scene.view_settings.look='AgX - Medium High Contrast'; scene.view_settings.exposure=0.6
root.rotation_euler.x=math.radians(34)
bpy.context.view_layer.update()
bounds=[root.matrix_world @ v.co for o in copied for v in o.data.vertices]
posed_bounds=[max(v[i] for v in bounds)-min(v[i] for v in bounds) for i in range(3)]
root.rotation_euler.x=0
scene.render.filepath=str(OUT/'hologram-vanguard-review.png')
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'hologram-vanguard.blend'))
bpy.ops.render.render(write_still=True)
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==SOURCE_HASH
manifest={'source':str(SOURCE),'source_sha256':SOURCE_HASH,'objects':len(copied),'vertices':sum(len(o.data.vertices) for o in copied),'polygons':sum(len(o.data.polygons) for o in copied),'contour_segments':edge_count,'local_bounds':[6.0,(high.x-low.x)*scale,(high.z-low.z)*scale],'bank_34_bounds':posed_bounds,'collection':'HologramVanguard','root':'HologramVanguardRoot','packed_images':[i.name for i in bpy.data.images if i.packed_file],'source_unchanged':True}
(OUT/'hologram-vanguard-manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps(manifest,indent=2))
