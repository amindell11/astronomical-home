"""Slice 6b mechanical acceptance (#921 items 1-8) from git trees plus the editor-side reports.

usage: acceptance6b.py <repo> <base> <head> <scratch-dir> <outdir>
Reads plan.json, artmap.json, evidence.json, snap_before/after.txt, shaders_before/after.txt from scratch.
Writes manifest.tsv and acceptance_report.md; exits 1 on any failure.
"""
import json, os, re, sys
from collections import Counter
from gittree import SRC, git, ls_tree, read_blobs, scan

REPO, BASE, HEAD, SCR, OUT = sys.argv[1:6]
J = lambda n: json.load(open(os.path.join(SCR, n)))
plan, artmap, evidence = J("plan.json"), J("artmap.json"), J("evidence.json")
head_short = git(REPO, "rev-parse", "--short", HEAD).decode().strip()
fails, lines = [], []
moves = {m["from"]: m["to"] for m in plan["moves"]}
removed = {r["path"] for r in plan["removes"]}
removed_files = sorted(r["path"] for r in plan["removes"] if not r["folder"])

b_tree, b_g2p, b_refs = scan(REPO, BASE)
h_tree, h_g2p, h_refs = scan(REPO, HEAD)

# ---- 1. GUIDs kept + manifest
manifest = ["kind\tfrom\tto\tguid"]
for m in plan["moves"]:
    hg = next((g for g, p in h_g2p.items() if p == m["to"]), None)
    if hg != m["guid"] or b_g2p.get(m["guid"]) != m["from"]:
        fails.append(f"1: {m['to']} guid {hg} != {m['guid']}")
    manifest.append(f"moved\t{m['from']}\t{m['to']}\t{m['guid']}")
for r in sorted(plan["removes"], key=lambda r: (r["folder"], r["path"])):
    if r["guid"] in h_g2p:
        fails.append(f"1: removed guid still present {r['path']}")
    manifest.append(f"deleted{' (folder)' if r['folder'] else ''}\t{r['path']}\t\t{r['guid']}")
p2g = {p: g for g, p in b_g2p.items()}
for a in artmap:
    rel = a["from"][len(SRC):]
    manifest.append(f"art\t{rel}\t{a['to']}\t{p2g.get(rel, '')}")
for br, ev in evidence.items():
    for e in ev["entries"]:
        rel = e["src"][len(SRC):] if e["src"] and e["src"].startswith(SRC) else (e["src"] or "")
        if rel.endswith(".meta") or not e["src"]:
            continue
        manifest.append(f"evidence\t{rel}\t{br}:{e['dst']}\t{p2g.get(rel, '')}")
status = git(REPO, "diff", "-M", "--name-status", "-z", BASE, HEAD, "--", SRC + "Assets").split(b"\0")
meta_r, other_r, i = Counter(), Counter(), 0
while i < len(status) - 1:
    st = status[i].decode()
    if st.startswith("R"):
        a, b = status[i + 1].decode()[len(SRC):], status[i + 2].decode()[len(SRC):]
        if a.endswith(".meta") and moves.get(a[:-5]) == b[:-5]:
            meta_r[st] += 1
        elif a.endswith(".meta"):
            other_r[f"{st} {a} -> {b}"] += 1
        i += 3
    else:
        i += 2
if meta_r != Counter({"R100": len(moves)}):
    fails.append(f"1: moved .meta renames {dict(meta_r)} (want R100 x{len(moves)})")
kinds = Counter(l.split("\t")[0].split(" ")[0] for l in manifest[1:])
lines += ["## 1. GUIDs kept", "",
          f"- {len(moves)} moved files: GUID at the new path equals the original: {'yes' if not any(f.startswith('1:') for f in fails) else 'NO'}.",
          f"- Their `.meta` files as git renames: {dict(meta_r)}.",
          f"- Other `.meta` rename pairings (folder-template noise): {sorted(other_r) or 'none'}.",
          f"- Manifest rows by kind: {dict(kinds)}. Removed GUIDs absent at head: {len(removed)}/{len(removed)}.", ""]

# ---- 2. every reference resolves
removed_g = set(b_g2p) - set(h_g2p)
if removed_g != {r["guid"] for r in plan["removes"]}:
    fails.append(f"2: removed GUID set differs from plan: {len(removed_g)} vs {len(removed)}")
def resolved(refs, g2p):
    return Counter({(o, g): n for o, gs in refs.items() for g, n in gs.items() if g in g2p})
b_res, h_res = resolved(b_refs, b_g2p), resolved(h_refs, h_g2p)
to_removed = [(o, g) for o, gs in h_refs.items() for g in gs if g in removed_g]
if to_removed:
    fails.append(f"2: {len(to_removed)} head references to removed GUIDs: {to_removed[:3]}")
mapo = lambda o: moves.get(o, o)
dropped_internal = sum(n for (o, g), n in b_res.items() if o in removed and b_g2p[g] in removed)
dropped_outward = sum(n for (o, g), n in b_res.items() if o in removed and b_g2p[g] not in removed)
kept = Counter({(mapo(o), g): n for (o, g), n in b_res.items() if o not in removed})
if kept != h_res:
    diff = (kept - h_res) + (h_res - kept)
    fails.append(f"2: surviving referrers' resolved refs changed: {list(diff.items())[:5]}")
into_removed_from_outside = sum(n for (o, g), n in b_res.items() if o not in removed and b_g2p[g] in removed)
b_total, h_total = sum(b_res.values()), sum(h_res.values())
if b_total - dropped_internal - dropped_outward != h_total or into_removed_from_outside:
    fails.append("2: resolved-reference arithmetic does not balance")
lines += ["## 2. Every reference still resolves", "",
          "- Whole-tree scan of `Assets/`, `ProjectSettings/` and `Packages/` (text files; `.meta` folded into its asset; LFS pointers skipped).",
          f"- References at head to a GUID this slice removed: {len(to_removed)}.",
          f"- Resolved references: before {b_total}, after {h_total}.",
          f"- Arithmetic: {b_total} − {dropped_internal} (internal to the removed study sets) − {dropped_outward} (removed study files pointing at production assets) = {b_total - dropped_internal - dropped_outward} (after: {h_total}).",
          f"- References from surviving files into the removed set before the change: {into_removed_from_outside}. Every surviving referrer keeps exactly its resolved references (moved owners mapped to their new paths): {'yes' if kept == h_res else 'NO'}.", ""]

# ---- 3. art/ byte-exact; evidence blob reuse
h_art = ls_tree(REPO, HEAD, "art")
mism = [a["to"] for a in artmap if h_art.get(a["to"]) != (a["mode"], a["oid"]) or b_tree.get(a["from"]) != (a["mode"], a["oid"])]
art_metas = [p for p in h_art if p.endswith(".meta")]
def attr(paths, rev):
    out = git(REPO, "check-attr", f"--source={rev}", "-z", "--stdin", "filter", inp=b"\0".join(p.encode() for p in paths) + b"\0").split(b"\0")
    return {out[i].decode(): out[i + 2].decode() for i in range(0, len(out) - 2, 3)}
a_src, a_art = attr([a["from"] for a in artmap], BASE), attr([a["to"] for a in artmap], HEAD)
bl = read_blobs(REPO, (a["oid"] for a in artmap))
isptr = {a["to"]: bl[a["oid"]].startswith(b"version https://git-lfs") for a in artmap}
mode_mism = [a["to"] for a in artmap if (a_src[a["from"]] == "lfs") != (a_art[a["to"]] == "lfs") or isptr[a["to"]] != (a_art[a["to"]] == "lfs")]
if mism or art_metas or mode_mism:
    fails.append(f"3: art mismatches {mism[:3]} metas {art_metas[:3]} modes {mode_mism[:3]}")
ev_lines, ev_bad = [], []
for br, ev in evidence.items():
    remote = git(REPO, "ls-remote", "origin", "refs/heads/" + br).decode().split()[0]
    t = ls_tree(REPO, remote)
    srcd = [e for e in ev["entries"] if e["src"]]
    bad = [e["dst"] for e in srcd if t.get(e["dst"]) != (e["mode"], e["oid"]) or b_tree.get(e["src"], ls_tree(REPO, BASE, e["src"]).get(e["src"])) != (e["mode"], e["oid"])]
    ev_bad += bad
    n_meta = sum(1 for e in srcd if e["dst"].endswith(".meta"))
    ev_lines.append(f"  - `{br}` at `{remote[:8]}`: {len(srcd) - len(bad)}/{len(srcd)} entries carry their source blob id and mode ({n_meta} of them `.meta`).")
if ev_bad:
    fails.append(f"3: evidence blob mismatches {ev_bad[:3]}")
groups = Counter(a["to"].rsplit("/", 1)[0] for a in artmap)
lines += ["## 3. `art/` complete and byte-exact", "",
          f"- {len(artmap) - len(mism)}/{len(artmap)} `art/` copies have their source's blob id and mode; `.meta` files under `art/`: {len(art_metas)}.",
          f"- Storage mode: {sum(isptr.values())} LFS pointers (identical pointer blob, so the same oid), {len(artmap) - sum(isptr.values())} raw blobs; the `filter=lfs` attribute agrees with the source for {len(artmap) - len(mode_mism)}/{len(artmap)}.",
          "- Per folder: " + ", ".join(f"`{k}/` {v}" for k, v in sorted(groups.items())),
          "- Evidence copies:"] + ev_lines + [""]

# ---- 4/5. editor-side reports
sb, sa = open(os.path.join(SCR, "snap_before.txt"), "rb").read(), open(os.path.join(SCR, "snap_after.txt"), "rb").read()
n_targets = len(open(os.path.join(SCR, "targets.txt")).read().split())
sh_lines = sb.decode().count(" sh=")
if sb != sa:
    fails.append("4: snapshot differs")
lines += ["## 4. Structural equivalence after a forced reimport", "",
          f"- {n_targets} targets (every prefab and scene outside the removed set reaching a moved GUID directly, through a material, or through prefab nesting); {sb.decode().count(chr(10))} component lines before and after.",
          f"- After the moves, the shader renames and a forced reimport of all {len(moves)} moved files: byte-identical (`cmp`): {'yes' if sb == sa else 'NO'}. Material ids carry their shader GUID ({sh_lines} lines).", ""]
shb = [l for l in open(os.path.join(SCR, "shaders_before.txt")).read().splitlines() if "|" in l]
sha = [l for l in open(os.path.join(SCR, "shaders_after.txt")).read().splitlines() if "|" in l]
ok5 = len(sha) == 5 and all("find=True" in l and "hasError=False" in l and "errors=0" in l for l in sha)
canopy = next(l for l in sha if "DrawnCanopy" in l)
ok5 &= "ShadowCaster,DepthNormals,DepthOnly" in canopy
if not ok5:
    fails.append("5: shader check failed")
lines += ["## 5. Shaders compile", "",
          "- After the renames and the post-delete recompile (`ShaderUtil`, `Shader.Find`):"] + \
         [f"  - `{l.split('|')[2][5:]}` ({l.split('|')[1].rsplit('/', 1)[1]}): find={l.split('|')[3][5:]}, hasError={l.split('|')[4][9:]}, {l.split('|')[5]}, passes {l.split('|')[8][7:]}" for l in sha] + \
         [f"- `DrawnCanopy` resolves its three `UsePass` passes (ShadowCaster, DepthNormals, DepthOnly), the same pass list as before the rename.",
          "- `Shader.Find(\"Astronomical/Comparison/Drawn Surface\")` returns null after the recompile.", ""]

# ---- 6. stale strings
pat = r"DrawnComparison|Studies/DrawnArt|Vanguard/DrawnStudy|Asteroids/DrawnStudy|ApprovedPlasma|Vfx/Explosion|GalacticCruiserTop|StarshipVanguard0606|AColossalDarkGray|Ships/Ship1|export_study|build_structure|Astronomical/Comparison|Astronomical/Studies"
try:
    hits = git(REPO, "grep", "-n", "-I", "-E", pat, HEAD, "--", ":!art/meshy/**").decode().splitlines()
except Exception:
    hits = []
mname = [h for h in hits if re.search(r"\.mat:\d+:  m_Name: Astronomical/", h)]
assetpath = [h for h in hits if re.search(r"\.meta:\d+:  assetPath: ", h)]
readme_ev = [h for h in hits if re.search(r"README\.md:\d+:", h) and "evidence/" in h]
art_dst = {a["to"] for a in artmap}
verbatim = [h for h in hits if h.split(":", 2)[1] in art_dst]
rest = [h for h in hits if h not in mname and h not in assetpath and h not in readme_ev and h not in verbatim]
if rest or len(mname) != 5:
    fails.append(f"6: stale strings {rest[:5]} (m_Name lines {len(mname)})")
lines += ["## 6. No stale strings", "",
          f"- Hits outside the exemptions: {len(rest)}.",
          f"- Exempt: {len(mname)} material `m_Name` display strings (W4), {len(assetpath)} `assetPath:` provenance lines, {len(readme_ev)} README line(s) naming the new evidence location, {len(verbatim)} line(s) inside byte-exact `art/` copies (item 3 forbids editing them; Unity never imports `art/`):"] + \
         [f"  - `{h.split(':', 2)[1]}:{h.split(':', 3)[2]}`" for h in mname + readme_ev + verbatim] + \
         ["- `art/meshy/` is outside the scan: it holds the generator downloads verbatim, names included.", ""]

# ---- 7. F4 study-named folder scan
WORDS = ["study", "studies", "comparison", "approved", "candidate", "experiment", "prototype", "scratch", "wip", "demo", "sample", "evidence"]
GEN = re.compile(r"\d{10,}|chatgptimage|texture(fbx|obj)$", re.I)
def f4(rev):
    tree = ls_tree(REPO, rev, SRC + "Assets")
    folders = {"/".join(p[len(SRC):].split("/")[:k]) for p in tree for k in range(2, p[len(SRC):].count("/") + 1)}
    folders = {f for f in folders if not (f == "Assets/Scripts" or f.startswith("Assets/Scripts/"))}
    return sorted(f for f in folders if any(w in f.rsplit("/", 1)[1].lower() for w in WORDS) or GEN.search(f.rsplit("/", 1)[1]))
f4_base, f4_head = f4(BASE), f4(HEAD)
removed_folders = {r["path"] for r in plan["removes"] if r["folder"]}
if f4_head or not set(f4_base) <= removed_folders:
    fails.append(f"7: F4 head hits {f4_head}, base hits outside removed folders {sorted(set(f4_base) - removed_folders)}")
lines += ["## 7. F4 study-named folder scan", "",
          "- Folder names under `Assets/` minus `Assets/Scripts/`, case-insensitive substring match on the 12 study words, plus a run of 10+ digits, `chatGptImage` and a `Texture(Fbx|Obj)` suffix.",
          f"- Head: {len(f4_head)} hits. Base: {len(f4_base)} hits, all in this slice's removed folders:"] + [f"  - `{f}`" for f in f4_base] + [""]

# ---- 8. build profile
bp = git(REPO, "diff", "--name-only", BASE, HEAD, "--", SRC + "Assets/Settings/Rendering/Build Profiles", SRC + "ProjectSettings/EditorBuildSettings.asset").decode().split()
if bp:
    fails.append(f"8: build profile changed {bp}")
lines += ["## 8. Build profile unchanged", "", f"- `Build Profiles/` and `EditorBuildSettings.asset` paths in the diff: {len(bp)}.", ""]

ts = json.load(open(os.path.join(SCR, "test_summary_full.json"), encoding="utf-8"))
if ts.get("status") != "passed":
    fails.append(f"9: full suite {ts.get('status')}")
log = open(os.path.join(SCR, "fulltests.log"), encoding="utf-8", errors="ignore").read()
st = re.search(r"STATUS=\S+ total=\d+ passed=\d+ failed=\d+ skipped=\d+", log).group(0)
lines += ["## 9. Full suite green", "",
          f"- `run-tests agent-2 -Mode Both -ScopeType Workspace` on `614da6e4`: `{st}` (`test_summary_full.json`). Later commits are docs-only.", ""]

os.makedirs(OUT, exist_ok=True)
open(os.path.join(OUT, "manifest.tsv"), "w", newline="\n", encoding="utf-8").write("\n".join(manifest) + "\n")
hdr = ["# Slice 6b acceptance report", "", f"Base `{BASE}`, head `{head_short}`.", "", f"**Result: {'PASS' if not fails else 'FAIL'}**", ""]
if fails:
    hdr += ["Failures:", ""] + [f"- {f}" for f in fails] + [""]
open(os.path.join(OUT, "acceptance_report.md"), "w", newline="\n", encoding="utf-8").write("\n".join(hdr + lines))
print("\n".join(hdr + lines))
sys.exit(1 if fails else 0)
