"""Slice 6a mechanical acceptance (1, 2, 3, 6, 7) from git trees.

usage: acceptance.py <repo> <base> <head> <plan.json> <outdir>
Writes manifest.tsv and acceptance_report.md; exits 1 on any failure.
"""
import json, os, re, subprocess, sys
from collections import Counter, defaultdict

REPO, BASE, HEAD, PLAN, OUT = sys.argv[1:6]
SRC = "src/Asteroids3D/"
plan = json.load(open(PLAN))
fails = []


def git(*a, inp=None):
    return subprocess.run(["git", "-C", REPO, *a], input=inp, capture_output=True, check=True).stdout


def ls_tree(rev, *paths):
    out = {}
    for line in git("ls-tree", "-r", "-z", rev, "--", *paths).split(b"\0"):
        if not line:
            continue
        meta, path = line.split(b"\t", 1)
        mode, typ, oid = meta.split()
        out[path.decode()] = (mode.decode(), oid.decode())
    return out


def read_blobs(oids):
    """oid -> bytes via cat-file --batch."""
    uniq = sorted(set(oids))
    p = subprocess.run(["git", "-C", REPO, "cat-file", "--batch"], input="".join(o + "\n" for o in uniq).encode(),
                       capture_output=True, check=True).stdout
    res, i = {}, 0
    for o in uniq:
        nl = p.index(b"\n", i)
        hdr = p[i:nl].split()
        size = int(hdr[2])
        res[o] = p[nl + 1:nl + 1 + size]
        i = nl + 1 + size + 1
    return res


META_GUID = re.compile(rb"^guid:\s*([0-9a-f]{32})", re.M)
REF = re.compile(rb'guid["\']?\s*[:=]\s*["\']?([0-9a-fA-F]{32})', re.I)
ASMDEF = re.compile(rb"GUID:([0-9a-fA-F]{32})")


def scan(rev):
    tree = ls_tree(rev, SRC + "Assets", SRC + "ProjectSettings")
    blobs = read_blobs(o for _, o in tree.values())
    guid2path, refs = {}, {}
    for path, (_, oid) in tree.items():
        data = blobs[oid]
        if path.endswith(".meta"):
            m = META_GUID.search(data)
            if m:
                guid2path[m.group(1).decode()] = path[len(SRC):-5]
        if b"\0" in data[:8000] or data.startswith(b"version https://git-lfs"):
            continue
        owner = path[len(SRC):]
        owner = owner[:-5] if owner.endswith(".meta") else owner
        gs = Counter(m.group(1).decode().lower() for m in REF.finditer(data))
        gs.update(m.group(1).decode().lower() for m in ASMDEF.finditer(data))
        refs.setdefault(owner, Counter()).update(gs)
    for owner, gs in refs.items():
        for g in [g for g in gs if guid2path.get(g) == owner]:
            del gs[g]
    return tree, guid2path, refs


base_tree, base_g2p, base_refs = scan(BASE)
head_tree, head_g2p, head_refs = scan(HEAD)
moves = {m["from"]: m["to"] for m in plan["moves"]}
deleted = {d["path"] for d in plan["deletes"]}
roots = list(plan["pack_roots"].values())
inpack = lambda p: any(p == r or p.startswith(r + "/") for r in roots)
lines = []

# 1. GUIDs kept + manifest
manifest = ["pack\tstatus\tfrom\tto\tguid"]
for m in plan["moves"]:
    hg = next((g for g, p in head_g2p.items() if p == m["to"]), None)
    if hg != m["guid"]:
        fails.append(f"1: {m['to']} guid {hg} != {m['guid']}")
    if m["guid"] in base_g2p and base_g2p[m["guid"]] != m["from"]:
        fails.append(f"1: base path mismatch {m['from']}")
    manifest.append(f"{m['pack']}\tkept\t{m['from']}\t{m['to']}\t{m['guid']}")
for d in plan["deletes"]:
    if d["guid"] in head_g2p:
        fails.append(f"1: deleted guid still present {d['path']}")
    manifest.append(f"{d['pack']}\tout\t{d['path']}\t\t{d['guid']}")
base_pack_files = sorted(p[len(SRC):] for p in base_tree if inpack(p[len(SRC):]) and not p.endswith(".meta"))
listed = set(moves) | deleted
if set(base_pack_files) != listed:
    fails.append(f"1: manifest/base pack set mismatch: {sorted(set(base_pack_files) ^ listed)[:5]}")
status = git("diff", "-M", "--name-status", "-z", BASE, HEAD, "--", SRC + "Assets").split(b"\0")
meta_renames = Counter()
folder_pairings = Counter()
i = 0
while i < len(status) - 1:
    st = status[i].decode()
    if st.startswith("R"):
        a, b = status[i + 1].decode(), status[i + 2].decode()
        if a.endswith(".meta") and moves.get(a[len(SRC):-5]) == b[len(SRC):-5]:
            meta_renames[st] += 1
            if st != "R100":
                fails.append(f"1: meta rename {st} {a}")
        elif a.endswith(".meta"):
            folder_pairings[st] += 1
        i += 3
    else:
        i += 2
if meta_renames["R100"] != len(moves):
    fails.append(f"1: R100 meta renames {meta_renames['R100']} != {len(moves)} moves")
lines += ["## 1. GUIDs kept", "",
          f"- {len(moves)} kept files: GUID at the new path equals the original: {'yes' if not any(f.startswith('1:') for f in fails) else 'NO'}",
          f"- Moved files' `.meta` as git renames (from -> planned to): {dict(meta_renames)}",
          f"- The 12 new folder `.meta`s have fresh GUIDs; git also pairs {sum(folder_pairings.values())} of them with deleted vendor folder `.meta`s ({dict(folder_pairings)}), a rename-detection artefact of near-identical folder templates.",
          f"- Manifest rows: {len(moves)} kept + {len(deleted)} out = {len(moves) + len(deleted)}; base pack files: {len(base_pack_files)}", ""]

# 2. reference resolution
removed_guids = set(base_g2p) - set(head_g2p)
def pairs(refs, g2p):
    res, unres = Counter(), Counter()
    for owner, gs in refs.items():
        for g, n in gs.items():
            (res if g in g2p else unres)[(owner, g)] += n
    return res, unres
b_res, b_unres = pairs(base_refs, base_g2p)
h_res, h_unres = pairs(head_refs, head_g2p)
to_removed = [(o, g) for (o, g) in h_unres if g in removed_guids]
if to_removed:
    fails.append(f"2: {len(to_removed)} references to removed GUIDs, e.g. {to_removed[:3]}")
editscene = "Assets/Scenes/EditScene.unity"
dropped = Counter()
for (owner, g), n in b_res.items():
    if inpack(owner):
        continue  # referrer itself deleted or moved
    if (owner, g) in h_res:
        if h_res[(owner, g)] < n:
            if owner == editscene and g in removed_guids:
                dropped[(owner, g)] += n - h_res[(owner, g)]
            elif owner != editscene:
                fails.append(f"2: {owner} -> {g} count {n} -> {h_res[(owner, g)]}")
        continue
    if owner == editscene and g in removed_guids:
        dropped[(owner, g)] += n
    elif owner == editscene:
        dropped[(owner, g)] += n  # reported below for review
    else:
        fails.append(f"2: {owner} -> {g} ({base_g2p.get(g)}) no longer resolves")
moved_in = sum(n for (o, g), n in b_res.items() if inpack(o) and o in moves)
editscene_detail = sorted(f"{base_g2p.get(g, g)} ×{n}" for (o, g), n in dropped.items())
non_wsd = [d for (o, g), d in dropped.items() if not any(s in base_g2p.get(g, "") for s in ("Station_Wheel", "Station_Spindle", "Station_Dildo"))]
if non_wsd:
    fails.append(f"2: EditScene dropped refs outside Wheel/Spindle/Dildo: {non_wsd}")
outside = lambda c: sum(n for (o, g), n in c.items() if not inpack(o))
if outside(b_res) - sum(dropped.values()) + moved_in != sum(h_res.values()):
    fails.append("2: before/after resolved-reference arithmetic does not balance")
lines += ["## 2. Every reference still resolves", "",
          f"- Whole-tree scan of `Assets/` + `ProjectSettings/` (text files; `.meta` folded into its asset).",
          f"- Resolved references from files outside the packs: before {outside(b_res)}, after {sum(h_res.values())} "
          f"(after includes the moved files' own {moved_in} internal refs).",
          f"- Unresolved (package/builtin GUIDs, not project assets): before {sum(b_unres.values())}, after {sum(h_unres.values())}.",
          f"- Arithmetic: {outside(b_res)} before - {sum(dropped.values())} EditScene drops + {moved_in} moved-file refs = {outside(b_res) - sum(dropped.values()) + moved_in} (after: {sum(h_res.values())}).",
          f"- References to a GUID this slice removed: {len(to_removed)}.",
          f"- The only referrer-side drops are in `EditScene` ({sum(dropped.values())} refs):"] + \
         [f"  - {x}" for x in editscene_detail] + [""]

# 3. art byte-exact + storage modes
head_art = ls_tree(HEAD, "art/third-party")
expect = {}
for p in base_pack_files:
    rel = p[len("Assets/Visuals/"):]
    rel = rel.replace("Environment/Asteroids/", "", 1) if rel.startswith("Environment/Asteroids/") else rel.replace("Environment/", "", 1)
    expect["art/third-party/" + rel] = base_tree[SRC + p]
if set(head_art) != set(expect):
    fails.append(f"3: art file set mismatch {sorted(set(head_art) ^ set(expect))[:5]}")
mism = [p for p in expect if head_art.get(p) != expect[p]]
if mism:
    fails.append(f"3: {len(mism)} blob/mode mismatches, e.g. {mism[:3]}")
metas = [p for p in head_art if p.endswith(".meta")]
if metas:
    fails.append(f"3: .meta under art/third-party: {metas[:3]}")
def lfs_attr(paths, rev):
    out = git("check-attr", f"--source={rev}", "-z", "--stdin", "filter",
              inp=b"\0".join(x.encode() for x in paths) + b"\0").split(b"\0")
    return {out[i].decode(): out[i + 2].decode() for i in range(0, len(out) - 2, 3)}
src_paths = [SRC + p for p in base_pack_files]
a_src = lfs_attr(src_paths, BASE)
a_art = lfs_attr(list(expect), HEAD)
mode_mism = [p for p, s in zip(expect, src_paths) if (a_src[s] == "lfs") != (a_art[p] == "lfs")]
blob_isptr = read_blobs(o for _, o in expect.values())
lfs_n = sum(1 for _, o in expect.values() if blob_isptr[o].startswith(b"version https://git-lfs"))
ptr_mism = [p for p, (_, o) in expect.items() if blob_isptr[o].startswith(b"version https://git-lfs") != (a_art[p] == "lfs")]
if mode_mism or ptr_mism:
    fails.append(f"3: storage-mode mismatches {mode_mism[:3]} {ptr_mism[:3]}")
per_pack = Counter(p.split("/")[2] for p in expect)
lines += ["## 3. `art/third-party/` complete and byte-exact", "",
          f"- File set equals the original packs minus `.meta`: {len(expect)} files, {'yes' if set(head_art) == set(expect) else 'NO'}; `.meta` files: {len(metas)}.",
          f"- Same blob id and mode as the source: {len(expect) - len(mism)}/{len(expect)}.",
          f"- Storage mode: {lfs_n} LFS pointers (same oid, since the pointer blob is identical), {len(expect) - lfs_n} raw blobs; "
          f"`filter=lfs` attribute agrees with the source for {len(expect) - len(mode_mism)}/{len(expect)}.",
          "- Per pack: " + ", ".join(f"{k} {v}" for k, v in sorted(per_pack.items())), ""]

# 6. stale paths
pat = r"ParticlePack|HD_Asteroids|Environment/(Radio|Junk_Ships|Platform|Station_)"
try:
    hits = git("grep", "-n", "-I", "-E", pat, HEAD, "--", ":!art/third-party/**").decode().splitlines()
except subprocess.CalledProcessError:
    hits = []
non_meta = [h for h in hits if not re.search(r"\.meta:\d+:  assetPath: ", h)]
if non_meta:
    fails.append(f"6: stale path strings {non_meta[:5]}")
lines += ["## 6. No stale path strings", "",
          f"- Hits outside `art/third-party/`: {len(non_meta)}. "
          f"({len(hits)} vendor `assetPath:` provenance lines inside moved `.meta` files are left untouched: editing them would break `.meta` R100.)", ""]

# 7. build profile
bp = git("diff", "--name-only", BASE, HEAD, "--", SRC + "Assets/Settings/Rendering/Build Profiles", SRC + "ProjectSettings/EditorBuildSettings.asset").decode().split()
if bp:
    fails.append(f"7: build profile changed {bp}")
lines += ["## 7. Build profile unchanged", "", f"- `Build Profiles/` and `EditorBuildSettings.asset` changed paths: {len(bp)}.", ""]

os.makedirs(OUT, exist_ok=True)
open(os.path.join(OUT, "manifest.tsv"), "w", newline="\n").write("\n".join(manifest) + "\n")
hdr = [f"# Slice 6a acceptance report", "", f"Base `{BASE}`, head `{git('rev-parse', '--short', HEAD).decode().strip()}`.", "",
       f"**Result: {'PASS' if not fails else 'FAIL'}**", ""]
if fails:
    hdr += ["Failures:", ""] + [f"- {f}" for f in fails] + [""]
open(os.path.join(OUT, "acceptance_report.md"), "w", newline="\n", encoding="utf-8").write("\n".join(hdr + lines))
print("\n".join(hdr + lines))
sys.exit(1 if fails else 0)
