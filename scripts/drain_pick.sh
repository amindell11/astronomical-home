#!/usr/bin/env bash
set -euo pipefail

# Drain run pick and claim (arc #617 Slice-3a): the deterministic half of a drain run. `pick`
# reads the ready queue and names the top `unity:none` item; `claim` locks it (issue assignee)
# and takes a genuinely free pool slot for it. The session reads the scope and names the lease
# in between. Procedure: .claude/skills/agent-worktree-pr-loop/SKILL.md § Drain run.
#
# Usage: drain_pick.sh pick [--dry-run]
#        drain_pick.sh claim <issue> <lease>
#   pick    read-only. Queue = open `ready-for-agent` issues; an issue is picked only when it
#           carries `unity:none`, has no assignee, no open blocked-by dependency, and a scope
#           block: a comment by amindell11 whose first line starts `Ready proposal` (the latest
#           wins), else a body with a `What to build` heading and an `Acceptance` heading.
#           Order: pri:now > pri:next > pri:later > none, then oldest createdAt.
#           --dry-run changes nothing (pick never writes); it only stamps DRY_RUN=1.
#   claim   re-reads the assignee (taken → stop), reads `agent_worktree_pool.sh status
#           --porcelain` for a state=free slot (none → stop before any write), assigns @me,
#           then `acquire <lease> <slot>` — strict, so a stale slot is never reclaimed. An
#           acquire failure unassigns before exiting.
# Env:  GITHUB_REPOSITORY (owner/repo; default `gh repo view`) · DRAIN_POOL (pool script
#       path; default the sibling agent_worktree_pool.sh — tests inject a fake).
# Exit: pick — 0 done, picked or not · 1 infra (gh failed) · 2 usage.
#       claim — 0 claimed · 1 infra (gh or pool failed; an issue left assigned is named on
#       stderr) · 2 usage · 3 no_slot · 4 taken · 5 acquire_failed (unassigned again).
# Stdout trailers, one per line, stable:
#   pick:  SKIP=<n> <reason>[,<reason>…]  (one per queue issue not picked; reasons: no-unity-label
#          unity:<value> assigned:<login> blocked:<open-count> no-scope-block)
#          ISSUE=<n>|none  SCOPE=proposal:<comment-url>|body  (SCOPE only when picked)
#          DRY_RUN=<0|1>
#   claim: CLAIM=<claimed|no_slot|taken|acquire_failed>
#          SLOT=<slot> PATH=<abs-path> LEASE=<lease>  (claimed only)
# Prose to stderr.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="${DRAIN_POOL:-$SCRIPT_DIR/agent_worktree_pool.sh}"
PROPOSAL_AUTHOR="amindell11"

usage() { echo "Usage: drain_pick.sh pick [--dry-run] | drain_pick.sh claim <issue> <lease>" >&2; exit 2; }
infra() { echo "drain_pick: $1" >&2; exit 1; }
say() { echo "drain_pick: $1" >&2; }

repo() {
  if [[ -n "${GITHUB_REPOSITORY:-}" ]]; then echo "$GITHUB_REPOSITORY"
  else gh repo view --json nameWithOwner --jq .nameWithOwner; fi
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
  TMP="$(mktemp -d)"
  trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT
  gh api graphql -F owner="$owner" -F name="$name" -f query='query($owner: String!, $name: String!) {
    repository(owner: $owner, name: $name) {
      issues(states: OPEN, labels: ["ready-for-agent"], first: 100, orderBy: {field: CREATED_AT, direction: ASC}) {
        nodes { number createdAt body
          labels(first: 50) { nodes { name } }
          assignees(first: 10) { nodes { login } }
          issueDependenciesSummary { blockedBy }
          comments(last: 100) { nodes { author { login } url body } } } } } }' \
    > "$TMP/queue.json" || infra "gh api graphql (ready queue) failed"
  python3 - "$TMP/queue.json" "$PROPOSAL_AUTHOR" <<'PY'
import json, re, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
author = sys.argv[2]
nodes = data["data"]["repository"]["issues"]["nodes"]
RANK = {"pri:now": 0, "pri:next": 1, "pri:later": 2}
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
    if "unity:none" not in labels:
        unity = [l for l in labels if l.startswith("unity:")]
        reasons += unity or ["no-unity-label"]
    reasons += [f"assigned:{a['login']}" for a in n["assignees"]["nodes"]]
    blocked = (n.get("issueDependenciesSummary") or {}).get("blockedBy") or 0
    if blocked:
        reasons.append(f"blocked:{blocked}")
    s = scope(n)
    if s is None:
        reasons.append("no-scope-block")
    if reasons:
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
  [[ $# -eq 2 && "$1" =~ ^[0-9]+$ && -n "$2" ]] || usage
  local issue="$1" lease="$2" r assignees slot out
  r="$(repo)" || infra "gh repo view failed"

  assignees="$(gh issue view "$issue" --repo "$r" --json assignees --jq '[.assignees[].login] | join(",")')" \
    || infra "gh issue view $issue failed"
  if [[ -n "$assignees" ]]; then
    say "#$issue is already assigned ($assignees)"
    echo "CLAIM=taken"; exit 4
  fi

  out="$("$POOL" status --porcelain)" || infra "pool status --porcelain failed"
  slot="$(awk -v RS= '/(^|\n)state=free(\n|$)/ && match($0, /(^|\n)slot=[^\n]+/) {
    s = substr($0, RSTART, RLENGTH); sub(/^\n?slot=/, "", s); print s; exit }' <<<"$out")"
  if [[ -z "$slot" ]]; then
    say "no free slot (stale slots are never reclaimed by a drain run)"
    echo "CLAIM=no_slot"; exit 3
  fi

  gh issue edit "$issue" --repo "$r" --add-assignee @me >/dev/null || infra "assigning #$issue failed"
  if ! out="$("$POOL" acquire "$lease" "$slot")"; then
    gh issue edit "$issue" --repo "$r" --remove-assignee @me >/dev/null \
      || infra "acquire $slot failed and unassigning #$issue failed — #$issue is left assigned"
    say "acquire $lease $slot failed; #$issue unassigned"
    echo "CLAIM=acquire_failed"; exit 5
  fi
  out="$(grep -m1 '^SLOT=' <<<"$out")" || infra "acquire printed no SLOT= line — #$issue is left assigned"
  echo "CLAIM=claimed"
  echo "SLOT=$slot"
  echo "PATH=${out#* PATH=}"
  echo "LEASE=$lease"
}

[[ $# -ge 1 ]] || usage
verb="$1"; shift
case "$verb" in
  pick) cmd_pick "$@" ;;
  claim) cmd_claim "$@" ;;
  *) usage ;;
esac
