#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/drain_pick.sh

# Hermetic regression for scripts/drain_pick.sh: the pick filter (unity label, assignee, open
# blocker, scope block, proposal author), priority-then-age order, a claimed issue's three
# readings by its closing PRs (unfinished, building, pr-closed), claim's assignee re-read and
# single write, release, and every owed verdict. gh is a stub; every call it does not model
# fails closed.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRAIN="$SCRIPT_DIR/../drain_pick.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_WRITE_LOG="$TMP/gh-writes.log"
export GITHUB_REPOSITORY="owner/repo"
mkdir -p "$FIX" "$TMP/bin"

cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
case "$args" in
  "api graphql "*) cat "$FIX/queue.json" ;;
  "issue view "*"--json assignees"*) cat "$FIX/assignees.txt" ;;
  "issue edit "*) echo "$args" >> "$GH_WRITE_LOG" ;;
  "pr view "*"--json body"*) cat "$FIX/body.md" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh"
export PATH="$TMP/bin:$PATH"

# issue <number> <createdAt> <labels csv> [assignee] [blockedBy] [body] [comments-json] [closing PRs: <n>:<STATE>,…]
ISSUES=()
issue() {
  ISSUES+=("$(python3 - "$@" <<'PY'
import json, sys
a = sys.argv[1:] + [""] * 8
number, created, labels, assignee, blocked, body, comments, prs = a[:8]
print(json.dumps({"number": int(number), "createdAt": created, "body": body,
  "labels": {"nodes": [{"name": l} for l in labels.split(",") if l]},
  "assignees": {"nodes": [{"login": assignee}] if assignee else []},
  "issueDependenciesSummary": {"blockedBy": int(blocked or 0)},
  "closedByPullRequestsReferences": {"nodes": [
    {"number": int(p.split(":")[0]), "state": p.split(":")[1]} for p in prs.split(",") if p]},
  "comments": {"nodes": json.loads(comments or "[]")}}))
PY
)")
}
write_queue() {
  local IFS=,
  printf '{"data":{"repository":{"issues":{"nodes":[%s]}}}}' "${ISSUES[*]:-}" > "$FIX/queue.json"
}
SLICE_BODY=$'## What to build\nThe thing.\n\n## Acceptance criteria\n- it works'
proposal() { printf '[{"author":{"login":"%s"},"url":"https://x/c/%s","body":"Ready proposal 2026-09-26\\nScope: s"}]' "$1" "$2"; }

reset() {
  ISSUES=()
  : > "$GH_WRITE_LOG"
  : > "$FIX/assignees.txt"
}
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }
skip_of() { grep "^SKIP=$1 " | cut -d' ' -f2-; }

# --- usage -----------------------------------------------------------------------------------
reset
for bad in "" "frob" "pick --force" "claim" "claim abc" "claim 12 lease" "release" "release abc" "owed" "owed abc"; do
  rc=0; bash "$DRAIN" $bad > /dev/null 2>&1 || rc=$?
  [[ "$rc" -eq 2 ]] || fail "'$bad' should exit 2 (got $rc)"
done

# --- pick: the filter ------------------------------------------------------------------------
reset
issue 10 2026-09-01T00:00:00Z ready-for-agent "" 0 "$SLICE_BODY"
issue 11 2026-09-02T00:00:00Z ready-for-agent,unity:editor "" 0 "$SLICE_BODY"
issue 12 2026-09-03T00:00:00Z ready-for-agent,unity:none someone 0 "$SLICE_BODY"
issue 13 2026-09-04T00:00:00Z ready-for-agent,unity:none "" 2 "$SLICE_BODY"
issue 14 2026-09-05T00:00:00Z ready-for-agent,unity:none "" 0 $'## What to build\nno acceptance heading'
issue 15 2026-09-06T00:00:00Z ready-for-agent,unity:none "" 0 "no scope" "$(proposal stranger 15)"
issue 16 2026-09-07T00:00:00Z ready-for-agent,unity:none "" 0 "no scope" "$(proposal amindell11 16)"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(skip_of 10 <<<"$out")" == no-unity-label ]] || fail "no unity label is skipped (got: $out)"
[[ "$(skip_of 11 <<<"$out")" == unity:editor ]] || fail "unity:editor is skipped by label (got: $out)"
[[ "$(skip_of 12 <<<"$out")" == assigned:someone ]] || fail "assigned is skipped (got: $out)"
[[ "$(skip_of 13 <<<"$out")" == blocked:2 ]] || fail "open blocker is skipped (got: $out)"
[[ "$(skip_of 14 <<<"$out")" == no-scope-block ]] || fail "What to build without acceptance is no scope block (got: $out)"
[[ "$(skip_of 15 <<<"$out")" == no-scope-block ]] || fail "a stranger's proposal is no scope block (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == 16 ]] || fail "the one eligible issue is picked (got: $out)"
[[ "$(trailer SCOPE <<<"$out")" == proposal:https://x/c/16 ]] || fail "SCOPE names the proposal comment (got: $out)"
[[ "$(trailer DRY_RUN <<<"$out")" == 0 ]] || fail "DRY_RUN=0 without the flag"

reset
issue 20 2026-09-01T00:00:00Z ready-for-agent,unity:editor,pri:now someone 1
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(skip_of 20 <<<"$out")" == unity:editor,assigned:someone,blocked:1,no-scope-block ]] || fail "every reason is listed (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == none ]] || fail "nothing eligible picks none (got: $out)"
! grep -q '^SCOPE=' <<<"$out" || fail "no SCOPE when nothing is picked"

# --- pick: one admission rule over the four unity labels -------------------------------------
reset
issue 50 2026-09-04T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 51 2026-09-01T00:00:00Z ready-for-agent,unity:editor,drain:approved "" 0 "$SLICE_BODY"
issue 52 2026-09-03T00:00:00Z ready-for-agent,unity:headless "" 0 "$SLICE_BODY"
issue 53 2026-09-02T00:00:00Z ready-for-agent,unity:local-proof "" 0 "$SLICE_BODY"
issue 54 2026-09-01T00:00:00Z ready-for-agent,unity:headless,unity:editor "" 0 "$SLICE_BODY"
issue 55 2026-09-01T00:00:00Z ready-for-agent,unity:none,unity:local-proof "" 0 "$SLICE_BODY"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(skip_of 51 <<<"$out")" == unity:editor ]] || fail "editor is never admitted, whatever else it carries (got: $out)"
[[ "$(skip_of 54 <<<"$out")" == unity:editor,unity:headless,unity-conflict ]] || fail "an editor label beside headless is a conflict (got: $out)"
[[ "$(skip_of 55 <<<"$out")" == unity:local-proof,unity:none,unity-conflict ]] || fail "two admitted unity labels are still a conflict (got: $out)"
[[ "$(grep -c '^SKIP=' <<<"$out")" -eq 3 ]] || fail "none, headless (with no drain:approved) and local-proof are all admitted (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == 53 ]] || fail "the three admitted labels share one priority-then-age order (got: $out)"

# --- pick: priority, then age ------------------------------------------------------------------
reset
issue 30 2026-09-01T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 31 2026-09-02T00:00:00Z ready-for-agent,unity:none,pri:later "" 0 "$SLICE_BODY"
issue 32 2026-09-04T00:00:00Z ready-for-agent,unity:none,pri:next "" 0 "$SLICE_BODY"
issue 33 2026-09-03T00:00:00Z ready-for-agent,unity:none,pri:next "" 0 "$SLICE_BODY"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(trailer ISSUE <<<"$out")" == 33 ]] || fail "pri:next beats later/none, older wins within it (got: $out)"
[[ "$(trailer SCOPE <<<"$out")" == body ]] || fail "a slice-shaped body is the scope (got: $out)"
issue 34 2026-09-09T00:00:00Z ready-for-agent,unity:none,pri:now "" 0 "$SLICE_BODY"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(trailer ISSUE <<<"$out")" == 34 ]] || fail "pri:now beats an older pri:next (got: $out)"

reset
issue 40 2026-09-01T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY" '[{"author":{"login":"amindell11"},"url":"https://x/c/a","body":"Ready proposal 2026-09-20"},{"author":{"login":"amindell11"},"url":"https://x/c/b","body":"Ready proposal 2026-09-25"}]'
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(trailer SCOPE <<<"$out")" == proposal:https://x/c/b ]] || fail "the latest proposal wins over an older one and the body (got: $out)"

# --- pick: --dry-run writes nothing --------------------------------------------------------------
out="$(bash "$DRAIN" pick --dry-run 2>/dev/null)"
[[ "$(trailer DRY_RUN <<<"$out")" == 1 && "$(trailer ISSUE <<<"$out")" == 40 ]] || fail "dry run stamps DRY_RUN=1 and still picks (got: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "pick must write nothing"

# --- pick: a claimed issue reads by the PRs closing it -----------------------------------------
reset
issue 60 2026-09-01T00:00:00Z ready-for-agent,unity:headless,drain:building me 0 "$SLICE_BODY"
issue 61 2026-09-02T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "no scope" "$(proposal amindell11 61)" 161:OPEN
issue 62 2026-09-03T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY" "" 162:CLOSED,163:CLOSED
issue 63 2026-09-04T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY" "" 164:CLOSED,165:OPEN
issue 64 2026-09-05T00:00:00Z ready-for-agent,unity:none,drain:building me 1 "$SLICE_BODY"
issue 65 2026-09-06T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "no scope" "$(proposal amindell11 65)"
issue 66 2026-09-07T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY" "" 166:CLOSED
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(grep '^UNFINISHED=' <<<"$out")" == $'UNFINISHED=60 body\nUNFINISHED=65 proposal:https://x/c/65' ]] || fail "a claimed issue with no PR of any state is unfinished, with its scope (got: $out)"
[[ -z "$(skip_of 60 <<<"$out")" ]] || fail "an unfinished issue has no SKIP line (got: $out)"
[[ "$(skip_of 61 <<<"$out")" == drain:building ]] || fail "a claimed issue with an open PR is building; the assignee is the claim's (got: $out)"
[[ "$(skip_of 62 <<<"$out")" == drain:building,pr-closed:162,pr-closed:163 ]] || fail "only closed-unmerged PRs names each one (got: $out)"
[[ "$(skip_of 63 <<<"$out")" == drain:building ]] || fail "an open PR beside a closed one is building, not pr-closed (got: $out)"
[[ "$(skip_of 64 <<<"$out")" == drain:building,blocked:1 ]] || fail "a claimed issue blocked since its claim is skipped, not unfinished (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == 66 ]] || fail "a released issue is picked again whatever its closed PRs (got: $out)"

# --- claim: one write, assignee and label together ------------------------------------------------
reset
out="$(bash "$DRAIN" claim 40 2>/dev/null)"
[[ "$out" == CLAIM=claimed ]] || fail "claim prints one trailer and no slot (got: $out)"
[[ "$(cat "$GH_WRITE_LOG")" == "issue edit 40 --repo owner/repo --add-assignee @me --add-label drain:building" ]] || fail "one claim write (got: $(cat "$GH_WRITE_LOG"))"

# --- claim: assignee changed since pick → taken, nothing written ---------------------------------
reset; echo "someone" > "$FIX/assignees.txt"
rc=0; out="$(bash "$DRAIN" claim 40 2>/dev/null)" || rc=$?
[[ "$rc" -eq 4 && "$out" == CLAIM=taken ]] || fail "an assignee at re-read is taken, exit 4 (rc=$rc: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "taken writes nothing"

# --- release: one write, both removed -------------------------------------------------------------
reset
out="$(bash "$DRAIN" release 40 2>/dev/null)"
[[ "$out" == RELEASE=released ]] || fail "release prints its trailer (got: $out)"
[[ "$(cat "$GH_WRITE_LOG")" == "issue edit 40 --repo owner/repo --remove-assignee @me --remove-label drain:building" ]] || fail "one release write (got: $(cat "$GH_WRITE_LOG"))"

# --- owed: every verdict ----------------------------------------------------------------------------
# owed_is <verdict> <open unity> <open script> <open eyes> <why> <body>
owed_is() {
  printf '%s' "$6" > "$FIX/body.md"
  local out rc=0
  out="$(bash "$DRAIN" owed 77 2>"$TMP/err")" || rc=$?
  [[ "$rc" -eq 0 ]] || fail "owed exits 0 on every verdict: $5 (rc=$rc: $(cat "$TMP/err"))"
  [[ "$out" == "OWED=$1"$'\n'"OPEN_UNITY=$2"$'\n'"OPEN_SCRIPT=$3"$'\n'"OPEN_EYES=$4" ]] || fail "$5 (got: $out)"
}
HEAD=$'## Test status\n\nHosted: green on `76b9204`\n\n### Owed local\n\n'
reset
owed_is absent 0 0 0 "a body with no section is absent" $'## Test status\n\n- [ ] unity: not under the heading\n'
owed_is none 0 0 0 "None. alone is nothing owed" "${HEAD}None."$'\n'
owed_is open 2 1 1 "unticked items count per kind" "${HEAD}"$'- [ ] unity: `run-tests <slot> -WithGraphics`\n- [ ] unity: second boot\n- [x] unity: done\n- [ ] script: `run-script-tests <slot>`\n- [ ] eyes: no bars behind the backdrop\n'
owed_is discharged 0 0 0 "all ticked is discharged; indented lines are an item's result" "${HEAD}"$'- [x] unity: `run-tests <slot>`\n  passed on `76b9204`, 12/12\n    - [ ] eyes: nested text is result prose\n\n- [x] eyes: looks right\n'
owed_is open 1 0 1 "CRLF bodies parse, and the section ends at the next heading" "${HEAD//$'\n'/$'\r\n'}"$'- [ ] unity: boot\r\n  result line\r\n- [ ] eyes: look\r\n\r\n## Merge order\r\n\r\n- after #747 — same file\r\n'
owed_is malformed 0 0 0 "an unknown kind is malformed" "${HEAD}"$'- [ ] unity: boot\n- [ ] manual: something\n'
owed_is malformed 0 0 0 "nothing owed is never a checkbox" "${HEAD}"$'- [ ] None\n'
owed_is malformed 0 0 0 "None. beside items is malformed" "${HEAD}"$'- [x] script: ran\nNone.\n'
owed_is malformed 0 0 0 "None. before an item is malformed, not nothing owed" "${HEAD}None."$'\n- [ ] unity: boot\n'
owed_is malformed 0 0 0 "an empty section is malformed" "${HEAD}"$'## Merge order\n'
owed_is malformed 0 0 0 "prose in the section is malformed" "${HEAD}"$'The hosted run skips graphics.\n- [ ] unity: boot\n'
owed_is malformed 0 0 0 "an indented line under no item is malformed" "${HEAD}"$'  - [ ] unity: boot\n- [ ] unity: boot\n'
owed_is malformed 0 0 0 "two sections are malformed" "${HEAD}None."$'\n\n### Owed local\n\n- [ ] unity: boot\n'
grep -q 'more than one' "$TMP/err" || fail "malformed names why on stderr (got: $(cat "$TMP/err"))"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "owed must write nothing"

echo "PASS test_drain_pick.sh"
