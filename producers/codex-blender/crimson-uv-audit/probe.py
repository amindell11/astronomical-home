import bpy, math, runpy, sys
from pathlib import Path
source=Path('C:/Users/amind/.codex/artifact-archives/crimson-uv-audit/measure.py').read_text(encoding='utf-8-sig')
probe='''
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    mesh=obj.data; mesh.calc_loop_triangles()
    la=wa=0.0
    for tri in mesh.loop_triangles:
        a,b,c=[mesh.vertices[i].co for i in tri.vertices]
        la+=(b-a).cross(c-a).length/2
        a,b,c=[obj.matrix_world@p for p in (a,b,c)]
        wa+=(b-a).cross(c-a).length/2
    factor=math.sqrt(wa/la)
    for loop in mesh.uv_layers.active.data: loop.uv*=factor
print('PROBE: corrected UV area for world-space scale in memory only; no blend saved')
'''
source=source.replace('rows=[]',probe+'\nrows=[]').replace('/measurement.json','/normalized-probe.json')
exec(compile(source,'density_probe','exec'))
