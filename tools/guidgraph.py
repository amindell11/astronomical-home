"""GUID graph over Assets/ + ProjectSettings/: keep sets for slice 6a vendor packs.

usage: guidgraph.py <project-root> <out.json>
"""
import json, os, re, sys
from collections import defaultdict

ROOT = sys.argv[1]
OUT = sys.argv[2]
PACKS = {
    "ParticlePack": "Assets/Visuals/ParticlePack",
    "HD_Asteroids": "Assets/Visuals/Environment/Asteroids/HD_Asteroids",
    "Station_Hug": "Assets/Visuals/Environment/Station_Hug",
    "Station_Fork": "Assets/Visuals/Environment/Station_Fork",
    "Radio": "Assets/Visuals/Environment/Radio",
    "Junk_Ships": "Assets/Visuals/Environment/Junk_Ships",
    "Platform": "Assets/Visuals/Environment/Platform",
    "Station_Wheel": "Assets/Visuals/Environment/Station_Wheel",
    "Station_Spindle": "Assets/Visuals/Environment/Station_Spindle",
    "Station_Dildo": "Assets/Visuals/Environment/Station_Dildo",
}
EXCLUDED_SOURCES = {"Assets/Scenes/EditScene.unity": {"Station_Wheel", "Station_Spindle", "Station_Dildo"}}
GUID_RE = re.compile(rb'guid["\']?\s*[:=]\s*["\']?([0-9a-fA-F]{32})', re.I)
ASMDEF_RE = re.compile(rb'GUID:([0-9a-fA-F]{32})')
META_GUID_RE = re.compile(rb'^guid:\s*([0-9a-f]{32})', re.M)


def pack_of(path):
    for name, root in PACKS.items():
        if path == root or path.startswith(root + "/"):
            return name
    return None


def walk(top):
    for dp, dns, fns in os.walk(os.path.join(ROOT, top)):
        for fn in fns:
            full = os.path.join(dp, fn)
            yield os.path.relpath(full, ROOT).replace("\\", "/")


guid_to_path = {}
for rel in walk("Assets"):
    if rel.endswith(".meta"):
        with open(os.path.join(ROOT, rel), "rb") as f:
            m = META_GUID_RE.search(f.read())
        if m:
            guid_to_path[m.group(1).decode()] = rel[:-5]

refs = defaultdict(set)  # asset path (meta folded into its asset) -> guids
for top in ("Assets", "ProjectSettings"):
    for rel in walk(top):
        with open(os.path.join(ROOT, rel), "rb") as f:
            data = f.read()
        if b"\0" in data[:8000]:
            continue
        owner = rel[:-5] if rel.endswith(".meta") else rel
        own_guid = None
        for m in GUID_RE.finditer(data):
            refs[owner].add(m.group(1).decode().lower())
        for m in ASMDEF_RE.finditer(data):
            refs[owner].add(m.group(1).decode().lower())

for owner, gs in refs.items():
    selfg = [g for g, p in guid_to_path.items() if p == owner]
    for g in selfg:
        gs.discard(g)

referrers = defaultdict(set)
for owner, gs in refs.items():
    for g in gs:
        if g in guid_to_path:
            referrers[guid_to_path[g]].add(owner)

# closure from owned files
kept = set()
frontier = []
for owner, gs in refs.items():
    if pack_of(owner):
        continue
    for g in gs:
        p = guid_to_path.get(g)
        if not p or not pack_of(p):
            continue
        if pack_of(p) in EXCLUDED_SOURCES.get(owner, ()):
            continue
        frontier.append(p)
while frontier:
    p = frontier.pop()
    if p in kept:
        continue
    kept.add(p)
    for g in refs.get(p, ()):
        q = guid_to_path.get(g)
        if q and pack_of(q) and q not in kept:
            frontier.append(q)

result = {}
for name, root in PACKS.items():
    files = sorted(p for p in guid_to_path.values() if pack_of(p) == name and os.path.isfile(os.path.join(ROOT, p)))
    k = sorted(p for p in files if p in kept)
    result[name] = {
        "files": len(files),
        "kept": k,
        "kept_referrers": {p: sorted(r for r in referrers[p] if pack_of(r) != name) for p in k},
    }
    cross = sorted(p for p in kept if pack_of(p) == name and not os.path.isfile(os.path.join(ROOT, p)))
    if cross:
        result[name]["kept_dirs"] = cross

edit_refs = sorted(guid_to_path[g] for g in refs.get("Assets/Scenes/EditScene.unity", ()) if g in guid_to_path and pack_of(guid_to_path[g]))
result["_EditScene_pack_refs"] = edit_refs
with open(OUT, "w") as f:
    json.dump(result, f, indent=1)
for name in PACKS:
    print(name, result[name]["files"], len(result[name]["kept"]))
