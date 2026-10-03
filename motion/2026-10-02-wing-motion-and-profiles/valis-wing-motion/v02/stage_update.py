import re
import subprocess
from pathlib import Path

root = Path('D:/amind/git/agent-5')
path = 'src/Asteroids3D/Assets/Prefabs/Ships/Valis.prefab'
source = root.joinpath(path).read_text()
base = subprocess.check_output(['git','-C',str(root),'show','HEAD:'+path]).decode()
documents = {m.group(1):m.group(0) for m in re.finditer(r'^--- !u!\d+ &(\d+)\n.*?(?=^--- !u!|\Z)',base,re.M|re.S)}
def clean_document(match):
    text = match.group(0)
    old = documents.get(match.group(1))
    if 'guid: b6f133f9d6e4e194784b99b55e3d0dc2' in text:
        assert '\n  m_Enabled: 0\n' in text
        text = text.replace('\n  m_Enabled: 0\n','\n  m_Enabled: 1\n',1)
    if old and '\nLight:\n' in text and '\n  m_UseBoundingSphereOverride: 0\n' in text:
        value = re.search(r'^  m_BoundingSphereOverride: .*$',old,re.M)
        assert value
        text = re.sub(r'^  m_BoundingSphereOverride: .*$',lambda _:value.group(0),text,flags=re.M)
    return text
staged = re.sub(r'^--- !u!\d+ &(\d+)\n.*?(?=^--- !u!|\Z)',clean_document,source,flags=re.M|re.S)
Path(__file__).with_name('Valis-staged.prefab').write_text(staged)
files = ['art/ships/valis/README.md','art/ships/valis/Valis.blend','art/ships/valis/mesh-approval.json','src/Asteroids3D/Assets/Scripts/Ships/Visuals/Wings/WingVisuals.cs','src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/Presentation/Wings/WingVisualsPlayModeTests.cs','src/Asteroids3D/Assets/Visuals/Ships/Valis/Meshes/Valis hull.asset']
subprocess.run(['git','-C',str(root),'add','--',*files],check=True)
blob = subprocess.check_output(['git','-C',str(root),'hash-object','-w','--stdin'],input=staged.encode()).decode().strip()
subprocess.run(['git','-C',str(root),'update-index','--add','--cacheinfo','100644',blob,path],check=True)
print('Staged the approved source, hull, rig and tests; left local HullVisuals/light values outside the index.')
