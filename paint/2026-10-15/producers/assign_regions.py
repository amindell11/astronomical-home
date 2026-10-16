import bpy,json,sys
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source
scene=bpy.context.scene
before=ship_source.fingerprint(scene)['components']
colors={'vanguard':{'HullPrimary':'EDE2C8','HullAccent':'FF6514','Structure':'33424F','CanopyGlass':'111D36','CoreGlow':'159CFF','Linework':'070D15'},'mint_plum':{'HullPrimary':'B7DDD4','HullAccent':'8D5BC7','Structure':'283D44','CanopyGlass':'112438','CoreGlow':'159CFF','Linework':'070D15'}}
def linear(hex):
    channels=[int(hex[i:i+2],16)/255 for i in (0,2,4)]
    return [v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in channels]+[1.]
palettes={name:{region:linear(color) for region,color in regions.items()} for name,regions in colors.items()}
materials={}
for name,color in palettes['vanguard'].items():
    material=bpy.data.materials.get(name)
    if material is None:material=bpy.data.materials.new(name)
    material.diffuse_color=color
    materials[name]=material
part_regions={'MVP Fuselage':['HullPrimary'],'MVP Cockpit.001':['HullPrimary'],'MVP Sparrow Tail':['HullPrimary'],'MVP Wing':['HullPrimary'],'MVP Tail':['HullAccent'],'MVP wing_armature':['Structure'],'MVP wing_armor':['HullAccent'],'MVP wing_tail':['HullAccent'],'MVP Power Nacelle Housing':['Structure'],'MVP Power Nacelle Core':['CoreGlow'],'MVP Canopy':['CanopyGlass','HullAccent'],'Vanguard structural ink':['Linework'],'Cockpit cooling vent':['HullPrimary','Structure'],'Pod cooling hatch':['HullPrimary','Structure'],'Wing service hatch':['HullPrimary','Structure']}
for name,regions in part_regions.items():
    obj=scene.objects[name]
    indices=[p.material_index for p in obj.data.polygons]
    obj.data.materials.clear()
    for region in regions:obj.data.materials.append(materials[region])
    for p,index in zip(obj.data.polygons,indices):
        p.material_index=(index if index<2 else 0) if name=='MVP Canopy' or name in {'Cockpit cooling vent','Pod cooling hatch','Wing service hatch'} else 0
    for p in obj.data.polygons:assert p.material_index<len(regions),name
assert ship_source.fingerprint(scene)['components']['geometry']==before['geometry'],'Region assignment changed geometry'
assert ship_source.fingerprint(scene)['components']['uv']==before['uv'],'Region assignment changed UVs'
root=Path(bpy.data.filepath).parent
(root/'palettes.json').write_text(json.dumps({'color_space':'linear-sRGB','default':'vanguard','palettes':palettes},indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
out=Path('D:/amind/git/agent-2/results/vanguard-redo/paint/concept')
out.mkdir(parents=True,exist_ok=True)
(out/'region-assignment.json').write_text(json.dumps({'parts':part_regions,'sRGB_hex':colors,'geometry_unchanged':True,'uv_unchanged':True},indent=2)+'\n')
