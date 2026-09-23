#!/usr/bin/env bash
set -euo pipefail

# On-event triage of one issue (arc #617 Slice-2): board add + Status, the one-priority rule
# and `needs-triage` run here deterministically; the retry / premise / dead-pointer checks run
# in a read-only `claude -p` whose structured verdict this script renders into the issue's one
# marked note. Claude holds Read/Grep/Glob only; every tracker write is a `gh` call below.
#
# Usage: on_event_triage.sh --event <event-json-path> [--dry-run]
#   <event-json-path>  the Actions payload ($GITHUB_EVENT_PATH): an `issues` event carries
#                      .action, .issue and (edited) .changes; a workflow_dispatch carries
#                      .inputs.issue_number and is triaged like `opened`.
#   --dry-run          reads only: every write is printed as a WRITE= line and run nowhere.
# Env:  GH_TOKEN (issue reads, labels, comment) · PROJECTS_TOKEN (board reads + mutations;
#       classic PAT, `project` scope only — GITHUB_TOKEN cannot mutate a user-owned project) ·
#       CLAUDE_CODE_OAUTH_TOKEN (claude -p) · GITHUB_REPOSITORY (owner/repo; default `gh repo
#       view`) · GITHUB_STEP_SUMMARY (optional: the closing report is appended there) ·
#       TRIAGE_RETRY_SLEEP (seconds before the one retry of a rate-limited write; default 60).
# Exit: 0 done — including every early exit (closed issue, non-allowlisted author, edit gate
#       skipped) · 1 infra (gh or claude failed, verdict unparsable, PROJECTS_TOKEN absent on a
#       live run) · 2 usage.
# Stdout trailers, one per line, stable:
#   ISSUE=<n>  GATE=<closed|non-allowlisted|edit-skipped|run>  VERDICT=<none|clean|findings>
#   WRITE=<gh command>  (one per write, applied or — dry run — printed)  WRITES=<count>
#   DRY_RUN=<0|1>  TOKENS_IN=  TOKENS_OUT=  TOKENS_CACHE=  COST_USD=
# Prose to stderr.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROMPT_FILE="$SCRIPT_DIR/../.claude/skills/issue-triage/on-event.md"

ALLOWLIST="amindell11"
BOT_LOGIN="github-actions[bot]"
MARKER="<!-- on-event-triage -->"
CLAUDE_MODEL="claude-opus-5-5"
CLAUDE_MAX_TURNS=12

# Operative copy of doc/agents/issue-tracker.md § Projects board sync; Doing / Done are human-owned.
PROJECT_ID="PVT_kwHOAJsCkc4BfiTv"
STATUS_FIELD_ID="PVTSSF_lAHOAJsCkc4BfiTvzhZ0hiE"
STATUS_MAP="needs-triage=d6567434 bug=76914216 pri:now=291743a0 pri:next=4dbdbff5 pri:later=225f15fa"
STICKY_STATUS="772cf1a0 165b6aec"

VERDICT_SCHEMA='{"type":"object","additionalProperties":false,"properties":{"retry_of":{"type":["integer","null"]},"retry_evidence":{"type":"string"},"premise_holds":{"type":"boolean"},"premise_evidence":{"type":"string"},"dead_pointers":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{"path":{"type":"string"},"replacement":{"type":["string","null"]}},"required":["path","replacement"]}}},"required":["retry_of","retry_evidence","premise_holds","premise_evidence","dead_pointers"]}'

usage() { echo "Usage: on_event_triage.sh --event <event-json-path> [--dry-run]" >&2; exit 2; }
infra() { echo "on_event_triage: $1" >&2; exit 1; }
say() { echo "on_event_triage: $1" >&2; }

EVENT="" DRY_RUN=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --event) [[ $# -ge 2 ]] || usage; EVENT="$2"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    *) usage ;;
  esac
done
[[ -n "$EVENT" && -f "$EVENT" ]] || usage

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
DATE="$(date -u +%F)"
REPO="${GITHUB_REPOSITORY:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"
RETRY_SLEEP="${TRIAGE_RETRY_SLEEP:-60}"

SUMMARY_LINES=()
summary() { SUMMARY_LINES+=("$1"); }
WRITES=0
run_write() {
  local board=0
  [[ "$1" != --board ]] || { board=1; shift; }
  local shown="$*"
  [[ "$board" -eq 0 ]] || shown="$shown  (PROJECTS_TOKEN)"
  echo "WRITE=$shown"
  summary "- \`$shown\`"
  WRITES=$((WRITES + 1))
  [[ "$DRY_RUN" -eq 0 ]] || return 0
  if [[ "$board" -eq 1 ]]; then
    [[ -n "${PROJECTS_TOKEN:-}" ]] || infra "PROJECTS_TOKEN is not set and a board write is due"
    GH_TOKEN="$PROJECTS_TOKEN" gh_retry "$@"
  else
    gh_retry "$@"
  fi
}
# One retry after a minute on a rate-limited write (doc/agents/issue-tracker.md § Operations).
gh_retry() {
  local err
  if err="$("$@" 2>&1 >/dev/null)"; then return 0; fi
  if [[ "$err" == *"403"* || "$err" == *"rate limit"* ]]; then
    say "rate limited, retrying in ${RETRY_SLEEP}s: $err"
    sleep "$RETRY_SLEEP"
    "$@" >/dev/null || infra "write failed after retry: $*"
  else
    infra "write failed: $* — $err"
  fi
}

finish() {
  local gate="$1" verdict="$2"
  echo "GATE=$gate"
  echo "VERDICT=$verdict"
  echo "WRITES=$WRITES"
  echo "DRY_RUN=$DRY_RUN"
  echo "TOKENS_IN=${TOKENS_IN:-0}"
  echo "TOKENS_OUT=${TOKENS_OUT:-0}"
  echo "TOKENS_CACHE=${TOKENS_CACHE:-0}"
  echo "COST_USD=${COST_USD:-0}"
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    {
      echo "## On-event triage #$NUMBER"
      echo "- Gate: \`$gate\` · Verdict: \`$verdict\` · Dry run: $DRY_RUN"
      echo "- Writes ($WRITES):"
      printf '%s\n' "${SUMMARY_LINES[@]:-- none}"
      echo "- Usage: in=${TOKENS_IN:-0} out=${TOKENS_OUT:-0} cache=${TOKENS_CACHE:-0} cost_usd=${COST_USD:-0}"
    } >> "$GITHUB_STEP_SUMMARY"
  fi
  exit 0
}

# ---- Event ------------------------------------------------------------------------------------
eval "$(python3 - "$EVENT" "$TMP" <<'PY'
import json, shlex, sys
ev = json.load(open(sys.argv[1], encoding="utf-8"))
tmp = sys.argv[2]
changes = ev.get("changes") or {}
if "issue" in ev:
    action = ev.get("action") or ""
    number = ev["issue"]["number"]
else:
    action = "dispatch"
    number = int((ev.get("inputs") or {})["issue_number"])
old_body = (changes.get("body") or {}).get("from")
has_old = old_body is not None
if has_old:
    open(f"{tmp}/old_body.txt", "w", encoding="utf-8", newline="\n").write(old_body)
print(f"ACTION={shlex.quote(action)}")
print(f"NUMBER={number}")
print(f"TITLE_CHANGED={1 if 'title' in changes else 0}")
print(f"HAS_OLD_BODY={1 if has_old else 0}")
PY
)"
echo "ISSUE=$NUMBER"
say "issue #$NUMBER, action=$ACTION, dry_run=$DRY_RUN"

gh issue view "$NUMBER" --repo "$REPO" --json number,title,body,state,author,labels,assignees,id \
  > "$TMP/issue.json" || infra "gh issue view $NUMBER failed"

eval "$(python3 - "$TMP/issue.json" <<'PY'
import json, shlex, sys
issue = json.load(open(sys.argv[1], encoding="utf-8"))
labels = [l["name"] for l in issue.get("labels") or []]
print(f"STATE={shlex.quote((issue.get('state') or '').upper())}")
print(f"AUTHOR={shlex.quote((issue.get('author') or {}).get('login') or '')}")
print(f"NODE_ID={shlex.quote(issue.get('id') or '')}")
print(f"LABELS={shlex.quote(' '.join(labels))}")
print(f"ASSIGNED={1 if issue.get('assignees') else 0}")
PY
)"

[[ "$STATE" != CLOSED ]] || { say "closed issue, nothing to do"; finish closed none; }

# ---- Labels -----------------------------------------------------------------------------------
has_label() { [[ " $LABELS " == *" $1 "* ]]; }
add_label() { has_label "$1" || { run_write gh issue edit "$NUMBER" --repo "$REPO" --add-label "$1"; LABELS="$LABELS $1"; }; }
remove_label() { run_write gh issue edit "$NUMBER" --repo "$REPO" --remove-label "$1"; LABELS=" $LABELS "; LABELS="${LABELS// $1 / }"; LABELS="${LABELS# }"; LABELS="${LABELS% }"; }

pri_labels() { local l; for l in $LABELS; do [[ "$l" == pri:* ]] && echo "$l"; done; true; }

if [[ "$AUTHOR" != "$ALLOWLIST" ]]; then
  say "author '$AUTHOR' is not allowlisted: board add, Status Triage, needs-triage only"
  add_label needs-triage
else
  pris=($(pri_labels))
  if [[ ${#pris[@]} -gt 1 ]]; then
    gh api "repos/$REPO/issues/$NUMBER/events" --paginate > "$TMP/events.json" || infra "gh api issue events failed"
    keep="$(python3 - "$TMP/events.json" "${pris[@]}" <<'PY'
import json, sys
events = json.load(open(sys.argv[1], encoding="utf-8"))
present = sys.argv[2:]
last = {}
for e in events:
    if e.get("event") == "labeled" and (e.get("label") or {}).get("name") in present:
        last[e["label"]["name"]] = e.get("created_at") or ""
print(max(present, key=lambda p: last.get(p, "")))
PY
)"
    say "one-priority rule: keeping $keep (added last)"
    for p in "${pris[@]}"; do [[ "$p" == "$keep" ]] || remove_label "$p"; done
  elif [[ ${#pris[@]} -eq 0 ]]; then
    add_label needs-triage
  fi
fi

# ---- Board ------------------------------------------------------------------------------------
status_option() {
  local pair
  for pair in $STATUS_MAP; do
    has_label "${pair%%=*}" && { echo "${pair##*=}"; return; }
  done
  true
}
OPTION="$(status_option)"
ITEM_ID="" CURRENT_OPTION=""
board_lookup() {
  GH_TOKEN="$PROJECTS_TOKEN" gh api graphql -F id="$NODE_ID" -f query='query($id: ID!) { node(id: $id) { ... on Issue { projectItems(first: 50) { nodes { id project { id } fieldValueByName(name: "Status") { ... on ProjectV2ItemFieldSingleSelectValue { optionId } } } } } } }' \
    > "$TMP/board.json" || infra "board query failed"
  eval "$(python3 - "$TMP/board.json" "$PROJECT_ID" <<'PY'
import json, shlex, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
nodes = ((data.get("data") or {}).get("node") or {}).get("projectItems", {}).get("nodes") or []
for n in nodes:
    if (n.get("project") or {}).get("id") == sys.argv[2]:
        print(f"ITEM_ID={shlex.quote(n['id'])}")
        print(f"CURRENT_OPTION={shlex.quote(((n.get('fieldValueByName') or {}).get('optionId')) or '')}")
PY
)"
}
if [[ -n "${PROJECTS_TOKEN:-}" ]]; then
  board_lookup
else
  [[ "$DRY_RUN" -eq 1 ]] || infra "PROJECTS_TOKEN is not set (board reads and writes need it)"
  say "PROJECTS_TOKEN absent: board membership unknown, printing the add + Status mutations"
fi
if [[ -z "$ITEM_ID" ]]; then
  run_write --board gh api graphql -f query="mutation { addProjectV2ItemById(input: {projectId: \"$PROJECT_ID\", contentId: \"$NODE_ID\"}) { item { id } } }"
  [[ "$DRY_RUN" -eq 1 ]] || board_lookup
  ITEM_ID="${ITEM_ID:-<item-id>}"
fi
if [[ -n "$OPTION" && "$OPTION" != "$CURRENT_OPTION" && " $STICKY_STATUS " != *" $CURRENT_OPTION "* ]]; then
  run_write --board gh api graphql -f query="mutation { updateProjectV2ItemFieldValue(input: {projectId: \"$PROJECT_ID\", itemId: \"$ITEM_ID\", fieldId: \"$STATUS_FIELD_ID\", value: {singleSelectOptionId: \"$OPTION\"}}) { projectV2Item { id } } }"
fi

[[ "$AUTHOR" == "$ALLOWLIST" ]] || finish non-allowlisted none

# ---- Edit gate --------------------------------------------------------------------------------
if [[ "$ACTION" == edited && "$TITLE_CHANGED" -eq 0 ]]; then
  gate_open=0
  if [[ "$HAS_OLD_BODY" -eq 1 ]]; then
    gate_open="$(python3 - "$TMP/issue.json" "$TMP/old_body.txt" <<'PY'
import json, re, sys
SEG = r"[\w@-]+(?:\.[\w@-]+)*"
PATH_RE = re.compile(rf"(?<![\w/.])(?:{SEG}/)+{SEG}|(?<![\w/.])[\w-]+\.(?:cs|md|sh|ps1|py|yml|yaml|json|asmdef|unity|prefab|asset|mat|shader|hlsl|cginc|txt)\b")
new = json.load(open(sys.argv[1], encoding="utf-8")).get("body") or ""
old = open(sys.argv[2], encoding="utf-8").read()
print(1 if set(PATH_RE.findall(new)) - set(PATH_RE.findall(old)) else 0)
PY
)"
  fi
  if [[ "$gate_open" -eq 0 ]]; then
    say "edit gate: no title change and no new path-like token — checks wait for the sweep"
    finish edit-skipped none
  fi
fi

# ---- Claude -----------------------------------------------------------------------------------
[[ -f "$PROMPT_FILE" ]] || infra "prompt file missing: $PROMPT_FILE"
gh issue list --repo "$REPO" --state closed --limit 1000 --json number,title,closedAt > "$TMP/closed.json" \
  || infra "gh issue list --state closed failed"
python3 - "$PROMPT_FILE" "$TMP/issue.json" "$TMP/closed.json" "$TMP/prompt.md" <<'PY'
import json, sys
prompt = open(sys.argv[1], encoding="utf-8").read()
issue = json.load(open(sys.argv[2], encoding="utf-8"))
closed = json.load(open(sys.argv[3], encoding="utf-8"))
n = issue["number"]
labels = ", ".join(l["name"] for l in issue.get("labels") or []) or "none"
assignees = ", ".join(a["login"] for a in issue.get("assignees") or []) or "none"
out = [prompt.rstrip(), "", "## Packet", "",
       f"Issue #{n} · author {issue['author']['login']} · labels: {labels} · assignees: {assignees}", "",
       f"<issue-body number={n}>", f"# {issue['title']}", "", issue.get("body") or "", f"</issue-body>", "",
       "<closed-issues>"]
out += [f"#{c['number']} · {c['title']} · {(c.get('closedAt') or '')[:10]}" for c in closed]
out += ["</closed-issues>", ""]
open(sys.argv[4], "w", encoding="utf-8", newline="\n").write("\n".join(out))
PY

say "running claude -p ($CLAUDE_MODEL, read-only tools, max $CLAUDE_MAX_TURNS turns)"
claude -p --tools Read,Grep,Glob --json-schema "$VERDICT_SCHEMA" --output-format json \
  --max-turns "$CLAUDE_MAX_TURNS" --model "$CLAUDE_MODEL" < "$TMP/prompt.md" > "$TMP/claude.json" \
  || infra "claude -p exited non-zero"

eval "$(python3 - "$TMP/claude.json" "$TMP/note.md" "$DATE" "$ASSIGNED" "$MARKER" <<'PY'
import json, shlex, sys
try:
    out = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception as e:
    print(f"CLAUDE_ERROR={shlex.quote(f'output is not JSON: {e}')}"); sys.exit(0)
if not isinstance(out, dict) or out.get("is_error"):
    print(f"CLAUDE_ERROR={shlex.quote('claude reported an error: ' + str(out.get('result') if isinstance(out, dict) else out)[:300])}"); sys.exit(0)
v = out.get("structured_output")
if v is None:
    try:
        v = json.loads(out.get("result") or "")
    except Exception:
        v = None
required = {"retry_of": (int, type(None)), "retry_evidence": str, "premise_holds": bool,
            "premise_evidence": str, "dead_pointers": list}
if not isinstance(v, dict) or any(k not in v or not isinstance(v[k], t) for k, t in required.items()) \
        or any(not isinstance(d, dict) or "path" not in d for d in v["dead_pointers"]):
    print(f"CLAUDE_ERROR={shlex.quote('verdict does not match the schema: ' + json.dumps(v)[:300])}"); sys.exit(0)
usage = out.get("usage") or {}
print(f"TOKENS_IN={int(usage.get('input_tokens') or 0)}")
print(f"TOKENS_OUT={int(usage.get('output_tokens') or 0)}")
print(f"TOKENS_CACHE={int(usage.get('cache_creation_input_tokens') or 0) + int(usage.get('cache_read_input_tokens') or 0)}")
print(f"COST_USD={out.get('total_cost_usd') or 0}")
lines = []
if v["retry_of"] is not None:
    lines.append(f"Retry of #{v['retry_of']} — {v['retry_evidence']}")
if not v["premise_holds"]:
    lines.append(f"Premise not in the tree — {v['premise_evidence']}")
for d in v["dead_pointers"]:
    rep = d.get("replacement")
    lines.append(f"Dead pointer: `{d['path']}` → " + (f"`{rep}`" if rep else "no replacement found"))
print(f"FINDINGS={len(lines)}")
if lines:
    if sys.argv[4] == "1":
        lines.append("In flight (assigned)")
    open(sys.argv[2], "w", encoding="utf-8", newline="\n").write(
        f"{sys.argv[5]}\nOn-event triage {sys.argv[3]}\n" + "\n".join(lines) + "\n")
PY
)"
[[ -z "${CLAUDE_ERROR:-}" ]] || infra "$CLAUDE_ERROR"
say "verdict: $FINDINGS finding(s)"

# ---- Note -------------------------------------------------------------------------------------
gh api "repos/$REPO/issues/$NUMBER/comments" --paginate > "$TMP/comments.json" || infra "gh api issue comments failed"
PRIOR_ID="$(python3 - "$TMP/comments.json" "$BOT_LOGIN" "$MARKER" <<'PY'
import json, sys
comments = json.load(open(sys.argv[1], encoding="utf-8"))
ids = [c["id"] for c in comments if (c.get("user") or {}).get("login") == sys.argv[2] and sys.argv[3] in (c.get("body") or "")]
print(ids[-1] if ids else "")
PY
)"
if [[ "$FINDINGS" -gt 0 ]]; then
  if [[ -n "$PRIOR_ID" ]]; then
    run_write gh api -X PATCH "repos/$REPO/issues/comments/$PRIOR_ID" -F "body=@$TMP/note.md"
  else
    run_write gh issue comment "$NUMBER" --repo "$REPO" --body-file "$TMP/note.md"
  fi
  [[ "$DRY_RUN" -eq 0 ]] || { say "note (dry run):"; sed 's/^/    /' "$TMP/note.md" >&2; }
  finish run findings
fi
if [[ -n "$PRIOR_ID" ]]; then
  printf '%s\nOn-event triage %s — earlier findings resolved.\n' "$MARKER" "$DATE" > "$TMP/note.md"
  run_write gh api -X PATCH "repos/$REPO/issues/comments/$PRIOR_ID" -F "body=@$TMP/note.md"
fi
finish run clean
