"""Root-relative world matrices of the brief's subjects, old Valis vs the rebuilt variant."""
import re, sys
def load(p):
    d = {}
    for line in open(p, encoding='utf-8'):
        m = re.match(r'(\S+) (.*) (\[[^\]]*\])$', line.strip())
        if m: d[(m.group(1), m.group(2))] = [float(x) for x in m.group(3)[1:-1].split(',')]
    return d
old, new = load(sys.argv[1]), load(sys.argv[2])
H_OLD, H_NEW = '/Valis VisualRig/Valis', '/ShipBaseRig/Hull/Valis'
bones = ['', '/Flight surfaces/Upper wing right', '/Flight surfaces/Upper wing left', '/Flight surfaces/Lower wing right', '/Flight surfaces/Lower wing left']
subjects = [('renderer', H_OLD + '#SkinnedMeshRenderer', H_NEW + '#SkinnedMeshRenderer')]
subjects += [('transform', H_OLD + b + '#Transform', H_NEW + b + '#Transform') for b in bones]
subjects += [('transform', H_OLD + '/Flight surfaces#Transform', H_NEW + '/Flight surfaces#Transform')]
subjects += [('collider', '/Mesh#MeshCollider', '/Mesh#MeshCollider'), ('collider', '/ShipBody#SphereCollider', '/ShipBody#SphereCollider')]
subjects += [('hardpoint[0]', '/Hardpoints/Primary#Transform', '/Hardpoints/Primary#Transform'), ('hardpoint[1]', '/Hardpoints/Secondary#Transform', '/Hardpoints/Secondary#Transform')]
for f in ('ThrustMain', 'ThrustSmall'):
    subjects.append(('emitter', f'/Valis VisualRig/Thruster/{f}#ParticleSystem', f'/ShipBaseRig/Thruster/EngineExhaust/{f}#ParticleSystem'))
subjects += [('renderer', '/Valis VisualRig/MinimapMarker#MeshRenderer', '/ShipBaseRig/MinimapMarker#MeshRenderer')]
worst = 0.0
for kind, a, b in subjects:
    ma, mb = old.get((kind, a)), new.get((kind, b))
    if ma is None or mb is None:
        print(f'MISSING {kind} {a if ma is None else ""} {b if mb is None else ""}'); worst = float('inf'); continue
    dev = max(abs(x - y) for x, y in zip(ma, mb))
    worst = max(worst, dev)
    print(f'{kind:12} {a:58} -> {b:62} max|d|={dev:.3g}')
print(f'WORST {worst:.3g}')
