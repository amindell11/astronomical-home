#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/merge_reconcile.sh

# Hermetic regression for scripts/merge_reconcile.sh: subject parse, the no-PR exit, the
# closed / touched split, Shipped-note and Done skips, Touched append vs first note, the residue
# listing, the mismatch trailer, --dry-run issuing zero writes, and the PAT rule on live runs.
# gh is a stub on PATH answering from fixtures; the residue grep runs in a throwaway git tree.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RECONCILE="$SCRIPT_DIR/../merge_reconcile.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_CALL_LOG="$TMP/gh-calls.log"
export GH_WRITE_LOG="$TMP/gh-writes.log"
export GITHUB_REPOSITORY="owner/repo"
export GITHUB_STEP_SUMMARY="$TMP/summary.md"
export PROJECTS_TOKEN="projects-pat"
export GH_TOKEN="actions-token"
export TRIAGE_RETRY_SLEEP=0
mkdir -p "$FIX"

export STUB_BIN="$TMP/bin"
mkdir -p "$STUB_BIN"

# Reads answer from fixtures (a missing issue fixture is a 404); writes are logged with the token
# they ran under and any body file inlined, so a test can assert on the comment text.
cat > "$STUB_BIN/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
echo "$args" >> "$GH_CALL_LOG"
inline_body() {
  local a prev="" body=""
  for a in "$@"; do
    [[ "$prev" != --body-file ]] || body="$(cat "$a")"
    [[ "$a" != body=@* ]] || body="$(cat "${a#body=@}")"
    prev="$a"
  done
  printf '%s' "$body"
}
pages() { if [[ -f "$1" ]]; then printf '[%s,[]]' "$(cat "$1")"; else printf '[[],[]]'; fi; }
case "$args" in
  "pr view "*" --json title,body,closingIssuesReferences") n="${args#pr view }"; n="${n%% *}"; cat "$FIX/pr-$n.json" ;;
  "api repos/owner/repo/issues/"*"/comments --paginate --slurp") n="${args#api repos/owner/repo/issues/}"; pages "$FIX/comments-${n%%/*}.json" ;;
  "api repos/owner/repo/issues/"*) n="${args##*/}"; [[ -f "$FIX/issue-$n.json" ]] && cat "$FIX/issue-$n.json" || { echo "gh: Not Found (HTTP 404)" >&2; exit 1; } ;;
  "api repos/owner/repo/commits/"*) cat "$FIX/commit-${args##*/}.json" ;;
  "api -X PATCH repos/"*"/comments/"*) echo "token=$GH_TOKEN $args body=<<$(inline_body "$@")>>" >> "$GH_WRITE_LOG" ;;
  "api graphql "*"mutation "*) echo "token=$GH_TOKEN $args" >> "$GH_WRITE_LOG"; echo '{"data":{}}' ;;
  "api graphql -F id="*"projectItems"*) id="${args#*-F id=}"; id="${id%% *}"; echo "token=$GH_TOKEN board-query $id" >> "$GH_CALL_LOG"
    if [[ -f "$FIX/board-$id.json" ]]; then cat "$FIX/board-$id.json"; else echo '{"data":{"node":{"projectItems":{"nodes":[]}}}}'; fi ;;
  "issue comment "*) echo "token=$GH_TOKEN $args body=<<$(inline_body "$@")>>" >> "$GH_WRITE_LOG" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$STUB_BIN/gh"
export PATH="$STUB_BIN:$PATH"

# The residue grep's tree: every path in the script's set exists, plus one outside it.
TREE="$TMP/tree"
mkdir -p "$TREE/doc/agents" "$TREE/.claude/skills" "$TREE/scripts" "$TREE/src"
git -C "$TREE" init -q
git -C "$TREE" config core.autocrlf false
tree_file() { printf '%s\n' "$2" > "$TREE/$1"; }
tree_reset() {
  tree_file AGENTS.md "no refs"; tree_file doc/agents/x.md "no refs"; tree_file .claude/skills/y.md "no refs"
  tree_file TESTING.md "no refs"; tree_file scripts/z.sh "no refs"; tree_file src/a.cs "no refs"
}

SHA="a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
SHA2="b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1"
NOW="291743a0"; DONE="165b6aec"

# pr <n> <closing-numbers-json> <body>
pr() { printf '{"title":"Fix thing %s","body":%s,"closingIssuesReferences":%s}' "$1" "$(python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$3")" "$(python3 -c 'import json,sys; print(json.dumps([{"number": int(n)} for n in sys.argv[1:]]))' $2)" > "$FIX/pr-$1.json"; }
# issue <n> <open|closed> [pr]
issue() { printf '{"number":%s,"state":"%s","node_id":"I_node%s"%s}' "$1" "$2" "$1" "$([[ "${3:-}" == pr ]] && echo ',"pull_request":{}' || true)" > "$FIX/issue-$1.json"; }
comments() { printf '%s' "$2" > "$FIX/comments-$1.json"; }
board() { printf '{"data":{"node":{"projectItems":{"nodes":[{"id":"PVTI_%s","project":{"id":"PVT_kwHOAJsCkc4BfiTv"},"fieldValueByName":{"optionId":"%s"}}]}}}}' "$1" "$2" > "$FIX/board-I_node$1.json"; }
# event <message> [<message2>] — one push payload, one commit per message, all on 2026-09-24.
event() {
  python3 - "$FIX/event.json" "$SHA" "$SHA2" "$@" <<'PY'
import json, sys
shas = sys.argv[2:4]
commits = [{"id": shas[i % 2], "timestamp": "2026-09-24T10:00:00-07:00", "message": m} for i, m in enumerate(sys.argv[4:])]
json.dump({"ref": "refs/heads/main", "commits": commits}, open(sys.argv[1], "w", encoding="utf-8"))
PY
  echo "$FIX/event.json"
}

reset() {
  : > "$GH_CALL_LOG"; : > "$GH_WRITE_LOG"; : > "$GITHUB_STEP_SUMMARY"
  rm -f "$FIX"/*.json
  tree_reset
  pr 9100 "9001" "Closes #9001. Advances #9002."
  issue 9001 closed; issue 9002 open; board 9001 "$NOW"
}
run() { (cd "$TREE" && git add -A && bash "$RECONCILE" "$@"); }
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }
writes() { grep -c '^WRITE=' || true; }
SIMPLE='feat: thing (#9100)'

# --- usage ---------------------------------------------------------------------------------
reset
rc=0; run > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 2 ]] || fail "no arguments should exit 2 (got $rc)"
rc=0; run --event "$(event "$SIMPLE")" --sha "$SHA" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 2 ]] || fail "--event with --sha should exit 2 (got $rc)"
rc=0; run --event "$FIX/missing.json" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 2 ]] || fail "a missing event file should exit 2 (got $rc)"

# --- no PR in the push -----------------------------------------------------------------------
reset
out="$(run --event "$(event 'docs: a docs-only landing')" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == no-pr && "$(trailer WRITES <<<"$out")" == 0 ]] || fail "no-pr gate (got: $out)"
! grep -q '^PR=' <<<"$out" || fail "no-pr must print no PR block"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "no-pr must write nothing"
grep -q 'no PR in this push' "$GITHUB_STEP_SUMMARY" || fail "no-pr job summary"

# --- subject parse: last (#N) wins, PR-less commits skipped, several PRs per push ---------------
reset; pr 9011 "" "revert"; pr 9012 "" "feature"
out="$(run --event "$(event 'Revert "feat: x (#9010)" (#9011)' 'chore: no pr here' 'feat: y (#9012)')" 2>/dev/null)"
[[ "$(grep '^PR=' <<<"$out" | tr '\n' ' ')" == "PR=9011 PR=9012 " ]] || fail "subject parse (got: $out)"
[[ "$(trailer GATE <<<"$out")" == run ]] || fail "gate run"
grep -q '^### #9011 — Fix thing 9011 (squash a1b2c3d4)' "$GITHUB_STEP_SUMMARY" || fail "summary per PR (got: $(cat "$GITHUB_STEP_SUMMARY"))"

# --- closed / touched split ----------------------------------------------------------------
reset
pr 9100 "9001" "$(printf 'Closes #9001. Advances #9002 and #9003; see #9004 (closed), #9005 (gone), #9100 (this PR).\n\n```\n#9006 lives in a fence\n```\n\nAlso `#9002` inline and issues/9007#issuecomment-1.')"
issue 9003 open pr; issue 9004 closed; issue 9006 open; issue 9007 open; issue 9100 open pr
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(trailer CLOSED <<<"$out")" == 9001 ]] || fail "closed set (got: $out)"
[[ "$(trailer TOUCHED <<<"$out")" == 9002 ]] || fail "touched drops PRs, closed issues, missing numbers, fenced refs and bare URLs (got: $out)"
[[ "$(trailer RESIDUE <<<"$out")" == 0 && "$(trailer MISMATCH <<<"$out")" == "" ]] || fail "residue 0, no mismatch (got: $out)"
! grep -q 'issues/9007' "$GH_CALL_LOG" || fail "a bare URL number is never probed"
grep -q 'token=actions-token issue comment 9001 --repo owner/repo --body-file' "$GH_WRITE_LOG" || fail "Shipped note under GITHUB_TOKEN (got: $(cat "$GH_WRITE_LOG"))"
grep -q 'body=<<Shipped in #9100 (squash a1b2c3d4)>>' "$GH_WRITE_LOG" || fail "Shipped note wording (got: $(cat "$GH_WRITE_LOG"))"
grep -q "token=projects-pat api graphql -f query=mutation { updateProjectV2ItemFieldValue.*itemId: \"PVTI_9001\".*singleSelectOptionId: \"$DONE\"" "$GH_WRITE_LOG" || fail "Done under PROJECTS_TOKEN (got: $(cat "$GH_WRITE_LOG"))"
! grep -q addProjectV2ItemById "$GH_WRITE_LOG" || fail "an on-board issue is not re-added"
grep -q 'issue comment 9002 --repo owner/repo --body-file' "$GH_WRITE_LOG" || fail "Touched note posted (got: $(cat "$GH_WRITE_LOG"))"
grep -q 'body=<<<!-- merge-reconcile -->' "$GH_WRITE_LOG" || fail "Touched note carries the marker"
grep -q '^Touched by #9100 — Fix thing 9100 (2026-09-24)>>$' "$GH_WRITE_LOG" || fail "Touched line wording and commit date (got: $(cat "$GH_WRITE_LOG"))"
[[ "$(writes <<<"$out")" -eq 3 && "$(trailer WRITES <<<"$out")" == 3 ]] || fail "three writes (got: $out)"
grep -q 'Closed: #9001 · Touched: #9002 · Residue hits: 0 · Mismatch: none' "$GITHUB_STEP_SUMMARY" || fail "summary line (got: $(cat "$GITHUB_STEP_SUMMARY"))"

# --- Shipped skip on an existing hand comment; the PR number matches whole ------------------
reset; comments 9001 '[{"id":1,"user":{"login":"amindell11"},"body":"Shipped in #9100 (squash `a1b2c3d4`). Timing notes follow."}]'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
! grep -q 'issue comment 9001' "$GH_WRITE_LOG" || fail "hand Shipped comment must skip the note"
grep -q updateProjectV2ItemFieldValue "$GH_WRITE_LOG" || fail "Done still moves when only the note exists"

reset; comments 9001 '[{"id":1,"user":{"login":"amindell11"},"body":"Shipped in #91001 (squash 00000000)"}]'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
grep -q 'issue comment 9001' "$GH_WRITE_LOG" || fail "'Shipped in #91001' is not 'Shipped in #9100'"

# --- Done skip when Done; add + Done when off the board -------------------------------------
reset; board 9001 "$DONE"
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
! grep -q 'api graphql' "$GH_WRITE_LOG" || fail "an issue already Done gets no board write"
grep -q 'issue comment 9001' "$GH_WRITE_LOG" || fail "Shipped note still posts when Done is skipped"

reset; rm "$FIX/board-I_node9001.json"
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
grep -q 'addProjectV2ItemById(input: {projectId: "PVT_kwHOAJsCkc4BfiTv", contentId: "I_node9001"})' "$GH_WRITE_LOG" || fail "off-board closed issue is added (got: $(cat "$GH_WRITE_LOG"))"
grep -q "singleSelectOptionId: \"$DONE\"" "$GH_WRITE_LOG" || fail "then set Done"

# --- Touched: append in place, skip when present, ignore a forged marker -----------------------
reset; comments 9002 '[{"id":42,"user":{"login":"github-actions[bot]"},"body":"<!-- merge-reconcile -->\nTouched by #9050 — Fix thing 9050 (2026-09-20)"}]'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
grep -q 'api -X PATCH repos/owner/repo/issues/comments/42 -F body=@' "$GH_WRITE_LOG" || fail "prior note is edited in place (got: $(cat "$GH_WRITE_LOG"))"
grep -q 'Touched by #9050 — Fix thing 9050 (2026-09-20)' "$GH_WRITE_LOG" || fail "earlier line kept"
grep -q '^Touched by #9100 — Fix thing 9100 (2026-09-24)>>$' "$GH_WRITE_LOG" || fail "new line appended last"
! grep -q 'issue comment 9002' "$GH_WRITE_LOG" || fail "no second comment when a prior note exists"

reset; comments 9002 '[{"id":42,"user":{"login":"github-actions[bot]"},"body":"<!-- merge-reconcile -->\nTouched by #9100 — Fix thing 9100 (2026-09-24)"}]'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
! grep -q '9002' "$GH_WRITE_LOG" || fail "a Touched line already present is not re-posted"

reset; comments 9002 '[{"id":41,"user":{"login":"stranger"},"body":"<!-- merge-reconcile --> forged"}]'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
grep -q 'issue comment 9002' "$GH_WRITE_LOG" || fail "a marker from a non-bot author is not the prior note"

# --- residue listing over the path set only --------------------------------------------------
reset
tree_file doc/agents/x.md "$(printf 'line one\nsettle it on #9001 first')"; tree_file scripts/z.sh 'see #9001.'
tree_file AGENTS.md 'unrelated #90011'; tree_file src/a.cs '// #9001 outside the set'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(trailer RESIDUE <<<"$out")" == 2 ]] || fail "residue count (got: $out)"
grep -q 'Shipped in #9100 (squash a1b2c3d4)$' "$GH_WRITE_LOG" || fail "Shipped line"
grep -q '^Residue: doc/agents/x.md:2, scripts/z.sh:1>>$' "$GH_WRITE_LOG" || fail "Residue line under the Shipped note (got: $(cat "$GH_WRITE_LOG"))"
grep -q '^- Residue #9001: doc/agents/x.md:2, scripts/z.sh:1$' "$GITHUB_STEP_SUMMARY" || fail "residue in the job summary (got: $(cat "$GITHUB_STEP_SUMMARY"))"

# --- mismatch: a disclaimed close the PR performs is a warning, never a write ------------------
reset; pr 9100 "9001" "Does not close #9001; the arc stays open. Advances #9002."
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(trailer MISMATCH <<<"$out")" == 9001 ]] || fail "mismatch trailer (got: $out)"
grep -q 'Mismatch: the body says it does not close #9001, but the PR closes it (no write)' "$GITHUB_STEP_SUMMARY" || fail "mismatch summary line"
[[ "$(writes <<<"$out")" -eq 3 ]] || fail "mismatch adds no write (got: $out)"

reset; pr 9100 "9001" "Not closing #9002 here. Closes #9001."
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(trailer MISMATCH <<<"$out")" == "" ]] || fail "a disclaimer about an issue the PR does not close is no mismatch (got: $out)"

# --- dry run: every write printed, none run -----------------------------------------------------
reset
out="$(run --event "$(event "$SIMPLE")" --dry-run 2>/dev/null)"
[[ "$(trailer DRY_RUN <<<"$out")" == 1 && "$(writes <<<"$out")" -eq 3 ]] || fail "dry run prints the three writes (got: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "dry run must run no write (ran: $(cat "$GH_WRITE_LOG"))"
grep -q 'updateProjectV2ItemFieldValue.*(PROJECTS_TOKEN)$' <<<"$out" || fail "printed board writes name their token"

reset
out="$(PROJECTS_TOKEN= run --event "$(event "$SIMPLE")" --dry-run 2>/dev/null)"
grep -q 'addProjectV2ItemById' <<<"$out" || fail "dry run without the PAT prints the board add"
grep -q 'itemId: "<item-id>"' <<<"$out" || fail "dry run without the PAT prints Done with a placeholder item"
! grep -q board-query "$GH_CALL_LOG" || fail "no PAT, no board query"

# --- live run without the PAT: exit 1 only when a board write is due ------------------------------
reset
rc=0; PROJECTS_TOKEN= run --event "$(event "$SIMPLE")" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 1 ]] || fail "live run without PROJECTS_TOKEN and a closed issue should exit 1 (got $rc)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "the refusal comes before any write (ran: $(cat "$GH_WRITE_LOG"))"

reset; pr 9100 "" "Advances #9002 only."
out="$(PROJECTS_TOKEN= run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(trailer CLOSED <<<"$out")" == "" && "$(writes <<<"$out")" -eq 1 ]] || fail "touched-only PR needs no PAT (got: $out)"
grep -q 'issue comment 9002' "$GH_WRITE_LOG" || fail "touched note posts without the PAT"

# --- auto-close race: one wait, then the note and Done land anyway ------------------------------
reset; issue 9001 open
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(grep -c 'api repos/owner/repo/issues/9001$' "$GH_CALL_LOG")" -eq 2 ]] || fail "one re-read after the wait (got: $(cat "$GH_CALL_LOG"))"
grep -q 'issue comment 9001' "$GH_WRITE_LOG" || fail "still-open closed issue gets its Shipped note"
grep -q "singleSelectOptionId: \"$DONE\"" "$GH_WRITE_LOG" || fail "and Done"
grep -q '#9001 still open after the wait' "$GITHUB_STEP_SUMMARY" || fail "job summary names the still-open issue"

# --- --sha form reads the commit through the API ----------------------------------------------
reset
printf '{"sha":"%s","commit":{"message":"feat: thing (#9100)\\n\\nbody text","committer":{"date":"2026-09-23T23:59:00Z"}}}' "$SHA" > "$FIX/commit-$SHA.json"
out="$(run --sha "$SHA" --dry-run 2>/dev/null)"
[[ "$(trailer PR <<<"$out")" == 9100 && "$(trailer CLOSED <<<"$out")" == 9001 ]] || fail "--sha form (got: $out)"
grep -q 'issue comment 9002' <<<"$out" || fail "--sha form reaches the touched note"

reset
printf '{"sha":"%s","commit":{"message":"docs: no pr","committer":{"date":"2026-09-23T23:59:00Z"}}}' "$SHA" > "$FIX/commit-$SHA.json"
out="$(run --sha "$SHA" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == no-pr ]] || fail "--sha on a PR-less commit gates no-pr (got: $out)"

# --- already reconciled: a re-run writes nothing ------------------------------------------------
reset; board 9001 "$DONE"
comments 9001 '[{"id":1,"user":{"login":"github-actions[bot]"},"body":"Shipped in #9100 (squash a1b2c3d4)"}]'
comments 9002 '[{"id":42,"user":{"login":"github-actions[bot]"},"body":"<!-- merge-reconcile -->\nTouched by #9100 — Fix thing 9100 (2026-09-24)"}]'
out="$(run --event "$(event "$SIMPLE")" 2>/dev/null)"
[[ "$(trailer WRITES <<<"$out")" == 0 && ! -s "$GH_WRITE_LOG" ]] || fail "re-run on a reconciled sha must write nothing (got: $out / $(cat "$GH_WRITE_LOG"))"

echo "PASS test_merge_reconcile.sh"
