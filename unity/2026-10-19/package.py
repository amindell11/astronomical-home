import sys,json,hashlib,shutil
from pathlib import Path
import numpy as np
from PIL import Image
repo=Path('D:/amind/git/agent-3');stage=Path(__file__).parent;source=repo/'art/ships/bulwark'
sys.path.insert(0,str(repo/'art/tools/ship'))
import ship_unity,ship_regions
facts=json.loads((stage/'materials.json').read_text())
coat=np.array(Image.open(source/'layers/BaseRegion.png').convert('L'))
packed=ship_regions.pack({'Region_Substrate':255-coat,'Region_Coat':coat},['Substrate','Coat'])[0]
Image.fromarray(packed).save(stage/'RegionWeights0.png')
assert np.array_equal(packed[...,1],coat)
assert np.all(packed[...,0].astype(int)+packed[...,1]==255)
bundle=ship_unity.Bundle(stage,'Bulwark',repo/'src/Asteroids3D/Assets/Visuals/Ships/Bulwark')
bundle.folder('Paint');bundle.folder('Materials');bundle.folder('Materials/plum-bone')
guids={name:bundle.texture(f'Paint/{name}.png',source/'layers'/f'{name}.png',2048) for name in ['Shadow','Light','Ink','DetailEmission']}
guids['RegionWeights0']=bundle.texture('Paint/RegionWeights0.png',stage/'RegionWeights0.png',2048)
for name,fact in facts.items():
    base=fact['a'] if fact['masked'] else fact['base']
    maps={k:None for k in ship_unity.UNBOUND_MAPS}
    maps.update({f'_Paint{key}Map':guids[key] for key in ('Shadow','Light','Ink')})
    maps['_EmissionMap']=guids['DetailEmission']
    floats=dict(ship_unity.MATERIAL_FLOATS)
    floats.update(_PaintShadowStrength=.85,_PaintLightStrength=.65,_PaintInkStrength=.75,
                  _PaintEmissionBlend=1,_PaintRegionCount=2 if fact['masked'] else 0,
                  _PaintRegionLightLift=.2,_ShadowThreshold=.22,_ShadowSoftness=.16)
    colors={'_BaseColor':[1,1,1],'_OrangeGain':[1,1,1], '_PaperColor':ship_unity.srgb(base),
            '_PaintLightColor':ship_unity.srgb(ship_unity.lift(base,.2)),
            '_PaintInkColor':ship_unity.srgb([.009,.008,.012]),'_ShadowColor':[.78,.74,.70],
            '_EmissionColor':[226/255*1.15,185/255*1.15,250/255*1.15]}
    if fact['masked']:
        maps['_PaintRegionMask0']=guids['RegionWeights0']
        colors['_PaintRegionColor0']=ship_unity.srgb(fact['a'])
        colors['_PaintRegionColor1']=ship_unity.srgb(fact['b'])
    bundle.material(f'Materials/plum-bone/{name}.mat',ship_unity.material_yaml(name,ship_unity.shader_guid(),maps,floats,colors))
target=repo/'src/Asteroids3D/Assets/Visuals/Ships/Bulwark'
target.mkdir(parents=True,exist_ok=True)
for rel in bundle.files:
    dst=target/rel;dst.parent.mkdir(parents=True,exist_ok=True)
    if not rel.endswith('.meta') or not dst.exists():shutil.copyfile(bundle.root/rel,dst)
shutil.copyfile(stage/'export/Bulwark.fbx',target/'Bulwark.fbx')
hashes={n:hashlib.sha256((source/'layers'/f'{n}.png').read_bytes()).hexdigest() for n in ('BaseRegion','Shadow','Light','Ink','DetailEmission')}
for n in ('Shadow','Light','Ink','DetailEmission'):
    assert hashes[n]==hashlib.sha256((target/'Paint'/f'{n}.png').read_bytes()).hexdigest()
(stage/'package-verification.json').write_text(json.dumps({'base_coat_green_channel_exact':True,'weights_sum_255':True,'source_mask_sha256':hashes,'material_count':len(facts),'shader_revision':'22fbd8e1ad68a7f8a2171e29d3d0ec43c2e48e87'},indent=2))
print('BULWARK_BUNDLE='+str(target))
