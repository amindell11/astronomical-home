from pathlib import Path
import subprocess,re,json,hashlib
from PIL import Image
root=Path('D:/amind/git/agent-5')
paths=['src/Asteroids3D/Assets/Visuals/Shaders','src/Asteroids3D/Assets/Prefabs/Ships/Crimson.prefab','src/Asteroids3D/Assets/Visuals/Ships/Crimson/Canopy','src/Asteroids3D/Assets/Visuals/Ships/Crimson/Crimson.fbx','src/Asteroids3D/Assets/Visuals/Ships/Crimson/Hull paint.mat.meta','src/Asteroids3D/Assets/Visuals/Ships/Crimson/Breakup/Debris paint.mat.meta']
subprocess.run(['git','diff','--exit-code','HEAD','--',*paths],cwd=root,check=True,capture_output=True)
material_checks={}
for relative in ['Hull paint.mat','Breakup/Debris paint.mat']:
    p='src/Asteroids3D/Assets/Visuals/Ships/Crimson/'+relative
    old=subprocess.check_output(['git','show','HEAD:'+p],cwd=root,text=True)
    new=(root/p).read_text()
    preserved=['_AmbientStrength','_CastShadowStrength','_ShadowThreshold','_ShadowSoftness','_PaletteLighting','_SpecularStrength','_ShadowColor','_SootStrength','_DebrisVisibility','m_CustomRenderQueue','m_Shader']
    for key in preserved:
        pattern=r'^.*\b'+key+r':.*$'
        assert re.findall(pattern,old,re.M)==re.findall(pattern,new,re.M),(relative,key)
    assert '_PAINT_LAYERS' in new and '_PaintRegionCount: 11' in new
    material_checks[relative]=preserved
masks={}
for name in ['Shadow','Light','Ink']:
    path=root/'art/ships/crimson/layers'/f'{name}.png'
    with Image.open(path) as image:
        assert image.mode=='L' and image.size==(4096,4096)
    paths_to_match=[root/'art/ships/crimson/layers/baseline'/path.name,root/'src/Asteroids3D/Assets/Visuals/Ships/Crimson/Paint'/path.name]
    digest=hashlib.sha256(path.read_bytes()).hexdigest()
    assert all(hashlib.sha256(p.read_bytes()).hexdigest()==digest for p in paths_to_match)
    masks[name]=digest
before=json.loads((root/'scratch/crimson-paint/source-preparation.json').read_text())['before']
after=json.loads((root/'scratch/crimson-paint/source-authoring.json').read_text())['after']
assert all(before[k]==after[k] for k in ['geometry','uv'])
report={'unchanged_paths':paths,'preserved_material_properties':material_checks,'grayscale_4096_mask_sha256':masks,'geometry':after['geometry'],'uv':after['uv'],'result':'pass'}
(root/'scratch/crimson-paint/final-verification.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
