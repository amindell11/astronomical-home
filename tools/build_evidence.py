"""Build the slice 6b evidence commits with plumbing (temp index, source blob ids, no worktree).

usage: build_evidence.py <repo> <base> <scratch-dir>
Writes <scratch>/evidence.json: branch -> {parent, commit, entries[{src, dst, mode, oid}]}.
"""
import json, os, subprocess, sys

REPO, BASE, SCRATCH = sys.argv[1:4]
SRC = "src/Asteroids3D/"
ART = "art/ships/vanguard/drawn-study/"


def git(*a, inp=None, env=None):
    return subprocess.run(["git", "-C", REPO, *a], input=inp, capture_output=True, check=True,
                          env={**os.environ, **(env or {})}).stdout


def tree_of(rev, *paths):
    out = {}
    for line in git("ls-tree", "-r", "-z", rev, "--", *paths).split(b"\0"):
        if line:
            meta, path = line.split(b"\t", 1)
            mode, _, oid = meta.decode().split()
            out[path.decode()] = (mode, oid)
    return out


def blob(text):
    return git("hash-object", "-w", "--stdin", inp=text.encode()).decode().strip()


base_attr = tree_of(BASE, SRC + ".gitattributes")[SRC + ".gitattributes"]

DRAWN_ART_README = f"""# Vanguard drawn-art study (Unity side)

Removed from main by slice 6b of #868 (issue #921). Source commit `{BASE}`. Every file is
byte-identical to its source (same blob id), `.meta` files included, at its original path
under `src/Asteroids3D/`. The nested `.gitattributes` is `src/Asteroids3D/.gitattributes`, so
a checkout reproduces the source bytes (LFS files smudge, Unity YAML keeps LF).

| Original path | Contents |
| --- | --- |
| `Assets/Visuals/Studies/DrawnArt/` | 3 study scenes (`AsteroidField`, `HangarHero`, `SpaceHero`), 2 prefabs (`AsteroidContext`, `VanguardPreview`), 13 materials, 11 contour mesh assets, `Lighting/PodBloom.asset`, the study README |
| `Assets/Visuals/Ships/Vanguard/DrawnStudy/` | 4 backdrop plates and `ShadowedPlate.shader` (`Astronomical/Comparison/Shadowed Plate`) |
| `Assets/Scripts/Editor/Visuals/Studies/` | `ArtPreviewAuthoring.cs`, `ArtPreviewCapture.cs`, `Visuals.Studies.Editor.asmdef` |

The backdrop plates and `ShadowedPlate.shader` also stay on main as raw files, without
`.meta`, in `{ART}`. The Blender producers that exported the study meshes are in
`producers/drawn-study/` on this branch.

## Revive

From a checkout of this branch:

    cp -r studies/drawn-art/Assets/. <repo>/src/Asteroids3D/Assets/

Copy each `.meta` with its asset: it carries the GUID. The study points at production GUIDs,
so it revives only while those exist: Vanguard's `VanguardStructure.fbx`,
`VanguardBaseColor.png` and `Canopy`, `Engine glow` and `Structural ink` materials; the ten
`DrawnField` shapes (`Asteroid<N>.fbx`, `Asteroid<N>Drawing.fbx`, `Asteroid<N>Paint.mat`) and
`FieldGraphite.mat`; and the drawn shaders, now `Assets/Visuals/Shaders/DrawnSurface.shader`
and `DrawnContour.shader`, named `Astronomical/Drawn/Surface` and `Astronomical/Drawn/Contour`.
`ArtPreviewAuthoring.cs` finds those shaders by their old names (`Astronomical/Comparison/*`);
update the strings when reviving it.
"""

DRAWN_STUDY_README = f"""# Drawn asteroid study (Unity side)

Removed from main by slice 6b of #868 (issue #921). Source commit `{BASE}`. The ten study
files of `src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy/` with their `.meta`
files (and the folder's), byte-identical (same blob ids), at their original paths: 3 FBX
(`AsteroidFractureStudy`, `AsteroidPaintStudy`, `AsteroidSurfaceDrawing`), 4 textures
(`AsteroidFracturePaint`, `AsteroidPaint`, `AsteroidScuffNormal`, `AsteroidStoneAlbedo`) and
3 materials. The nested `.gitattributes` is `src/Asteroids3D/.gitattributes`, so a checkout
reproduces the source bytes.

The folder's fifth texture, `AsteroidBrushAlbedo.png`, is production: it moved (same GUID)
to `Assets/Visuals/Environment/Asteroids/DrawnField/`. The raw FBX and textures also stay on
main, without `.meta`, in `art/asteroid-study/` beside their blends.

## Revive

From a checkout of this branch:

    cp -r studies/drawn-study/Assets/. <repo>/src/Asteroids3D/Assets/

Copy each `.meta` with its asset: it carries the GUID. The three materials use the drawn
surface shader by GUID, now `Assets/Visuals/Shaders/DrawnSurface.shader`
(`Astronomical/Drawn/Surface`); they revive only while it exists.
"""


def entries(prefix, rels, exclude=()):
    paths = [SRC + r for r in rels]
    t = tree_of(BASE, *paths)
    # include the parent folder .meta of each root
    for r in rels:
        m = SRC + r + ".meta"
        t.update(tree_of(BASE, m))
    out = []
    for p, (mode, oid) in sorted(t.items()):
        rel = p[len(SRC):]
        if any(rel == e or rel == e + ".meta" for e in exclude):
            continue
        out.append({"src": p, "dst": prefix + rel, "mode": mode, "oid": oid})
    return out


def build(branch, adds, readme_edit, message):
    tip = git("rev-parse", "origin/" + branch).decode().strip()
    idx = os.path.join(SCRATCH, branch.replace("/", "_") + ".idx")
    if os.path.exists(idx):
        os.remove(idx)
    env = {"GIT_INDEX_FILE": idx}
    git("read-tree", tip, env=env)
    existing = set(tree_of(tip).keys())
    clash = [a["dst"] for a in adds if a["dst"] in existing]
    assert not clash, clash
    root_readme = git("cat-file", "-p", tip + ":README.md").decode()
    new_readme = readme_edit(root_readme)
    assert new_readme != root_readme
    info = "".join(f"{a['mode']} {a['oid']}\t{a['dst']}\n" for a in adds)
    info += f"100644 {blob(new_readme)}\tREADME.md\n"
    git("update-index", "--add", "--index-info", inp=info.encode(), env=env)
    tree = git("write-tree", env=env).decode().strip()
    commit = git("commit-tree", tree, "-p", tip, "-m", message).decode().strip()
    return {"parent": tip, "commit": commit, "entries": adds}


def text_entry(dst, text):
    return {"src": None, "dst": dst, "mode": "100644", "oid": blob(text)}


van = entries("studies/drawn-art/", ["Assets/Visuals/Studies", "Assets/Visuals/Ships/Vanguard/DrawnStudy",
                                     "Assets/Scripts/Editor/Visuals"])
van += [{"src": SRC + ".gitattributes", "dst": "studies/drawn-art/.gitattributes", "mode": base_attr[0], "oid": base_attr[1]},
        text_entry("studies/drawn-art/README.md", DRAWN_ART_README)]
prod = tree_of(BASE, ART + "export_study.py", ART + "build_structure.py")
van += [{"src": p, "dst": "producers/drawn-study/" + p.rsplit("/", 1)[1], "mode": m, "oid": o} for p, (m, o) in sorted(prod.items())]
assert len(prod) == 2


def van_readme(t):
    old_row = "| producers | `producers/drawn-study/` | `author-vanguard-wear.py` and the `inspect-vanguard*.py` probes, saved from pool slot 4's gitignored `results/`. |"
    assert t.count(old_row) == 1
    new_rows = (old_row[:-2] + " `export_study.py` and `build_structure.py`, the drawn study's Blender-to-Unity export, retired from `art/ships/vanguard/drawn-study/` by #921. |\n"
                "| studies | `studies/drawn-art/` | The Unity side of the drawn-art study, removed from main by #921: scenes, prefabs, materials, meshes, backdrop plates and capture tools, `.meta` included. Its README has the revive recipe. |")
    return t.replace(old_row, new_rows)


ast = entries("studies/drawn-study/", ["Assets/Visuals/Environment/Asteroids/DrawnStudy"],
              exclude=["Assets/Visuals/Environment/Asteroids/DrawnStudy/AsteroidBrushAlbedo.png"])
ast += [{"src": SRC + ".gitattributes", "dst": "studies/drawn-study/.gitattributes", "mode": base_attr[0], "oid": base_attr[1]},
        text_entry("studies/drawn-study/README.md", DRAWN_STUDY_README)]


def ast_readme(t):
    return t.rstrip("\n") + ("\n\n## studies/drawn-study/\n\n"
                             "The Unity side of the single-rock drawn study, removed from main by #921: the study FBX, "
                             "textures and materials of `Assets/Visuals/Environment/Asteroids/DrawnStudy/`, `.meta` "
                             "included. Its README has the revive recipe.\n")


res = {
    "evidence/vanguard": build("evidence/vanguard", van, van_readme,
                               f"evidence: drawn-art study and its producers, removed from main by #921 (source {BASE})"),
    "evidence/asteroids": build("evidence/asteroids", ast, ast_readme,
                                f"evidence: drawn asteroid study, removed from main by #921 (source {BASE})"),
}
json.dump(res, open(os.path.join(SCRATCH, "evidence.json"), "w"), indent=1)
for b, r in res.items():
    print(b, r["parent"][:8], "->", r["commit"][:8], len(r["entries"]), "entries")
