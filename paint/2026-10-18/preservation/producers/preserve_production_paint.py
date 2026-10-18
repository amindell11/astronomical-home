import bpy,json,hashlib,sys
from pathlib import Path
from types import SimpleNamespace
import numpy as np
from mathutils import Vector,Matrix

ROOT=Path('D:/amind/git/astronomical-home')
HERE=Path(__file__).parent
OUT=HERE.parent/'review'
TOOLS=HERE.parents[1]/'paint-tool-snapshot/art/tools/ship'
sys.path.insert(0,str(TOOLS))
import ship_paint,ship_render,ship_source

SOURCE=ROOT/'art/ships/vanguard/drawn-study/VanguardPainted.blend'
BRUSH=ROOT/'art/ships/crimson/textures/painted-brush-source.png'
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
before_hash=sha(SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.data.scenes['Vanguard - Texture MVP'];bpy.context.window.scene=scene
visible=[o for o in scene.objects if o.type=='MESH' and not o.hide_render]
painted=[o for o in visible if 'MVP_Atlas' in o.data.uv_layers]
assert len(painted)==9, len(painted)
base=bpy.data.images.load(str(OUT/'clean-base.png'),check_existing=False)
brush=bpy.data.images.load(str(BRUSH),check_existing=False)
brush.colorspace_settings.name='Non-Color'
paint={
    'brush':{'scale':1.3,'contrast':1.2,'blend':.25},
    'side_wall_depth':0.,'ramp':[[.10,.38],[.55,.64],[.92,1.]],
    'canopy_glass':[],'canopy_frames':[],
}
flags={'plate':False,'sweep':False,'ink_walls':False}
material,graph=ship_paint.build_material(SimpleNamespace(name='Preserved Vanguard hull',thickness=1.),None,'Original artwork',flags,paint,brush,[1.,1.,1.],None,[])

bpy.ops.object.select_all(action='DESELECT')
copies=[]
depsgraph=bpy.context.evaluated_depsgraph_get()
for obj in painted:
    mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph),preserve_all_data_layers=True,depsgraph=depsgraph)
    copy=bpy.data.objects.new('review brush '+obj.name,mesh)
    scene.collection.objects.link(copy);copy.matrix_world=obj.matrix_world.copy()
    copy.select_set(True);copies.append(copy)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.object.join()
surface=bpy.context.object
surface.data.materials.clear();surface.data.materials.append(material)
for face in surface.data.polygons:face.material_index=0
surface.data.uv_layers.active=surface.data.uv_layers['MVP_Atlas']
surface.data.uv_layers['MVP_Atlas'].active_render=True
target=bpy.data.images.new('review only canonical brush',4096,4096,alpha=True,float_buffer=True)
target.colorspace_settings.name='Non-Color'
weights=ship_paint.bake(scene,surface,{'brush':(material,graph)},target,'masks',12)
bpy.data.objects.remove(surface,do_unlink=True)
bpy.data.images.remove(target)
pixels=np.empty(len(base.pixels),np.float32);base.pixels.foreach_get(pixels)
base_pixels=pixels.reshape(4096,4096,4)
shadow=weights[...,:1].astype(np.float32)/255
light=weights[...,1:2].astype(np.float32)/255
color=base_pixels[...,:3]*(1-shadow)
lift=base_pixels[...,:3]+(1-base_pixels[...,:3])*.25
color=color*(1-light)+lift*light
composite=np.concatenate([color,np.ones((4096,4096,1),np.float32)],axis=2)
composite_path=OUT/'PreservedArtwork-Brush-Composite.png'
ship_source.save_pixels(composite_path,np.clip(composite,0,1))
composite_image=bpy.data.images.load(str(composite_path),check_existing=False)

def review_material(name,image):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
    mat.node_tree.links.new(tex.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    mat.node_tree.nodes.active=tex
    return mat

points=[obj.matrix_world@Vector(p) for obj in visible for p in obj.bound_box]
center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
data=bpy.data.cameras.new('preserved paint review');camera=bpy.data.objects.new('preserved paint review',data)
scene.collection.objects.link(camera);scene.camera=camera;data.type='ORTHO';data.clip_start=.01;data.clip_end=100

def render(name,size,axis,up,focus=None,span=None):
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=scene.render.resolution_y=size
    scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
    scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=False
    sh=scene.display.shading;sh.light='FLAT';sh.color_type='TEXTURE';sh.show_shadows=False;sh.show_cavity=False
    sh.show_object_outline=True;sh.object_outline_color=(.008,.01,.016);sh.background_type='WORLD'
    scene.world.color=(.045,.055,.08);scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    scene.display.render_aa='32'
    z=Vector(axis).normalized();x=Vector(up).cross(z).normalized();y=z.cross(x)
    focus=Vector(focus) if focus is not None else center
    camera.rotation_euler=Matrix((x,y,z)).transposed().to_euler();camera.location=focus+z*8
    if span is None:span=max(max((p-focus).dot(v) for p in points)-min((p-focus).dot(v) for p in points) for v in [x,y])*1.15
    data.ortho_scale=span
    path=OUT/(name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
    return path

original={o.name:([slot.material for slot in o.material_slots],[f.material_index for f in o.data.polygons]) for o in painted}
variants=[('original',None),('preserved-flat',base),('preserved-brush',composite_image)]
written={}
for name,image in variants:
    for obj in painted:
        mats,indices=original[obj.name]
        obj.data.materials.clear()
        if image is None:
            for mat in mats:obj.data.materials.append(mat)
            for face,index in zip(obj.data.polygons,indices):face.material_index=index
        else:
            obj.data.materials.append(review_material(name+' '+obj.name,image))
            for face in obj.data.polygons:face.material_index=0
        obj.data.uv_layers.active=obj.data.uv_layers['MVP_Atlas']
        obj.data.uv_layers['MVP_Atlas'].active_render=True
    written[name]={}
    for view,axis,up in [('top',(0,0,1),(0,1,0)),('hero',(1,1,1.5),(0,0,1))]:
        written[name][view]=render(name+'_'+view,1024,axis,up)
    written[name]['scale']=render(name+'_scale96',96,(0,0,1),(0,1,0))
    written[name]['pod']=render(name+'_pod',640,(0,0,1),(0,1,0),(.38,-.25,.3),.62)

ship_source.save_pixels(OUT/'old-versus-preserved.png',ship_render.grid([ship_source.load_pixels(written[v][p]) for p in ('top','hero') for v in ('original','preserved-brush')],2))
ship_source.save_pixels(OUT/'paint-separation.png',ship_render.grid([ship_source.load_pixels(written[v][p]) for p in ('top','hero') for v,_ in variants],3))
ship_source.save_pixels(OUT/'pod-detail-preservation.png',ship_render.grid([ship_source.load_pixels(written[v]['pod']) for v,_ in variants],3))
ship_source.save_pixels(OUT/'preserved-scale.png',ship_render.grid([ship_source.load_pixels(written[v]['scale']).repeat(4,axis=0).repeat(4,axis=1) for v,_ in variants],3))
assert sha(SOURCE)==before_hash,'Production source was changed'
layout=json.loads((HERE/'inputs/legacy-paint-layout.json').read_text(encoding='utf-8-sig'))
service=json.loads((ROOT/'art/ships/vanguard/drawn-study/service-panels.json').read_text())
record={
    'source':str(SOURCE),'source_sha256':before_hash,'source_unchanged':True,'source_saved':False,
    'original_artwork':'Recovered production generate_base.py, base_palette.py, legacy-paint-layout.json and service-panels.json; original structural ink meshes retained',
    'authored_shapes_kept':len([s for s in layout['shapes'] if s['color'] not in ('line','wear')]),
    'service_patches_kept':len(service['patches']),
    'palette_change':'Only restore the selected Vivid orange (#FF5C11) in the recovered clean base',
    'removed':'Old paint_vanguard.py diagonal dark/light sweep and normal ramp are absent from the clean base',
    'added':'Canonical ship_paint.py brush Shadow/Light calculation; no regenerated boundaries, no new plate lines, no sweep',
    'canonical_tool_commit':'6b2f743613851747d38e5c08e925c90240e894b9 (ship_paint.py unchanged from df12417 snapshot)',
    'brush_sha256':sha(BRUSH),'brush_settings':paint['brush'],'brush_mid':ship_paint.BRUSH_MID,
    'geometry_edited':False,'uv_edited':False,'texture_masks_written':False,'runtime_assets_changed':False,
    'review_only':True,'variants':[name for name,_ in variants],
}
(OUT/'preservation-review.json').write_text(json.dumps(record,indent=2)+'\n')
print('PRESERVED_PAINT_REVIEW='+str(OUT/'old-versus-preserved.png'))
