"""Serialized-property diff of every component, old Valis vs the rebuilt variant, paths mapped onto the base rig."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from props import load
a, ga = load(sys.argv[1]); b, gb = load(sys.argv[2])
def mp(p):
    p = p.replace('/Valis VisualRig/Valis', '/ShipBaseRig/Hull/Valis').replace('/Valis VisualRig', '/ShipBaseRig')
    for f in ('ThrustMain', 'ThrustSmall', 'ThrustLight'): p = p.replace('/Thruster/' + f, '/Thruster/EngineExhaust/' + f)
    return p
seen = set()
for k in a:
    kb = mp(k); seen.add(kb)
    if kb not in b: print('GONE   ', k); continue
    d = [(p, a[k].get(p), b[kb].get(p)) for p in sorted(set(a[k]) | set(b[kb])) if a[k].get(p) != b[kb].get(p)]
    d = [x for x in d if not (x[1] and x[2] and x[1].startswith('self:') and mp(x[1]) == x[2])]
    for p, x, y in d: print(f'CHANGED {k} .{p}: {x} -> {y}')
for k in b:
    if k not in seen: print('NEW    ', k)
for p, v in ga.items():
    q = mp('/' + p)[1:]
    if q in gb and gb[q] != v: print('GOFLAGS', p, v, '->', gb[q])
