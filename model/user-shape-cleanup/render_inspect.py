import bpy,sys,json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
source,prefix=args
bpy.ops.wm.open_mainfile(filepath=source)
p=Path('D:/amind/git/agent-2/results/valis-cleanup')
s=bpy.context.scene
s.render.engine='BLENDER_WORKBENCH'
s.display.shading.light='STUDIO';s.display.shading.studio_light='paint.sl';s.display.shading.color_type='MATERIAL'
s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH'
s.display.shading.curvature_ridge_factor=.35;s.display.shading.curvature_valley_factor=.65
s.display.shading.show_object_outline=True;s.display.shading.object_outline_color=(.014,.018,.028)
s.display.shading.background_type='WORLD';s.world.color=(.047,.057,.080)
s.render.resolution_x=1300;s.render.resolution_y=1050;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.display.render_aa='16'
camdata=bpy.data.cameras.new('Cleanup review');cam=bpy.data.objects.new('Cleanup review',camdata);s.collection.objects.link(cam);s.camera=cam
camdata.type='ORTHO';camdata.ortho_scale=12.8
for name,pos in {'top':(0,0,25),'bottom':(0,0,-25),'front-quarter':(13,-18,17),'rear-quarter':(-13,18,17),'left':(25,0,0),'front':(0,-25,0)}.items():
 cam.location=pos;cam.rotation_euler=(0,0,0) if name=='top' else ((3.141592653589793,0,0) if name=='bottom' else (-cam.location).to_track_quat('-Z','Y').to_euler());s.render.filepath=str(p/(prefix+'-'+name+'.png'));bpy.ops.render.render(write_still=True)
records=[]
for o in s.objects:
 if o.type=='MESH':records.append({'name':o.name,'matrix':[list(row) for row in o.matrix_world],'vertices':[list(o.matrix_world@v.co) for v in o.data.vertices],'faces':[list(f.vertices) for f in o.data.polygons],'modifiers':[{'type':m.type,'thickness':m.thickness if m.type=='SOLIDIFY' else None} for m in o.modifiers]})
(p/(prefix+'-geometry.json')).write_text(json.dumps(records,indent=2))

