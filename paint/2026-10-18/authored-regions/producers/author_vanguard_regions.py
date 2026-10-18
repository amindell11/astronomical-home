import bpy,json,sys,hashlib
from pathlib import Path

ROOT=Path('D:/amind/git/agent-4')
SCRATCH=Path(__file__).parent
SOURCE=ROOT/'art/ships/vanguard/Vanguard.blend'
ORIGINAL=Path('D:/amind/git/astronomical-home/art/ships/vanguard/drawn-study/VanguardPainted.blend')
sys.path.insert(0,str(ROOT/'art/tools/ship'))
import ship_source,ship_regions
layout=json.loads((SCRATCH/'old-livery/legacy-paint-layout.json').read_text(encoding='utf-8-sig'))
parameters=json.loads((SCRATCH/'old-livery/placement-parameters.json').read_text())
service=json.loads((ROOT/'art/ships/vanguard/drawn-study/service-panels.json').read_text())

bpy.ops.wm.open_mainfile(filepath=str(ORIGINAL))
scene=bpy.data.scenes['Vanguard - Texture MVP']
original_canopy=[list(m.diffuse_color) for m in scene.objects['MVP Canopy'].data.materials]
original_core=list(scene.objects['MVP Power Nacelle Core'].data.materials[0].diffuse_color)
original_ink=list(scene.objects['Vanguard structural ink'].data.materials[0].diffuse_color)

def linear(values):return [v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in values]+[1.]
def hexcolor(code):return linear([int(code[i:i+2],16)/255 for i in (0,2,4)])
colors={'HullPrimary':hexcolor(layout['palette']['ivory']),'HullAccent':linear([1,.36,.065]),
        'Structure':hexcolor(layout['palette']['dark']),'Underside':hexcolor(layout['palette']['belly']),
        'Linework':hexcolor(layout['palette']['ink']),'WarningMark':hexcolor(layout['palette']['red']),
        'Indicator':hexcolor(layout['palette']['cyan']),'CanopyGlass':original_canopy[0],
        'CanopyFrame':original_canopy[1],'CoreGlow':original_core,'Contour':original_ink,
        **{region:linear(service['palette'][name]) for name,region in
           [('charcoal','HatchFrame'),('gray','PanelGray'),('edge','PanelEdge'),('bluegray','HullInset'),('amber','AmberIndex')]}}
assert len(colors)==16, colors
alternate=dict(colors)
alternate.update({name:hexcolor(code) for name,code in {'HullPrimary':'B7DDD4','HullAccent':'8D5BC7',
    'CanopyFrame':'8D5BC7','Structure':'283D44','Underside':'3A5057','HullInset':'4F787A',
    'PanelGray':'809E99','PanelEdge':'B6CFBE','HatchFrame':'354E51'}.items()})

aliases={'MVP wing_armor':['Pod cooling hatch'],'MVP Wing':['Wing service hatch'],
         'MVP Cockpit.001':['Cockpit cooling vent']}
marks=[]
def mark(parts,region,shape=None,where=None,mirror=False,opacity=1):
    if isinstance(parts,str):parts=[parts]
    expanded=list(parts)
    for part in parts:expanded.extend(aliases.get(part,[]))
    marks.append({'parts':list(dict.fromkeys(expanded)),'region':region,'mirror_x':mirror,
                  'where':where or {},'shape':shape or {'kind':'box'},'opacity':opacity})
def polygon(points):
    points=[list(p) for p in points]
    if points[0]==points[-1]:points=points[:-1]
    return {'kind':'polygon','points':points}
def path(points,width,closed=False):
    points=[list(p) for p in points]
    if points[0]==points[-1]:points=points[:-1];closed=True
    return {'kind':'path','points':points,'width':width,'closed':closed}

body=['MVP '+n for n in ['Fuselage','Cockpit.001','Sparrow Tail','Wing','wing_armor','wing_armature']]
mark(body,'Underside',where={'nz':[None,.28]})
mark('MVP Sparrow Tail','Structure',where={'nz':[None,.28]})
mark('MVP Tail','HullAccent',where={'y':[None,-.61],'z':[.277,None]})
mark('MVP wing_tail','Structure',where={'nz':[-.28,.28]})
mark('MVP Fuselage','HullAccent',where={'nz':[.28,None],'y':[-.67,-.25]})
for center in (-.525,-.596):
    mark('MVP Fuselage','HullPrimary',where={'nz':[.28,None],'y':[center-.011,center+.011]})
edge=parameters['wing_polygons']['Wing']
for facing in ([.28,None],[None,-.28]):
    mark('MVP Wing','HullAccent',path([edge[-2],edge[-1],edge[0]],.16),{'nz':facing},True)
for name in ('MVP Wing','MVP wing_armor'):
    mark(name,'HullPrimary',{'kind':'circle','center':parameters['core_center'],'radius':.079},
         {'nz':[.28,None]},True)
canopy=parameters['canopy_outline']
mark('MVP Cockpit.001','Linework',path(canopy,.042,True),{'nz':[.28,None]})
mark('MVP Cockpit.001','HullAccent',path(canopy,.038,True),{'nz':[.28,None]})
mark('MVP Power Nacelle Housing','HullPrimary',where={'nz':[.55,None]})
nx,ny=parameters['core_center']
mark('MVP Power Nacelle Housing','Linework',where={'x':[nx-.003,nx+.003],
     'y':[ny-.057-.008,ny-.057+.008]},mirror=True)

region_for={'ivory':'HullPrimary','orange':'HullAccent','dark':'Structure','belly':'Underside',
            'ink':'Linework','red':'WarningMark','cyan':'Indicator'}
retained_shapes=[]
for entry in layout['shapes']:
    if entry['color'] in ('line','wear'):continue
    bounds={'nz':[.28,None]} if entry['side']=='top' else {'nz':[None,-.28]} if entry['side']=='bottom' else {}
    shape=path(entry['points'],entry['width']) if entry['kind']=='line' else polygon(entry['points'])
    mark(['MVP '+name for name in entry['objects']],region_for[entry['color']],shape,bounds,
         opacity=entry['opacity'])
    retained_shapes.append(entry)
service_regions={'charcoal':'HatchFrame','gray':'PanelGray','edge':'PanelEdge','bluegray':'HullInset','amber':'AmberIndex'}
for entry in service['patches']:
    mark(entry['object'],service_regions[entry['color']],polygon(entry['points']),{'nz':[.15,None]},entry['mirror'])
descriptor=ship_regions.parse({'schema_version':1,'marks':marks})

bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene
before=ship_source.fingerprint(scene)['components']
materials={}
for name,color in colors.items():
    material=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color=color;materials[name]=material
base_parts={
    'MVP Fuselage':['HullPrimary'],'MVP Cockpit.001':['HullPrimary'],'MVP Sparrow Tail':['HullPrimary'],
    'MVP Wing':['HullPrimary'],'MVP Tail':['HullPrimary'],'MVP wing_armature':['Structure'],
    'MVP wing_armor':['HullPrimary'],'MVP wing_tail':['HullAccent'],'MVP Power Nacelle Housing':['Structure'],
    'MVP Power Nacelle Core':['CoreGlow'],'MVP Canopy':['CanopyGlass','CanopyFrame'],
    'Vanguard structural ink':['Contour'],'Cockpit cooling vent':['HullPrimary','Structure'],
    'Pod cooling hatch':['HullPrimary','Structure'],'Wing service hatch':['HullPrimary','Structure']}
for name,regions in base_parts.items():
    obj=scene.objects[name];indices=[face.material_index for face in obj.data.polygons]
    obj.data.materials.clear()
    for region in regions:obj.data.materials.append(materials[region])
    for face,index in zip(obj.data.polygons,indices):face.material_index=index if len(regions)>1 else 0
after=ship_source.fingerprint(scene)['components']
assert before['geometry']==after['geometry'] and before['uv']==after['uv']
folder=SOURCE.parent
(folder/'regions.json').write_text(json.dumps(descriptor,indent=2)+'\n')
(folder/'palettes.json').write_text(json.dumps({'color_space':'linear-sRGB','default':'vanguard',
      'palettes':{'vanguard':colors,'mint_plum':alternate}},indent=2)+'\n')
paint={
 'schema_version':1,'authored_regions':'regions.json',
 'brush':{'image':'../crimson/textures/painted-brush-source.png','scale':1.3,'contrast':1.2,'blend':.25},
 'ramp':[[0,1],[1,1]],'shadow_coverage':0.,'shadow_depth':0.,'side_wall_depth':0.,
 'sweep':{'weight_x':.65,'light_above':.66,'jitter':.5,'light':1.},
 'ink':{'inset':.003,'rim':.0065,'rim_light':.35,'wall_facing':.65},
 'plates':{'cap_facing':.9,'thin_wall':.12},
 'regions':{name:{'plate':False,'sweep':False,'ink_walls':False} for name in colors},
 'canopy_glass':['MVP Canopy'],'canopy_frames':['Vanguard structural ink'],
 'accents':[{'part':'MVP Power Nacelle Core','target':'emission','where':{'z':[-100,None]},
             'value':1.,'color':colors['CoreGlow'][:3]}],
 'material':{'shadow_strength':1.,'light_strength':1.,'ink_strength':1.,'light_lift':.25,
             'ink_color':colors['Linework'][:3],'emission_blend':1.,
             'lighting':{'_ShadowThreshold':.55}}}
(folder/'paint.json').write_text(json.dumps(paint,indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
out=ROOT/'results/vanguard-redo/authored-regions'
out.mkdir(parents=True,exist_ok=True)
(out/'authoring-audit.json').write_text(json.dumps({'geometry_unchanged':True,'uv_unchanged':True,
    'regions':list(colors),'source_marks':len(retained_shapes),'service_patches':len(service['patches']),
    'authored_marks':len(marks),'original_source_sha256':hashlib.sha256(ORIGINAL.read_bytes()).hexdigest(),
    'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest()},indent=2)+'\n')
print('VANGUARD_AUTHORED_REGIONS='+str(out/'authoring-audit.json'))
