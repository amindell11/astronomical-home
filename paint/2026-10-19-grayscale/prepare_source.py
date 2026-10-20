import bpy
import json
import sys
from pathlib import Path
sys.path.insert(0, 'D:/amind/git/agent-5/art/tools/ship')
import ship_source
folder = Path('D:/amind/git/agent-5/art/ships/crimson')
bpy.ops.wm.open_mainfile(filepath=str(folder / 'Crimson.blend'), load_ui=False)
before = ship_source.fingerprint(bpy.context.scene)
srgb = {
    'Silver': [.94,.92,.86], 'SilverStructure': [.94,.92,.86],
    'Red': [.92,.038,.023], 'RedStructure': [.92,.038,.023],
    'Frame': [.85,.045,.025], 'Charcoal': [.14,.15,.17],
    'Rim': [.57,.58,.55], 'NozzleRing': [.38,.39,.38],
    'FloorIndicator': [.10,.66,.13], 'YokeIndicator': [.18,.72,.12],
    'Crimson canopy': [.028,.034,.041]
}
def linear(values):
    return [v / 12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in values]
def region(name):
    if name == 'Canopy': return 'Crimson canopy'
    if name in ('Canopy perimeter rim', 'Canopy transverse frame'): return 'Frame'
    if 'wing foundation' in name or 'fin' in name.lower(): return 'Red'
    if name in ('Dorsal service spine', 'Service pod cap', 'Engine housing'): return 'RedStructure'
    if any(word in name for word in ('armor','prow','Wing root beam')): return 'Silver'
    if name in ('Cockpit surround','Rear cockpit yoke','Engine neck','Engine support frame'): return 'SilverStructure'
    if name == 'Engine forward cowling': return 'Rim'
    return 'Charcoal'
materials = {}
for name, color in srgb.items():
    if name == 'Crimson canopy':
        materials[name] = bpy.data.materials[name]
        continue
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*linear(color), 1)
    material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = material.diffuse_color
    materials[name] = material
assignments = {}
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH': continue
    name = region(obj.name)
    assignments[obj.name] = name
    if obj.name == 'Canopy': continue
    obj.data.materials.clear()
    obj.data.materials.append(materials[name])
    for face in obj.data.polygons: face.material_index = 0
after = ship_source.fingerprint(bpy.context.scene)
for component in ('geometry','uv'):
    assert before['components'][component] == after['components'][component], component
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_mainfile()
palettes = {'color_space':'linear-sRGB', 'default':'crimson',
            'palettes':{'crimson':{name:[*linear(color),1] for name,color in srgb.items()}}}
paint = {
    'schema_version':1, 'authored_regions':'regions.json',
    'shadow_coverage':.33, 'shadow_depth':.39, 'side_wall_depth':1.,
    'brush':{'image':'textures/painted-brush-source.png','scale':1.3,'contrast':3.2,'blend':.25},
    'ramp':[[.1,.38],[.55,.64],[.92,1]],
    'sweep':{'weight_x':.65,'light_above':.66,'jitter':.5,'light':1.},
    'ink':{'inset':.003,'rim':.0065,'rim_light':.35,'wall_facing':.65},
    'plates':{'cap_facing':.9,'thin_wall':.12},
    'regions':{name:{'plate':name in ('Silver','Red'),'sweep':name=='Silver','ink_walls':name=='Red'} for name in srgb},
    'canopy_glass':['Canopy'], 'canopy_frames':['Canopy perimeter rim','Canopy transverse frame'],
    'accents':[],
    'material':{'shadow_strength':1.,'light_strength':1.,'ink_strength':1.,'light_lift':.25,
        'ink_color':linear([.019,.023,.029]),'emission_blend':1.,
        'lighting':{'_ShadowThreshold':.55,'_ShadowSoftness':.18,'_CastShadowStrength':.96,
            '_AmbientStrength':.35,'_PaletteLighting':1,'_SpecularStrength':0,'_ShadowColor':[.35,.45,.8]}}
}
def mark(part, name, bounds):
    return {'parts':[part], 'region':name,'mirror_x':True,'where':bounds,'shape':{'kind':'box'},'opacity':1}
nozzle = bpy.data.objects['Engine nozzle']
ys = [(nozzle.matrix_world @ v.co).y for v in nozzle.data.vertices]
low, high = min(ys),max(ys)
regions={'schema_version':1,'marks':[
    mark('Service channel floor','FloorIndicator',{'x':[.068,None]}),
    mark('Rear cockpit yoke','YokeIndicator',{'x':[.115,.142],'y':[-.203,-.178]}),
    mark('Engine nozzle','NozzleRing',{'y':[low+.016,low+.024]}),
    mark('Engine nozzle','NozzleRing',{'y':[high-.029,high-.021]})
]}
for name,value in (('palettes',palettes),('paint',paint),('regions',regions)):
    (folder / (name+'.json')).write_text(json.dumps(value,indent=2)+'\n')
Path('D:/amind/git/agent-5/scratch/crimson-paint/source-preparation.json').write_text(
    json.dumps({'before':before['components'],'after':after['components'],'assignments':assignments},indent=2))
print('SOURCE_PREPARED', after['components'])
