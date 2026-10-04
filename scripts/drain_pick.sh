#!/usr/bin/env bash
set -euo pipefail

# Drain pipeline control plane (arc #830): the deterministic half of a cloud batch. `pick` reads
# the ready queue and names the top admitted item; `claim` marks it as a cloud build's; `release`
# gives it back; `owed` grades a PR body's owed-local checklist; `verify-queue`, `merge-queue`
# and `digest` read every pipeline PR; `instruct` records the user's merge instruction and
# `land-facts` reads it back for `agent_worktree_pool.sh land`. Procedure:
# .claude/skills/agent-worktree-pr-loop/SKILL.md § Cloud batch and § Merge task.
#
# Usage: drain_pick.sh pick [--dry-run]
#        drain_pick.sh claim <issue>
#        drain_pick.sh release <issue>
#        drain_pick.sh owed <pr>
#        drain_pick.sh verify-queue | merge-queue | digest
#        drain_pick.sh instruct <pr>@<sha> [<pr>@<sha>…]
#        drain_pick.sh land-facts <pr>
#   pick    read-only. Queue = open `ready-for-agent` issues; an issue is admitted only when it
#           carries exactly one `unity:*` label and that label is `unity:none`, `unity:headless`
#           or `unity:local-proof` (`unity:editor` never), has no assignee, no open blocked-by
#           dependency, and a scope block: a comment by amindell11 whose first line starts
#           `Ready proposal` (the latest wins), else a body with a `What to build` heading and an
#           `Acceptance` heading.
#           Order: pri:now > pri:next > pri:later > none, then oldest created_at.
#           An issue labelled `drain:building` is claimed and never picked. The PRs closing it
#           are the PRs, in every state, whose body names it after a closing keyword
#           (`Closes #<n>`): with none, and nothing else against it, it is a dead build's
#           and prints UNFINISHED; with only closed-unmerged ones its SKIP names each as
#           `pr-closed:<pr>`, and it stays claimed until someone runs `release`.
#           --dry-run changes nothing (pick never writes); it only stamps DRY_RUN=1.
#   claim   re-reads the assignee (any → taken, nothing written), else adds assignee @me, then
#           the label `drain:building`: two writes. Every session is the same GitHub
#           account, so the assignee alone cannot tell a cloud build from the user: the label
#           marks the machine claim. No lock: one cloud batch at a time is the only picker.
#   release removes assignee @me, then `drain:building` when the issue carries it.
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
#           repeatedly take the lowest by (has the fact scripts or github, PR number) among the
#           unplaced candidates with no merge-order-malformed, hosted:failure or order-cycle
#           reason whose live constraints name only placed PRs.
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
#   instruct  the only writer of a *recorded instruction*: the user's merge instruction for a PR at
#           commit <sha>, 7 to 40 hex characters naming exactly one of the PR's commits. Posts one
#           comment per PR whose first line is exactly
#             Merge instruction <YYYY-MM-DD>: `<40-hex sha>`
#           Every argument is checked before anything is written: a PR not open against main, or a
#           <sha> naming none of its commits, refuses the whole call. Every session is the same
#           GitHub account, so nothing here can tell who gave the instruction: run it only on the
#           user's own word.
#   land-facts  read-only, GraphQL (local sessions only). `land`'s one source for a PR's recorded
#           instruction, Codex's review state and merge-order constraints; merge-queue's
#           instructed and class facts come from the same parsers.
#           Instruction: the latest comment by amindell11 whose first line is a record; it names
#           nothing when its commit is not among the PR's latest 100. There is no withdraw format:
#           closing the PR or making it a draft withdraws it, since `land` refuses both.
#           Review: Codex's summary comment (marker `<!-- codex-pull-request-review-summary -->`, the
#           latest by chatgpt-codex-connector), its `Code Review` row, status Completed and a
#           backticked short SHA naming one of the PR's commits. Codex's 👀 on the PR, any other
#           status, no summary, or any other shape is no completed review.
#   verify-queue, merge-queue, digest and land-facts call GraphQL. Every other verb calls only
#   REST, paged by hand: cloud sessions refuse GraphQL, and the Link URLs `gh api --paginate` follows.
# Env:  GITHUB_REPOSITORY (owner/repo; default: the repository of the git remote, as gh reads it).
# Exit: pick, owed, verify-queue, merge-queue, digest, land-facts — 0 a verdict (or the digest)
#       was printed · 1 infra (gh failed) · 2 usage.
#       claim — 0 claimed · 1 infra (gh failed; the issue may be half-written) · 2 usage · 4 taken.
#       release — 0 released · 1 infra (as claim) · 2 usage.
#       instruct — 0 recorded · 1 infra (gh failed; INSTRUCTED lines name what was written) ·
#       2 usage · 3 refused, nothing written.
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
#            .github/) · instructed (a recorded instruction names one of its commits, as
#            land-facts reads it) · class (the auto-merge class as GitHub shows it: owed:none,
#            neither scripts nor github, merge-proof/headless and merge-proof/resharper SUCCESS
#            on the head, Codex's completed review on the head, no unresolved review thread).
#            scripts and github come from GitHub's changed-file list and only set landing order.
#            The merge gate's landing diff stays the authority for the script suite, and `land`
#            alone decides whether an instruction covers the landing tree and what CLASS is.)
#            SKIP=<pr> <reason>[,<reason>…]  (one per candidate with no MERGE line, by PR number,
#            after the MERGE lines; reasons: merge-order-malformed · hosted:failure
#            (merge-proof/headless or merge-proof/resharper on the head commit is FAILURE or
#            ERROR; a pending or missing status is no skip) · order-cycle (it is in a cycle of
#            constraints, or names itself) · after:<pr> (a live constraint names a PR that gets
#            no MERGE line). Any other pipeline PR has a SKIP line only for
#            merge-order-malformed or order-cycle.)
#            MERGE=none  (no MERGE line)
#   instruct: INSTRUCTED=<pr> <sha>  (one per PR once its comment is posted; the full commit)
#            REFUSED=<pr> <reason>  (one per refused argument; reasons: not-open · base:<branch> ·
#            not-a-commit)
#   land-facts:
#            OWED=<verdict>  (owed's verdict)
#            INSTRUCTION=<sha>|none  (the recorded instruction's full commit)
#            REVIEW=completed <sha>|running|absent|unreadable  (Codex's latest review; <sha> is the
#            full commit it reviewed)
#            UNRESOLVED=<n>  (unresolved review threads, whoever opened them)
#            AFTER=none|malformed|<pr>[,<pr>…]  (the live `## Merge order` constraints)
# Prose to stderr.

PROPOSAL_AUTHOR="amindell11"

usage() { echo "Usage: drain_pick.sh pick [--dry-run] | claim <issue> | release <issue> | owed <pr> | verify-queue | merge-queue | digest | instruct <pr>@<sha> [<pr>@<sha>…] | land-facts <pr>" >&2; exit 2; }
infra() { echo "drain_pick: $1" >&2; exit 1; }
say() { echo "drain_pick: $1" >&2; }

repo() {
  if [[ -n "${GITHUB_REPOSITORY:-}" ]]; then echo "$GITHUB_REPOSITORY"
  else gh api 'repos/{owner}/{repo}' --jq .full_name; fi
}

scratch() {
  TMP="$(mktemp -d)"
  trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT
}

# rest_list <path> [<jq>]: every item of a REST list endpoint, one line each as <jq> renders it
# (default: compact JSON).
rest_list() {
  local sep='?' page=0 rows
  [[ "$1" != *'?'* ]] || sep='&'
  while :; do
    page=$((page + 1))
    rows="$(gh api "$1${sep}per_page=100&page=$page" --jq ".[] | ${2:-tojson}")" || return 1
    [[ -z "$rows" ]] || printf '%s\n' "$rows"
    [[ "$(wc -l <<<"$rows")" -eq 100 ]] || return 0
  done
}

# Pick's one reading of the ready queue; digest mode adds ADMITTED, which no verb prints.
ready_queue() {
  local n
  rest_list "repos/$1/issues?state=open&labels=ready-for-agent&sort=created&direction=asc" \
    '"\(.number) \(tojson)"' > "$TMP/issues.txt" || infra "gh api (ready queue) failed"
  while read -r n _; do
    rest_list "repos/$1/issues/$n/comments" > "$TMP/comments-$n.jsonl" || infra "gh api (#$n comments) failed"
  done < "$TMP/issues.txt"
  rest_list "repos/$1/pulls?state=all&sort=created&direction=asc" '{number, state, merged_at, body} | tojson' \
    > "$TMP/pulls.jsonl" || infra "gh api (pull requests) failed"
  python3 - "$TMP" "$PROPOSAL_AUTHOR" "$2" <<'PY'
import json, re, sys
sys.stdout.reconfigure(newline="\n")  # Windows python would end each trailer in CR
tmp, author = sys.argv[1], sys.argv[2]
def rows(name):
    return [json.loads(line) for line in open(f"{tmp}/{name}", encoding="utf-8")]
listed = [json.loads(line.split(" ", 1)[1]) for line in open(f"{tmp}/issues.txt", encoding="utf-8")]
nodes = [n for n in listed if "pull_request" not in n]  # REST lists PRs among issues
RANK = {"pri:now": 0, "pri:next": 1, "pri:later": 2}
ADMITTED = {"unity:none", "unity:headless", "unity:local-proof"}
WHAT = re.compile(r"^#{1,6}\s*What to build\b", re.I | re.M)
ACCEPT = re.compile(r"^#{1,6}\s*Acceptance\b", re.I | re.M)
# GitHub's closing keywords, as scripts/lib/negated_close.py reads them.
CLOSES = re.compile(r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?):?\s+#(\d+)\b", re.I)
closing = {}
for p in rows("pulls.jsonl"):
    for number in set(CLOSES.findall(p["body"] or "")):
        closing.setdefault(int(number), []).append(p)
def scope(n):
    props = [c for c in rows(f"comments-{n['number']}.jsonl")
             if (c.get("user") or {}).get("login") == author
             and (c.get("body") or "").lstrip().startswith("Ready proposal")]
    if props:
        return f"proposal:{props[-1]['html_url']}"
    body = n.get("body") or ""
    return "body" if WHAT.search(body) and ACCEPT.search(body) else None
picked = []
for n in nodes:
    labels = [l["name"] for l in n["labels"]]
    reasons = []
    unity = sorted(l for l in labels if l.startswith("unity:"))
    if not unity:
        reasons.append("no-unity-label")
    elif len(unity) > 1:
        reasons += unity + ["unity-conflict"]
    elif unity[0] not in ADMITTED:
        reasons.append(unity[0])
    building = "drain:building" in labels
    prs = closing.get(n["number"], [])
    if building:
        # The claim wrote the assignee, so the label stands for both.
        reasons.append("drain:building")
        if prs and all(p["state"] == "closed" and not p["merged_at"] for p in prs):
            reasons += [f"pr-closed:{p['number']}" for p in prs]
    else:
        reasons += [f"assigned:{a['login']}" for a in n["assignees"]]
    blocked = (n.get("issue_dependencies_summary") or {}).get("blocked_by") or 0
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
        picked.append((rank, n["created_at"], n["number"], s))
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
  r="$(repo)" || infra "gh api (repository) failed"
  scratch
  ready_queue "$r" pick
  echo "DRY_RUN=$dry_run"
}

cmd_claim() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local issue="$1" r assignees me
  r="$(repo)" || infra "gh api (repository) failed"
  assignees="$(gh api "repos/$r/issues/$issue" --jq '[.assignees[].login] | join(",")')" \
    || infra "gh api (#$issue) failed"
  if [[ -n "$assignees" ]]; then
    say "#$issue is already assigned ($assignees)"
    echo "CLAIM=taken"; exit 4
  fi
  me="$(gh api user --jq .login)" || infra "gh api user failed"
  gh api -X POST "repos/$r/issues/$issue/assignees" -f "assignees[]=$me" --silent \
    && gh api -X POST "repos/$r/issues/$issue/labels" -f "labels[]=drain:building" --silent \
    || infra "claiming #$issue failed — check its assignee and its drain:building label"
  echo "CLAIM=claimed"
}

cmd_release() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local issue="$1" r me labelled
  r="$(repo)" || infra "gh api (repository) failed"
  me="$(gh api user --jq .login)" || infra "gh api user failed"
  labelled="$(gh api "repos/$r/issues/$issue" --jq 'any(.labels[]; .name == "drain:building")')" \
    || infra "gh api (#$issue) failed"
  gh api -X DELETE "repos/$r/issues/$issue/assignees" -f "assignees[]=$me" --silent \
    && { [[ "$labelled" == false ]] || gh api -X DELETE "repos/$r/issues/$issue/labels/drain:building" --silent; } \
    || infra "releasing #$issue failed — check its assignee and its drain:building label"
  echo "RELEASE=released"
}

# pr_views <verb> <json> [<ready_queue output> <owner/repo>]; <json>: one PR for owed and land-facts,
# else every open PR.
pr_views() {
  python3 - "$PROPOSAL_AUTHOR" "$@" <<'PY'
import json, re, sys
sys.stdout.reconfigure(encoding="utf-8", newline="\n")  # Windows python: cp1252, CR line ends
author, verb, data = sys.argv[1], sys.argv[2], json.load(open(sys.argv[3], encoding="utf-8"))
ITEM = re.compile(r"- \[([ x])\] (unity|script|eyes): (\S.*)")
HEADING = re.compile(r"#{1,6}(\s|$)")
SHA = re.compile(r"`([0-9a-f]{7,40})`", re.I)
AFTER = re.compile(r"- after #(\d+)(\s|$)")
RECORD = re.compile(r"Merge instruction \d{4}-\d{2}-\d{2}: `([0-9a-f]{40})`")
SUMMARY = "<!-- codex-pull-request-review-summary -->"
CODEX = "chatgpt-codex-connector"
STATE = {(False, False): "untried", (False, True): "tried", (True, False): "behind", (True, True): "ticked"}
PROOF = ("merge-proof/headless", "merge-proof/resharper")
CANNOT_BUILD = ("no-scope-block", "no-unity-label", "unity-conflict")

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

def merge_order(pr):
    headings, rows = section(pr["body"], "## Merge order")
    named = [AFTER.match(line) for _, line in rows]
    if headings > 1 or not all(named):
        print(f"drain_pick: #{pr['number']} `## Merge order` section malformed", file=sys.stderr)
        return None
    return {int(m[1]) for m in named}

def login(actor):
    return re.sub(r"\[bot\]$", "", (actor or {}).get("login") or "")

def on_pr(pr, sha):
    hits = [c["commit"]["oid"] for c in pr["history"]["nodes"] if c["commit"]["oid"].startswith(sha.lower())]
    return hits[0] if len(hits) == 1 else None

# The one parser of the recorded instruction: the latest record names the commit, or nothing.
def instruction(pr):
    records = [m[1] for c in pr["comments"]["nodes"] if login(c["author"]) == author
               for m in [RECORD.fullmatch(((c["body"] or "").splitlines() or [""])[0])] if m]
    return on_pr(pr, records[-1]) if records else None

# The one parser of Codex's review state: ("completed", <the commit it reviewed>), else (<why not>, None).
def review(pr):
    if any(login(r["user"]) == CODEX for r in pr["reactions"]["nodes"]):
        return "running", None
    summaries = [c["body"] for c in pr["comments"]["nodes"] if login(c["author"]) == CODEX and SUMMARY in (c["body"] or "")]
    if not summaries:
        return "absent", None
    rows = [l.strip().strip("|").split("|") for l in summaries[-1].splitlines() if "**Code Review**" in l]
    if len(rows) != 1 or len(rows[0]) < 3:
        return "unreadable", None
    if "**Completed**" not in rows[0][1]:
        return "running", None
    short = re.fullmatch(r"`([0-9a-f]{7,40})`", rows[0][2].strip())
    reviewed = short and on_pr(pr, short[1])
    return ("completed", reviewed) if reviewed else ("unreadable", None)

# Threads past the first 100 count as unresolved: they were never read.
def unresolved(pr):
    threads = pr["reviewThreads"]
    return sum(not t["isResolved"] for t in threads["nodes"]) + threads["totalCount"] - len(threads["nodes"])

if verb == "owed":
    verdict, items = owed_verdict(data)
    print(f"OWED={verdict}")
    for kind in ("unity", "script", "eyes"):
        print(f"OPEN_{kind.upper()}={count(items, 'open', 'untried', 'tried', kind=kind)}")
    for kind in ("unity", "script"):
        print(f"TRIED_{kind.upper()}={count(items, 'tried', kind=kind)}")
    print(f"BEHIND={count(items, 'behind')}")
    sys.exit()

if verb == "land-facts":
    pr = data["data"]["repository"]["pullRequest"]
    live = {n["number"] for n in data["data"]["repository"]["pullRequests"]["nodes"]}
    order = merge_order(pr)
    after = sorted(a for a in order or () if a in live)
    state, reviewed = review(pr)
    print(f"OWED={owed_verdict(pr)[0]}")
    print(f"INSTRUCTION={instruction(pr) or 'none'}")
    print(f"REVIEW={state}{' ' + reviewed if reviewed else ''}")
    print(f"UNRESOLVED={unresolved(pr)}")
    print(f"AFTER={'malformed' if order is None else ','.join(map(str, after)) or 'none'}")
    sys.exit()

open_prs = {n["number"]: n for n in data["data"]["repository"]["pullRequests"]["nodes"]}
prs = []
for number in sorted(open_prs):
    verdict, items = owed_verdict(open_prs[number])
    if verdict != "absent":
        prs.append({**open_prs[number], "verdict": verdict, "items": items})
verify = [pr for pr in prs if count(pr["items"], "untried")]

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
        states = {c["context"]: c["state"] for c in status["contexts"]}
        scripts = pr["changedFiles"] > 100 or any(p.startswith("scripts/") for p in paths)
        github = any(p.startswith(".github/") for p in paths)
        in_class = (pr["verdict"] == "none" and not scripts and not github
                    and all(states.get(c) == "SUCCESS" for c in PROOF)
                    and review(pr) == ("completed", pr["headRefOid"]) and not unresolved(pr))
        facts[n] = [fact for fact, holds in (
            (f"owed:{pr['verdict']}", True),
            ("behind-head", count(pr["items"], "behind")),
            ("draft", pr["isDraft"]),
            ("scripts", scripts),
            ("github", github),
            ("instructed", instruction(pr)),
            ("class", in_class)) if holds]
        if any(states.get(c) in ("FAILURE", "ERROR") for c in PROOF):
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

base = f"https://github.com/{sys.argv[5]}"
ready = [line.split("=", 1) for line in open(sys.argv[4], encoding="utf-8").read().splitlines()]
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
  r="$(repo)" || infra "gh api (repository) failed"
  scratch
  gh api "repos/$r/pulls/$pr" --jq '{number, body: (.body // ""), headRefOid: .head.sha}' > "$TMP/pr.json" \
    || infra "gh api (PR #$pr) failed"
  pr_views owed "$TMP/pr.json"
}

# Every GraphQL read of a PR asks for these, so merge-queue and land-facts parse one shape.
PR_FIELDS='number title isDraft headRefOid body changedFiles
  files(first: 100) { nodes { path } }
  commits(last: 1) { nodes { commit { status { contexts { context state } } } } }
  history: commits(last: 100) { nodes { commit { oid } } }
  comments(last: 100) { nodes { author { login } body } }
  reviewThreads(first: 100) { totalCount nodes { isResolved } }
  reactions(content: EYES, first: 20) { nodes { user { login } } }'
OPEN_PRS='pullRequests(states: OPEN, baseRefName: "main", first: 100, orderBy: {field: CREATED_AT, direction: ASC})'

cmd_views() {
  [[ $# -eq 1 ]] || usage
  local r
  r="$(repo)" || infra "gh api (repository) failed"
  scratch
  gh api graphql -F owner="${r%%/*}" -F name="${r##*/}" -f query="query(\$owner: String!, \$name: String!) {
    repository(owner: \$owner, name: \$name) { $OPEN_PRS { nodes { $PR_FIELDS } } } }" \
    > "$TMP/prs.json" || infra "gh api graphql (open PRs) failed"
  if [[ "$1" == digest ]]; then ready_queue "$r" digest > "$TMP/ready.txt"; fi
  pr_views "$1" "$TMP/prs.json" "$TMP/ready.txt" "$r"
}

cmd_land_facts() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local r
  r="$(repo)" || infra "gh api (repository) failed"
  scratch
  gh api graphql -F owner="${r%%/*}" -F name="${r##*/}" -F number="$1" -f query="query(\$owner: String!, \$name: String!, \$number: Int!) {
    repository(owner: \$owner, name: \$name) {
      pullRequest(number: \$number) { $PR_FIELDS }
      $OPEN_PRS { nodes { number } } } }" \
    > "$TMP/pr.json" || infra "gh api graphql (PR #$1) failed"
  pr_views land-facts "$TMP/pr.json"
}

cmd_instruct() {
  [[ $# -ge 1 ]] || usage
  local arg pr sha r state base matches refused=0 records=() record
  for arg in "$@"; do [[ "$arg" =~ ^[0-9]+@[0-9a-fA-F]{7,40}$ ]] || usage; done
  r="$(repo)" || infra "gh api (repository) failed"
  for arg in "$@"; do
    pr="${arg%@*}"
    sha="${arg#*@}"
    sha="${sha,,}"
    IFS=$'\t' read -r state base < <(gh api "repos/$r/pulls/$pr" --jq '[.state, .base.ref] | @tsv') \
      || infra "gh api (PR #$pr) failed"
    if [[ "$state" != open ]]; then
      say "#$pr is $state, not open"; echo "REFUSED=$pr not-open"; refused=1; continue
    fi
    if [[ "$base" != main ]]; then
      say "#$pr is against $base, not main"; echo "REFUSED=$pr base:$base"; refused=1; continue
    fi
    matches="$(rest_list "repos/$r/pulls/$pr/commits" .sha)" || infra "gh api (#$pr commits) failed"
    matches="$(grep "^$sha" <<<"$matches" || true)"
    if [[ -z "$matches" || "$matches" == *$'\n'* ]]; then
      say "#$pr: $sha names $([[ -n "$matches" ]] && echo 'more than one' || echo none) of its commits"
      echo "REFUSED=$pr not-a-commit"; refused=1; continue
    fi
    records+=("$pr $matches")
  done
  [[ "$refused" -eq 0 ]] || exit 3
  for record in "${records[@]}"; do
    gh api -X POST "repos/$r/issues/${record% *}/comments" \
      -f body="Merge instruction $(date -u +%F): \`${record#* }\`" --silent \
      || infra "recording the instruction on #${record% *} failed"
    echo "INSTRUCTED=$record"
  done
}

[[ $# -ge 1 ]] || usage
verb="$1"; shift
case "$verb" in
  pick) cmd_pick "$@" ;;
  claim) cmd_claim "$@" ;;
  release) cmd_release "$@" ;;
  owed) cmd_owed "$@" ;;
  verify-queue|merge-queue|digest) cmd_views "$verb" "$@" ;;
  instruct) cmd_instruct "$@" ;;
  land-facts) cmd_land_facts "$@" ;;
  *) usage ;;
esac
