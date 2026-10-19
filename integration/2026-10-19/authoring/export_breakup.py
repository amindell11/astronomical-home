import hashlib
import json
from pathlib import Path
import sys

import bpy

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'art/tools/ship'))
import ship_contract
import ship_export
import ship_source

SOURCE = ROOT / 'art/ships/vanguard/Vanguard.blend'
EXPECTED = 'e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031'
OUT = Path(__file__).resolve().parent / 'breakup-export'
OUT.mkdir(parents=True, exist_ok=True)
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == EXPECTED

PAIRS = {
    'Wing': ['MVP Wing', 'Wing service hatch'],
    'WingRoot': ['MVP wing_armature'],
    'Armor': ['MVP wing_armor', 'Pod cooling hatch'],
    'Nacelle': ['MVP Power Nacelle Housing', 'MVP Power Nacelle Core'],
    'SparrowTail': ['MVP Sparrow Tail'],
    'TailFin': ['MVP Tail'],
    'WingFin': ['MVP wing_tail'],
}
NAMES = {'Wing': 'Wing', 'WingRoot': 'Wing root', 'Armor': 'Armor', 'Nacelle': 'Nacelle',
         'SparrowTail': 'Sparrow tail', 'TailFin': 'Tail fin', 'WingFin': 'Wing fin'}
WHOLE = {
    'Central': ['MVP Canopy', 'MVP Cockpit.001', 'MVP Fuselage', 'Cockpit cooling vent'],
    'Ink': ['Vanguard structural ink'],
}
original_open = ship_source.open_source
fingerprints = {}

def open_with_temporary_groups(path):
    scene = original_open(path)
    assert not any(key.startswith('debris.') for key in ship_source.Roles(scene).members)
    fingerprints['before'] = ship_source.fingerprint(scene)['components']
    for group, names in list(WHOLE.items()) + [
        (group + '.' + side, names) for group, names in PAIRS.items() for side in ('L', 'R')
    ]:
        collection = bpy.data.collections.new('role.debris.' + group)
        scene.collection.children.link(collection)
        for name in names:
            collection.objects.link(scene.objects[name])
    fingerprints['with_temporary_groups'] = ship_source.fingerprint(scene)['components']
    assert fingerprints['before'] == fingerprints['with_temporary_groups']
    return scene

ship_source.open_source = open_with_temporary_groups
ship_export.main(['--ship', str(ROOT / 'art/ships/vanguard/ship.json'), '--out', str(OUT), '--debris'])
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == EXPECTED
sidecar = json.loads((OUT / 'VanguardBreakup.export.json').read_text())
mapping = {
    'source_sha256': EXPECTED,
    'source_unchanged': True,
    'fingerprints': fingerprints,
    'piece_groups': {
        group + '.' + side: {
            'existing_piece': name + (' left' if side == 'L' else ' right'),
            'parts': PAIRS[group],
            'material_slots': sidecar['roles'][group + '.' + side]['material_slots'],
        }
        for group, name in NAMES.items() for side in ('L', 'R')
    },
    'helper_groups': {
        'Central': 'Do not instantiate: the existing explosion intentionally omits the central hull and canopy.',
        'Ink': 'Do not instantiate when retaining legacy piece submesh 1: it already contains the exact per-piece ink ownership and uses a flat, untextured material.',
    },
    'integration': [
        'Preserve all 14 existing GameObject names, local pivots, animation curves, piece lifetimes and root transform.',
        'Transform each exported mesh from its model root into the same legacy hull space as the intact substitution; then subtract the existing piece pivot.',
        'L/R here means source symmetry-space X, not a promise about the final left/right coordinate after the hull transform. Match the existing pieces by position.',
        'Replace each saved mesh in place to retain its GUID. Bind all exported material slots from the approved palette, with debris visibility and existing soot treatment.',
        'Retain source PaintUV as UV0; canonical export already puts it first. Do not bind new masks to the old atlas UVs.',
        'Append existing piece submesh 1 unchanged in piece-local coordinates and retain its flat Debris ink material; it contains no baked paint or atlas dependency.',
        'For painted material clones retain debris render queue 3100, soot strength 0.9, and disable core emission; contours retain queue 3101.',
        'To preserve old soot vertex coloring, evaluate its existing formula from transformed hull-space positions and normals before subtracting each pivot.',
        'No source save, debris annotation persistence, geometry change or lock reopen occurred; this scratch producer must be retained with evidence.',
    ],
}
(OUT / 'integration-map.json').write_text(json.dumps(mapping, indent=2) + '\n')
print('BREAKUP_EXPORT_PASS', str(OUT))
