#!/usr/bin/env bash
set -euo pipefail

# Merge reconcile (arc #617 Slice-4): after a push to main, reconcile the tracker with what each
# squash-merged PR in the push shipped — a Shipped note and board Done on the issues the PR
# closes, a Touched note on the open issues its body cites, a residue listing of `#N` citations
# left in the agent docs, and a mismatch warning when the body disclaims a close the PR performs.
# Mechanical: no Claude, no body edits; never closes or reopens an issue. Idempotent by text
# match (Shipped), current Status (Done) and marker + line match (Touched).
#
# Usage: merge_reconcile.sh --event <event-json-path> [--dry-run]
#        merge_reconcile.sh --sha <sha> [--dry-run]
#   <event-json-path>  the Actions `push` payload ($GITHUB_EVENT_PATH); every entry of .commits[]
#                      whose subject ends in `(#N)` names a merged PR to reconcile.
#   --sha <sha>        one commit, read through the API (the workflow_dispatch form).
#   --dry-run          reads only: every write is printed as a WRITE= line and run nowhere.
# The residue grep runs over the tracked files of the git tree at the working directory.
# Env:  GH_TOKEN (PR and issue reads, comments) · PROJECTS_TOKEN (board reads + the Done
#       mutation; classic PAT, `project` scope only — GITHUB_TOKEN cannot mutate a user-owned
#       project) · GITHUB_REPOSITORY (owner/repo; default `gh repo view`) · GITHUB_STEP_SUMMARY
#       (optional: the closing report is appended there) · TRIAGE_RETRY_SLEEP (seconds for the
#       one retry of a rate-limited write and the one wait for GitHub's auto-close; default 60).
# Exit: 0 done — including `no-pr` · 1 infra (gh failed, or PROJECTS_TOKEN absent on a live run
#       that has a closed issue to move) · 2 usage.
# Stdout trailers, one per line, stable — one block per reconciled PR:
#   PR=<n>  CLOSED=<n,…>  TOUCHED=<n,…>  RESIDUE=<hit count>  MISMATCH=<n,…>
#   WRITE=<gh command>  (one per write, applied or — dry run — printed)
# then the closing trailers: GATE=<no-pr|run>  WRITES=<count>  DRY_RUN=<0|1>
# Prose to stderr.

BOT_LOGIN="github-actions[bot]"
MARKER="<!-- merge-reconcile -->"
RESIDUE_PATHS=(AGENTS.md doc/ .claude/ TESTING.md scripts/)

# Operative copy of doc/agents/issue-tracker.md § Projects board sync.
PROJECT_ID="PVT_kwHOAJsCkc4BfiTv"
STATUS_FIELD_ID="PVTSSF_lAHOAJsCkc4BfiTvzhZ0hiE"
DONE_OPTION="165b6aec"

usage() { echo "Usage: merge_reconcile.sh (--event <event-json-path> | --sha <sha>) [--dry-run]" >&2; exit 2; }
infra() { echo "merge_reconcile: $1" >&2; exit 1; }
say() { echo "merge_reconcile: $1" >&2; }

EVENT="" SHA="" DRY_RUN=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --event) [[ $# -ge 2 ]] || usage; EVENT="$2"; shift 2 ;;
    --sha) [[ $# -ge 2 ]] || usage; SHA="$2"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    *) usage ;;
  esac
done
[[ -n "$EVENT" || -n "$SHA" ]] || usage
[[ -z "$EVENT" || -z "$SHA" ]] || usage
[[ -z "$EVENT" || -f "$EVENT" ]] || usage

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
REPO="${GITHUB_REPOSITORY:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"
RETRY_SLEEP="${TRIAGE_RETRY_SLEEP:-60}"

SUMMARY_LINES=()
summary() { SUMMARY_LINES+=("$1"); }
hash_list() { local s="$*"; if [[ -n "$s" ]]; then printf '#%s' "${s// /, #}"; else printf none; fi; }
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
  echo "GATE=$1"
  echo "WRITES=$WRITES"
  echo "DRY_RUN=$DRY_RUN"
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    {
      echo "## Merge reconcile"
      echo "- Gate: \`$1\` · Dry run: $DRY_RUN · Writes: $WRITES"
      printf '%s\n' "${SUMMARY_LINES[@]:-- no PR in this push}"
    } >> "$GITHUB_STEP_SUMMARY"
  fi
  exit 0
}

# ---- Commits ----------------------------------------------------------------------------------
# One line per PR-bearing commit: <sha>|<date>|<pr>; the last `(#N)` of the subject is the PR,
# so `Revert "… (#N)" (#M)` names M.
if [[ -n "$EVENT" ]]; then
  cp "$EVENT" "$TMP/commits-src.json"
else
  gh api "repos/$REPO/commits/$SHA" > "$TMP/commits-src.json" || infra "gh api commits/$SHA failed"
fi
python3 - "$TMP/commits-src.json" "$TMP/commits.tsv" <<'PY'
import json, re, sys
src = json.load(open(sys.argv[1], encoding="utf-8"))
if "commits" in src:
    commits = [(c["id"], c.get("timestamp") or "", c.get("message") or "") for c in src["commits"]]
else:
    commits = [(src["sha"], ((src.get("commit") or {}).get("committer") or {}).get("date") or "", (src.get("commit") or {}).get("message") or "")]
with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as out:
    for sha, when, message in commits:
        m = re.search(r"\(#(\d+)\)\s*$", message.splitlines()[0] if message else "")
        if m:
            out.write(f"{sha}|{when[:10]}|{m.group(1)}\n")
PY
[[ -s "$TMP/commits.tsv" ]] || { say "no commit subject ends in (#N): nothing to reconcile"; finish no-pr; }

# ---- Tracker reads ----------------------------------------------------------------------------
# REST issue record → `pr` (the number is a pull request) · `open` · `closed` · `missing` (404),
# with node id and state files beside it for the callers that need them.
probe_issue() {
  local out
  if out="$(gh api "repos/$REPO/issues/$1" 2>&1)"; then
    printf '%s' "$out" > "$TMP/issue-$1.json"
    python3 -c 'import json,sys; d=json.load(open(sys.argv[1], encoding="utf-8")); print("pr" if "pull_request" in d else d.get("state") or "")' "$TMP/issue-$1.json"
  elif [[ "$out" == *"HTTP 404"* ]]; then
    echo missing
  else
    infra "gh api issues/$1 failed: $out"
  fi
}
issue_field() { python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8")).get(sys.argv[2]) or "")' "$TMP/issue-$1.json" "$2"; }
fetch_comments() {
  gh api "repos/$REPO/issues/$1/comments" --paginate --slurp > "$TMP/comments-$1.json" || infra "gh api issue comments for #$1 failed"
}

ITEM_ID="" CURRENT_OPTION=""
board_lookup() {
  GH_TOKEN="$PROJECTS_TOKEN" gh api graphql -F id="$1" -f query='query($id: ID!) { node(id: $id) { ... on Issue { projectItems(first: 50) { nodes { id project { id } fieldValueByName(name: "Status") { ... on ProjectV2ItemFieldSingleSelectValue { optionId } } } } } } }' \
    > "$TMP/board.json" || infra "board query failed"
  eval "$(python3 - "$TMP/board.json" "$PROJECT_ID" <<'PY'
import json, shlex, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
nodes = ((data.get("data") or {}).get("node") or {}).get("projectItems", {}).get("nodes") or []
print("ITEM_ID='' CURRENT_OPTION=''")
for n in nodes:
    if (n.get("project") or {}).get("id") == sys.argv[2]:
        print(f"ITEM_ID={shlex.quote(n['id'])}")
        print(f"CURRENT_OPTION={shlex.quote(((n.get('fieldValueByName') or {}).get('optionId')) or '')}")
PY
)"
}

# ---- Reconcile one PR -------------------------------------------------------------------------
reconcile() {
  local sha="$1" when="$2" pr="$3" short="${1:0:8}"
  say "commit $short → PR #$pr"
  gh pr view "$pr" --repo "$REPO" --json title,body,closingIssuesReferences > "$TMP/pr.json" || infra "gh pr view $pr failed"
  eval "$(python3 - "$TMP/pr.json" <<'PY'
import json, re, shlex, sys
pr = json.load(open(sys.argv[1], encoding="utf-8"))
closed = sorted({int(r["number"]) for r in pr.get("closingIssuesReferences") or []})
body = pr.get("body") or ""
prose = re.sub(r"^(`{3,}|~{3,}).*?^\1[^\n]*$", "", body, flags=re.S | re.M)
refs = sorted({int(n) for n in re.findall(r"(?<![\w/])#(\d+)\b", prose)} - set(closed))
mismatch = sorted({int(n) for n in re.findall(r"(?i)\b(?:does not close|not closing)\s+#(\d+)\b", body)} & set(closed))
print(f"TITLE={shlex.quote(pr.get('title') or '')}")
print(f"CLOSED={shlex.quote(' '.join(map(str, closed)))}")
print(f"REFS={shlex.quote(' '.join(map(str, refs)))}")
print(f"MISMATCH={shlex.quote(' '.join(map(str, mismatch)))}")
PY
)"

  local n touched="" kind
  for n in $REFS; do
    kind="$(probe_issue "$n")"
    [[ "$kind" == open ]] && touched="$touched $n"
  done
  touched="${touched# }"

  local residue_total=0 hits
  declare -A residue=()
  for n in $CLOSED; do
    hits="$(git grep -n -E "#$n([^0-9]|\$)" -- "${RESIDUE_PATHS[@]}" 2>/dev/null | cut -d: -f1,2 | paste -sd, - | sed 's/,/, /g' || true)"
    residue[$n]="$hits"
    [[ -z "$hits" ]] || residue_total=$((residue_total + $(tr -cd , <<<"$hits" | wc -c) + 1))
  done

  echo "PR=$pr"
  echo "CLOSED=${CLOSED// /,}"
  echo "TOUCHED=${touched// /,}"
  echo "RESIDUE=$residue_total"
  echo "MISMATCH=${MISMATCH// /,}"
  summary "### #$pr — $TITLE (squash $short)"
  summary "- Closed: $(hash_list $CLOSED) · Touched: $(hash_list $touched) · Residue hits: $residue_total · Mismatch: $(hash_list $MISMATCH)"
  for n in $CLOSED; do
    [[ -z "${residue[$n]}" ]] || summary "- Residue #$n: ${residue[$n]}"
  done
  for n in $MISMATCH; do
    say "mismatch: the body says it does not close #$n, but the PR closes it"
    summary "- ⚠ Mismatch: the body says it does not close #$n, but the PR closes it (no write)"
  done

  if [[ -n "$CLOSED" && "$DRY_RUN" -eq 0 && -z "${PROJECTS_TOKEN:-}" ]]; then
    infra "PROJECTS_TOKEN is not set and a board write is due (closed: ${CLOSED// /,})"
  fi

  # Shipped note + Done on each closed issue. GitHub's auto-close can trail the push event, so an
  # issue still open gets one wait; still open after it, the note and Done land anyway.
  for n in $CLOSED; do
    kind="$(probe_issue "$n")"
    if [[ "$kind" == open ]]; then
      say "#$n is still open (auto-close pending?); waiting ${RETRY_SLEEP}s once"
      sleep "$RETRY_SLEEP"
      kind="$(probe_issue "$n")"
      [[ "$kind" != open ]] || summary "- #$n still open after the wait: Shipped note and Done posted anyway"
    fi
    [[ "$kind" == open || "$kind" == closed ]] || infra "#$n is $kind, not an issue"
    fetch_comments "$n"
    if python3 -c 'import json,re,sys; cs=[c for p in json.load(open(sys.argv[1], encoding="utf-8")) for c in p]; sys.exit(0 if any(re.search(rf"Shipped in #{sys.argv[2]}(?!\d)", c.get("body") or "") for c in cs) else 1)' "$TMP/comments-$n.json" "$pr"; then
      say "#$n already carries 'Shipped in #$pr'"
    else
      {
        echo "Shipped in #$pr (squash $short)"
        [[ -z "${residue[$n]}" ]] || echo "Residue: ${residue[$n]}"
      } > "$TMP/shipped-$n.md"
      run_write gh issue comment "$n" --repo "$REPO" --body-file "$TMP/shipped-$n.md"
    fi
    local node_id
    node_id="$(issue_field "$n" node_id)"
    if [[ -n "${PROJECTS_TOKEN:-}" ]]; then
      board_lookup "$node_id"
    else
      ITEM_ID="" CURRENT_OPTION=""
      say "PROJECTS_TOKEN absent: board membership of #$n unknown, printing the add + Done mutations"
    fi
    if [[ "$CURRENT_OPTION" == "$DONE_OPTION" ]]; then
      say "#$n is already Done on the board"
      continue
    fi
    if [[ -z "$ITEM_ID" ]]; then
      run_write --board gh api graphql -f query="mutation { addProjectV2ItemById(input: {projectId: \"$PROJECT_ID\", contentId: \"$node_id\"}) { item { id } } }"
      [[ "$DRY_RUN" -eq 1 ]] || board_lookup "$node_id"
      ITEM_ID="${ITEM_ID:-<item-id>}"
    fi
    run_write --board gh api graphql -f query="mutation { updateProjectV2ItemFieldValue(input: {projectId: \"$PROJECT_ID\", itemId: \"$ITEM_ID\", fieldId: \"$STATUS_FIELD_ID\", value: {singleSelectOptionId: \"$DONE_OPTION\"}}) { projectV2Item { id } } }"
  done

  # Touched note: one marked bot comment per issue, one line per PR appended in place.
  local line="Touched by #$pr — $TITLE ($when)"
  for n in $touched; do
    fetch_comments "$n"
    eval "$(python3 - "$TMP/comments-$n.json" "$BOT_LOGIN" "$MARKER" "$pr" "$line" "$TMP/touched-$n.md" <<'PY'
import json, re, shlex, sys
comments = [c for page in json.load(open(sys.argv[1], encoding="utf-8")) for c in page]
prior = [c for c in comments if (c.get("user") or {}).get("login") == sys.argv[2] and sys.argv[3] in (c.get("body") or "")]
prior = prior[-1] if prior else None
body = (prior or {}).get("body") or ""
present = re.search(rf"Touched by #{sys.argv[4]}(?!\d)", body) is not None
print(f"PRIOR_ID={shlex.quote(str(prior['id']) if prior else '')}")
print(f"HAS_LINE={1 if present else 0}")
if not present:
    text = (body.rstrip("\n") + "\n" if prior else sys.argv[3] + "\n") + sys.argv[5] + "\n"
    open(sys.argv[6], "w", encoding="utf-8", newline="\n").write(text)
PY
)"
    if [[ "$HAS_LINE" -eq 1 ]]; then
      say "#$n already carries the Touched line for #$pr"
    elif [[ -n "$PRIOR_ID" ]]; then
      run_write gh api -X PATCH "repos/$REPO/issues/comments/$PRIOR_ID" -F "body=@$TMP/touched-$n.md"
    else
      run_write gh issue comment "$n" --repo "$REPO" --body-file "$TMP/touched-$n.md"
    fi
  done
}

while IFS='|' read -r sha when pr; do
  reconcile "$sha" "$when" "$pr" < /dev/null
done < "$TMP/commits.tsv"
finish run
