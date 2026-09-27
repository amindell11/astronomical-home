#!/usr/bin/env bash
set -euo pipefail

# Sweep leads (arc #617 Slice-5): the triage sweep's lead pass. A sweep lead is a mechanical
# reason to research an open issue since a watermark; this script finds them from PR and issue
# data alone — no Claude, no writes — and writes one research packet per led issue.
#
# Usage: sweep_leads.sh --since <date> --out <dir>
#   --since <date>  YYYY-MM-DD (00:00 UTC) or an ISO-8601 timestamp with an offset or Z.
#   --out <dir>     created if absent; must be empty. One `issue-<N>.md` per led issue: the
#                   issue's number, title, labels, assignees, updatedAt, its leads, its body inside
#                   <issue-body number=N> … </issue-body>, and per citing PR its number, title,
#                   mergedAt, closing refs and only the body paragraphs citing #N inside
#                   <pr-body number=M> … </pr-body>.
# Lead kinds, per open issue #N:
#   cited         a PR merged on/after --since names #N in its title, its body prose outside
#                 code fences, or its closingIssuesReferences (merge_reconcile.sh's prose rule)
#   child-closed  an issue #M the body prose names closed on/after --since
#   dead-path     a path token in the body prose (one with a `/` and a file extension, or a
#                 backticked token ending in `/`) matches no path, nor path suffix, tracked at
#                 origin/main of the git repo at the working directory — the caller fetches
#   updated       the issue's own updatedAt is on/after --since
# Exit: 0 done · 1 infra (gh or git failed, or a listing reached its limit) · 2 usage.
# Stdout trailers, one per line, stable:
#   LED=<n,…>  QUIET=<n,…>  (ascending; every open issue is in exactly one)
#   CITED=<count>  CHILD_CLOSED=<count>  DEAD_PATH=<count>  UPDATED=<count>  (led issues per kind)
# Prose to stderr.

LIMIT=1000

usage() { echo "Usage: sweep_leads.sh --since <date> --out <dir>" >&2; exit 2; }
infra() { echo "sweep_leads: $1" >&2; exit 1; }

SINCE="" OUT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --since) [[ $# -ge 2 ]] || usage; SINCE="$2"; shift 2 ;;
    --out) [[ $# -ge 2 ]] || usage; OUT="$2"; shift 2 ;;
    *) usage ;;
  esac
done
[[ -n "$SINCE" && -n "$OUT" ]] || usage
[[ ! -e "$OUT" || -d "$OUT" ]] || usage
[[ ! -d "$OUT" || -z "$(ls -A "$OUT")" ]] || { echo "sweep_leads: --out $OUT is not empty" >&2; exit 2; }

SINCE_DAY="$(python3 - "$SINCE" <<'PY'
import re, sys
from datetime import datetime, timezone
s = sys.argv[1]
try:
    d = datetime.fromisoformat(s.replace("Z", "+00:00"))
except ValueError:
    sys.exit(1)
if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", s) and d.tzinfo is None:
    sys.exit(1)
print((d if d.tzinfo else d.replace(tzinfo=timezone.utc)).astimezone(timezone.utc).date().isoformat())
PY
)" || usage

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# The searches narrow by UTC day; the exact --since cut is applied below.
gh issue list --state open --limit "$LIMIT" --json number,title,labels,assignees,updatedAt,body > "$TMP/open.json" \
  || infra "gh issue list (open) failed"
gh issue list --state closed --limit "$LIMIT" --search "closed:>=$SINCE_DAY" --json number,closedAt > "$TMP/closed.json" \
  || infra "gh issue list (closed) failed"
gh pr list --state merged --limit "$LIMIT" --search "merged:>=$SINCE_DAY" --json number,title,mergedAt,body,closingIssuesReferences > "$TMP/merged.json" \
  || infra "gh pr list (merged) failed"
git ls-tree -r --name-only origin/main > "$TMP/tree.txt" || infra "git ls-tree origin/main failed"

mkdir -p "$OUT"
python3 - "$SINCE" "$LIMIT" "$TMP" "$OUT" <<'PY'
import json, os, re, sys
from datetime import datetime, timezone

since_s, limit, tmp, out = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]

def ts(s):
    d = datetime.fromisoformat(s.replace("Z", "+00:00"))
    return (d if d.tzinfo else d.replace(tzinfo=timezone.utc)).astimezone(timezone.utc)

since = ts(since_s)

def load(name):
    rows = json.load(open(os.path.join(tmp, name), encoding="utf-8"))
    if len(rows) >= limit:
        print(f"sweep_leads: {name} reached the {limit}-row limit; narrow --since", file=sys.stderr)
        sys.exit(1)
    return rows

open_issues, closed, merged = load("open.json"), load("closed.json"), load("merged.json")
tree = open(os.path.join(tmp, "tree.txt"), encoding="utf-8").read().splitlines()

FENCE = re.compile(r"^(`{3,}|~{3,}).*?^\1[^\n]*$", re.S | re.M)
REF = r"(?<![\w/])#(\d+)\b"
FILE_TOKEN = re.compile(r"(?<![\w./:@~=?#%&-])((?:[\w.-]+/)+[\w-][\w.-]*\.[A-Za-z][A-Za-z0-9]{0,7})(?![\w/-])")
DIR_TOKEN = re.compile(r"`((?:[\w.-]+/)+)`")

def prose(text): return FENCE.sub("", text or "")
def refs(text): return {int(n) for n in re.findall(REF, text or "")}

file_suffixes, dir_suffixes = set(), set()
for p in tree:
    parts = p.split("/")
    for i in range(len(parts)):
        file_suffixes.add("/".join(parts[i:]))
        for j in range(i + 1, len(parts)):
            dir_suffixes.add("/".join(parts[i:j]))

def dead_paths(body_prose):
    dead = []
    for tok in FILE_TOKEN.findall(body_prose):
        t = tok[2:] if tok.startswith("./") else tok
        if t not in file_suffixes and tok not in dead:
            dead.append(tok)
    for tok in DIR_TOKEN.findall(body_prose):
        t = (tok[2:] if tok.startswith("./") else tok).rstrip("/")
        if t and t not in dir_suffixes and tok not in dead:
            dead.append(tok)
    return dead

citing = {}
for pr in merged:
    if not pr.get("mergedAt") or ts(pr["mergedAt"]) < since:
        continue
    closes = sorted({int(r["number"]) for r in pr.get("closingIssuesReferences") or []})
    pr["_closes"], pr["_prose"] = closes, prose(pr.get("body"))
    for n in refs(pr.get("title")) | refs(pr["_prose"]) | set(closes):
        citing.setdefault(n, []).append(pr)

closed_at = {i["number"]: i["closedAt"] for i in closed if i.get("closedAt") and ts(i["closedAt"]) >= since}

def cite_paragraphs(pr, n):
    pat = re.compile(rf"(?<![\w/])#{n}\b")
    return [p.strip() for p in re.split(r"\n\s*\n", pr["_prose"]) if pat.search(p)]

def hashes(ns): return ", ".join(f"#{n}" for n in ns) or "none"

led, quiet = [], []
kinds = {"cited": 0, "child-closed": 0, "dead-path": 0, "updated": 0}
for issue in sorted(open_issues, key=lambda i: i["number"]):
    n, body = issue["number"], issue.get("body") or ""
    body_prose = prose(body)
    prs = sorted(citing.get(n, []), key=lambda p: p["number"])
    children = sorted(m for m in refs(body_prose) - {n} if m in closed_at)
    dead = dead_paths(body_prose)
    updated = ts(issue["updatedAt"]) >= since
    leads = []
    leads += [f"cited: PR #{p['number']} — {p['title']} (merged {p['mergedAt']})" for p in prs]
    leads += [f"child-closed: #{m} (closed {closed_at[m]})" for m in children]
    leads += [f"dead-path: `{d}` (not at origin/main)" for d in dead]
    leads += [f"updated: {issue['updatedAt']}"] if updated else []
    for kind, hit in (("cited", prs), ("child-closed", children), ("dead-path", dead), ("updated", updated)):
        kinds[kind] += 1 if hit else 0
    if not leads:
        quiet.append(n)
        continue
    led.append(n)
    labels = ", ".join(l["name"] for l in issue.get("labels") or []) or "none"
    assignees = ", ".join(a["login"] for a in issue.get("assignees") or []) or "none"
    lines = [f"# #{n} — {issue['title']}", "",
             f"labels: {labels} · assignees: {assignees} · updatedAt: {issue['updatedAt']}", "",
             "## Leads", ""] + [f"- {l}" for l in leads] + ["",
             f"<issue-body number={n}>", body, "</issue-body>"]
    if prs:
        lines += ["", "## Citing PRs"]
        for p in prs:
            paras = cite_paragraphs(p, n)
            lines += ["", f"### PR #{p['number']} — {p['title']}", "",
                      f"mergedAt: {p['mergedAt']} · closes: {hashes(p['_closes'])}", ""]
            lines += [f"<pr-body number={p['number']}>", "\n\n".join(paras), "</pr-body>"] if paras \
                else [f"(no body paragraph cites #{n})"]
    with open(os.path.join(out, f"issue-{n}.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")

print(f"sweep_leads: {len(open_issues)} open, {len(led)} led, {len(quiet)} quiet -> {out}", file=sys.stderr)
print("LED=" + ",".join(map(str, led)))
print("QUIET=" + ",".join(map(str, quiet)))
print(f"CITED={kinds['cited']}")
print(f"CHILD_CLOSED={kinds['child-closed']}")
print(f"DEAD_PATH={kinds['dead-path']}")
print(f"UPDATED={kinds['updated']}")
PY
