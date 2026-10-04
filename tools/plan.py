"""Build the slice 6a move/delete plan from graph_before.json.

usage: plan.py <project-root> <graph.json> <plan.json>
"""
import json, os, sys

ROOT, GRAPH, OUT = sys.argv[1:4]
g = json.load(open(GRAPH))
V = "Assets/Visuals"
E = V + "/Environment"
PARTICLES = V + "/Vfx/Particles"
SHAPES = E + "/Asteroids/Shapes"
ST = E + "/Stations"

FE = "EffectExamples/Fire & Explosion Effects/"
HUG_TEX = {"ao": "AO", "color": "Color", "emission": "Emission",
           "metallicRoughnessChannelsG": "MetallicRoughnessG", "normal": "Normal"}


def target(pack, rel):
    base, name = os.path.split(rel)
    stem, ext = os.path.splitext(name)
    if pack == "ParticlePack":
        if rel.startswith(FE + "Materials/"):
            return f"{PARTICLES}/Materials/{name}"
        if rel.startswith(FE + "Textures/"):
            return f"{PARTICLES}/Textures/{name}"
        if rel == FE + "Prefabs/ParticlesLight.prefab":
            return f"{PARTICLES}/{name}"
    if pack == "HD_Asteroids":
        if rel.startswith("Models/"):
            return f"{SHAPES}/Models/{name}"
        if rel.startswith("Materials/"):
            return f"{SHAPES}/Materials/Asteroid{name}"
        if rel.startswith("Textures/Tiled_Tiled_"):
            return f"{SHAPES}/Textures/Asteroid_Tiled_{name[len('Tiled_Tiled_'):]}"
        if rel.startswith("Textures/"):
            return f"{SHAPES}/Textures/{name}"
    if pack == "Station_Hug":
        d = f"{ST}/Station"
        if ext in (".obj", ".mtl"):
            return f"{d}/{name}"
        if rel == "test.mat":
            return f"{d}/Station.mat"
        if rel.startswith("Textures/"):
            return f"{d}/Station_{HUG_TEX[stem]}{ext}"
    if pack == "Station_Fork":
        d = f"{ST}/Station 3"
        if ext == ".fbx":
            return f"{d}/{name}"
        if name == "Station1.mat":
            return f"{d}/Station 3.mat"
        if stem.startswith("station_1_"):
            return f"{d}/Station 3_{stem[len('station_1_'):]}{ext}"
    if pack == "Radio":
        d = f"{ST}/Radio"
        if ext == ".fbx":
            return f"{d}/{name}"
        if name == "RL_station_mat_N1.mat":
            return f"{d}/Radio.mat"
        if stem.startswith("RL_station_mat_N_01_"):
            return f"{d}/Radio_{stem[len('RL_station_mat_N_01_'):]}{ext}"
    if pack == "Junk_Ships":
        d = f"{V}/Ships/Cargo Ships/Junker"
        if ext == ".fbx":
            return f"{d}/{name}"
        if name == "Junkyard.mat":
            return f"{d}/Junker.mat"
        if stem.startswith("Material__0_"):
            return f"{d}/Junker_{stem[len('Material__0_'):]}{ext}"
    raise SystemExit(f"no target for {pack}/{rel}")


PACK_ROOTS = {
    "ParticlePack": V + "/ParticlePack",
    "HD_Asteroids": E + "/Asteroids/HD_Asteroids",
}
moves, deletes = [], []
for pack, info in g.items():
    if pack.startswith("_"):
        continue
    root = PACK_ROOTS.get(pack, f"{E}/{pack}")
    kept = list(info["kept"])
    if pack == "Station_Hug":
        kept.append(root + "/p162Spaceship004.mtl")  # OBJ sidecar: read by name, no GUID edge
    for p in kept:
        moves.append({"pack": pack, "from": p, "to": target(pack, p[len(root) + 1:])})
    keptset = set(kept)
    for dp, dns, fns in os.walk(os.path.join(ROOT, root)):
        for fn in fns:
            if fn.endswith(".meta"):
                continue
            rel = os.path.relpath(os.path.join(dp, fn), ROOT).replace("\\", "/")
            if rel not in keptset:
                deletes.append({"pack": pack, "path": rel})
tos = [m["to"] for m in moves]
assert len(tos) == len(set(tos)), "target collision"
for m in moves:
    with open(os.path.join(ROOT, m["from"] + ".meta")) as f:
        m["guid"] = next(l.split()[1] for l in f if l.startswith("guid:"))
for d in deletes:
    with open(os.path.join(ROOT, d["path"] + ".meta")) as f:
        d["guid"] = next(l.split()[1] for l in f if l.startswith("guid:"))
json.dump({"moves": moves, "deletes": deletes,
           "pack_roots": {p: PACK_ROOTS.get(p, f"{E}/{p}") for p in g if not p.startswith("_")}},
          open(OUT, "w"), indent=1)
print(len(moves), "moves", len(deletes), "deletes")
for m in moves:
    print(f"{m['from'][len('Assets/Visuals/'):]}  ->  {m['to'][len('Assets/Visuals/'):]}")
