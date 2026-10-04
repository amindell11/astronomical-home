"""Zero-referrer re-check: every to-delete GUID (files + pack folders) has no referrer outside the delete set."""
import json, os, re, sys
ROOT, PLAN, OUT_FILES, REPORT = sys.argv[1:5]
plan = json.load(open(PLAN))
roots = list(plan["pack_roots"].values())
dels = [d["path"] for d in plan["deletes"]]
folders = []
for r in roots:
    for dp, dn, fn in os.walk(os.path.join(ROOT, r)):
        folders.append(os.path.relpath(dp, ROOT).replace("\\", "/"))
    # any file left that's neither delete-listed nor a meta?
leftover = []
for r in roots:
    for dp, dn, fn in os.walk(os.path.join(ROOT, r)):
        for f in fn:
            rel = os.path.relpath(os.path.join(dp, f), ROOT).replace("\\", "/")
            if not f.endswith(".meta") and rel not in set(dels):
                leftover.append(rel)
assert not leftover, leftover
META = re.compile(rb'^guid:\s*([0-9a-f]{32})', re.M)
doomed = {}
for p in dels + folders:
    m = META.search(open(os.path.join(ROOT, p + ".meta"), "rb").read())
    doomed[m.group(1).decode()] = p
inset = lambda rel: any(rel == r or rel == r + ".meta" or rel.startswith(r + "/") for r in roots)
GUID = re.compile(rb'([0-9a-f]{32})')
hits = {}
scanned = 0
for top in ("Assets", "ProjectSettings", "Packages"):
    for dp, dn, fn in os.walk(os.path.join(ROOT, top)):
        for f in fn:
            rel = os.path.relpath(os.path.join(dp, f), ROOT).replace("\\", "/")
            if inset(rel):
                continue
            data = open(os.path.join(ROOT, rel), "rb").read()
            if b"\0" in data[:8000]:
                continue
            scanned += 1
            for g in set(GUID.findall(data)):
                g = g.decode()
                if g in doomed:
                    hits.setdefault(doomed[g], []).append(rel)
order = sorted(dels) + sorted(set(folders) - set(roots), key=lambda p: -p.count("/")) + roots
open(OUT_FILES, "w", newline="\n").write("".join(p + "\n" for p in order))
rep = {"scanned_files": scanned, "doomed_guids": len(doomed), "files": len(dels), "folders": len(folders), "referrers_outside_delete_set": hits}
json.dump(rep, open(REPORT, "w"), indent=1)
print(json.dumps({k: (v if k != "referrers_outside_delete_set" else len(v)) for k, v in rep.items()}))
if hits:
    print(json.dumps(hits, indent=1)); sys.exit(1)
