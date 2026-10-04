"""Zero-referrer re-check right before DeleteAsset: every GUID in the delete set (files + folders)
has no referrer outside the set across Assets/, ProjectSettings/, Packages/ (any 32-hex token, .meta included).

usage: recheck6b.py <project-root> <plan.json> <out-deletes.txt> <report.json>
"""
import json, os, re, sys

ROOT, PLAN, OUT_LIST, REPORT = sys.argv[1:5]
plan = json.load(open(PLAN))
roots = plan["removed_roots"]
files = [r["path"] for r in plan["removes"] if not r["folder"]]
folders = [r["path"] for r in plan["removes"] if r["folder"]] + roots
under = lambda rel: any(rel == r or rel.startswith(r + "/") for r in roots)
leftover = []
for r in roots:
    for dp, dn, fn in os.walk(os.path.join(ROOT, r)):
        for f in fn:
            rel = os.path.relpath(os.path.join(dp, f), ROOT).replace("\\", "/")
            if not f.endswith(".meta") and rel not in files:
                leftover.append(rel)
missing = [p for p in files if not os.path.isfile(os.path.join(ROOT, p))]
assert not leftover and not missing, (leftover, missing)
META = re.compile(rb'^guid:\s*([0-9a-f]{32})', re.M)
doomed = {}
for p in files + folders:
    doomed[META.search(open(os.path.join(ROOT, p + ".meta"), "rb").read()).group(1).decode()] = p
in_set = set(files) | set(folders)
is_doomed_file = lambda rel: (rel[:-5] if rel.endswith(".meta") else rel) in in_set
HEX = re.compile(rb'([0-9a-fA-F]{32})')
hits, scanned = {}, 0
for top in ("Assets", "ProjectSettings", "Packages"):
    for dp, dn, fn in os.walk(os.path.join(ROOT, top)):
        for f in fn:
            rel = os.path.relpath(os.path.join(dp, f), ROOT).replace("\\", "/")
            if is_doomed_file(rel):
                continue
            data = open(os.path.join(ROOT, rel), "rb").read()
            if b"\0" in data[:8000]:
                continue
            scanned += 1
            for g in {m.lower() for m in HEX.findall(data)}:
                g = g.decode()
                if g in doomed:
                    hits.setdefault(doomed[g], []).append(rel)
folders_sorted = sorted(set(folders), key=lambda p: (-p.count("/"), p))
open(OUT_LIST, "w", newline="\n").write("".join(p + "\n" for p in sorted(files) + folders_sorted))
rep = {"scanned_files": scanned, "doomed_guids": len(doomed), "files": len(files), "folders": len(set(folders)),
       "referrers_outside_delete_set": hits}
json.dump(rep, open(REPORT, "w"), indent=1)
print(json.dumps({k: (v if k != "referrers_outside_delete_set" else len(v)) for k, v in rep.items()}))
if hits:
    print(json.dumps(hits, indent=1))
    sys.exit(1)
