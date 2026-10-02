#!/usr/bin/env bash
set -euo pipefail

# Drain pipeline control plane (arc #830): the deterministic half of a cloud batch. `pick` reads
# the ready queue and names the top admitted item; `claim` marks it as a cloud build's; `release`
# gives it back; `owed` grades a PR body's owed-local checklist. Procedure:
# .claude/skills/agent-worktree-pr-loop/SKILL.md § Cloud batch.
#
# Usage: drain_pick.sh pick [--dry-run]
#        drain_pick.sh claim <issue>
#        drain_pick.sh release <issue>
#        drain_pick.sh owed <pr>
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
#   owed    read-only. Grades the owed-local checklist in the PR body. Grammar:
#             section  from the line `### Owed local` to the next heading or the end of the
#                      body; LF or CRLF.
#             item     one line at column 0, `- [ ] <kind>: <text>` or `- [x] <kind>: <text>`;
#                      kind is unity | script | eyes.
#             result   lines indented two or more spaces under an item belong to that item.
#             none     `None.` as the section's only content: nothing is owed.
#           Blank lines are skipped. Anything else in the section, an empty section, `None.`
#           beside items, or a second `### Owed local` heading is malformed; stderr names why.
# Env:  GITHUB_REPOSITORY (owner/repo; default `gh repo view`).
# Exit: pick, owed — 0 a verdict was printed · 1 infra (gh failed) · 2 usage.
#       claim — 0 claimed · 1 infra (gh failed; the issue may be half-written) · 2 usage · 4 taken.
#       release — 0 released · 1 infra (as claim) · 2 usage.
# Stdout trailers, one per line, stable:
#   pick:    SKIP=<n> <reason>[,<reason>…]  (one per queue issue neither picked nor unfinished;
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
# Prose to stderr.

PROPOSAL_AUTHOR="amindell11"

usage() { echo "Usage: drain_pick.sh pick [--dry-run] | claim <issue> | release <issue> | owed <pr>" >&2; exit 2; }
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

cmd_pick() {
  local dry_run=0
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --dry-run) dry_run=1; shift ;;
      *) usage ;;
    esac
  done
  local r owner name
  r="$(repo)" || infra "gh repo view failed"
  owner="${r%%/*}" name="${r##*/}"
  scratch
  gh api graphql -F owner="$owner" -F name="$name" -f query='query($owner: String!, $name: String!) {
    repository(owner: $owner, name: $name) {
      issues(states: OPEN, labels: ["ready-for-agent"], first: 100, orderBy: {field: CREATED_AT, direction: ASC}) {
        nodes { number createdAt body
          labels(first: 50) { nodes { name } }
          assignees(first: 10) { nodes { login } }
          issueDependenciesSummary { blockedBy }
          closedByPullRequestsReferences(first: 20, includeClosedPrs: true) { nodes { number state } }
          comments(last: 100) { nodes { author { login } url body } } } } } }' \
    > "$TMP/queue.json" || infra "gh api graphql (ready queue) failed"
  python3 - "$TMP/queue.json" "$PROPOSAL_AUTHOR" <<'PY'
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
PY
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

# The one parser of the owed-local checklist: every verb that grades a PR body calls it.
owed_verdict() {
  python3 - "$1" <<'PY'
import re, sys
sys.stdout.reconfigure(newline="\n")
lines = open(sys.argv[1], "rb").read().decode("utf-8", errors="replace").splitlines()
ITEM = re.compile(r"- \[([ x])\] (unity|script|eyes): \S")
HEADING = re.compile(r"#{1,6}(\s|$)")
KINDS = ("unity", "script", "eyes")
def grade():
    unticked = dict.fromkeys(KINDS, 0)
    def malformed(why):
        print(f"drain_pick: owed-local checklist malformed: {why}", file=sys.stderr)
        return "malformed", unticked
    starts = [i for i, l in enumerate(lines) if l.rstrip() == "### Owed local"]
    if not starts:
        return "absent", unticked
    if len(starts) > 1:
        return malformed("more than one `### Owed local` heading")
    items = 0
    none = False
    for number, line in enumerate(lines[starts[0] + 1:], starts[0] + 2):
        if HEADING.match(line):
            break
        if not line.strip():
            continue
        item = ITEM.match(line)
        if item and not none:
            items += 1
            if item[1] == " ":
                unticked[item[2]] += 1
        elif items and line.startswith("  "):
            continue
        elif not items and not none and line.rstrip() == "None.":
            none = True
        else:
            return malformed(f"body line {number}: {line.strip()}")
    if none:
        return "none", unticked
    if not items:
        return malformed("the section is empty")
    return ("open" if any(unticked.values()) else "discharged"), unticked
verdict, unticked = grade()
print(f"OWED={verdict}")
for kind in KINDS:
    print(f"OPEN_{kind.upper()}={unticked[kind] if verdict == 'open' else 0}")
PY
}

cmd_owed() {
  [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]] || usage
  local pr="$1" r
  r="$(repo)" || infra "gh repo view failed"
  scratch
  gh pr view "$pr" --repo "$r" --json body --jq .body > "$TMP/body.md" || infra "gh pr view $pr failed"
  owed_verdict "$TMP/body.md"
}

[[ $# -ge 1 ]] || usage
verb="$1"; shift
case "$verb" in
  pick) cmd_pick "$@" ;;
  claim) cmd_claim "$@" ;;
  release) cmd_release "$@" ;;
  owed) cmd_owed "$@" ;;
  *) usage ;;
esac
