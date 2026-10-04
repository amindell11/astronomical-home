import sys, re
def norm(p, rules):
    for a, b in rules:
        p = re.sub(a, b, p)
    return p
RULES_SHIP1 = [
    (r'^/Ship_1_IllustratedRig', '/ShipBaseRig'),
    (r'^/ShipBaseRig/Model', '/ShipBaseRig/Hull'),
    (r'^/ShipBaseRig/Thruster/(ThrustMain|ThrustSmall|ThrustLight)', r'/ShipBaseRig/Thruster/EngineExhaust/\1'),
    (r'^/ShipBaseRig/Thruster/Reactor(?=#|/|$)', '/ShipBaseRig/Thruster/Reactor/ReactorGlow'),
]
def parse(f, rules):
    comps = {}; cur = None
    for line in open(f, encoding='utf-8'):
        if line.startswith('== '): continue
        if line.startswith('  -- '):
            cur = norm(line[5:].strip(), rules); comps[cur] = {}; continue
        k, _, v = line.strip().partition(' = ')
        v = re.sub(r'self:(/[^#\s]*)', lambda m: 'self:' + norm(m.group(1), rules), v)
        comps[cur][k] = v
    return comps
if __name__ == "__main__":
  a = parse(sys.argv[1], RULES_SHIP1 if 'Ship_1' in sys.argv[1] else [])
  b = parse(sys.argv[2], RULES_SHIP1 if 'Ship_1' in sys.argv[2] else [])
  for c in sorted(set(a) | set(b)):
      if c not in a: print('ONLY-B', c); continue
      if c not in b: print('ONLY-A', c); continue
      for k in sorted(set(a[c]) | set(b[c])):
          if a[c].get(k) != b[c].get(k):
              print(f'{c} .{k}: A={a[c].get(k)} | B={b[c].get(k)}')
