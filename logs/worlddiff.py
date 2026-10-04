import sys, re
from propdiff import norm, RULES_SHIP1
def load(f, rules):
    d = {}
    for line in open(f, encoding='utf-8'):
        m = re.match(r'^(\S+) (.*) \[(.*)\]$', line.strip())
        k, p, mx = m.group(1), m.group(2), [float(x) for x in m.group(3).split(',')]
        d[(k, norm(p, rules))] = mx
    return d
a = load(sys.argv[1], RULES_SHIP1); b = load(sys.argv[2], [])
worst = {}
for key in sorted(set(a) | set(b)):
    if key not in a: print('ONLY-AFTER', *key); continue
    if key not in b: print('ONLY-BEFORE', *key); continue
    d = max(abs(x - y) for x, y in zip(a[key], b[key]))
    worst[key[0]] = max(worst.get(key[0], 0), d)
    if d > 0: print(f'{d:.3g}', *key)
print('MAX by kind:', {k: f'{v:.3g}' for k, v in worst.items()})
