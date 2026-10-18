import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path('D:/amind/git/agent-4')
OLD = Path('C:/Users/amind/AppData/Local/Temp/codex-vanguard-redo/layer-evidence/paint/2026-10-18/authored-regions/implementation/art/ships/vanguard')
NEW = ROOT / 'art/ships/vanguard'
OUT = ROOT / 'results/vanguard-redo/painted-layers'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

unchanged = ['Vanguard.blend', 'lock.json', 'ship.json', 'palettes.json', 'regions.json']
unchanged += [str(path.relative_to(OLD)) for path in (OLD / 'layers').rglob('*.png')
              if path.stem not in ('Shadow', 'Light')]
for name in unchanged:
    assert digest(OLD / name) == digest(NEW / name), name

changes = {}
for name in ('Shadow', 'Light'):
    old = np.asarray(Image.open(OLD / 'layers' / f'{name}.png'))
    new = np.asarray(Image.open(NEW / 'layers' / f'{name}.png'))
    assert new.shape == (4096, 4096), new.shape
    changed = int(np.count_nonzero(old != new))
    assert changed > 0, name
    changes[name] = {'changed_texels': changed, 'unique_levels': len(np.unique(new)),
                     'min': int(new.min()), 'max': int(new.max())}

paint_report = json.loads((OUT / 'paint/ship_paint.json').read_text())
assert paint_report['verdict'] == 'pass'
assert paint_report['baseline'] == 'reused'
assert paint_report['source_sha256'] == digest(NEW / 'Vanguard.blend')
result = {'verdict': 'pass', 'unchanged_files': unchanged, 'mask_changes': changes,
          'source_sha256': digest(NEW / 'Vanguard.blend'), 'baseline': 'reused',
          'unity_review': 'deferred by owner'}
(OUT / 'layer-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
