"""Slice 6b art/ copy map (source path at base -> art path), and the index-info that stages it with the
source blob ids.

usage: artmap.py <repo> <base> <out-artmap.json> <out-index-info>
"""
import json, subprocess, sys

REPO, BASE, OUT_MAP, OUT_INFO = sys.argv[1:5]
V = "src/Asteroids3D/Assets/Visuals/"
groups = {
    "art/ships/nightshade/concepts/": ["Ships/Nightshade/" + f for f in (
        "chatGptImageJan272026121500Am.png", "chatGptImageJan272026121643Am.png", "jan272026122322Am.png",
        "ship3EnginesOff.png", "ship3EnginesOn.png", "ship3NoWings.png", "shipTemplate2.cfg")],
    "art/ships/vanguard/concepts/": ["Ships/Ship1/" + f for f in ("ship.png", "ship2Small.png", "shipEnginesOn.png", "shipTemplate.cfg")],
    "art/concepts/": ["Environment/Asteroids/Jan 27, 2026, 12_23_20 AM.png"],
    "art/vfx/explosion/refs/": ["Vfx/Explosion/exp1Zps03A1Bdde.gif"],
    "art/ships/vanguard/drawn-study/": ["Ships/Vanguard/DrawnStudy/" + f for f in (
        "HangarBackground-v2-ui.png", "NebulaBackground-v2.png", "PlanetBackground-v1.png", "PlanetBackground-v2-ui.png",
        "ShadowedPlate.shader")],
    "art/asteroid-study/": ["Environment/Asteroids/DrawnStudy/" + f for f in (
        "AsteroidFractureStudy.fbx", "AsteroidPaintStudy.fbx", "AsteroidSurfaceDrawing.fbx", "AsteroidFracturePaint.png",
        "AsteroidPaint.png", "AsteroidScuffNormal.png", "AsteroidStoneAlbedo.png")],
    "art/meshy/StarshipVanguard0606163455TextureObj/": [
        "Ships/_Shared/StarshipVanguard0606163455TextureObj/starshipVanguard.obj",
        "Ships/_Shared/StarshipVanguard0606163455TextureObj/starshipVanguard0606163455Texture.png",
        "Ships/Ship1/StarshipVanguard0606163455TextureObj/starshipVanguardMetal.png",
        "Ships/Ship1/StarshipVanguard0606163455TextureObj/shippEmission.png"],
    "art/meshy/GalacticCruiserTop0705014531TextureFbx/": [
        "Ships/Nightshade/GalacticCruiserTop0705014531TextureFbx/cruiserUpdate1.fbx",
        "Ships/Nightshade/GalacticCruiserTop0705014531TextureFbx/textureAlbedo.png"],
    "art/meshy/AColossalDarkGray0729085551TextureFbx/": [
        "Environment/Models/AColossalDarkGray0729085551TextureFbx/AColossalDarkGray0729085551TextureFbx/" + f
        for f in ("aColossalDarkGray0729085551Texture.fbx", "aColossalDarkGray0729085551Texture.png")],
}
srcs = [V + s for g in groups.values() for s in g]
out = subprocess.run(["git", "-C", REPO, "ls-tree", "-z", BASE, "--", *srcs], capture_output=True, check=True).stdout
tree = {}
for line in out.split(b"\0"):
    if line:
        meta, path = line.split(b"\t", 1)
        mode, _, oid = meta.decode().split()
        tree[path.decode()] = (mode, oid)
missing = [s for s in srcs if s not in tree]
assert not missing, missing
amap, info = [], []
for dest, files in groups.items():
    for s in files:
        src = V + s
        dst = dest + s.rsplit("/", 1)[1]
        mode, oid = tree[src]
        amap.append({"from": src, "to": dst, "mode": mode, "oid": oid})
        info.append(f"{mode} {oid}\t{dst}\n")
dsts = [a["to"] for a in amap]
assert len(set(dsts)) == len(dsts)
existing = subprocess.run(["git", "-C", REPO, "ls-tree", "-r", "--name-only", "-z", BASE, "--", "art"], capture_output=True, check=True).stdout.decode().split("\0")
clash = set(dsts) & set(existing)
assert not clash, clash
json.dump(amap, open(OUT_MAP, "w"), indent=1)
open(OUT_INFO, "w", newline="\n").write("".join(info))
print(len(amap), "art copies")
