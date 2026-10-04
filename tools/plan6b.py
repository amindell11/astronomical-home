"""Slice 6b plan: the brief's moves/removals as data, plus GUID-graph verification on a project tree.

usage: plan6b.py <project-root> <out-plan.json>
Prints per-file referrers; exits 1 when a move has no outside referrer or a removal has one.
"""
import json, os, re, sys
from collections import defaultdict

ROOT, OUT = sys.argv[1:3]
V = "Assets/Visuals/"
MOVES = [
    ("Shaders/DrawnComparison/DrawnSurface.shader", "Shaders/DrawnSurface.shader"),
    ("Shaders/DrawnComparison/DrawnContour.shader", "Shaders/DrawnContour.shader"),
    ("Shaders/DrawnComparison/DrawnCanopy.shader", "Shaders/DrawnCanopy.shader"),
    ("Environment/Asteroids/DrawnStudy/AsteroidBrushAlbedo.png", "Environment/Asteroids/DrawnField/AsteroidBrushAlbedo.png"),
    ("Vfx/ApprovedPlasma/DrawnExplosion.shader", "Vfx/LayeredExplosion/Materials/DrawnExplosion.shader"),
    ("Ships/Nightshade/GalacticCruiserTop0705014531TextureFbx/cruiserUpdate1.fbx", "Ships/Nightshade/cruiserUpdate1.fbx"),
    ("Ships/Nightshade/GalacticCruiserTop0705014531TextureFbx/textureAlbedo.png", "Ships/Nightshade/Nightshade_Albedo.png"),
    ("Ships/Ship1/StarshipVanguard0606163455TextureObj/shippEmission.png", "Ships/_Shared/ShipMaterial_Emission.png"),
    ("Ships/Ship1/StarshipVanguard0606163455TextureObj/starshipVanguardMetal.png", "Ships/_Shared/ShipMaterial_Metal.png"),
    ("Ships/_Shared/StarshipVanguard0606163455TextureObj/starshipVanguard0606163455Texture.png", "Ships/_Shared/ShipMaterial_Texture.png"),
    ("Ships/Ship1/ship2.png", "Ui/AppIcon/ship2.png"),
]
MOVES = [(V + a, V + b) for a, b in MOVES]
# Folders emptied and deleted by this slice; every non-moved file inside is removed.
REMOVED_ROOTS = [V + p for p in (
    "Studies", "Ships/Vanguard/DrawnStudy", "Environment/Asteroids/DrawnStudy", "Shaders/DrawnComparison",
    "Vfx/ApprovedPlasma", "Vfx/Explosion", "Environment/Models",
    "Ships/Nightshade/GalacticCruiserTop0705014531TextureFbx", "Ships/Ship1",
    "Ships/_Shared/StarshipVanguard0606163455TextureObj")] + ["Assets/Scripts/Editor/Visuals"]
# Loose removed files outside the removed roots.
REMOVED_LOOSE = [V + p for p in (
    "Ships/Nightshade/chatGptImageJan272026121500Am.png", "Ships/Nightshade/chatGptImageJan272026121643Am.png",
    "Ships/Nightshade/jan272026122322Am.png", "Ships/Nightshade/ship3EnginesOff.png",
    "Ships/Nightshade/ship3EnginesOn.png", "Ships/Nightshade/ship3NoWings.png", "Ships/Nightshade/shipTemplate2.cfg",
    "Environment/Asteroids/Jan 27, 2026, 12_23_20 AM.png")]

GUID_RE = re.compile(rb'guid["\']?\s*[:=]\s*["\']?([0-9a-fA-F]{32})', re.I)
ASMDEF_RE = re.compile(rb'GUID:([0-9a-fA-F]{32})')
META_GUID_RE = re.compile(rb'^guid:\s*([0-9a-f]{32})', re.M)


def walk(top):
    for dp, dns, fns in os.walk(os.path.join(ROOT, top)):
        for d in dns:
            yield os.path.relpath(os.path.join(dp, d), ROOT).replace("\\", "/"), True
        for fn in fns:
            yield os.path.relpath(os.path.join(dp, fn), ROOT).replace("\\", "/"), False


under = lambda p, roots: any(p == r or p.startswith(r + "/") for r in roots)
moved_from = {a for a, _ in MOVES}

guid_to_path, files, folders = {}, [], []
refs = defaultdict(set)
for top in ("Assets", "ProjectSettings", "Packages"):
    for rel, isdir in walk(top):
        if isdir:
            if top == "Assets":
                folders.append(rel)
            continue
        with open(os.path.join(ROOT, rel), "rb") as f:
            data = f.read()
        if rel.endswith(".meta"):
            m = META_GUID_RE.search(data)
            if m:
                guid_to_path[m.group(1).decode()] = rel[:-5]
        elif top == "Assets":
            files.append(rel)
        if b"\0" in data[:8000] or data.startswith(b"version https://git-lfs"):
            continue
        owner = rel[:-5] if rel.endswith(".meta") else rel
        refs[owner].update(m.group(1).decode().lower() for m in GUID_RE.finditer(data))
        refs[owner].update(m.group(1).decode().lower() for m in ASMDEF_RE.finditer(data))
path_to_guid = {p: g for g, p in guid_to_path.items()}
for owner, gs in refs.items():
    gs.discard(path_to_guid.get(owner))
referrers = defaultdict(set)
for owner, gs in refs.items():
    for g in gs:
        if g in guid_to_path:
            referrers[guid_to_path[g]].add(owner)

removed_files = sorted(p for p in files if (under(p, REMOVED_ROOTS) and p not in moved_from) or p in REMOVED_LOOSE)
missing = [p for p in REMOVED_LOOSE + [a for a, _ in MOVES] if not os.path.isfile(os.path.join(ROOT, p))]
removed_folders = sorted((p for p in folders if under(p, REMOVED_ROOTS)), key=lambda p: (-p.count("/"), p))
removed_set = set(removed_files) | set(removed_folders)
fails = []
if missing:
    fails.append(f"missing planned sources: {missing}")

print("== moves (referrers outside the removed set)")
moves_out = []
for a, b in MOVES:
    outside = sorted(r for r in referrers[a] if r not in removed_set)
    inside = sorted(r for r in referrers[a] if r in removed_set)
    print(f"{a}\n  -> {b}\n  guid {path_to_guid.get(a)} outside={outside} removed-set referrers={len(inside)}")
    if not outside:
        fails.append(f"move without outside referrer: {a}")
    moves_out.append({"from": a, "to": b, "guid": path_to_guid[a], "referrers": outside, "removed_set_referrers": inside})

print("== removals with a referrer outside the removed set")
removes_out = []
for p in removed_files + removed_folders:
    outside = sorted(r for r in referrers[p] if r not in removed_set)
    if outside:
        print(f"  {p}: {outside}")
        fails.append(f"removal referenced from outside: {p}")
    removes_out.append({"path": p, "guid": path_to_guid.get(p), "folder": p in removed_folders})
print(f"removed files {len(removed_files)}, removed folders {len(removed_folders)}")
for p in removed_files:
    print("  rm", p)
json.dump({"moves": moves_out, "removes": removes_out, "removed_roots": REMOVED_ROOTS}, open(OUT, "w"), indent=1)
print("FAILS:", fails if fails else "none")
sys.exit(1 if fails else 0)
