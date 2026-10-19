import hashlib
import json
from pathlib import Path
import subprocess

root = Path('D:/amind/git/agent-4')
out = root / 'results/vanguard-redo/asymmetric'
previous = root / 'results/vanguard-redo/painted-layers/paint'
current = out / 'paint'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(root / 'art/ships/vanguard/Vanguard.blend') == 'e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031'
protected = ['art/ships/vanguard/Vanguard.blend', 'art/ships/vanguard/ship.json',
             'art/ships/vanguard/palette.json', 'art/ships/vanguard/lock.json']
subprocess.run(['git', 'diff', '--exit-code', 'd4f68ad3', '--', *protected], cwd=root, check=True)
unchanged = {}
for name in ['Ink.png', 'Emission.png', *[f'RegionWeights{i}.png' for i in range(4)]]:
    relative = Path('unity/Vanguard/Paint') / name
    assert sha(previous / relative) == sha(current / relative), name
    unchanged[name] = sha(current / relative)
for name in ['Light.png', 'Shadow.png']:
    relative = Path('unity/Vanguard/Paint') / name
    assert sha(previous / relative) != sha(current / relative), name
report = json.loads((current / 'ship_paint.json').read_text())
assert report['verdict'] == 'pass'
assert report['baseline'] == 'reused'
assert not any(report['edited_texels'].values())
result = {'verdict': 'pass', 'source_sha256': sha(root / 'art/ships/vanguard/Vanguard.blend'),
          'unchanged_runtime_masks': unchanged, 'changed_runtime_masks': ['Light', 'Shadow'],
          'owner_edited_texels': report['edited_texels'], 'baseline': report['baseline']}
(out / 'preservation.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result, indent=2))
