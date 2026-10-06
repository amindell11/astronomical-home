from pathlib import Path
import bpy,sys
from mathutils import Vector
root=Path('D:/amind/git/agent-1');out=root/'results/nightshade-aft-taper/round-02/focus'
out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_source,ship_render
for label,path in [('before',out.parent.parent/'before/owner-saved.blend'),('after',root/'art/ships/nightshade/Nightshade.blend')]:
    scene=ship_source.open_source(path)
    evaluation=ship_source.Evaluation(scene);evaluation.show_only(['Rebuilt pitched hull'])
    scene=evaluation.scene;ship_source.apply_review_preset(scene,1327)
    scene.render.resolution_x=1327;scene.render.resolution_y=274
    scene.render.film_transparent=True
    s=scene.display.shading;s.light='STUDIO';s.color_type='SINGLE'
    s.single_color=(.25,.35,.42);s.show_shadows=False;s.show_cavity=False
    s.show_specular_highlight=False
    camera=bpy.data.objects.new('aft review camera',bpy.data.cameras.new('aft review camera'))
    scene.collection.objects.link(camera);scene.camera=camera
    camera.data.type='ORTHO';camera.data.ortho_scale=1.9
    camera.matrix_world=ship_render.camera_matrix((-1,0,0),(0,0,1),Vector((0,-.066,.018)),3)
    scene.render.filepath=str(out/(label+'.png'))
    bpy.ops.render.render(write_still=True)

