import bpy, json, math, statistics
from pathlib import Path
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/astronomical-home/art/ships/crimson/Crimson.blend')
rows=[]
dg=bpy.context.evaluated_depsgraph_get()
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    ev=obj.evaluated_get(dg); mesh=ev.to_mesh(); mesh.calc_loop_triangles(); uv=mesh.uv_layers.active.data
    world_area=local_area=uv_area=0.0
    for tri in mesh.loop_triangles:
        a,b,c=[mesh.vertices[i].co for i in tri.vertices]
        local_area+=(b-a).cross(c-a).length/2
        a,b,c=[obj.matrix_world@p for p in (a,b,c)]
        world_area+=(b-a).cross(c-a).length/2
        a,b,c=[uv[i].uv for i in tri.loops]
        ab=b-a; ac=c-a
        uv_area+=abs(ab.x*ac.y-ab.y*ac.x)/2
    rows.append(dict(name=obj.name,scale=list(obj.scale),world_scale=list(obj.matrix_world.to_scale()),world_area=world_area,local_area=local_area,uv_area=uv_area,density=4096*math.sqrt(uv_area/world_area),local_density=4096*math.sqrt(uv_area/local_area)))
    ev.to_mesh_clear()
rows.sort(key=lambda r:-r['density'])
Path('C:/Users/amind/.codex/artifact-archives/crimson-uv-audit/measurement.json').write_text(json.dumps(rows,indent=2))
median=statistics.median(r['density'] for r in rows)
for r in rows[:5]: print('AUDIT',json.dumps(r))
ratio=rows[0]['density']/median
print('AUDIT median',median,'maximum_ratio',ratio)
assert ratio<2, f'DENSITY_FAIL: {rows[0]["name"]} has {ratio:.3f}x median density (investigation threshold 2x)'
