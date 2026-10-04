"""Equivalence targets: every prefab and scene outside the removed set that reaches a moved GUID,
directly, through a material, or through prefab nesting (closure over all text referrers).

usage: targets6b.py <project-root> <plan.json> <out-guids.txt>
"""
import json, os, re, sys
from collections import defaultdict

ROOT, PLAN, OUT = sys.argv[1:4]
plan = json.load(open(PLAN))
removed = {r["path"] for r in plan["removes"]}
META = re.compile(rb'^guid:\s*([0-9a-f]{32})', re.M)
REF = re.compile(rb'guid["\']?\s*[:=]\s*["\']?([0-9a-fA-F]{32})', re.I)
g2p, refs = {}, defaultdict(set)
for dp, dn, fn in os.walk(os.path.join(ROOT, "Assets")):
    for f in fn:
        rel = os.path.relpath(os.path.join(dp, f), ROOT).replace("\\", "/")
        data = open(os.path.join(ROOT, rel), "rb").read()
        if f.endswith(".meta"):
            m = META.search(data)
            if m:
                g2p[m.group(1).decode()] = rel[:-5]
            continue
        if b"\0" in data[:8000] or data.startswith(b"version https://git-lfs"):
            continue
        refs[rel] = {m.group(1).decode().lower() for m in REF.finditer(data)}
p2g = {p: g for g, p in g2p.items()}
reached, frontier = set(), {m["guid"] for m in plan["moves"]}
while frontier:
    new = {p for p, gs in refs.items() if p not in reached and p not in removed and gs & frontier}
    reached |= new
    frontier = {p2g[p] for p in new if p in p2g}
targets = sorted(p for p in reached if p.endswith((".prefab", ".unity")))
open(OUT, "w", newline="\n").write("".join(p2g[p] + "\n" for p in targets))
print(len(targets), "targets;", len(reached), "reached files")
for t in targets:
    print(" ", t)
