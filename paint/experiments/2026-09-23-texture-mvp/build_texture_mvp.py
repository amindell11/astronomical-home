import bpy
import json
import math
import numpy as np
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
SIZE = 4096
PAL = {'ivory':'E3DCCD','orange':'EA941F','dark':'353A50','belly':'535D70',
       'ink':'303347','line':'777477','red':'A9574D','blue':'279DFF',
       'cyan':'A5ECF2','glass':'151F3A','wear':'ADA89F'}

def rgb(key):
    h = PAL.get(key, key)
    return np.array([int(h[i:i+2],16)/255 for i in (0,2,4)],dtype=np.float32)

def linear(color):
    c = np.asarray(color)
    return np.where(c <= .04045,c/12.92,((c+.055)/1.055)**2.4)

manifest = json.loads((OUT/'source-manifest.json').read_text())
source = bpy.data.scenes[manifest['scene']]
bpy.context.window.scene = source
bpy.context.view_layer.update()
deps = bpy.context.evaluated_depsgraph_get()
copies = []
for name in manifest['visible_meshes']:
    if name == 'Tail Paint':
        continue
    original = source.objects[name]
    mesh = bpy.data.meshes.new_from_object(original.evaluated_get(deps),depsgraph=deps)
    mesh.transform(original.matrix_world)
    mesh.update()
    copies.append((name,mesh))

scene = bpy.data.scenes.new('Vanguard - Texture MVP')
bpy.context.window.scene = scene
objects = {}
for name,mesh in copies:
    obj = bpy.data.objects.new('MVP '+name,mesh)
    scene.collection.objects.link(obj)
    obj['source_object'] = name
    obj['source_snapshot'] = 'source-live.blend'
    objects[name]=obj

paint_objects = [obj for name,obj in objects.items() if name not in ('Canopy','Power Nacelle Core')]
bpy.ops.object.select_all(action='DESELECT')
for obj in paint_objects:
    obj.select_set(True)
    for uv in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(uv)
    obj.data.uv_layers.new(name='MVP_Atlas')
bpy.context.view_layer.objects.active=paint_objects[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),margin_method='FRACTION',
                         island_margin=.006,area_weight=.2,correct_aspect=False)
bpy.ops.object.mode_set(mode='OBJECT')
print('UV atlas ready',flush=True)

design = []
def shape(names,kind,points,color='line',width=.0014,side='top',opacity=1):
    design.append({'objects':names.split('|'),'kind':kind,'points':points,'color':color,
                   'width':width,'side':side,'opacity':opacity})

def line(names,points,color='line',width=.0014,side='top',opacity=1):
    shape(names,'line',points,color,width,side,opacity)

def rect(names,cx,cy,w,h,color='ink',side='top'):
    shape(names,'polygon',[[cx-w/2,cy-h/2],[cx+w/2,cy-h/2],
                           [cx+w/2,cy+h/2],[cx-w/2,cy+h/2]],color,side=side)

def tri(names,cx,cy,size,color='red',side='top'):
    shape(names,'polygon',[[cx,cy+size*.6],[cx-size*.5,cy-size*.4],
                           [cx+size*.5,cy-size*.4]],color,side=side)

wing_polygons={}
for name in ('Wing','wing_armor','Sparrow Tail','wing_tail'):
    mesh=objects[name].data
    faces=[p for p in mesh.polygons if p.normal.z>.65 and p.center.x>0]
    primary=max(faces,key=lambda p:p.area)
    polygon=np.array([mesh.vertices[i].co[:] for i in primary.vertices])[:,:2]
    wing_polygons[name]=polygon.tolist()
    if name in ('Wing','wing_armor'):
        inset=polygon.mean(axis=0)+(polygon-polygon.mean(axis=0))*.89
        for sign in (-1,1):
            pts=inset*np.array([sign,1])
            line(name,pts.tolist()+[pts[0].tolist()],width=.00115)

core=objects['Power Nacelle Core'].data
right=np.array([v.co[:] for v in core.vertices if v.co.x>0])
nacelle=right.mean(axis=0)
nx,ny=float(nacelle[0]),float(nacelle[1])

for sign in (-1,1):
    x=sign*nx
    # Flat mounting art wraps under the existing modeled nacelle ring.
    rect('Wing|wing_armor',x,ny,.177,.061,'dark')
    rect('Wing|wing_armor',x-sign*.079,ny,.005,.038,'cyan')
    line('Wing|wing_armor',[[x+sign*.083,ny-.024],[x+sign*.072,ny-.024],
                           [x+sign*.072,ny+.024],[x+sign*.083,ny+.024]],'ivory',.002)
    for offset in (-.009,.009):
        rect('wing_armor',sign*.342+offset,-.431,.008,.032)
    rect('Wing',sign*.502,-.28,.009,.055)
    rect('wing_armor',sign*.35,-.268,.008,.014,'red')
    line('wing_armor',[[sign*.28,-.257],[sign*.422,-.28]],width=.001)
    # One repeated glyph: two strokes and a small square, authored once per side.
    gx=sign*.352
    gy=.218
    for delta in (0,.012):
        line('Wing',[[gx+sign*delta,gy],[gx+sign*(delta+.01),gy+.013]],'ink',.004)
    rect('Wing',gx+sign*.029,gy+.002,.005,.005)
    line('Wing',[[sign*.34,-.19],[sign*.51,-.22]],width=.0012,side='bottom')
    rect('Wing',sign*.39,-.30,.014,.025,'dark',side='bottom')
    rect('Fuselage',sign*.09,.045,.005,.01,'red')
    tri('Cockpit.001',sign*.104,.463,.014)
    for yy in (.279,.299,.319):
        rect('Cockpit.001',sign*.104,yy,.019,.0045)
    line('Cockpit.001',[[sign*.119,.43],[sign*.145,.62],[sign*.113,.835],
                         [sign*.063,.929],[0,.949]],width=.00125)
    line('Cockpit.001',[[sign*.06,.337],[sign*.134,.36]],width=.0011)
    line('Fuselage',[[sign*.117,.006],[sign*.123,-.115],[sign*.076,-.202]],width=.0011)
    for yy in (-.32,-.73):
        line('Fuselage',[[sign*.01,yy],[sign*.049,yy]],width=.0012)
    line('Fuselage',[[sign*.032,-.3],[sign*.022,-.94]],width=.0011)
    line('Cockpit.001',[[sign*.077,.86],[sign*.122,.56],[sign*.11,.19]],width=.0012,side='bottom')
    line('Fuselage',[[sign*.09,.29],[sign*.096,.11],[sign*.061,-.18]],width=.0012,side='bottom')
    rect('Sparrow Tail',sign*.22,-.087,.014,.025,'dark',side='bottom')

# Port-only cockpit access: these three physical rungs cannot mirror accidentally.
for yy in (.54,.566,.592):
    line('Cockpit.001',[[-.161,yy],[-.139,yy]],'ink',.004)

# Short subdued scuffs, not all-over wear or bright chipped paint.
for sign in (-1,1):
    for x,y,length in ((.40,-.32,.026),(.33,-.47,.018),(.45,-.21,.024)):
        line('Wing|wing_armor',[[sign*x,y],[sign*(x+.002),y+length]],'wear',.0015,opacity=.45)
    for x,y in ((.13,.71),(.12,.4),(.067,-.72)):
        line('Cockpit.001|Fuselage',[[sign*x,y],[sign*(x+.0015),y+.026]],'wear',.0011,opacity=.5)

design_path=OUT/'paint-layout.json'
if design_path.exists():
    design=json.loads(design_path.read_text())['shapes']
else:
    design_path.write_text(json.dumps({'palette':PAL,'space':'model world XY in meters; Z up, nose +Y; negative X port','shapes':design},indent=2))
PAL.update(json.loads(design_path.read_text())['palette'])

def segment_distance(q,a,b):
    a=np.asarray(a); b=np.asarray(b); d=b-a
    t=np.clip(np.sum((q-a)*d,axis=1)/np.dot(d,d),0,1)
    return np.linalg.norm(q-a-t[:,None]*d,axis=1)

def path_distance(q,points):
    value=np.full(len(q),100.,dtype=np.float32)
    for a,b in zip(points[:-1],points[1:]):
        value=np.minimum(value,segment_distance(q,a,b))
    return value

def inside(q,points):
    hit=np.zeros(len(q),dtype=bool)
    for a,b in zip(points,points[1:]+points[:1]):
        if abs(b[1]-a[1])<1e-12:
            continue
        hit ^= ((a[1]>q[:,1])!=(b[1]>q[:,1])) & (q[:,0] < (b[0]-a[0])*(q[:,1]-a[1])/(b[1]-a[1])+a[0])
    return hit

def paint(name,p,normal):
    x,y,z=p.T
    ax=np.abs(x)
    q=p[:,:2]
    n=len(p)
    upper=normal[2]>.28
    lower=normal[2]<-.28
    color=np.tile(rgb('ivory'),(n,1))
    if lower:
        color[:]=rgb('belly')
    elif not upper and name not in ('Tail','Power Nacelle Housing'):
        color[:]=rgb('belly')*.88
    if name=='wing_armature':
        color[:]=rgb('dark') if upper else rgb('belly')*.82
    if name=='Sparrow Tail':
        color[:]=rgb('ivory') if upper else rgb('dark')
    if name=='Tail':
        color[:]=rgb('ivory')
        color[(y<-.61)&(z>.277)]=rgb('orange')
        yz=p[:,[1,2]]
        seam=path_distance(yz,[[-.62,.151],[-.569,.236],[-.33,.095]])<.0015
        color[seam]=rgb('line')
    if name=='wing_tail':
        color[:]=rgb('orange') if upper or lower else rgb('dark')
    if name=='Fuselage' and upper:
        stripe=(y<-.25)&(y>-.67)
        color[stripe]=rgb('orange')
        color[stripe&((np.abs(y+.525)<.011)|(np.abs(y+.596)<.011))]=rgb('ivory')
    if name in ('Wing','wing_armor') and (upper or lower):
        poly=wing_polygons[name]
        # Select the two outboard contour segments of each main wing plate.
        distance=path_distance(np.column_stack([ax,y]),[poly[-2],poly[-1],poly[0]])
        orange=distance < (.045 if name=='Wing' else .055)
        color[orange]=rgb('orange')
        if upper:
            clear=(ax-nx)**2+(y-ny)**2 < .079**2
            color[clear]=rgb('ivory')
    if name=='Power Nacelle Housing':
        color[:]=rgb('ivory') if normal[2]>.55 else rgb('dark')
        tick=(np.abs(y-(ny-.057))<.008)&(np.abs(ax-nx)<.003)
        color[tick]=rgb('ink')
    for entry in design:
        if name not in entry['objects']:
            continue
        if entry['side']=='top' and not upper:
            continue
        if entry['side']=='bottom' and not lower:
            continue
        if entry['kind']=='line':
            mask=path_distance(q,entry['points'])<entry['width']/2
        else:
            mask=inside(q,entry['points'])
        alpha=entry['opacity']
        color[mask]=color[mask]*(1-alpha)+rgb(entry['color'])*alpha
    # Broad, barely visible pigment variation instead of pixel grain.
    variation=1+.005*np.sin(x*51+y*38)+.003*np.sin(x*93-y*17)
    return np.clip(color*variation[:,None],0,1)

atlas=np.zeros((SIZE,SIZE,4),dtype=np.float32)
atlas[:,:,:3]=rgb('dark')
atlas[:,:,3]=1
coverage=np.zeros((SIZE,SIZE),dtype=bool)
uv_lines=[]
for obj in paint_objects:
    mesh=obj.data
    name=obj['source_object']
    mesh.calc_loop_triangles()
    uv=mesh.uv_layers.active.data
    vertices=np.array([v.co[:] for v in mesh.vertices],dtype=np.float32)
    for tri_face in mesh.loop_triangles:
        coords=np.array([uv[i].uv[:] for i in tri_face.loops])*SIZE
        lo=np.maximum(np.floor(coords.min(axis=0)).astype(int),0)
        hi=np.minimum(np.ceil(coords.max(axis=0)).astype(int),SIZE-1)
        if np.any(hi<lo): continue
        gx,gy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        a,b,c=coords
        denom=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(denom)<1e-8: continue
        u=((b[1]-c[1])*(gx-c[0])+(c[0]-b[0])*(gy-c[1]))/denom
        v=((c[1]-a[1])*(gx-c[0])+(a[0]-c[0])*(gy-c[1]))/denom
        w=1-u-v
        mask=(u>=-1e-6)&(v>=-1e-6)&(w>=-1e-6)
        if not mask.any(): continue
        points=np.column_stack([u[mask],v[mask],w[mask]])@vertices[list(tri_face.vertices)]
        ys=gy[mask].astype(int);xs=gx[mask].astype(int)
        atlas[ys,xs,:3]=paint(name,points,np.array(mesh.polygons[tri_face.polygon_index].normal))
        coverage[ys,xs]=True
    for poly in mesh.polygons:
        pts=[uv[i].uv[:] for i in poly.loop_indices]
        uv_lines.append(pts)
    print('Painted '+name,flush=True)

# Pad island colors into their gutters for filtered sampling.
for step in range(12):
    add=np.zeros_like(coverage)
    for axis,shift in ((0,1),(0,-1),(1,1),(1,-1)):
        valid=np.roll(coverage,shift,axis=axis)&~coverage&~add
        rolled=np.roll(atlas,shift,axis=axis)
        atlas[valid]=rolled[valid]
        add|=valid
    coverage|=add
textures=OUT/'textures'
textures.mkdir(exist_ok=True)
image=bpy.data.images.new('Vanguard MVP - Base Color',width=SIZE,height=SIZE,alpha=True)
image.colorspace_settings.name='sRGB'
image.pixels.foreach_set(atlas.ravel())
image.filepath_raw=str(textures/'vanguard-basecolor.png')
image.file_format='PNG'
image.save()
del atlas
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="4096" height="4096" viewBox="0 0 4096 4096">','<g fill="none" stroke="#33BBD0" stroke-width="1">']
for poly in uv_lines:
    points=' '.join(f'{u*SIZE:.2f},{(1-v)*SIZE:.2f}' for u,v in poly)
    svg.append(f'<polygon points="{points}"/>')
svg+=['</g>','</svg>']
(textures/'vanguard-uv-guide.svg').write_text('\n'.join(svg))

def toon(name,texture=None,color=None):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    nodes=mat.node_tree.nodes; links=mat.node_tree.links
    nodes.clear()
    out=nodes.new('ShaderNodeOutputMaterial');out.location=(920,0)
    diffuse=nodes.new('ShaderNodeBsdfDiffuse');diffuse.location=(-700,-180)
    diffuse.inputs['Color'].default_value=(1,1,1,1)
    shade=nodes.new('ShaderNodeShaderToRGB');shade.location=(-490,-180)
    links.new(diffuse.outputs[0],shade.inputs[0])
    ramp=nodes.new('ShaderNodeValToRGB');ramp.location=(-260,-180)
    ramp.color_ramp.interpolation='CONSTANT'
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    for i,(position,value) in enumerate(((0,.36),(.23,.63),(.55,.86),(.85,1.))):
        el=ramp.color_ramp.elements[0] if i==0 else ramp.color_ramp.elements.new(position)
        el.position=position;el.color=(value,value,value,1)
    links.new(shade.outputs[0],ramp.inputs[0])
    if texture:
        base=nodes.new('ShaderNodeTexImage');base.image=texture;base.location=(-490,180)
        base.interpolation='Linear'
    else:
        base=nodes.new('ShaderNodeRGB');base.location=(-490,180)
        base.outputs[0].default_value=(*linear(rgb(color)),1)
    multiply=nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY'
    multiply.inputs[0].default_value=1;multiply.location=(0,100)
    links.new(base.outputs[0],multiply.inputs[1]);links.new(ramp.outputs[0],multiply.inputs[2])
    fresnel=nodes.new('ShaderNodeFresnel');fresnel.inputs['IOR'].default_value=1.25;fresnel.location=(0,-240)
    threshold=nodes.new('ShaderNodeMath');threshold.operation='GREATER_THAN';threshold.inputs[1].default_value=.48
    threshold.location=(210,-230);links.new(fresnel.outputs[0],threshold.inputs[0])
    ink=nodes.new('ShaderNodeMixRGB');ink.location=(430,90)
    links.new(threshold.outputs[0],ink.inputs[0]);links.new(multiply.outputs[0],ink.inputs[1])
    ink.inputs[2].default_value=(*linear(rgb('ink')),1)
    emit=nodes.new('ShaderNodeEmission');emit.location=(680,80)
    links.new(ink.outputs[0],emit.inputs[0]);links.new(emit.outputs[0],out.inputs['Surface'])
    return mat

hull=toon('Vanguard | Painted atlas + preview cel shading',texture=image)
for obj in paint_objects:
    obj.data.materials.clear();obj.data.materials.append(hull)
    for p in obj.data.polygons:p.material_index=0
glass=toon('Vanguard | Canopy navy',color='glass')
orange=toon('Vanguard | Canopy orange surround',color='orange')
canopy=objects['Canopy']
canopy.data.materials.clear();canopy.data.materials.append(glass);canopy.data.materials.append(orange)

emission=bpy.data.materials.new('Vanguard | Blue core emission - bloom separate')
emission.use_nodes=True
nodes=emission.node_tree.nodes;nodes.clear()
em=nodes.new('ShaderNodeEmission');em.inputs['Color'].default_value=(*linear(rgb('blue')),1)
em.inputs['Strength'].default_value=2.5
output=nodes.new('ShaderNodeOutputMaterial');emission.node_tree.links.new(em.outputs[0],output.inputs[0])
objects['Power Nacelle Core'].data.materials.clear();objects['Power Nacelle Core'].data.materials.append(emission)
for p in objects['Power Nacelle Core'].data.polygons:p.material_index=0
emission['authoring_note']='Uniform blue emission. Glow belongs to render bloom; none is painted into base color.'

scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1600;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.world=bpy.data.worlds.new('Vanguard preview world')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.055,.082,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
scene.view_settings.exposure=0;scene.view_settings.gamma=1
lightdata=bpy.data.lights.new('Large soft key','AREA');lightdata.energy=450;lightdata.shape='DISK';lightdata.size=5
light=bpy.data.objects.new('Large soft key',lightdata);scene.collection.objects.link(light)
light.location=(-3,2.5,5);light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
filldata=bpy.data.lights.new('Cool fill','AREA');filldata.energy=170;filldata.size=4
fill=bpy.data.objects.new('Cool fill',filldata);scene.collection.objects.link(fill)
fill.location=(4,-2,2);fill.rotation_euler=(-fill.location).to_track_quat('-Z','Y').to_euler()
camera_data=bpy.data.cameras.new('MVP review camera');camera=bpy.data.objects.new('MVP review camera',camera_data)
scene.collection.objects.link(camera);scene.camera=camera;camera_data.type='ORTHO'
camera_data.clip_start=.01;camera_data.clip_end=100

all_points=[v.co.copy() for o in objects.values() for v in o.data.vertices]
minimum=Vector(tuple(min(p[i] for p in all_points) for i in range(3)))
maximum=Vector(tuple(max(p[i] for p in all_points) for i in range(3)))
center=(minimum+maximum)/2
views=[('top',(0,0,1)),('front-quarter',(1.15,1.6,1.25)),
       ('rear-quarter',(-1.15,-1.6,1.05)),('underside',(0,0,-1)),('front',(0,1,.12))]
renders=OUT/'renders';renders.mkdir(exist_ok=True)
def pose(direction):
    direction=Vector(direction).normalized()
    camera.location=center+direction*8
    camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    bpy.context.view_layer.update()
    inverse=camera.matrix_world.inverted()
    projected=[inverse@p for p in all_points]
    width=max(p.x for p in projected)-min(p.x for p in projected)
    height=max(p.y for p in projected)-min(p.y for p in projected)
    camera_data.ortho_scale=max(width,height*scene.render.resolution_x/scene.render.resolution_y)*1.12
    midpoint=Vector(((max(p.x for p in projected)+min(p.x for p in projected))/2,
                     (max(p.y for p in projected)+min(p.y for p in projected))/2,0))
    camera.location+=camera.matrix_world.to_3x3()@midpoint
for label,direction in views:
    pose(direction)
    scene.render.filepath=str(renders/(label+'.png'))
    bpy.ops.render.render(write_still=True)
    print('Rendered '+label,flush=True)
pose((1.15,1.6,1.25))
scene.render.filepath='//renders/front-quarter.png'
bpy.ops.object.select_all(action='DESELECT')
objects['Fuselage'].select_set(True);bpy.context.view_layer.objects.active=objects['Fuselage']
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_perspective='CAMERA'
image.filepath='//textures/vanguard-basecolor.png'
scene['MVP_scope']='Textured review copy; source scene retained. No Unity assets changed.'
scene['paint_source']='paint-layout.json; original surface-coordinate primitives rasterized to new atlas.'
scene['preview_shader']='Eevee cel shading is a Blender preview; base color texture remains light independent.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'vanguard-textured-v1.blend'),relative_remap=True)
report={'blend':str(OUT/'vanguard-textured-v1.blend'),'atlas':str(textures/'vanguard-basecolor.png'),
        'resolution':SIZE,'atlas_objects':[o.name for o in paint_objects],
        'objects':{name:{'vertices':len(o.data.vertices),'faces':len(o.data.polygons)} for name,o in objects.items()},
        'renders':[str(renders/(name+'.png')) for name,_ in views],
        'emission_strength':2.5,'source':manifest}
(OUT/'build-report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report),flush=True)
