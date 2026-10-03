import bpy, json, ast, sys, numpy as np
from pathlib import Path
from mathutils import Vector
import argparse
parser=argparse.ArgumentParser(description='Regenerate clean Vanguard paint in the existing UV layout.')
parser.add_argument('--repo-root',type=Path,required=True)
parser.add_argument('--output-dir',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
ROOT,OUT=args.repo_root,args.output_dir
OUT.mkdir(parents=True,exist_ok=True)
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
layout=json.loads((HERE/'inputs/legacy-paint-layout.json').read_text())
import base_palette as palette
palette.PAL=layout['palette']
palette.design=[s for s in layout['shapes'] if s['color'] not in ('line','wear')]
rgb,inside,convex_hull=palette.rgb,palette.inside,palette.convex_hull
paint=palette.paint
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/ships/vanguard/drawn-study/VanguardStudy.blend'))
scene=bpy.data.scenes['Vanguard - Texture MVP'];bpy.context.window.scene=scene
wing_polygons={}
for name in ('Wing','wing_armor'):
    obj=scene.objects['MVP '+name]
    faces=[p for p in obj.data.polygons if p.normal.z>.65 and p.center.x>0]
    primary=max(faces,key=lambda p:p.area)
    wing_polygons[name]=np.array([obj.matrix_world@obj.data.vertices[i].co for i in primary.vertices])[:,:2].tolist()
core=scene.objects['MVP Power Nacelle Core']
right=np.array([core.matrix_world@v.co for v in core.data.vertices if (core.matrix_world@v.co).x>0])
nx,ny=right.mean(axis=0)[:2]
palette.nx,palette.ny=nx,ny
palette.wing_polygons=wing_polygons
canopy=scene.objects['MVP Canopy']
glass_vertices={i for face in canopy.data.polygons if face.material_index==0 for i in face.vertices}
palette.canopy_outline=convex_hull([tuple((canopy.matrix_world@canopy.data.vertices[i].co)[:2]) for i in glass_vertices])
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/ships/vanguard/drawn-study/VanguardStructure.blend'))
scene=bpy.data.scenes['Vanguard - Texture MVP'];bpy.context.window.scene=scene
service_layout=json.loads((ROOT/'art/ships/vanguard/drawn-study/service-panels.json').read_text())
paint_base=paint
def paint(name,points,normal):
    colors=paint_base(name,points,normal)
    if normal[2]>.15:
        for patch in service_layout['patches']:
            if patch['object']!='MVP '+name:continue
            for sign in ([1,-1] if patch['mirror'] else [1]):
                polygon=[[sign*x,y] for x,y in patch['points']]
                mask=inside(points[:,:2],polygon)
                colors[mask]=service_layout['palette'][patch['color']]
    return colors
SIZE=4096
atlas=np.zeros((SIZE,SIZE,4),dtype=np.float32);atlas[:,:,:3]=rgb('dark');atlas[:,:,3]=1
coverage=np.zeros((SIZE,SIZE),dtype=bool)
for obj in scene.objects:
    if obj.type!='MESH' or 'MVP_Atlas' not in obj.data.uv_layers:continue
    mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers['MVP_Atlas'].data
    vertices=np.array([obj.matrix_world@v.co for v in mesh.vertices],dtype=np.float32)
    name=obj.name.removeprefix('MVP ')
    for tri in mesh.loop_triangles:
        coords=np.array([uv[i].uv[:] for i in tri.loops])*SIZE
        lo=np.maximum(np.floor(coords.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(coords.max(axis=0)).astype(int),SIZE-1)
        if np.any(hi<lo):continue
        gx,gy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        a,b,c=coords;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        u=((b[1]-c[1])*(gx-c[0])+(c[0]-b[0])*(gy-c[1]))/den
        v=((c[1]-a[1])*(gx-c[0])+(a[0]-c[0])*(gy-c[1]))/den;w=1-u-v
        mask=(u>=-1e-6)&(v>=-1e-6)&(w>=-1e-6)
        if not mask.any():continue
        points=np.column_stack([u[mask],v[mask],w[mask]])@vertices[list(tri.vertices)]
        ys=gy[mask].astype(int);xs=gx[mask].astype(int)
        normal=np.array(obj.matrix_world.to_3x3()@mesh.polygons[tri.polygon_index].normal)
        atlas[ys,xs,:3]=paint(name,points,normal);coverage[ys,xs]=True
    print('Clean painted',name,flush=True)
for step in range(12):
    add=np.zeros_like(coverage)
    for axis,shift in ((0,1),(0,-1),(1,1),(1,-1)):
        valid=np.roll(coverage,shift,axis=axis)&~coverage&~add
        atlas[valid]=np.roll(atlas,shift,axis=axis)[valid];add|=valid
    coverage|=add
image=bpy.data.images.new('Vanguard clean base',width=SIZE,height=SIZE,alpha=True)
image.colorspace_settings.name='sRGB';image.pixels.foreach_set(atlas.ravel())
image.filepath_raw=str(OUT/'clean-base.png');image.file_format='PNG';image.save()
print(json.dumps({'clean_base':image.filepath_raw,'resolution':SIZE}),flush=True)



