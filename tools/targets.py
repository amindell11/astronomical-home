import json, os, re, sys
from collections import defaultdict
ROOT, PLAN, OUT = sys.argv[1:4]
plan = json.load(open(PLAN))
roots = list(plan["pack_roots"].values())
inpack = lambda p: any(p == r or p.startswith(r + "/") for r in roots)
META = re.compile(r'^guid:\s*([0-9a-f]{32})', re.M)
REF = re.compile(r'guid:\s*([0-9a-f]{32})')
g2p, refs = {}, defaultdict(set)
for dp, dn, fn in os.walk(os.path.join(ROOT, "Assets")):
    for f in fn:
        rel = os.path.relpath(os.path.join(dp, f), ROOT).replace("\\", "/")
        if f.endswith(".meta"):
            m = META.search(open(os.path.join(ROOT, rel), encoding="utf-8", errors="ignore").read())
            if m: g2p[m.group(1)] = rel[:-5]
        elif f.endswith((".prefab", ".unity")):
            refs[rel] = set(REF.findall(open(os.path.join(ROOT, rel), encoding="utf-8", errors="ignore").read()))
p2g = {p: g for g, p in g2p.items()}
seeds = {m["guid"] for m in plan["moves"]}
hit = set(); frontier = set(seeds)
while frontier:
    new = {p for p, gs in refs.items() if not inpack(p) and p not in hit and gs & frontier}
    hit |= new
    frontier = {p2g[p] for p in new}
targets = sorted(hit) + [m["from"] for m in plan["moves"] if m["from"].endswith(".prefab")]
open(OUT, "w", newline="\n").write("".join(p2g[p] + "\n" for p in targets))
for t in targets: print(t)
