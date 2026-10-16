import bpy,json,sys
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).parent))
from base_palette import convex_hull
source=Path('D:/amind/git/agent-2/art/ships/vanguard/drawn-study/VanguardStudy.blend')
bpy.ops.wm.open_mainfile(filepath=str(source))
scene=bpy.data.scenes['Vanguard - Texture MVP'];bpy.context.window.scene=scene
wing_polygons={}
for name in ('Wing','wing_armor'):
    obj=scene.objects['MVP '+name]
    primary=max([p for p in obj.data.polygons if p.normal.z>.65 and p.center.x>0],key=lambda p:p.area)
    wing_polygons[name]=[list((obj.matrix_world@obj.data.vertices[i].co)[:2]) for i in primary.vertices]
core=scene.objects['MVP Power Nacelle Core']
right=np.array([core.matrix_world@v.co for v in core.data.vertices if (core.matrix_world@v.co).x>0])
nx,ny=right.mean(axis=0)[:2]
canopy=scene.objects['MVP Canopy']
glass_vertices={i for face in canopy.data.polygons if face.material_index==0 for i in face.vertices}
canopy_outline=convex_hull([tuple((canopy.matrix_world@canopy.data.vertices[i].co)[:2]) for i in glass_vertices])
out=Path(__file__).with_name('placement-parameters.json')
out.write_text(json.dumps({'wing_polygons':wing_polygons,'core_center':[float(nx),float(ny)],'canopy_outline':canopy_outline},indent=2)+'\n')
print('PLACEMENT_PARAMETERS='+str(out))
