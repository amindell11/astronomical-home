#!/usr/bin/env bash
set -euo pipefail

# Drain pipeline control plane (arc #830): the deterministic half of a cloud batch. `pick` reads
# the ready queue and names the top admitted item; `claim` marks it as a cloud build's; `release`
# gives it back; `owed` grades a PR body's owed-local checklist; `verify-queue`, `merge-queue`
# and `digest` read every pipeline PR. Procedure:
# .claude/skills/agent-worktree-pr-loop/SKILL.md § Cloud batch.
#
# Usage: drain_pick.sh pick [--dry-run]
#        drain_pick.sh claim <issue>
#        drain_pick.sh release <issue>
#        drain_pick.sh owed <pr>
#        drain_pick.sh verify-queue | merge-queue | digest
#   pick    read-only. Queue = open `ready-for-agent` issues; an issue is admitted only when it
#           carries exactly one `unity:*` label and that label is `unity:none`, `unity:headless`
#           or `unity:local-proof` (`unity:editor` never), has no assignee, no open blocked-by
#           dependency, and a scope block: a comment by amindell11 whose first line starts
#           `Ready proposal` (the latest wins), else a body with a `What to build` heading and an
#           `Acceptance` heading.
#           Order: pri:now > pri:next > pri:later > none, then oldest createdAt.
#           An issue labelled `drain:building` is claimed and never picked. PRs closing it are
#           read in every state: with none, and nothing else against it, it is a dead build's
#           and prints UNFINISHED; with only closed-unmerged ones its SKIP names each as
#           `pr-closed:<pr>`, and it stays claimed until someone runs `release`.
#           --dry-run changes nothing (pick never writes); it only stamps DRY_RUN=1.
#   claim   re-reads the assignee (any → taken, nothing written), else adds assignee @me and the
#           label `drain:building` in one `gh issue edit`. Every session is the same GitHub
#           account, so the assignee alone cannot tell a cloud build from the user: the label
#           marks the machine claim. No lock: one cloud batch at a time is the only picker.
#   release removes assignee @me and `drain:building` in one `gh issue edit`.
#   owed    read-only. Grades the owed-local checklist in the PR body against the PR's head
#           commit. Grammar:
#             section  from the line `### Owed local` to the next heading or the end of the
#                      body; LF or CRLF.
#             item     one line at column 0, `- [ ] <kind>: <text>` or `- [x] <kind>: <text>`;
#                      kind is unity | script | eyes.
#             result   lines indented two or more spaces under an item belong to that item.
#             none     `None.` as the section's only content: nothing is owed.
#           Blank lines are skipped. Anything else in the section, an empty section, `None.`
#           beside items, or a second `### Owed local` heading is malformed; stderr names why.
#           The tried rule: a unity or script item names the head when one of its result lines
#           holds a backticked run of 7 to 40 hex characters that is a prefix of the head
#           commit's SHA, the commit its run was on ("Run at `eb3ddb9`"). Unticked, it is *tried*
#           when it names the head (it failed on this tree, and no queue serves it until the
#           head moves) and *untried* otherwise. Ticked, it is *behind head* when it does not
#           name the head. An eyes item is none of these: a person ticks it, with no result line.
#   The three verbs below take no argument, write nothing, and read the *pipeline PRs*: open PRs
#   with base `main` whose body has a `### Owed local` heading (owed's verdict is anything but
#   absent). Draft state, the closing issue and its labels play no part.
#   verify-queue  the untried items of every pipeline PR, lowest PR number first.
#   merge-queue   facts and landing order for the candidates: the pipeline PRs whose verdict is
#           none or discharged. It never says who may merge. Every pipeline PR's `## Merge order`
#           is read, so a fault in one shows before its PR is a candidate. Landing order:
#           repeatedly take, from the candidates not yet placed, the lowest by (has the fact
#           scripts or github, PR number) among those whose live constraints name only placed PRs.
#           `## Merge order` grammar:
#             section     from the line `## Merge order` to the next heading or the end of the
#                         body; LF or CRLF.
#             constraint  `- after #<n>`, then whitespace or the end of the line; the rest of
#                         the line is free text.
#           Blank lines are skipped. Anything else in the section, or a second `## Merge order`
#           heading, is malformed. A constraint is live only while the PR it names is open
#           against `main`.
#   digest  Markdown for a person, every issue and PR number linked: what waits on the user
#           (eyes items, tried items, malformed checklists, the merge queue, claimed issues whose
#           PRs all closed unmerged, ready-labelled issues no cloud batch can build) and what
#           waits on a session the user starts (build, verify). No machine contract: relay it as
#           printed.
# Env:  GITHUB_REPOSITORY (owner/repo; default `gh repo view`).
# Exit: pick, owed, verify-queue, merge-queue, digest — 0 a verdict (or the digest) was printed ·
#       1 infra (gh failed) · 2 usage.
#       claim — 0 claimed · 1 infra (gh failed; the issue may be half-written) · 2 usage · 4 taken.
#       release — 0 released · 1 infra (as claim) · 2 usage.
# Stdout trailers, one per line, stable:
#   pick:    SKIP=<n> <reason>[,<reason>…]  (one per queue issue neither admitted nor unfinished;
#            reasons: no-unity-label unity:<value> unity-conflict (more than one unity:*)
#            drain:building pr-closed:<pr> assigned:<login> blocked:<open-count> no-scope-block)
#            UNFINISHED=<n> <scope>  (one per dead build's issue; <scope> as SCOPE's value)
#            ISSUE=<n>|none  SCOPE=proposal:<comment-url>|body  (SCOPE only when picked)
#            DRY_RUN=<0|1>
#   claim:   CLAIM=<claimed|taken>
#   release: RELEASE=released
#   owed:    OWED=<absent|malformed|none|open|discharged>  (absent: no section · open: at least
#            one unticked item · discharged: at least one item, all ticked)
#            OPEN_UNITY=<n>  OPEN_SCRIPT=<n>  OPEN_EYES=<n>  (unticked items per kind; 0 unless open)
#            TRIED_UNITY=<n>  TRIED_SCRIPT=<n>  (the tried items among the open ones)
#            BEHIND=<n>  (ticked unity and script items behind head)
#   verify-queue:
#            VERIFY=<pr> <head-sha> unity:<n>,script:<n>  (one per PR with an untried item; the
#            full 40-character commit; untried items per kind)
#            ITEM=<pr> <kind> <text>  (one per untried item, in body order, under its VERIFY
#            line; <text> is the item line after `<kind>: `)
#            VERIFY=none  (no PR has an untried item)
#   merge-queue:
#            MERGE=<pr> <head-sha> <fact>[,<fact>…]  (one per placed candidate, in landing order;
#            facts: owed:none | owed:discharged (always first) · behind-head (a ticked item is
#            behind head) · draft · scripts (a changed path under scripts/, or more than 100
#            changed files: the read returns the first 100 paths) · github (a changed path under
#            .github/). scripts and github come from GitHub's changed-file list and only set
#            landing order; the merge gate's landing diff stays the authority for the script
#            suite and for class membership.)
#            SKIP=<pr> <reason>[,<reason>…]  (one per candidate with no MERGE line, by PR number,
#            after the MERGE lines; reasons: merge-order-malformed · hosted:failure
#            (merge-proof/headless or merge-proof/resharper on the head commit is FAILURE or
#            ERROR; a pending or missing status is no skip) · order-cycle (it is in a cycle of
#            constraints, or names itself) · after:<pr> (a live constraint names a PR that gets
#            no MERGE line). Any other pipeline PR has a SKIP line only for
#            merge-order-malformed or order-cycle.)
#            MERGE=none  (no MERGE line)
# Prose to stderr.

PROPOSAL_AUTHOR="amindell11"

usage() { echo "Usage: drain_pick.sh pick [--dry-run] | claim <issue> | release <issue> | owed <pr> | verify-queue | merge-queue | digest" >&2; exit 2; }
infra() { echo "drain_pick: $1" >&2; exit 1; }
say() { echo "drain_pick: $1" >&2; }

repo() {
  if [[ -n "${GITHUB_REPOSITORY:-}" ]]; then echo "$GITHUB_REPOSITORY"
  else gh repo view --json nameWithOwner --jq .nameWithOwner; fi
}

scratch() {
  TMP="$(mktemp -d)"
  trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT
}

# The one reading of the ready queue, pick's rule: ready_queue <owner/repo> <pick|digest> prints
# pick's trailers, and for digest also ADMITTED=<count>, which no verb prints.
ready_queue() {
  gh api graphql -F owner="${1%%/*}" -F name="${1##*/}" -f query='query($owner: String!, $name: String!) {
    repository(owner: $owner, name: $name) {
      issues(states: OPEN, labels: ["ready-for-agent"], first: 100, orderBy: {field: CREATED_AT, direction: ASC}) {
        nodes { number createdAt body
          labels(first: 50) { nodes { name } }
          assignees(first: 10) { nodes { login } }
          issueDependenciesSummary { blockedBy }
          closedByPullRequestsReferences(first: 20, includeClosedPrs: true) { nodes { number state } }
          comments(last: 100) { nodes { author { login } url body } } } } } }' \
    > "$TMP/queue.json" || infra "gh api graphql (ready queue) failed"
  python3 - "$TMP/queue.json" "$PROPOSAL_AUTHOR" "$2" <<'PY'
import json, re, sys
sys.stdout.reconfigure(newline="\n")  # Windows python would end each trailer in CR
data = json.load(open(sys.argv[1], encoding="utf-8"))
author = sys.argv[2]
nodes = data["data"]["repository"]["issues"]["nodes"]
RANK = {"pri:now": 0, "pri:next": 1, "pri:later": 2}
ADMITTED = {"unity:none", "unity:headless", "unity:local-proof"}
WHAT = re.compile(r"^#{1,6}\s*What to build\b", re.I | re.M)
ACCEPT = re.compile(r"^#{1,6}\s*Acceptance\b", re.I | re.M)
def scope(n):
    props = [c for c in n["comments"]["nodes"]
             if (c.get("author") or {}).get("login") == author
             and (c.get("body") or "").lstrip().startswith("Ready proposal")]
    if props:
        return f"proposal:{props[-1]['url']}"
    body = n.get("body") or ""
    return "body" if WHAT.search(body) and ACCEPT.search(body) else None
picked = []
for n in nodes:
    labels = [l["name"] for l in n["labels"]["nodes"]]
    reasons = []
    unity = sorted(l for l in labels if l.startswith("unity:"))
    if not unity:
        reasons.append("no-unity-label")
    elif len(unity) > 1:
        reasons += unity + ["unity-conflict"]
    elif unity[0] not in ADMITTED:
        reasons.append(unity[0])
    building = "drain:building" in labels
    prs = n["closedByPullRequestsReferences"]["nodes"]
    if building:
        # The claim wrote the assignee, so the label stands for both.
        reasons.append("drain:building")
        if prs and all(p["state"] == "CLOSED" for p in prs):
            reasons += [f"pr-closed:{p['number']}" for p in prs]
    else:
        reasons += [f"assigned:{a['login']}" for a in n["assignees"]["nodes"]]
    blocked = (n.get("issueDependenciesSummary") or {}).get("blockedBy") or 0
    if blocked:
        reasons.append(f"blocked:{blocked}")
    s = scope(n)
    if s is None:
        reasons.append("no-scope-block")
    if building and not prs and reasons == ["drain:building"]:
        print(f"UNFINISHED={n['number']} {s}")
    elif reasons:
        print(f"SKIP={n['number']} {','.join(reasons)}")
    else:
        rank = min([RANK[l] for l in labels if l in RANK], default=3)
        picked.append((rank, n["createdAt"], n["number"], s))
if picked:
    _, _, number, s = min(picked)
    print(f"ISSUE={number}")
    print(f"SCOPE={s}")
else:
    print("ISSUE=none")
if sys.argv[3] == "digest":
    print(f"ADMITTED={len(picked)}")
PY
}

cmd_pick() {
  local dry_run=0
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --dry-run) dry_run=1; shift ;;
      *) usage ;;
    esac
  done
  local r
  r="$(repo)" || infra "gh repo view failed"
  scratch
  ready_queue "$r" pick
  echo "DRY_RUN=$dry_run"
}

cmd_claim() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local issue="$1" r assignees
  r="$(repo)" || infra "gh repo view failed"
  assignees="$(gh issue view "$issue" --repo "$r" --json assignees --jq '[.assignees[].login] | join(",")')" \
    || infra "gh issue view $issue failed"
  if [[ -n "$assignees" ]]; then
    say "#$issue is already assigned ($assignees)"
    echo "CLAIM=taken"; exit 4
  fi
  gh issue edit "$issue" --repo "$r" --add-assignee @me --add-label drain:building >/dev/null \
    || infra "claiming #$issue failed — check its assignee and its drain:building label"
  echo "CLAIM=claimed"
}

cmd_release() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local issue="$1" r
  r="$(repo)" || infra "gh repo view failed"
  gh issue edit "$issue" --repo "$r" --remove-assignee @me --remove-label drain:building >/dev/null \
    || infra "releasing #$issue failed — check its assignee and its drain:building label"
  echo "RELEASE=released"
}

# Every view of the open PRs: pr_views <verb> <json> [<ready_queue output> <owner/repo>]. <json>
# is one PR for owed, the open-PR read for the queue verbs.
pr_views() {
  python3 - "$@" <<'PY'
import json, re, sys
sys.stdout.reconfigure(encoding="utf-8", newline="\n")  # Windows python: cp1252, CR line ends
verb, data = sys.argv[1], json.load(open(sys.argv[2], encoding="utf-8"))
ITEM = re.compile(r"- \[([ x])\] (unity|script|eyes): (\S.*)")
HEADING = re.compile(r"#{1,6}(\s|$)")
SHA = re.compile(r"`([0-9a-f]{7,40})`", re.I)
AFTER = re.compile(r"- after #(\d+)(\s|$)")
STATE = {(False, False): "untried", (False, True): "tried", (True, False): "behind", (True, True): "ticked"}
PROOF = ("merge-proof/headless", "merge-proof/resharper")
CANNOT_BUILD = ("no-scope-block", "no-unity-label", "unity-conflict")

# How many `heading` lines the body has, and the first one's non-blank lines up to the next heading.
def section(body, heading):
    lines = body.splitlines()
    starts = [i for i, l in enumerate(lines) if l.rstrip() == heading]
    rows = []
    for number, line in enumerate(lines[starts[0] + 1:], starts[0] + 2) if starts else []:
        if HEADING.match(line):
            break
        if line.strip():
            rows.append((number, line))
    return len(starts), rows

# The one parser of the owed-local checklist: every verb that grades a PR body calls it.
def owed_verdict(pr):
    def malformed(why):
        print(f"drain_pick: #{pr['number']} owed-local checklist malformed: {why}", file=sys.stderr)
        return "malformed", []
    headings, rows = section(pr["body"], "### Owed local")
    if not headings:
        return "absent", []
    if headings > 1:
        return malformed("more than one `### Owed local` heading")
    items, none = [], False
    for number, line in rows:
        item = ITEM.match(line)
        if item and not none:
            items.append({"kind": item[2], "ticked": item[1] == "x", "text": item[3].rstrip(), "at_head": False})
        elif items and line.startswith("  "):
            items[-1]["at_head"] |= any(pr["headRefOid"].startswith(s.lower()) for s in SHA.findall(line))
        elif not items and not none and line.rstrip() == "None.":
            none = True
        else:
            return malformed(f"body line {number}: {line.strip()}")
    if none:
        return "none", []
    if not items:
        return malformed("the section is empty")
    for i in items:
        eyes = "ticked" if i["ticked"] else "open"
        i["state"] = eyes if i["kind"] == "eyes" else STATE[i["ticked"], i["at_head"]]
    return ("discharged" if all(i["ticked"] for i in items) else "open"), items

def count(items, *states, kind=None):
    return sum(i["state"] in states and kind in (None, i["kind"]) for i in items)

def per_kind(items, state):
    return f"unity:{count(items, state, kind='unity')},script:{count(items, state, kind='script')}"

if verb == "owed":
    verdict, items = owed_verdict(data)
    print(f"OWED={verdict}")
    for kind in ("unity", "script", "eyes"):
        print(f"OPEN_{kind.upper()}={count(items, 'open', 'untried', 'tried', kind=kind)}")
    for kind in ("unity", "script"):
        print(f"TRIED_{kind.upper()}={count(items, 'tried', kind=kind)}")
    print(f"BEHIND={count(items, 'behind')}")
    sys.exit()

open_prs = {n["number"]: n for n in data["data"]["repository"]["pullRequests"]["nodes"]}
prs = []
for number in sorted(open_prs):
    verdict, items = owed_verdict(open_prs[number])
    if verdict != "absent":
        prs.append({**open_prs[number], "verdict": verdict, "items": items})
verify = [pr for pr in prs if count(pr["items"], "untried")]

def merge_order(pr):
    headings, rows = section(pr["body"], "## Merge order")
    named = [AFTER.match(line) for _, line in rows]
    if headings > 1 or not all(named):
        print(f"drain_pick: #{pr['number']} `## Merge order` section malformed", file=sys.stderr)
        return None
    return {int(m[1]) for m in named}

def merge_queue():
    facts, after, skip = {}, {}, {}
    for pr in prs:
        n = pr["number"]
        order = merge_order(pr)
        after[n] = sorted(a for a in order or () if a in open_prs)
        skip[n] = ["merge-order-malformed"] if order is None else []
        if pr["verdict"] not in ("none", "discharged"):
            continue
        paths = [f["path"] for f in pr["files"]["nodes"]]
        status = pr["commits"]["nodes"][0]["commit"]["status"] or {"contexts": []}
        facts[n] = [fact for fact, holds in (
            (f"owed:{pr['verdict']}", True),
            ("behind-head", count(pr["items"], "behind")),
            ("draft", pr["isDraft"]),
            ("scripts", pr["changedFiles"] > 100 or any(p.startswith("scripts/") for p in paths)),
            ("github", any(p.startswith(".github/") for p in paths))) if holds]
        if any(c["context"] in PROOF and c["state"] in ("FAILURE", "ERROR") for c in status["contexts"]):
            skip[n].append("hosted:failure")
    for n in after:
        seen, todo = set(), list(after[n])
        while todo:
            a = todo.pop()
            if a == n:
                skip[n].append("order-cycle")
                break
            if a in after and a not in seen:
                seen.add(a)
                todo += after[a]
    placed = []
    while True:
        ready = [n for n in facts
                 if n not in placed and not skip[n] and all(a in placed for a in after[n])]
        if not ready:
            break
        placed.append(min(ready, key=lambda n: ("scripts" in facts[n] or "github" in facts[n], n)))
    for n in facts:
        skip[n] += [f"after:{a}" for a in after[n] if a not in placed]
    return placed, facts, {n: skip[n] for n in after if skip[n]}

if verb == "verify-queue":
    for pr in verify:
        print(f"VERIFY={pr['number']} {pr['headRefOid']} {per_kind(pr['items'], 'untried')}")
        for i in pr["items"]:
            if i["state"] == "untried":
                print(f"ITEM={pr['number']} {i['kind']} {i['text']}")
    if not verify:
        print("VERIFY=none")
    sys.exit()

placed, facts, skipped = merge_queue()
if verb == "merge-queue":
    for n in placed:
        print(f"MERGE={n} {open_prs[n]['headRefOid']} {','.join(facts[n])}")
    if not placed:
        print("MERGE=none")
    for n, reasons in skipped.items():
        print(f"SKIP={n} {','.join(reasons)}")
    sys.exit()

base = f"https://github.com/{sys.argv[4]}"
ready = [line.split("=", 1) for line in open(sys.argv[3], encoding="utf-8").read().splitlines()]
skips = {int(v.split()[0]): v.split()[1].split(",") for k, v in ready if k == "SKIP"}
unfinished = [int(v.split()[0]) for k, v in ready if k == "UNFINISHED"]
admitted = int(dict(ready)["ADMITTED"])
def issue(n):
    return f"[#{n}]({base}/issues/{n})"
def link(n):
    return f"[#{n}]({base}/pull/{n})"
def titled(pr):
    return f"{link(pr['number'])} {pr['title']}"
def linked(reasons):
    return ", ".join(re.sub(r"^(after|pr-closed):(\d+)$", lambda m: f"{m[1]} {link(m[2])}", r)
                     for r in reasons)
def with_items(title, state, detail):
    return title, [f"{titled(pr)} — {detail(pr['items'], state)}" for pr in prs if count(pr["items"], state)]
waiting = (
    ("Waiting on the user", (
        with_items("`eyes` items to look at", "open", lambda items, state: f"{count(items, state)} unticked"),
        with_items("Owed items that failed at head", "tried", per_kind),
        ("Malformed owed-local checklists", [titled(pr) for pr in prs if pr["verdict"] == "malformed"]),
        ("Merge candidates, in landing order",
         [f"{titled(open_prs[n])} — {', '.join(facts[n])}" for n in placed]),
        ("Skipped from the merge queue",
         [f"{titled(open_prs[n])} — {linked(reasons)}" for n, reasons in skipped.items()]),
        ("Claimed issues whose PRs all closed unmerged",
         [f"{issue(n)} — {linked(r for r in reasons if r.startswith('pr-closed:'))}"
          for n, reasons in skips.items() if any(r.startswith("pr-closed:") for r in reasons)]),
        ("Ready-labelled issues no cloud batch can build",
         [f"{issue(n)} — {', '.join(reasons)}" for n, reasons in skips.items()
          if all(r in CANNOT_BUILD or r.startswith("unity:") and "unity-conflict" in reasons
                 for r in reasons)]))),
    ("Waiting on a session the user starts", (
        ("Build", [f"`pick` admits {admitted} issue{'s' * (admitted != 1)}"] * (admitted > 0)
                  + [f"unfinished: {', '.join(issue(n) for n in unfinished)}"] * bool(unfinished)),
        with_items("Verify", "untried", per_kind))))
out = []
for heading, groups in waiting:
    groups = [f"**{title}**\n" + "".join(f"- {row}\n" for row in rows) for title, rows in groups if rows]
    if groups:
        out += [f"## {heading}\n"] + groups
print("\n".join(out) if out else "Nothing is waiting in the drain pipeline.")
PY
}

cmd_owed() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local pr="$1" r
  r="$(repo)" || infra "gh repo view failed"
  scratch
  gh pr view "$pr" --repo "$r" --json number,body,headRefOid > "$TMP/pr.json" || infra "gh pr view $pr failed"
  pr_views owed "$TMP/pr.json"
}

cmd_views() {
  [[ $# -eq 1 ]] || usage
  local r
  r="$(repo)" || infra "gh repo view failed"
  scratch
  gh api graphql -F owner="${r%%/*}" -F name="${r##*/}" -f query='query($owner: String!, $name: String!) {
    repository(owner: $owner, name: $name) {
      pullRequests(states: OPEN, baseRefName: "main", first: 100, orderBy: {field: CREATED_AT, direction: ASC}) {
        nodes { number title isDraft headRefOid body changedFiles
          files(first: 100) { nodes { path } }
          commits(last: 1) { nodes { commit { status { contexts { context state } } } } } } } } }' \
    > "$TMP/prs.json" || infra "gh api graphql (open PRs) failed"
  if [[ "$1" == digest ]]; then ready_queue "$r" digest > "$TMP/ready.txt"; fi
  pr_views "$1" "$TMP/prs.json" "$TMP/ready.txt" "$r"
}

[[ $# -ge 1 ]] || usage
verb="$1"; shift
case "$verb" in
  pick) cmd_pick "$@" ;;
  claim) cmd_claim "$@" ;;
  release) cmd_release "$@" ;;
  owed) cmd_owed "$@" ;;
  verify-queue|merge-queue|digest) cmd_views "$verb" "$@" ;;
  *) usage ;;
esac
