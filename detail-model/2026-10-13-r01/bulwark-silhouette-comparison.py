import bpy,sys,json,hashlib
from pathlib import Path
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_render
root=Path('D:/amind/git/agent-3/results/bulwark/coherent-cleanup')
out=root/'comparison';out.mkdir(exist_ok=True)
sources={'owner':Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend'),'refined':root/'refined/Bulwark-refined.blend'}
views={'top':((0,0,1),(1,0,0),(0,0,1.5),11.1),'side':((-1,0,0),(0,0,1),(0,0,1.8),11.1),'front':((0,1,0),(0,0,1),(0,0,1.7),7.5),'rear':((0,-1,0),(0,0,1),(0,0,1.7),7.5),'hero':((10,15,8.5),(0,0,1),(0,0,1.5),14),'rear-quarter':((9,-16,7),(0,0,1),(0,-.4,1.5),13.5)}
masks={};hashes={}
for label,path in sources.items():
 hashes[label]=hashlib.sha256(path.read_bytes()).hexdigest()
 source_scene=ship_source.open_source(path)
 parts=[o.name for o in source_scene.objects if o.type=='MESH' and not o.hide_render]
 evaluation=ship_source.Evaluation(source_scene);evaluation.show_only(parts);scene=evaluation.scene
 ship_source.apply_review_preset(scene,768)
 scene.render.film_transparent=True
 shading=scene.display.shading;shading.light='FLAT';shading.color_type='SINGLE';shading.single_color=(1,1,1);shading.show_shadows=False;shading.show_cavity=False;shading.show_object_outline=False
 camera=bpy.data.objects.new('Silhouette camera',bpy.data.cameras.new('Silhouette camera'));scene.collection.objects.link(camera);scene.camera=camera
 camera.data.type='ORTHO';camera.data.clip_start=.01;camera.data.clip_end=100
 masks[label]={}
 for name,(direction,up,center,scale) in views.items():
  camera.matrix_world=ship_render.camera_matrix(direction,up,center,30);camera.data.ortho_scale=scale
  file=out/(label+'-'+name+'.png');scene.render.filepath=str(file);bpy.ops.render.render(write_still=True)
  pixels=ship_render.load_pixels(file);masks[label][name]=pixels[:,:,3]>.5
 assert hashlib.sha256(path.read_bytes()).hexdigest()==hashes[label]
metrics={}
for name in views:
 a,b=masks['owner'][name],masks['refined'][name]
 metrics[name]={'intersection_over_union':float((a&b).sum()/(a|b).sum()),'changed_pixels':int((a^b).sum()),'owner_pixels':int(a.sum()),'refined_pixels':int(b.sum())}
report={'source_sha256':hashes,'views':metrics,'all_source_files_unchanged':True,'minimum_overlap':min(x['intersection_over_union'] for x in metrics.values())}
(out/'silhouette-comparison.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
