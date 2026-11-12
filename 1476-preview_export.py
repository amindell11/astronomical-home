"""Render an exported Console.fbx with its baked paint from the manifest camera, flat, as the game draws it.
blender -b --factory-startup --python preview_export.py -- --assets DIR --manifest JSON --out DIR --tag NAME [--launch .35]"""
import argparse, json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector, Matrix, Quaternion
p=argparse.ArgumentParser(); p.add_argument('--assets',required=True); p.add_argument('--manifest',required=True)
p.add_argument('--out',required=True); p.add_argument('--tag',required=True); p.add_argument('--launch',type=float,default=.35)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
assets=Path(a.assets).resolve(); out=Path(a.out).resolve(); out.mkdir(parents=True,exist_ok=True)
man=json.loads(Path(a.manifest).resolve().read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(assets/'Console.fbx'))
sc=bpy.context.scene
paint=bpy.data.images.load(str(assets/'ConsolePaint.png'))
def mat(name,image=None,color=(0,0,0,1)):
    m=bpy.data.materials.new(name); m.use_nodes=True; n=m.node_tree.nodes; n.clear()
    e=n.new('ShaderNodeEmission'); o=n.new('ShaderNodeOutputMaterial'); m.node_tree.links.new(e.outputs[0],o.inputs[0])
    if image:
        t=n.new('ShaderNodeTexImage'); t.image=image; m.node_tree.links.new(t.outputs[0],e.inputs[0]); n.active=t
    else: e.inputs[0].default_value=color
    m.diffuse_color=color
    return m
mp=mat('paint',paint); ml=mat('legend',color=(1,.88,.70,1)); mg=mat('glass',color=(.02,.02,.02,1))
objs={o.name:o for o in sc.objects if o.type=='MESH'}
print('PARTS',sorted(objs))
for name,o in objs.items():
    o.data.materials.clear()
    o.data.materials.append(mg if 'Glass' in name else ml if name.endswith('Legend') else mp)
cam_data=bpy.data.cameras.new('cam'); cam=bpy.data.objects.new('cam',cam_data); sc.collection.objects.link(cam); sc.camera=cam
c=man['camera']; f=Vector(c['forward']).normalized(); u=Vector(c['up']).normalized(); r=f.cross(u).normalized()
m=Matrix((r,u,-f)).transposed().to_4x4(); m.translation=Vector(c['position']); cam.matrix_world=m
cam_data.sensor_fit='VERTICAL'; cam_data.angle_y=math.radians(c['verticalFieldOfView']); cam_data.clip_start=.1; cam_data.clip_end=200
sc.render.engine='BLENDER_WORKBENCH'; sh=sc.display.shading; sh.light='FLAT'; sh.color_type='TEXTURE'
sc.render.film_transparent=False; sc.world=bpy.data.worlds.new('w'); sc.world.color=(.01,.012,.03)
sc.view_settings.view_transform='Standard'; sc.render.image_settings.file_format='PNG'
sc.display.render_aa='16'
sc.render.resolution_x=1920; sc.render.resolution_y=1080
rest={n:o.matrix_world.copy() for n,o in objs.items()}
bpy.context.view_layer.update()
def bounds_along(o,axis):
    vals=[(o.matrix_world@v.co).dot(axis) for v in o.data.vertices]; return max(vals)-min(vals)
PANEL=Vector((0.2747471034526825,-0.78243088722229,0.5588522553443909)); LAUNCH=Vector((-0.0995984897017479,-0.22541402280330658,0.9691587090492249))
children={'PreviousButton':['PreviousLegend'],'NextButton':['NextLegend'],'LaunchButton':['LaunchLegend']}
def pose(prev=0,nxt=0,launch=0,knob=0):
    for n,o in objs.items(): o.matrix_world=rest[n].copy()
    bpy.context.view_layer.update()
    for name,axis,share in (('PreviousButton',PANEL,prev),('NextButton',PANEL,nxt),('LaunchButton',LAUNCH,launch)):
        if name not in objs or not share: continue
        d=-axis*bounds_along(objs[name],axis)*share
        for n in [name]+children[name]:
            if n in objs:
                mm=rest[n].copy(); mm.translation=mm.translation+d; objs[n].matrix_world=mm
    if knob and 'SelectorKnob' in objs:
        o=objs['SelectorKnob']; piv=rest['SelectorKnob'].translation
        R=Matrix.Translation(piv)@Quaternion(PANEL,math.radians(knob)).to_matrix().to_4x4()@Matrix.Translation(-piv)
        o.matrix_world=R@rest['SelectorKnob']
    bpy.context.view_layer.update()
def shot(name,border=None,pct=100):
    sc.render.resolution_percentage=pct
    sc.render.use_border=bool(border); sc.render.use_crop_to_border=bool(border)
    if border: sc.render.border_min_x,sc.render.border_max_x,sc.render.border_min_y,sc.render.border_max_y=border
    sc.render.filepath=str(out/f'{a.tag}-{name}.png'); bpy.ops.render.render(write_still=True)
LEFT=(0.015,0.125,0.08,0.47); LAUNCHB=(0.76,0.97,0.02,0.22)
pose(); shot('full'); shot('left-rest',LEFT,400); shot('launch-rest',LAUNCHB,400)
pose(prev=.25,nxt=.25,launch=a.launch); shot('left-pressed',LEFT,400); shot('launch-pressed',LAUNCHB,400)
pose(launch=a.launch*.7); shot('launch-pressed70',LAUNCHB,400)
pose(knob=-40); shot('left-knob-minus',LEFT,400)
pose(knob=40); shot('left-knob-plus',LEFT,400)
print('PREVIEW_DONE')
