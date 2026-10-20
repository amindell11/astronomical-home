from pathlib import Path
import re, shutil, json
root = Path('D:/amind/git/agent-5')
bundle = root / 'scratch/crimson-paint/paint/unity/Crimson'
visuals = root / 'src/Asteroids3D/Assets/Visuals/Ships/Crimson'
generated = (bundle / 'Materials/crimson/Silver.mat').read_text()
shutil.copytree(bundle / 'Paint', visuals / 'Paint', dirs_exist_ok=True)
shutil.copyfile(bundle / 'Paint.meta', visuals / 'Paint.meta')
def section(text, key, end):
    return text.split('    '+key+':\n',1)[1].split(end,1)[0]
def entries(body):
    return dict((m.group(1), m.group(0)) for m in re.finditer(r'^    - (\w+):[^\n]*\n(?:^        .*\n)*', body, re.M))
changes = {}
for relative in ['Hull paint.mat', 'Breakup/Debris paint.mat']:
    path = visuals / relative
    original = path.read_text()
    updated = original.replace('  m_ValidKeywords: []', '  m_ValidKeywords:\n  - _PAINT_LAYERS')
    for key, end in [('m_TexEnvs','    m_Ints:'), ('m_Floats','    m_Colors:'), ('m_Colors','  m_BuildTextureStacks:')]:
        old_body = section(updated, key, end)
        old = entries(old_body)
        new = entries(section(generated, key, end))
        chosen = {name: value for name,value in new.items() if name.startswith('_Paint') or name in ('_TextureStrength','_PaperColor','_BaseMap')}
        for name, value in chosen.items():
            old[name] = value
        updated = updated.replace(old_body, ''.join(old.values()))
    path.write_text(updated, newline='\n')
    changes[relative] = sorted(chosen)
print(json.dumps({'imported_maps':sorted(p.name for p in (visuals/'Paint').glob('*.png')), 'materials':list(changes)}, indent=2))
