import bpy,json,sys,math,colorsys
from pathlib import Path
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source,ship_contract
scene=bpy.context.scene
roles=ship_source.Roles(scene)
parts=sorted({n for r in ship_contract.VISUAL_ROLES for n in roles.members.get(r,()) if scene.objects[n].type=='MESH'})
before=ship_source.fingerprint(scene)['components']['geometry']
temporary=bpy.data.scenes.new('UV projection workspace')
bpy.context.window.scene=temporary
charts=[]
for name in parts:
    original=scene.objects[name]
    world=[original.matrix_world@v.co for v in original.data.vertices]
    for side in (-1,1):
        polygons=[p for p in original.data.polygons if (-1 if sum(world[v].x for v in p.vertices)/len(p.vertices)<0 else 1)==side]
        if not polygons:continue
        mesh=bpy.data.meshes.new('UV projection')
        mesh.from_pydata(world,[],[list(p.vertices) for p in polygons])
        mesh.uv_layers.new(name='UV')
        obj=bpy.data.objects.new('UV projection',mesh)
        temporary.collection.objects.link(obj)
        bpy.context.view_layer.objects.active=obj
        obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.001,area_weight=0,correct_aspect=False,scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        uv=ship_source.read_array(mesh.uv_layers.active.uv,'vector',np.float64,2).reshape(-1,2)
        co,corners,_,triangles=ship_source.mesh_arrays(mesh)
        a,b,c=(co[corners[triangles[:,i]]] for i in range(3))
        surface=.5*np.linalg.norm(np.cross(b-a,c-a),axis=1).sum()
        p,q,r=(uv[triangles[:,i]] for i in range(3))
        area=.5*np.abs((q-p)[:,0]*(r-p)[:,1]-(q-p)[:,1]*(r-p)[:,0]).sum()
        if area<=0 or surface<=0:raise ValueError('Empty UV surface: '+name)
        uv*=math.sqrt(surface/area)
        parents=list(range(len(mesh.polygons)))
        def find(i):
            while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
            return i
        edges={}
        for face in mesh.polygons:
            loops=list(face.loop_indices)
            for i,l in enumerate(loops):
                nxt=loops[(i+1)%len(loops)]
                v,w=mesh.loops[l].vertex_index,mesh.loops[nxt].vertex_index
                key=tuple(sorted((v,w)))
                entry=(face.index,l,nxt) if v<w else (face.index,nxt,l)
                for previous,x,y in edges.get(key,()):
                    if np.allclose(uv[entry[1]],uv[x],atol=1e-7) and np.allclose(uv[entry[2]],uv[y],atol=1e-7):parents[find(face.index)]=find(previous)
                edges.setdefault(key,[]).append(entry)
        islands={}
        for face in mesh.polygons:islands.setdefault(find(face.index),[]).append(face.index)
        for faces in islands.values():
            loops=[l for f in faces for l in mesh.polygons[f].loop_indices]
            destinations=[l for f in faces for l in polygons[f].loop_indices]
            coords=uv[loops]
            low=coords.min(axis=0);size=coords.max(axis=0)-low
            charts.append({'part':name,'side':side,'loops':destinations,'uv':coords-low,'size':size})
        bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.meshes.remove(mesh)
width,height=.972,.472
padding=8/4096
order=sorted(range(len(charts)),key=lambda i:max(charts[i]['size']),reverse=True)
def pack(scale):
    free=[(0.,0.,width,height)];placed={}
    for i in order:
        size=charts[i]['size']*scale+padding*2
        choices=[]
        for j,(x,y,w,h) in enumerate(free):
            for turn in (False,True):
                a,b=(size[1],size[0]) if turn else size
                if a<=w+1e-12 and b<=h+1e-12:choices.append((min(w-a,h-b),max(w-a,h-b),y,x,j,turn,a,b))
        if not choices:return None
        _,_,y,x,j,turn,a,b=min(choices)
        placed[i]=(x+padding,y+padding,turn)
        split=[]
        for fx,fy,fw,fh in free:
            if x>=fx+fw or x+a<=fx or y>=fy+fh or y+b<=fy:split.append((fx,fy,fw,fh));continue
            if x>fx:split.append((fx,fy,x-fx,fh))
            if x+a<fx+fw:split.append((x+a,fy,fx+fw-x-a,fh))
            if y>fy:split.append((fx,fy,fw,y-fy))
            if y+b<fy+fh:split.append((fx,y+b,fw,fy+fh-y-b))
        free=[r for j,r in enumerate(split) if r[2]>1e-10 and r[3]>1e-10 and not any(j!=k and r[0]>=s[0]-1e-12 and r[1]>=s[1]-1e-12 and r[0]+r[2]<=s[0]+s[2]+1e-12 and r[1]+r[3]<=s[1]+s[3]+1e-12 and (r!=s or j>k) for k,s in enumerate(split))]
    return placed
low,high=0.,1.
while pack(high) is not None:high*=2
for _ in range(25):
    middle=(low+high)/2
    if pack(middle) is None:high=middle
    else:low=middle
placement=pack(low*.999)
for name in parts:
    obj=scene.objects[name]
    layer=obj.data.uv_layers.get('PaintUV') or obj.data.uv_layers.new(name='PaintUV')
    obj.data.uv_layers.active=layer
    layer.active_render=True
    for modifier in obj.modifiers:
        if modifier.type=='MIRROR':modifier.offset_v=-.5
for i,chart in enumerate(charts):
    x,y,turn=placement[i]
    uv=chart['uv']*low*.999
    if turn:uv=np.column_stack((uv[:,1],chart['size'][0]*low*.999-uv[:,0]))
    uv+=np.array((x+.014,y+.014+(.5 if chart['side']>0 else 0)))
    data=scene.objects[chart['part']].data.uv_layers['PaintUV'].data
    for loop,coord in zip(chart['loops'],uv):data[loop].uv=coord
bpy.context.window.scene=scene
bpy.data.scenes.remove(temporary)
assert ship_source.fingerprint(scene)['components']['geometry']==before,'UV work changed geometry'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
out=Path('D:/amind/git/agent-2/results/vanguard-redo/uv')
out.mkdir(parents=True,exist_ok=True)
size=2048
pixels=np.zeros((size,size,4),np.float32);pixels[:,:,:3]=(.016,.022,.032);pixels[:,:,3]=1
pixels[size//2-1:size//2+1,:,:3]=(.8,.8,.8)
def line(a,b,color):
    a=np.rint(np.array(a)*(size-1)).astype(int);b=np.rint(np.array(b)*(size-1)).astype(int)
    count=int(max(abs(b-a)))+1
    p=np.rint(np.linspace(a,b,count)).astype(int)
    valid=(p[:,0]>=0)&(p[:,0]<size)&(p[:,1]>=0)&(p[:,1]<size)
    p=p[valid];pixels[p[:,1],p[:,0],:3]=color
for index,name in enumerate(parts):
    obj=scene.objects[name]
    mesh=obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    color=colorsys.hsv_to_rgb(index/len(parts),.45,.85)
    data=mesh.uv_layers['PaintUV'].data
    for face in mesh.polygons:
        loops=list(face.loop_indices)
        for i,l in enumerate(loops):line(data[l].uv,data[loops[(i+1)%len(loops)]].uv,color)
    obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
image=bpy.data.images.new('PaintUV guide',size,size,alpha=True)
image.pixels.foreach_set(pixels.ravel());image.filepath_raw=str(out/'PaintUV-guide.png');image.file_format='PNG';image.save()
(out/'uv-authoring.json').write_text(json.dumps({'parts':parts,'charts':len(charts),'padding_pixels':8,'texture_size':4096,'geometry_unchanged':True,'atlas':'negative-X lower half; positive-X upper half','scale':low*.999},indent=2)+'\n')
print('VANGUARD_UV_GUIDE='+str(out/'PaintUV-guide.png'))
