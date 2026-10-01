import bpy, json, math, argparse, sys
from pathlib import Path
from mathutils import Vector, Matrix
parser=argparse.ArgumentParser()
parser.add_argument('--repo-root',type=Path,required=True)
parser.add_argument('--output-dir',type=Path,required=True)
parser.add_argument('--base',type=Path,required=True)
parser.add_argument('--resolution',type=int,default=4096)
parser.add_argument('--variant',choices=['rich','vivid'],default='vivid')
parser.add_argument('--source-output',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
ROOT, OUT=args.repo_root,args.output_dir
OUT.mkdir(parents=True,exist_ok=True)
BRUSH=ROOT/'art/ships/crimson/textures/painted-brush-source.png'
SOURCE=ROOT/'art/ships/vanguard/drawn-study/VanguardStructure.blend'
ORIGINAL=args.base
PALETTES={'rich':(.98,.48,.10),'vivid':(1,.36,.065)}
VARIANTS=[(args.variant,PALETTES[args.variant])]
def linear(rgb):
    return tuple(v/12.92 if v<.04045 else ((v+.055)/1.055)**2.4 for v in rgb)
def build_paint(obj,base,brush,orange,strong):
    mat=bpy.data.materials.new('Preview paint '+obj.name)
    mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    nodes.clear()
    def node(kind): return nodes.new(kind)
    def assign(value,input):
        if isinstance(value,(tuple,list,Vector)):
            input.default_value=tuple(value)+(1,) if len(value)==3 and input.type=='RGBA' else value
        elif isinstance(value,(int,float)): input.default_value=value
        else: links.new(value,input)
    def mathn(op,a,b=None):
        n=node('ShaderNodeMath');n.operation=op
        assign(a,n.inputs[0])
        if b is not None: assign(b,n.inputs[1])
        return n.outputs[0]
    def mix(f,a,b,mode='MIX'):
        n=node('ShaderNodeMixRGB');n.blend_type=mode
        for v,i in [(f,0),(a,1),(b,2)]: assign(v,n.inputs[i])
        return n.outputs[0]
    def tint(a,b): return mix(1,a,b,'MULTIPLY')
    uv=node('ShaderNodeUVMap');uv.uv_map='MVP_Atlas'
    tex=node('ShaderNodeTexImage');tex.image=base;tex.extension='EXTEND';links.new(uv.outputs['UV'],tex.inputs['Vector'])
    color=tex.outputs['Color']
    hsv=node('ShaderNodeSeparateColor');hsv.mode='HSV';links.new(color,hsv.inputs[0])
    orange_mask=mathn('MULTIPLY',mathn('GREATER_THAN',hsv.outputs[1],.6),mathn('MULTIPLY',mathn('GREATER_THAN',hsv.outputs[0],.025),mathn('LESS_THAN',hsv.outputs[0],.18)))
    ivory_mask=mathn('MULTIPLY',mathn('LESS_THAN',hsv.outputs[1],.30),mathn('GREATER_THAN',hsv.outputs[2],.35))
    color=mix(orange_mask,color,linear(orange))
    geometry=node('ShaderNodeNewGeometry')
    xyz=node('ShaderNodeSeparateXYZ');links.new(geometry.outputs['Position'],xyz.inputs[0])
    folded=node('ShaderNodeCombineXYZ')
    links.new(mathn('ABSOLUTE',xyz.outputs['X']),folded.inputs['X'])
    links.new(xyz.outputs['Y'],folded.inputs['Y']);links.new(xyz.outputs['Z'],folded.inputs['Z'])
    scale=node('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(.65,.65,.65);links.new(folded.outputs[0],scale.inputs[0])
    brush_tex=node('ShaderNodeTexImage');brush_tex.image=brush;brush_tex.projection='BOX';brush_tex.projection_blend=.25;brush_tex.extension='REPEAT';links.new(scale.outputs[0],brush_tex.inputs['Vector'])
    variation=mathn('ADD',mathn('MULTIPLY',brush_tex.outputs['Color'],1.1 if strong else .75),.27 if strong else .48)
    bounds=[obj.matrix_world@Vector(p) for p in obj.bound_box]
    minx,maxx=min(abs(v.x) for v in bounds),max(abs(v.x) for v in bounds)
    miny,maxy=min(v.y for v in bounds),max(v.y for v in bounds)
    sweep=mathn('ADD',mathn('MULTIPLY',mathn('ABSOLUTE',xyz.outputs['X']),.85),mathn('MULTIPLY',mathn('DIVIDE',mathn('SUBTRACT',xyz.outputs['Y'],miny),max(maxy-miny,.001)),.55))
    sweep=mathn('ADD',sweep,mathn('MULTIPLY',mathn('SUBTRACT',brush_tex.outputs['Color'],.65),.32))
    dark=mathn('LESS_THAN',sweep,.30)
    light=mathn('GREATER_THAN',sweep,.68)
    painted=tint(color,variation)
    painted=mix(mathn('MULTIPLY',dark,ivory_mask),painted,tint(painted,(.60,.64,.72) if strong else (.72,.75,.80)))
    painted=mix(mathn('MULTIPLY',light,ivory_mask),painted,tint(painted,(1.25,1.22,1.15)))
    normal=node('ShaderNodeSeparateXYZ');links.new(geometry.outputs['Normal'],normal.inputs[0])
    ramp=node('ShaderNodeValToRGB');ramp.color_ramp.interpolation='EASE'
    ramp.color_ramp.elements[0].position=.1;ramp.color_ramp.elements[0].color=(.38,.42,.5,1)
    ramp.color_ramp.elements[1].position=.93;ramp.color_ramp.elements[1].color=(1,1,1,1)
    mid=ramp.color_ramp.elements.new(.55);mid.color=(.68,.7,.75,1)
    links.new(mathn('ABSOLUTE',normal.outputs['Z']),ramp.inputs[0]);painted=tint(painted,ramp.outputs[0])
    paint_mask=mathn('MAXIMUM',orange_mask,ivory_mask)
    final=mix(paint_mask,color,painted)
    emission=node('ShaderNodeEmission');links.new(final,emission.inputs['Color'])
    output=node('ShaderNodeOutputMaterial');links.new(emission.outputs[0],output.inputs['Surface'])
    obj.data.materials.clear();obj.data.materials.append(mat)
    for p in obj.data.polygons:p.material_index=0
    return mat
manifest=[]
for name,orange in VARIANTS:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene=bpy.data.scenes['Vanguard - Texture MVP'];bpy.context.window.scene=scene
    meshes=[o for o in scene.objects if o.type=='MESH']
    painted=[o for o in meshes if 'MVP_Atlas' in o.data.uv_layers]
    base=bpy.data.images.load(str(ORIGINAL),check_existing=True)
    atlas=base
    if orange:
        brush=bpy.data.images.load(str(BRUSH));brush.colorspace_settings.name='Non-Color'
        materials=[build_paint(o,base,brush,orange,name=='vivid') for o in painted]
        atlas=bpy.data.images.new('Vanguard '+name,width=args.resolution,height=args.resolution,alpha=False);atlas.colorspace_settings.name='sRGB';atlas.generated_color=(.03,.03,.035,1)
        for mat in materials:
            target=mat.node_tree.nodes.new('ShaderNodeTexImage');target.image=atlas;mat.node_tree.nodes.active=target
        bpy.ops.object.select_all(action='DESELECT')
        for o in painted:o.select_set(True)
        bpy.context.view_layer.objects.active=painted[0]
        scene.render.engine='CYCLES';scene.cycles.samples=1;scene.cycles.use_denoising=False;scene.render.bake.margin=12
        bpy.ops.object.bake(type='EMIT',use_clear=True,margin=12)
        atlas.filepath_raw=str(OUT/(name+'-atlas.png'));atlas.file_format='PNG';atlas.save()
    mat=bpy.data.materials.new('Preview atlas');mat.use_nodes=True
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
    mat.node_tree.links.new(tex.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    for o in painted:
        o.data.materials.clear();o.data.materials.append(mat)
        o.data.uv_layers.active=o.data.uv_layers['MVP_Atlas']
        o.data.uv_layers['MVP_Atlas'].active_render=True
    for o in meshes:
        if not o.data.materials:
            detail=bpy.data.materials.new('Preview ink');detail.diffuse_color=(.015,.02,.03,1);o.data.materials.append(detail)
    for o in meshes:
        if o.name=='MVP Canopy':
            for material in o.data.materials:
                material.diffuse_color=(*linear((.025,.065,.12) if 'navy' in material.name else (.96,.47,.10)),1)
        if o.name=='MVP Power Nacelle Core':
            for material in o.data.materials:material.diffuse_color=(*linear((.025,.55,1)),1)
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_percentage=100
    sh=scene.display.shading;sh.light='FLAT';sh.color_type='TEXTURE';sh.show_shadows=False;sh.show_cavity=False;sh.cavity_type='BOTH';sh.show_object_outline=True;sh.object_outline_color=(.008,.01,.016);sh.background_type='WORLD';scene.world.color=(.045,.055,.08);scene.view_settings.view_transform='Standard'
    for o in meshes:
        if o.name=='Vanguard surface wear':o.hide_render=True
        if o.name=='Vanguard service panels':o.hide_render=True
    points=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
    center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
    data=bpy.data.cameras.new('Preview camera');cam=bpy.data.objects.new('Preview camera',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO'
    views=[]
    for pose,axis,up,size in [('top',(0,0,1),(0,1,0),1024),('hero',(1,1,1.5),(0,0,1),1024),('small',(0,0,1),(0,1,0),96)]:
        z=Vector(axis).normalized();x=Vector(up).cross(z).normalized();y=z.cross(x)
        cam.rotation_euler=Matrix((x,y,z)).transposed().to_euler();cam.location=center+z*8
        span=max(max((p-center).dot(v) for p in points)-min((p-center).dot(v) for p in points) for v in [x,y]);data.ortho_scale=span*1.15
        scene.render.resolution_x=size;scene.render.resolution_y=size;scene.render.filepath=str(OUT/(name+'-'+pose+'.png'))
        bpy.ops.render.render(write_still=True);views.append(scene.render.filepath)
    atlas.pack()
    atlas.filepath='//../../../../src/Asteroids3D/Assets/Visuals/Ships/Vanguard/DrawnStudy/VanguardBaseColor.png'
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:
            path=Path(bpy.path.abspath(image.filepath))
            if path.is_file():image.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(args.source_output))
    manifest.append(dict(name=name,orange=orange,views=views,atlas=str(OUT/(name+'-atlas.png')),source=str(args.source_output),resolution=args.resolution))
(OUT/'preview-manifest.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'manifest':str(OUT/'preview-manifest.json'),'variants':manifest}),flush=True)





