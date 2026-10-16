import bpy, json, sys, math
from pathlib import Path
import numpy as np
from mathutils import Vector

ROOT = Path('D:/amind/git/agent-2')
HERE = Path(__file__).parent
OUT = ROOT/'results/vanguard-redo/paint/region-correction'
OUT.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(ROOT/'art/tools/ship'))
import ship_render, ship_source, ship_contract
sys.path.insert(0, str(HERE))
from base_palette import convex_hull

SOURCE = ROOT/'art/ships/vanguard/Vanguard.blend'
BASELINE = HERE.parent/'baseline/Vanguard.blend'
source_hash = ship_contract.sha256_file(SOURCE)
baseline_hash = ship_contract.sha256_file(BASELINE)
layout = json.loads((HERE/'legacy-paint-layout.json').read_text(encoding='utf-8-sig'))
service = json.loads((ROOT/'art/ships/vanguard/drawn-study/service-panels.json').read_text())
palettes = json.loads((SOURCE.parent/'palettes.json').read_text())['palettes']
for palette,colors in palettes.items():
    srgb=(.39,.46,.54) if palette=='vanguard' else (.31,.47,.48)
    colors['HullInset']=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in srgb]+[1.]

parameters=json.loads((HERE/'placement-parameters.json').read_text())
wing_polygons=parameters['wing_polygons']
nx,ny=parameters['core_center']
canopy_outline=parameters['canopy_outline']
def simplify(points, tolerance):
    if len(points)<3:return points
    a,b=np.asarray(points[0]),np.asarray(points[-1]);delta=b-a
    q=np.asarray(points[1:-1]);t=np.clip(((q-a)@delta)/(delta@delta),0,1)
    distances=np.linalg.norm(q-a-t[:,None]*delta,axis=1)
    i=int(np.argmax(distances))+1
    if distances[i-1]<=tolerance:return [points[0],points[-1]]
    return simplify(points[:i+1],tolerance)[:-1]+simplify(points[i:],tolerance)
middle=len(canopy_outline)//2
canopy_outline=simplify(canopy_outline[:middle+1],.0003)[:-1]+simplify(canopy_outline[middle:]+[canopy_outline[0]],.0003)[:-1]

class RegionShader:
    def __init__(self, name, colors):
        self.colors = colors
        self.mat = bpy.data.materials.new('recovered placement '+name)
        self.mat.use_nodes = True
        self.nodes, self.links = self.mat.node_tree.nodes, self.mat.node_tree.links
        self.nodes.clear()
        geometry = self.nodes.new('ShaderNodeNewGeometry')
        position = self.nodes.new('ShaderNodeSeparateXYZ')
        normal = self.nodes.new('ShaderNodeSeparateXYZ')
        self.links.new(geometry.outputs['Position'], position.inputs[0])
        self.links.new(geometry.outputs['True Normal'], normal.inputs[0])
        self.x,self.y,self.z = [position.outputs[i] for i in range(3)]
        self.ax = self.math('ABSOLUTE', self.x)
        self.nz = normal.outputs['Z']
        self.upper = self.math('GREATER_THAN', self.nz, .28)
        self.lower = self.math('LESS_THAN', self.nz, -.28)
        self.color = colors['HullPrimary']

    def assign(self, value, socket):
        if isinstance(value, (tuple,list)):
            socket.default_value = value
        elif isinstance(value, (int,float,np.floating)):
            socket.default_value = float(value)
        else:
            self.links.new(value, socket)

    def math(self, op, a, b=0):
        node = self.nodes.new('ShaderNodeMath')
        node.operation = op
        self.assign(a,node.inputs[0]); self.assign(b,node.inputs[1])
        return node.outputs[0]

    def both(self,*values):
        value=values[0]
        for other in values[1:]:value=self.math('MULTIPLY',value,other)
        return value

    def either(self,a,b):return self.math('MAXIMUM',a,b)

    def fill(self, condition, region):
        mix=self.nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX'
        self.assign(condition,mix.inputs[0]);self.assign(self.color,mix.inputs[1])
        self.assign(self.colors[region],mix.inputs[2]);self.color=mix.outputs[0]

    def inside(self, polygon, folded=False):
        x=self.ax if folded else self.x
        result=0
        for a,b in zip(polygon,polygon[1:]+polygon[:1]):
            if abs(b[1]-a[1])<1e-12:continue
            side=self.math('ABSOLUTE',self.math('SUBTRACT',self.math('GREATER_THAN',a[1],self.y),self.math('GREATER_THAN',b[1],self.y)))
            cross=self.math('ADD',self.math('MULTIPLY',self.math('SUBTRACT',self.y,a[1]),(b[0]-a[0])/(b[1]-a[1])),a[0])
            hit=self.both(side,self.math('LESS_THAN',x,cross))
            result=self.math('ABSOLUTE',self.math('SUBTRACT',result,hit))
        return result

    def path_distance(self,points,folded=False):
        x=self.ax if folded else self.x
        result=100.
        for a,b in zip(points[:-1],points[1:]):
            dx,dy=b[0]-a[0],b[1]-a[1]
            px,py=self.math('SUBTRACT',x,a[0]),self.math('SUBTRACT',self.y,a[1])
            t=self.math('DIVIDE',self.math('ADD',self.math('MULTIPLY',px,dx),self.math('MULTIPLY',py,dy)),dx*dx+dy*dy)
            t=self.math('MINIMUM',1,self.math('MAXIMUM',0,t))
            rx,ry=self.math('SUBTRACT',px,self.math('MULTIPLY',t,dx)),self.math('SUBTRACT',py,self.math('MULTIPLY',t,dy))
            distance=self.math('SQRT',self.math('ADD',self.math('MULTIPLY',rx,rx),self.math('MULTIPLY',ry,ry)))
            result=self.math('MINIMUM',result,distance)
        return result

    def finish(self):
        emission=self.nodes.new('ShaderNodeEmission');self.assign(self.color,emission.inputs['Color'])
        output=self.nodes.new('ShaderNodeOutputMaterial');self.links.new(emission.outputs[0],output.inputs['Surface'])
        return self.mat

def recovered_material(name,colors):
    shader=RegionShader(name,colors)
    s=shader
    s.fill(s.math('LESS_THAN',s.nz,.28),'HullInset')
    if name in ('Tail','Sparrow Tail'):
        s.fill(1,'HullPrimary')
        if name=='Sparrow Tail':s.fill(s.math('SUBTRACT',1,s.upper),'Structure')
        else:s.fill(s.both(s.math('LESS_THAN',s.y,-.61),s.math('GREATER_THAN',s.z,.277)),'HullAccent')
    if name=='wing_armature':s.fill(1,'Structure')
    if name=='wing_tail':s.fill(s.either(s.upper,s.lower),'HullAccent')
    if name=='Fuselage':
        stripe=s.both(s.upper,s.math('LESS_THAN',s.y,-.25),s.math('GREATER_THAN',s.y,-.67))
        s.fill(stripe,'HullAccent')
        gaps=s.either(s.math('LESS_THAN',s.math('ABSOLUTE',s.math('ADD',s.y,.525)),.011),s.math('LESS_THAN',s.math('ABSOLUTE',s.math('ADD',s.y,.596)),.011))
        s.fill(s.both(stripe,gaps),'HullPrimary')
    if name in ('Wing','wing_armor'):
        facing=s.either(s.upper,s.lower)
        s.fill(facing,'HullPrimary')
        if name=='Wing':
            poly=wing_polygons[name]
            s.fill(s.both(facing,s.math('LESS_THAN',s.path_distance([poly[-2],poly[-1],poly[0]],True),.08)),'HullAccent')
        dx,dy=s.math('SUBTRACT',s.ax,nx),s.math('SUBTRACT',s.y,ny)
        clear=s.math('LESS_THAN',s.math('ADD',s.math('MULTIPLY',dx,dx),s.math('MULTIPLY',dy,dy)),.079**2)
        s.fill(s.both(s.upper,clear),'HullPrimary')
    if name=='Cockpit.001':
        distance=s.path_distance(canopy_outline+[canopy_outline[0]])
        s.fill(s.both(s.upper,s.math('LESS_THAN',distance,.02)),'HullAccent')
    if name=='Power Nacelle Housing':
        s.fill(s.math('GREATER_THAN',s.nz,.55),'HullPrimary')
    for patch in service['patches']:
        if patch['object']!='MVP '+name:continue
        if patch['color'] not in ('bluegray','charcoal'):continue
        if 'vent' in patch['name'] or 'pod access' in patch['name'] or 'cover' in patch['name'] or 'cooling' in patch['name']:continue
        region='HullInset' if patch['color']=='bluegray' else 'Structure'
        s.fill(s.both(s.math('GREATER_THAN',s.nz,.15),s.inside(patch['points'],patch['mirror'])),region)
    return s.finish()

def flat_material(region,colors):
    s=RegionShader(region,colors);s.color=colors[region];return s.finish()

views={'top':((0,0,1),(0,1,0)),'hero':((1,1,1.5),(0,0,1))}

def render(evaluation,framing,prefix,size):
    scene=evaluation.scene
    ship_source.apply_review_preset(scene,size)
    scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=False
    scene.render.film_transparent=False
    scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.045,.055,.08,1)
    camera=bpy.data.objects.new('livery review camera',bpy.data.cameras.new('livery review camera'))
    scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO'
    low,high=framing;center=(low+high)/2
    corners=np.array([[x,y,z] for x in (low[0],high[0]) for y in (low[1],high[1]) for z in (low[2],high[2])])
    written={}
    for name,(direction,up) in views.items():
        matrix=ship_render.camera_matrix(direction,up,center,8)
        camera.matrix_world=matrix
        extent=(corners-center)@np.array(matrix.to_3x3())
        camera.data.ortho_scale=float(max(np.ptp(extent[:,0]),np.ptp(extent[:,1])))*1.15
        path=OUT/f'{prefix}{name}.png';scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True);written[name]=path
    return written

records=[]
for palette,colors in palettes.items():
    evaluation,framing=ship_render.prepare(SOURCE)
    for original_name,obj in evaluation.copies.items():
        if obj.type!='MESH' or obj.hide_render:continue
        indices=[p.material_index for p in obj.data.polygons]
        slots=[m.name for m in obj.data.materials]
        obj.data=obj.data.copy()
        obj.data.materials.clear()
        if original_name.startswith('MVP ') and original_name not in ('MVP Canopy','MVP Power Nacelle Core'):
            obj.data.materials.append(recovered_material(original_name.removeprefix('MVP '),colors))
            for p in obj.data.polygons:p.material_index=0
        else:
            for region in slots:obj.data.materials.append(flat_material(region,colors))
            for p,index in zip(obj.data.polygons,indices):p.material_index=index
    written=render(evaluation,framing,palette+'_',1024)
    records.append(written)
    render(evaluation,framing,palette+'_scale96_',96)

ship_render.save_pixels(OUT/'recovered-regions.png',ship_render.grid([ship_render.load_pixels(records[i][view]) for view in views for i in range(len(records))],2))
assert ship_contract.sha256_file(SOURCE)==source_hash
assert ship_contract.sha256_file(BASELINE)==baseline_hash
(OUT/'region-correction.json').write_text(json.dumps({'source_sha256':source_hash,'baseline_sha256':baseline_hash,'placement_source':'evidence/vanguard:paint/retexture/pipeline/base_palette.py and legacy-paint-layout.json; service-panels.json','method':'Temporary procedural emission materials; source not saved; no texture masks generated','source_unchanged':True,'geometry_unchanged':True,'palettes':list(palettes),'wing_polygons':wing_polygons,'core_center':[float(nx),float(ny)],'canopy_outline':canopy_outline},indent=2)+'\n')
print('RECOVERED_REGION_REVIEW='+str(OUT/'recovered-regions.png'))
