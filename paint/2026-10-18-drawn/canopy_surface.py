import bpy,json
from pathlib import Path
s=Path(__file__).parent;bpy.ops.wm.open_mainfile(filepath=str(s/'generation/Bulwark.blend'))
o=bpy.data.objects['Cockpit - Canopy'];e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh();m.calc_loop_triangles();uv=m.uv_layers['PaintUV']
d={'vertices':[list(e.matrix_world@v.co) for v in m.vertices],'triangles':[{'v':list(t.vertices),'uv':[list(uv.data[i].uv) for i in t.loops]} for t in m.loop_triangles]}
(s/'canopy-surface.json').write_text(json.dumps(d))
print('BOUNDS',*[tuple(f(v[i] for v in d['vertices']) for i in range(3)) for f in [min,max]])
