#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/on_event_triage.sh scripts/on_event_triage_helper.py

# Hermetic regression for scripts/on_event_triage.sh: the allowlist split, the edit gate, the
# one-priority rule, needs-triage on a priority-less issue, the board Status (Triage, then the
# kind only from no Status or Triage), note emission (new / edited in place / resolved / none),
# --dry-run issuing zero writes, the verdict parse, and infra exits on a bad claude output.
# gh and claude are stubs on PATH; every call the stub does not model fails closed.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TRIAGE="$SCRIPT_DIR/../on_event_triage.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_CALL_LOG="$TMP/gh-calls.log"
export GH_WRITE_LOG="$TMP/gh-writes.log"
export CLAUDE_CALL_LOG="$TMP/claude-calls.log"
export CLAUDE_PROMPT_CAPTURE="$TMP/claude-prompt.md"
export CLAUDE_OUTPUT="$TMP/claude-output.json"
export CLAUDE_EXIT_FILE="$TMP/claude.exit"
export GITHUB_REPOSITORY="owner/repo"
export GITHUB_STEP_SUMMARY="$TMP/summary.md"
export PROJECTS_TOKEN="projects-pat"
export GH_TOKEN="actions-token"
export TRIAGE_RETRY_SLEEP=0
mkdir -p "$FIX"

export STUB_BIN="$TMP/bin"
mkdir -p "$STUB_BIN"

# Reads answer from fixtures; writes are logged with the token they ran under and any body file
# inlined, so a test can assert on the comment text.
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
# Like gh, --paginate --slurp answers with one array per page; the fixture is page one, page two is empty.
pages() { printf '[%s,[]]' "$(cat "$1")"; }
case "$args" in
  "issue view "*) cat "$FIX/issue.json" ;;
  "issue list "*"--state closed"*) cat "$FIX/closed.json" ;;
  "api repos/"*"/events --paginate --slurp") pages "$FIX/events.json" ;;
  "api -X PATCH repos/"*"/comments/"*) echo "token=$GH_TOKEN $args body=<<$(inline_body "$@")>>" >> "$GH_WRITE_LOG" ;;
  "api repos/"*"/comments --paginate --slurp") pages "$FIX/comments.json" ;;
  # An add puts the issue on the board with no Status, as GitHub does.
  "api graphql "*"mutation "*) echo "token=$GH_TOKEN $args" >> "$GH_WRITE_LOG"; echo '{"data":{}}'
    [[ "$args" != *addProjectV2ItemById* ]] || echo '{"data":{"node":{"projectItems":{"nodes":[{"id":"PVTI_added","project":{"id":"PVT_kwHOAJsCkc4BfiTv"},"fieldValueByName":null}]}}}}' > "$FIX/board.json" ;;
  "api graphql "*"projectItems"*) echo "token=$GH_TOKEN board-query" >> "$GH_CALL_LOG"; cat "$FIX/board.json" ;;
  "issue edit "*) echo "token=$GH_TOKEN $args" >> "$GH_WRITE_LOG" ;;
  "issue comment "*) echo "token=$GH_TOKEN $args body=<<$(inline_body "$@")>>" >> "$GH_WRITE_LOG" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$STUB_BIN/gh"

cat > "$STUB_BIN/claude" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CLAUDE_CALL_LOG"
cat > "$CLAUDE_PROMPT_CAPTURE"
# The owner moving the item while claude runs.
[[ ! -f "$FIX/board-after-claude.json" ]] || mv "$FIX/board-after-claude.json" "$FIX/board.json"
cat "$CLAUDE_OUTPUT"
exit "$(cat "$CLAUDE_EXIT_FILE")"
EOF
chmod +x "$STUB_BIN/claude"
export PATH="$STUB_BIN:$PATH"

TODAY="$(date -u +%F)"
TRIAGE_ID="d6567434"; MECHANICAL_ID="225f15fa"; NOW_ID="291743a0"
# board_at <option-id> — the board query's answer for an item in that Status column.
board_at() { printf '{"data":{"node":{"projectItems":{"nodes":[{"id":"PVTI_item","project":{"id":"PVT_kwHOAJsCkc4BfiTv"},"fieldValueByName":{"optionId":"%s"}}]}}}}' "$1"; }
BOARD_OFF='{"data":{"node":{"projectItems":{"nodes":[]}}}}'

# issue <author> <labels-json> [assignees-json] [state] — the fixture gh issue view returns.
issue() {
  cat > "$FIX/issue.json" <<JSON
{"number": 700, "title": "Capture rig mirrors X", "state": "${4:-OPEN}", "id": "I_node700",
 "author": {"login": "$1"}, "labels": $2, "assignees": ${3:-[]},
 "body": "The rig in scripts/capture/assemble.py mirrors X. See doc/agents/unity-cli.md."}
JSON
}
event() { printf '%s' "$1" > "$FIX/event.json"; echo "$FIX/event.json"; }
verdict() { printf '%s' "$1" > "$CLAUDE_OUTPUT"; }
SURE='"kind":"mechanical","kind_confident":true,"kind_reason":"the body names the fix"'
UNSURE='"kind":"visual","kind_confident":false,"kind_reason":"a camera tweak or a design call"'
CLEAN='{"type":"result","subtype":"success","is_error":false,"usage":{"input_tokens":1200,"output_tokens":80,"cache_creation_input_tokens":300,"cache_read_input_tokens":500},"total_cost_usd":0.0421,"structured_output":{"retry_of":null,"retry_evidence":"none","premise_holds":true,"premise_evidence":"scripts/capture/assemble.py:12","dead_pointers":[],'"$SURE"'}}'
FINDINGS='{"type":"result","subtype":"success","is_error":false,"usage":{"input_tokens":1500,"output_tokens":120},"total_cost_usd":0.05,"structured_output":{"retry_of":632,"retry_evidence":"Capture rig X-mirror deferred","premise_holds":false,"premise_evidence":"scripts/capture/assemble.py:40 flips Y, not X","dead_pointers":[{"path":"doc/agents/unity-cli.md","replacement":null},{"path":"doc/Feature_Plans/capture.md","replacement":"#520"}],'"$SURE"'}}'
unsure() { printf '%s' "${1/"$SURE"/"$UNSURE"}"; }

reset() {
  : > "$GH_CALL_LOG"; : > "$GH_WRITE_LOG"; : > "$CLAUDE_CALL_LOG"; : > "$GITHUB_STEP_SUMMARY"
  rm -f "$CLAUDE_PROMPT_CAPTURE" "$FIX/board-after-claude.json"
  echo 0 > "$CLAUDE_EXIT_FILE"
  echo '[{"number":632,"title":"Capture rig X-mirror deferred","closedAt":"2026-09-21T10:00:00Z"}]' > "$FIX/closed.json"
  echo '[]' > "$FIX/events.json"
  echo '[]' > "$FIX/comments.json"
  board_at "$NOW_ID" > "$FIX/board.json"
  verdict "$CLEAN"
}
run() { bash "$TRIAGE" "$@"; }
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }
writes() { grep -c '^WRITE=' || true; }
claude_ran() { [[ -s "$CLAUDE_CALL_LOG" ]]; }

OPENED='{"action":"opened","issue":{"number":700}}'

# --- usage ---------------------------------------------------------------------------------
reset
rc=0; run > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 2 ]] || fail "no --event should exit 2 (got $rc)"

# --- closed issue exits early with no writes --------------------------------------------------
reset; issue amindell11 '[{"name":"pri:now"}]' '[]' CLOSED
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == closed ]] || fail "closed issue should gate closed (got: $out)"
[[ "$(writes <<<"$out")" -eq 0 && ! -s "$GH_WRITE_LOG" ]] || fail "closed issue should write nothing"
! claude_ran || fail "closed issue should never reach claude"

# --- allowlist split ------------------------------------------------------------------------
reset; issue stranger '[]'; echo "$BOARD_OFF" > "$FIX/board.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == non-allowlisted ]] || fail "stranger should gate non-allowlisted (got: $out)"
! claude_ran || fail "a non-allowlisted issue must never reach claude"
grep -q 'token=actions-token issue edit 700 --repo owner/repo --add-label needs-triage' "$GH_WRITE_LOG" || fail "stranger: needs-triage under GITHUB_TOKEN (got: $(cat "$GH_WRITE_LOG"))"
grep -q 'token=projects-pat api graphql -f query=mutation { addProjectV2ItemById' "$GH_WRITE_LOG" || fail "stranger: board add under PROJECTS_TOKEN"
grep -q 'singleSelectOptionId: "d6567434"' "$GH_WRITE_LOG" || fail "stranger: Status Triage"
[[ "$(writes <<<"$out")" -eq 3 ]] || fail "stranger: exactly three writes (got: $out)"
[[ "$(trailer VERDICT <<<"$out")" == none ]] || fail "stranger: verdict none"
grep -q '^## On-event triage #700' "$GITHUB_STEP_SUMMARY" || fail "job summary missing"

# --- allowlisted, no pri label -> needs-triage; claude runs on opened ----------------------------
reset; issue amindell11 '[{"name":"Capture"}]'
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == run ]] || fail "opened should run (got: $out)"
grep -q -- '--add-label needs-triage' "$GH_WRITE_LOG" || fail "priority-less issue gets needs-triage"
claude_ran || fail "opened allowlisted issue should reach claude"
grep -q -- '-p --tools Read,Grep,Glob --json-schema ' "$CLAUDE_CALL_LOG" || fail "claude surface is read-only (got: $(cat "$CLAUDE_CALL_LOG"))"
grep -q -- '--output-format json --max-turns 12 --model claude-opus-5-5' "$CLAUDE_CALL_LOG" || fail "claude flags (got: $(cat "$CLAUDE_CALL_LOG"))"
grep -q '^<issue-body number=700>' "$CLAUDE_PROMPT_CAPTURE" || fail "packet delimits the body"
grep -q '^#632 · Capture rig X-mirror deferred · 2026-09-21$' "$CLAUDE_PROMPT_CAPTURE" || fail "packet carries the closed list"
grep -q '^# On-event triage' "$CLAUDE_PROMPT_CAPTURE" || fail "packet starts with on-event.md"
[[ "$(trailer VERDICT <<<"$out")" == clean ]] || fail "clean verdict (got: $out)"
grep -q 'Verdict fields: `{"retry_of": null' "$GITHUB_STEP_SUMMARY" || fail "job summary carries the verdict fields"
[[ "$(trailer TOKENS_IN <<<"$out")" == 1200 && "$(trailer TOKENS_OUT <<<"$out")" == 80 && "$(trailer TOKENS_CACHE <<<"$out")" == 800 ]] || fail "usage trailers (got: $out)"
[[ "$(trailer COST_USD <<<"$out")" == 0.0421 ]] || fail "cost trailer"
! grep -q 'issue comment' "$GH_WRITE_LOG" || fail "clean verdict with no prior note posts nothing"

# --- board kind: Triage when there is no Status; the kind only from no Status or Triage ---------
board_writes() { { grep -o 'addProjectV2ItemById\|singleSelectOptionId: "[0-9a-f]*"' "$GH_WRITE_LOG" || true; } | sed 's/singleSelectOptionId: //; s/"//g' | tr '\n' ' '; }
reset; issue amindell11 '[{"name":"pri:now"}]'; echo "$BOARD_OFF" > "$FIX/board.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(board_writes)" == "addProjectV2ItemById $TRIAGE_ID $MECHANICAL_ID " ]] || fail "off the board: add, Triage, then the confident kind (got: $(cat "$GH_WRITE_LOG"))"
[[ "$(grep -c 'token=projects-pat .*itemId: "PVTI_added"' "$GH_WRITE_LOG")" -eq 2 ]] || fail "both Status writes land on the added item under PROJECTS_TOKEN"

reset; issue amindell11 '[{"name":"pri:now"}]'; board_at "$TRIAGE_ID" > "$FIX/board.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(board_writes)" == "$MECHANICAL_ID " && "$(writes <<<"$out")" -eq 1 ]] || fail "in Triage: the kind is the only write (got: $out)"
[[ "$(grep -c board-query "$GH_CALL_LOG")" -eq 2 ]] || fail "a live run re-reads the board before the kind write"

reset; issue amindell11 '[{"name":"pri:now"}]'
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(writes <<<"$out")" -eq 0 ]] || fail "an item in an owner column (Now) gets no Status write (got: $out)"

reset; issue amindell11 '[{"name":"pri:now"}]'; board_at "$TRIAGE_ID" > "$FIX/board.json"; board_at "$NOW_ID" > "$FIX/board-after-claude.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(writes <<<"$out")" -eq 0 ]] || fail "an item the owner moved while claude ran keeps its Status (got: $out)"

reset; issue amindell11 '[{"name":"pri:now"}]'; board_at "$TRIAGE_ID" > "$FIX/board.json"; verdict "$(unsure "$CLEAN")"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ -z "$(board_writes)" ]] || fail "an unsure kind is not written (got: $(cat "$GH_WRITE_LOG"))"
[[ "$(trailer VERDICT <<<"$out")" == findings && "$(writes <<<"$out")" -eq 1 ]] || fail "an unsure kind alone posts the note (got: $out)"
grep -q '^Kind unsure: best guess Visual — a camera tweak or a design call>>$' "$GH_WRITE_LOG" || fail "the note carries the best guess and the reason (got: $(cat "$GH_WRITE_LOG"))"

# --- one-priority rule: keep the pri:* added last ------------------------------------------
reset; issue amindell11 '[{"name":"pri:now"},{"name":"pri:next"},{"name":"pri:later"}]'
echo '[{"event":"labeled","label":{"name":"pri:now"},"created_at":"2026-09-01T00:00:00Z"},{"event":"labeled","label":{"name":"pri:later"},"created_at":"2026-09-10T00:00:00Z"},{"event":"labeled","label":{"name":"pri:next"},"created_at":"2026-09-05T00:00:00Z"},{"event":"unlabeled","label":{"name":"pri:later"},"created_at":"2026-09-11T00:00:00Z"}]' > "$FIX/events.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
grep -q -- '--remove-label pri:now' "$GH_WRITE_LOG" || fail "one-priority: pri:now removed"
grep -q -- '--remove-label pri:next' "$GH_WRITE_LOG" || fail "one-priority: pri:next removed"
! grep -q -- '--remove-label pri:later' "$GH_WRITE_LOG" || fail "one-priority: pri:later (added last) kept"

# --- edit gate ---------------------------------------------------------------------------------
reset; issue amindell11 '[{"name":"pri:now"}]'
ev="$(event '{"action":"edited","issue":{"number":700},"changes":{"body":{"from":"The rig in scripts/capture/assemble.py mirrors X. See doc/agents/unity-cli.md (typo)."}}}')"
out="$(run --event "$ev" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == edit-skipped ]] || fail "body edit with no new path should skip (got: $out)"
! claude_ran || fail "edit-skipped must not reach claude"

reset; issue amindell11 '[{"name":"pri:now"}]'
ev="$(event '{"action":"edited","issue":{"number":700},"changes":{"body":{"from":"The rig mirrors X."}}}')"
out="$(run --event "$ev" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == run ]] || fail "body edit naming a new path should run (got: $out)"
claude_ran || fail "new path token must reach claude"

reset; issue amindell11 '[{"name":"pri:now"}]'
ev="$(event '{"action":"edited","issue":{"number":700},"changes":{"title":{"from":"Old title"}}}')"
out="$(run --event "$ev" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == run ]] || fail "title edit should run (got: $out)"

reset; issue amindell11 '[{"name":"pri:now"}]'
ev="$(event '{"action":"edited","issue":{"number":700},"changes":{"body":{"from":null}}}')"
out="$(run --event "$ev" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == run ]] || fail "a first body on a bodyless issue is compared against empty (got: $out)"

for dotted in '.github/workflows/on-event-triage.yml' '.claude/skills/issue-triage/SKILL.md' './scripts/capture/assemble.py'; do
  reset; issue amindell11 '[{"name":"pri:now"}]'
  python3 - "$FIX/issue.json" "$dotted" <<'PY'
import json, sys
p = sys.argv[1]; d = json.load(open(p, encoding="utf-8")); d["body"] = f"Now also see {sys.argv[2]} for the rig."; json.dump(d, open(p, "w", encoding="utf-8"))
PY
  ev="$(event '{"action":"edited","issue":{"number":700},"changes":{"body":{"from":"Now also see nothing for the rig."}}}')"
  out="$(run --event "$ev" 2>/dev/null)"
  [[ "$(trailer GATE <<<"$out")" == run ]] || fail "dot-rooted path $dotted should open the gate (got: $out)"
done

reset; issue stranger '[{"name":"needs-triage"}]'
ev="$(event '{"action":"edited","issue":{"number":700},"changes":{"title":{"from":"Old title"}}}')"
out="$(run --event "$ev" 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == non-allowlisted ]] || fail "allowlist runs before the edit gate"
! claude_ran || fail "stranger title edit must not reach claude"

# --- findings: new note, assigned line, in-place edit, resolved rewrite ----------------------
reset; issue amindell11 '[{"name":"pri:now"}]' '[{"login":"amindell11"}]'; verdict "$(unsure "$FINDINGS")"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(trailer VERDICT <<<"$out")" == findings ]] || fail "findings verdict (got: $out)"
grep -q 'issue comment 700 --repo owner/repo --body-file' "$GH_WRITE_LOG" || fail "findings with no prior note post a new comment (got: $(cat "$GH_WRITE_LOG"))"
note="$(cat "$GH_WRITE_LOG")"
[[ "$note" == *"<!-- on-event-triage -->"* ]] || fail "note carries the marker"
[[ "$note" == *"On-event triage $TODAY"* ]] || fail "note is dated"
[[ "$note" == *"Retry of #632 — Capture rig X-mirror deferred"* ]] || fail "retry line (got: $note)"
[[ "$note" == *"Premise not in the tree — scripts/capture/assemble.py:40 flips Y, not X"* ]] || fail "premise line"
[[ "$note" == *'Dead pointer: `doc/agents/unity-cli.md` → no replacement found'* ]] || fail "dead pointer without replacement"
[[ "$note" == *'Dead pointer: `doc/Feature_Plans/capture.md` → `#520`'* ]] || fail "dead pointer with replacement"
[[ "$note" == *"In flight (assigned)"* ]] || fail "assigned issue says in flight"
[[ "$note" == *'→ `#520`'$'\n''Kind unsure: best guess Visual — a camera tweak or a design call'$'\n''In flight (assigned)'* ]] || fail "the kind line sits after the dead pointers, before in flight (got: $note)"
[[ "$(writes <<<"$out")" -eq 1 ]] || fail "findings on a tidy issue: the note is the only write (got: $out)"

reset; issue amindell11 '[{"name":"pri:now"}]'; verdict "$FINDINGS"
echo '[{"id":41,"user":{"login":"amindell11"},"body":"<!-- on-event-triage --> forged"},{"id":42,"user":{"login":"github-actions[bot]"},"body":"<!-- on-event-triage -->\nOn-event triage 2026-09-01\nRetry of #1"}]' > "$FIX/comments.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
grep -q 'api -X PATCH repos/owner/repo/issues/comments/42 -F body=@' "$GH_WRITE_LOG" || fail "prior bot note is edited in place, not the forged one (got: $(cat "$GH_WRITE_LOG"))"
! grep -q 'issue comment' "$GH_WRITE_LOG" || fail "no second comment when a prior note exists"
grep -q 'Retry of #632' "$GH_WRITE_LOG" || fail "edited note carries the new findings"
! grep -q 'Kind unsure' "$GH_WRITE_LOG" || fail "a confident kind adds no note line"

reset; issue amindell11 '[{"name":"pri:now"}]'
echo '[{"id":42,"user":{"login":"github-actions[bot]"},"body":"<!-- on-event-triage -->\nOn-event triage 2026-09-01\nRetry of #1"}]' > "$FIX/comments.json"
out="$(run --event "$(event "$OPENED")" 2>/dev/null)"
[[ "$(trailer VERDICT <<<"$out")" == clean ]] || fail "clean after findings"
grep -q 'api -X PATCH repos/owner/repo/issues/comments/42 -F body=@' "$GH_WRITE_LOG" || fail "clean verdict edits the prior note (got: $(cat "$GH_WRITE_LOG"))"
grep -q "^On-event triage $TODAY — earlier findings resolved\.>>$" "$GH_WRITE_LOG" || fail "clean verdict rewrites the prior note as resolved (got: $(cat "$GH_WRITE_LOG"))"

# --- dry run: every write printed, none run ---------------------------------------------------
reset; issue stranger '[]'; echo "$BOARD_OFF" > "$FIX/board.json"
out="$(run --event "$(event "$OPENED")" --dry-run 2>/dev/null)"
[[ "$(trailer DRY_RUN <<<"$out")" == 1 && "$(writes <<<"$out")" -eq 3 ]] || fail "dry run prints the three writes (got: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "dry run must run no write (ran: $(cat "$GH_WRITE_LOG"))"
grep -q '(PROJECTS_TOKEN)' <<<"$out" || fail "printed board writes name their token"

reset; issue amindell11 '[{"name":"pri:now"},{"name":"pri:next"}]' '[]'; verdict "$FINDINGS"; board_at "$TRIAGE_ID" > "$FIX/board.json"
echo '[{"event":"labeled","label":{"name":"pri:now"},"created_at":"2026-09-01T00:00:00Z"},{"event":"labeled","label":{"name":"pri:next"},"created_at":"2026-09-05T00:00:00Z"}]' > "$FIX/events.json"
out="$(run --event "$(event "$OPENED")" --dry-run 2>/dev/null)"
[[ "$(trailer GATE <<<"$out")" == run && "$(trailer VERDICT <<<"$out")" == findings ]] || fail "dry run still runs claude (got: $out)"
[[ "$(writes <<<"$out")" -eq 3 ]] || fail "dry run: remove pri:now, the kind, note (got: $out)"
grep -q "itemId: \"PVTI_item\".*singleSelectOptionId: \"$MECHANICAL_ID\"" <<<"$out" || fail "dry run prints the kind write (got: $out)"
[[ "$(grep -c board-query "$GH_CALL_LOG")" -eq 1 ]] || fail "a dry run does not re-read the board"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "dry run with findings must run no write (ran: $(cat "$GH_WRITE_LOG"))"
grep -q 'Writes (3)' "$GITHUB_STEP_SUMMARY" || fail "job summary counts printed writes"

# --- dry run without PROJECTS_TOKEN prints the board writes; a live run refuses --------------
reset; issue amindell11 '[{"name":"pri:now"}]'
out="$(PROJECTS_TOKEN= run --event "$(event "$OPENED")" --dry-run 2>/dev/null)"
grep -q 'addProjectV2ItemById' <<<"$out" || fail "dry run without the PAT prints the board add"
grep -q "itemId: \"<item-id>\".*singleSelectOptionId: \"$MECHANICAL_ID\"" <<<"$out" || fail "dry run without the PAT prints the kind with a placeholder item (got: $out)"
! grep -q board-query "$GH_CALL_LOG" || fail "no PAT, no board query"
rc=0; PROJECTS_TOKEN= run --event "$(event "$OPENED")" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 1 ]] || fail "live run without PROJECTS_TOKEN should exit 1 (got $rc)"

# --- bad claude output exits 1 and writes no note ----------------------------------------------
NO_KIND='{"type":"result","is_error":false,"structured_output":{"retry_of":null,"retry_evidence":"none","premise_holds":true,"premise_evidence":"x","dead_pointers":[]}}'
for bad in 'not json' '{"type":"result","subtype":"success","is_error":true,"result":"Failed to authenticate"}' '{"type":"result","is_error":false,"structured_output":{"retry_of":"x"}}' "$NO_KIND" "${CLEAN/mechanical/design}"; do
  reset; issue amindell11 '[{"name":"pri:now"}]'; verdict "$bad"
  rc=0; run --event "$(event "$OPENED")" > /dev/null 2>&1 || rc=$?
  [[ "$rc" -eq 1 ]] || fail "claude output '$bad' should exit 1 (got $rc)"
  ! grep -q 'comment' "$GH_WRITE_LOG" || fail "bad claude output must write no note"
done
reset; issue amindell11 '[{"name":"pri:now"}]'; echo 3 > "$CLAUDE_EXIT_FILE"
rc=0; run --event "$(event "$OPENED")" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 1 ]] || fail "claude exiting non-zero should exit 1 (got $rc)"

# --- workflow_dispatch payload is triaged like opened ------------------------------------------
reset; issue amindell11 '[{"name":"pri:now"}]'
out="$(run --event "$(event '{"inputs":{"issue_number":"700","dry_run":"true"}}')" --dry-run 2>/dev/null)"
[[ "$(trailer ISSUE <<<"$out")" == 700 && "$(trailer GATE <<<"$out")" == run ]] || fail "dispatch payload (got: $out)"

echo "PASS test_on_event_triage.sh"
