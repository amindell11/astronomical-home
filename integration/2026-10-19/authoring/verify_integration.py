import hashlib
import json
import pathlib
import re
import subprocess

root = pathlib.Path(__file__).resolve().parents[3]
folder = pathlib.Path(__file__).resolve().parent
data = json.loads((folder / 'applied.json').read_text(encoding='utf-8'))
checks = {}
for label, before, after in [('intact', data['current'], data['exported']), ('breakup', data['breakupBefore'], data['breakupAfter'])]:
    new = {row['path']: row for row in after}
    unchanged = 0
    for row in before:
        if label == 'intact' and row['path'].startswith('ShipBaseRig/Hull/Vanguard/Authored mesh/'):
            continue
        peer = new[row['path']]
        for key in ('position', 'rotation', 'scale'):
            assert row[key] == peer[key], (label, row['path'], key)
        unchanged += 1
    checks[label + '_preserved_transforms'] = unchanged
checks['breakup_bounds_max_delta'] = max(abs(row[key][axis] - data['breakupAfter'][i][key][axis]) for i, row in enumerate(data['breakupBefore']) for key in ('boundsCenter', 'boundsSize') for axis in 'xyz')
assert checks['breakup_bounds_max_delta'] < .02, checks
old = {row['path'].rsplit('/', 1)[-1]: row for row in data['current']}
new = {row['path'].rsplit('/', 1)[-1]: row for row in data['exported']}
for a, b in [('MVP Canopy', 'Canopy'), ('MVP Power Nacelle Core', 'Cores'), ('Vanguard painted hull', 'Hull')]:
    delta = max(abs(old[a][key][axis] - new[b][key][axis]) for key in ('worldBoundsCenter', 'worldBoundsSize') for axis in 'xyz')
    assert delta < .00001, (a, delta)
    checks[b + '_bounds_delta'] = delta
asset = root / 'src/Asteroids3D/Assets'
reference = root / 'results/vanguard-redo/clean-canopy-rim/paint/unity/Vanguard/Paint'
runtime = asset / 'Visuals/Ships/Vanguard/Paint'
for texture in reference.glob('*.png'):
    assert texture.read_bytes() == (runtime / texture.name).read_bytes(), texture.name
checks['approved_textures_identical'] = len(list(reference.glob('*.png')))
source_hash = hashlib.sha256((root / 'art/ships/vanguard/Vanguard.blend').read_bytes()).hexdigest()
assert source_hash == 'e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031'
checks['approved_source_sha256'] = source_hash
prefab = 'src/Asteroids3D/Assets/Prefabs/Ships/Vanguard.prefab'
old_text = subprocess.check_output(['git', 'show', '0a8d3dd6391393918a5fc056a68d2f69113ad505:' + prefab], cwd=root).decode()
new_text = (root / prefab).read_text()
def modifications(text):
    chunks = re.findall(r'    - target:.*?(?=    - target:|    m_RemovedComponents:)', text, re.S)
    return sorted(c.strip() for c in chunks if 'silhouette.Array' not in c)
assert modifications(old_text) == modifications(new_text)
checks['non_visual_prefab_overrides_identical'] = True
animation = 'src/Asteroids3D/Assets/Visuals/Ships/Vanguard/Breakup/Vanguard burst.anim'
assert subprocess.check_output(['git', 'show', '0a8d3dd6391393918a5fc056a68d2f69113ad505:' + animation], cwd=root).replace(b'\r\n', b'\n') == (root / animation).read_bytes().replace(b'\r\n', b'\n')
checks['breakup_animation_identical'] = True
(folder / 'verification.json').write_text(json.dumps(checks, indent=2) + '\n')
print(json.dumps(checks, indent=2))
