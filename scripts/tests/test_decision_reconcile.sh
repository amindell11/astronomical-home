#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/decision_reconcile.sh scripts/drain_pick.sh

# Hermetic regression for scripts/decision_reconcile.sh: add and set for an item off the board, no
# write when the field matches, set on a change, clear on none, no add only to clear, a PR's
# membership read, --all over open items and set rows, --dry-run issuing zero writes, the PAT rule
# on a live run, and no delete anywhere. drain_pick.sh decision runs for real; gh is a stub on PATH
# answering from fixtures, and every call it does not model fails closed.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RECONCILE="$SCRIPT_DIR/../decision_reconcile.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_CALL_LOG="$TMP/gh-calls.log"
export GH_WRITE_LOG="$TMP/gh-writes.log"
export GITHUB_REPOSITORY="owner/repo"
export GITHUB_STEP_SUMMARY="$TMP/summary.md"
export GH_TOKEN="actions-token"
mkdir -p "$FIX" "$TMP/bin"

# REST lists serve 100 rows a page. A board mutation is logged as its token, its name and its
# variables; the board reads answer as their --jq renders them.
cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
echo "token=$GH_TOKEN ${args//$'\n'/ }" >> "$GH_CALL_LOG"
page() { local n="${args##*&page=}"; n="${n%% *}"; sed -n "$((n * 100 - 99)),$((n * 100))p" "$1"; }
mutation() {
  local a prev="" name="" vars=()
  for a in "$@"; do
    if [[ "$prev" == -f && "$a" == query=* ]]; then name="$(grep -oE '[a-zA-Z]+ProjectV2[A-Za-z]*' <<<"$a" | head -n 1)"
    elif [[ "$prev" == -f ]]; then vars+=("$a"); fi
    prev="$a"
  done
  echo "token=$GH_TOKEN $name ${vars[*]}" >> "$GH_WRITE_LOG"
  [[ "$name" != addProjectV2ItemById ]] || echo PVTI_added
}
case "$args" in
  "api graphql "*"mutation("*) mutation "$@" ;;
  "api graphql "*"issueOrPullRequest"*) n="${args#*number=}"; n="${n%% *}"
    if [[ -f "$FIX/board-$n.txt" ]]; then cat "$FIX/board-$n.txt"; else printf 'I_node%s\n\n\n' "$n"; fi ;;
  "api graphql "*"items(first: 100"*) printf 'false null\n'; cat "$FIX/board-texts.txt" ;;
  "api --paginate repos/owner/repo/issues?state=open"*) cat "$FIX/open.txt" ;;
  "api repos/owner/repo/issues/"*"pull_request != null"*) n="${args#api repos/owner/repo/issues/}"; cat "$FIX/item-${n%% *}.tsv" ;;
  "api repos/owner/repo/issues/"*"/comments?"*) n="${args#api repos/owner/repo/issues/}"; page "$FIX/comments-${n%%/*}.jsonl" ;;
  "api repos/owner/repo/issues/"*"/reactions?"*|"api repos/owner/repo/pulls/"*"/files?"*|"api repos/owner/repo/commits/"*"/statuses?"*) ;;
  "api repos/owner/repo/pulls/"*"/commits?"*) echo 76b92040123456789abcdef0123456789abcdef0 ;;
  "api repos/owner/repo/pulls/"*"isDraft"*) n="${args#api repos/owner/repo/pulls/}"; n="${n%% *}"
    printf '%s 76b92040123456789abcdef0123456789abcdef0 {"number": %s, "title": "t", "isDraft": false, "headRefOid": "76b92040123456789abcdef0123456789abcdef0", "base": "main", "body": "no section"}\n' "$n" "$n" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh"
export PATH="$TMP/bin:$PATH"

FIELD=PVTF_lAHOAJsCkc4BfiTvzhkWGNc
note() { printf '{"author":{"login":"%s"},"body":"%s"}' "$1" "$2"; }
Q="$(note amindell11 'Question 2026-10-04: Cache or recompute?')"
# item <n> <open|closed> [<comment JSON>…]: an issue as drain_pick.sh decision reads it
item() { local n="$1"; printf '%s\tfalse\n' "$2" > "$FIX/item-$n.tsv"; shift 2; printf '%s\n' "$@" > "$FIX/comments-$n.jsonl"; }
# pull <n> [<comment JSON>…]: an open PR into main with no owed-local section
pull() { local n="$1"; printf 'open\ttrue\n' > "$FIX/item-$n.tsv"; shift; printf '%s\n' "$@" > "$FIX/comments-$n.jsonl"; }
# row <n> <node> [<item> <text>]: its board read; with no item it is off the board
row() { printf '%s\n%s\n%s\n' "$2" "${3:-}" "${4:-}" > "$FIX/board-$1.txt"; }

reset() {
  export PROJECTS_TOKEN="projects-pat"
  : > "$GH_CALL_LOG"; : > "$GH_WRITE_LOG"; : > "$GITHUB_STEP_SUMMARY"
  rm -f "$FIX"/*
  : > "$FIX/board-texts.txt"
}
run() { rc=0; out="$(bash "$RECONCILE" "$@" 2>"$TMP/err")" || rc=$?; }
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }
writes() { cat "$GH_WRITE_LOG"; }

# --- usage ---------------------------------------------------------------------------------------
reset
for bad in "" "abc" "12 13" "--all 12" "12 --all" "--frob"; do
  run $bad
  [[ "$rc" -eq 2 ]] || fail "'$bad' should exit 2 (got $rc)"
done

# --- an open decision off the board: add, then set on the new item -------------------------------
reset
item 10 open "$Q"
run 10
[[ "$rc" -eq 0 ]] || fail "a live run exits 0 (rc=$rc: $(cat "$TMP/err"))"
[[ "$out" == "ITEM=10"$'\n'"DECISION=Cache or recompute?"$'\n'"WRITE=board_add I_node10"$'\n'"WRITE=board_set_text PVTI_added $FIELD Cache or recompute?"$'\n'"ITEMS=1"$'\n'"WRITES=2"$'\n'"DRY_RUN=0" ]] \
  || fail "an item off the board is added, then its field set (got: $out)"
[[ "$(writes)" == "token=projects-pat addProjectV2ItemById project=PVT_kwHOAJsCkc4BfiTv content=I_node10"$'\n'"token=projects-pat updateProjectV2ItemFieldValue project=PVT_kwHOAJsCkc4BfiTv item=PVTI_added field=$FIELD text=Cache or recompute?" ]] \
  || fail "both writes run under PROJECTS_TOKEN, the set on the item the add returned (got: $(writes))"
grep -q "token=projects-pat api graphql .*issueOrPullRequest" "$GH_CALL_LOG" || fail "the board read runs under PROJECTS_TOKEN"
grep -q "token=actions-token api repos/owner/repo/issues/10/comments" "$GH_CALL_LOG" || fail "the decision's REST reads run under GH_TOKEN"
grep -q 'board_set_text' "$GITHUB_STEP_SUMMARY" || fail "the job summary lists the writes (got: $(cat "$GITHUB_STEP_SUMMARY"))"

# --- on the board: no write when it matches, a set when it differs -------------------------------
reset
item 11 open "$Q"; row 11 I_node11 PVTI_11 "Cache or recompute?"
item 12 open "$Q"; row 12 I_node12 PVTI_12 "an older question"
run 11
[[ "$(trailer WRITES <<<"$out")" == 0 && ! -s "$GH_WRITE_LOG" ]] || fail "a field that matches gets no write (got: $out)"
run 12
[[ "$(grep '^WRITE=' <<<"$out")" == "WRITE=board_set_text PVTI_12 $FIELD Cache or recompute?" ]] || fail "a field that differs is set in place, with no add (got: $out)"

# --- none: clear a set field; never add an item only to clear it ---------------------------------
reset
item 13 open "$(note amindell11 'Ruled: recompute')"; row 13 I_node13 PVTI_13 "Cache or recompute?"
item 14 open
item 15 closed "$Q"; row 15 I_node15 PVTI_15
run 13
[[ "$(trailer DECISION <<<"$out")" == none && "$(grep '^WRITE=' <<<"$out")" == "WRITE=board_clear PVTI_13 $FIELD" ]] || fail "none clears a set field (got: $out)"
[[ "$(writes)" == "token=projects-pat clearProjectV2ItemFieldValue project=PVT_kwHOAJsCkc4BfiTv item=PVTI_13 field=$FIELD" ]] || fail "the clear mutation (got: $(writes))"
: > "$GH_WRITE_LOG"
run 14
[[ "$(trailer WRITES <<<"$out")" == 0 && ! -s "$GH_WRITE_LOG" ]] || fail "none off the board writes nothing (got: $out)"
run 15
[[ "$(trailer WRITES <<<"$out")" == 0 && ! -s "$GH_WRITE_LOG" ]] || fail "none with the field unset writes nothing (got: $out)"

# --- a PR: its own node is the board content ----------------------------------------------------------
reset
pull 40 "$Q"; row 40 PR_node40
run 40
[[ "$(grep '^WRITE=' <<<"$out")" == "WRITE=board_add PR_node40"$'\n'"WRITE=board_set_text PVTI_added $FIELD Cache or recompute?" ]] \
  || fail "a PR is added by its pull-request node (got: $out)"

# --- --all: every open item, and every board row with the field set ----------------------------------
reset
printf '14\n10\n' > "$FIX/open.txt"
printf '13\n10\n' > "$FIX/board-texts.txt"
item 10 open "$Q"; row 10 I_node10 PVTI_10 "Cache or recompute?"
item 13 closed; row 13 I_node13 PVTI_13 "Cache or recompute?"
item 14 open
run --all
[[ "$(grep '^ITEM=' <<<"$out" | paste -sd, -)" == "ITEM=10,ITEM=13,ITEM=14" && "$(trailer ITEMS <<<"$out")" == 3 ]] \
  || fail "--all reconciles the union once each, in number order (got: $out)"
[[ "$(grep '^WRITE=' <<<"$out")" == "WRITE=board_clear PVTI_13 $FIELD" ]] || fail "--all clears the stale row of a closed item (got: $out)"

# --- --dry-run: every write printed, none run -----------------------------------------------------------
reset
item 10 open "$Q"
run 10 --dry-run
[[ "$(grep -c '^WRITE=' <<<"$out")" -eq 2 && "$(trailer DRY_RUN <<<"$out")" == 1 ]] || fail "a dry run prints the add and the set (got: $out)"
[[ "$(grep '^WRITE=' <<<"$out" | tail -n 1)" == "WRITE=board_set_text <item-id> $FIELD Cache or recompute?" ]] || fail "a dry run sets the item the add would return (got: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "a dry run writes nothing (got: $(writes))"

# --- PROJECTS_TOKEN: a live run needs it; a dry run prints the writes for an item off the board ------
reset
unset PROJECTS_TOKEN
item 10 open "$Q"
run 10
[[ "$rc" -eq 1 && ! -s "$GH_WRITE_LOG" ]] || fail "a live run without PROJECTS_TOKEN exits 1 having written nothing (rc=$rc)"
grep -q PROJECTS_TOKEN "$TMP/err" || fail "the exit names PROJECTS_TOKEN (got: $(cat "$TMP/err"))"
run 10 --dry-run
[[ "$rc" -eq 0 && "$(grep '^WRITE=' <<<"$out" | head -n 1)" == "WRITE=board_add <node-id>" ]] || fail "a dry run without the token assumes the item is off the board (rc=$rc: $out)"
! grep -q 'graphql' "$GH_CALL_LOG" || fail "without the token nothing reads the board"

# --- no path removes an item ------------------------------------------------------------------------------
! grep -nE 'delete|archive' "$RECONCILE" "$SCRIPT_DIR/../lib/board.sh" || fail "the writer and the board lib never delete or archive an item"

echo "PASS test_decision_reconcile.sh"
