#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/drain_pick.sh

# Hermetic regression for scripts/drain_pick.sh: the pick filter (unity label, assignee, open
# blocker, scope block, proposal author), priority-then-age order, and claim's lock order —
# assignee re-read, free slot only, no write on no_slot, unassign on acquire failure. gh and the
# pool script are stubs; every call a stub does not model fails closed.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRAIN="$SCRIPT_DIR/../drain_pick.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_WRITE_LOG="$TMP/gh-writes.log"
export POOL_LOG="$TMP/pool.log"
export GITHUB_REPOSITORY="owner/repo"
export DRAIN_POOL="$TMP/bin/pool.sh"
mkdir -p "$FIX" "$TMP/bin"

cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
case "$args" in
  "api graphql "*) cat "$FIX/queue.json" ;;
  "issue view "*"--json assignees"*) cat "$FIX/assignees.txt" ;;
  "issue edit "*) echo "$args" >> "$GH_WRITE_LOG" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
cat > "$TMP/bin/pool.sh" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$POOL_LOG"
case "$1" in
  status) [[ "${2:-}" == --porcelain ]] || exit 97; cat "$FIX/porcelain.txt" ;;
  acquire) rc="$(cat "$FIX/acquire.exit")"; [[ "$rc" -ne 0 ]] || echo "SLOT=$3 PATH=D:/pool dir/$3"; exit "$rc" ;;
  *) echo "pool stub: unmodelled call: $*" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh" "$TMP/bin/pool.sh"
export PATH="$TMP/bin:$PATH"

# issue <number> <createdAt> <labels csv> [assignee] [blockedBy] [body] [comments-json]
ISSUES=()
issue() {
  ISSUES+=("$(python3 - "$@" <<'PY'
import json, sys
a = sys.argv[1:] + [""] * 7
number, created, labels, assignee, blocked, body, comments = a[:7]
print(json.dumps({"number": int(number), "createdAt": created, "body": body,
  "labels": {"nodes": [{"name": l} for l in labels.split(",") if l]},
  "assignees": {"nodes": [{"login": assignee}] if assignee else []},
  "issueDependenciesSummary": {"blockedBy": int(blocked or 0)},
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

FREE_LAST=$'slot=agent-1\nstate=stale\npath=D:/pool dir/agent-1\nlease=old\n\nslot=agent-2\nstate=locked\npath=D:/pool dir/agent-2\n\nheld=parked\nbranch=held/parked\nleft_slot=agent-3\n\nslot=agent-3\nstate=free\npath=D:/pool dir/agent-3\n'
NO_FREE=$'slot=agent-1\nstate=stale\npath=D:/pool dir/agent-1\nlease=old\n\nslot=agent-2\nstate=locked\npath=D:/pool dir/agent-2\n'

reset() {
  ISSUES=()
  : > "$GH_WRITE_LOG"; : > "$POOL_LOG"
  : > "$FIX/assignees.txt"
  echo 0 > "$FIX/acquire.exit"
  printf '%s' "$FREE_LAST" > "$FIX/porcelain.txt"
}
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }
skip_of() { grep "^SKIP=$1 " | cut -d' ' -f2-; }

# --- usage -----------------------------------------------------------------------------------
reset
for bad in "" "frob" "pick --force" "claim 12" "claim abc lease"; do
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
issue 20 2026-09-01T00:00:00Z ready-for-agent,unity:headless,pri:now someone 1
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(skip_of 20 <<<"$out")" == unity:headless,assigned:someone,blocked:1,no-scope-block ]] || fail "every reason is listed (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == none ]] || fail "nothing eligible picks none (got: $out)"
! grep -q '^SCOPE=' <<<"$out" || fail "no SCOPE when nothing is picked"

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
[[ ! -s "$GH_WRITE_LOG" && ! -s "$POOL_LOG" ]] || fail "pick must write nothing and never touch the pool"

# --- claim: success takes the free slot, never a stale one ---------------------------------------
reset
out="$(bash "$DRAIN" claim 40 drain-thing 2>/dev/null)"
[[ "$(trailer CLAIM <<<"$out")" == claimed ]] || fail "claim succeeds (got: $out)"
[[ "$(trailer SLOT <<<"$out")" == agent-3 ]] || fail "claim names the free slot past stale, locked and held records (got: $out)"
[[ "$(trailer PATH <<<"$out")" == "D:/pool dir/agent-3" ]] || fail "PATH keeps spaces (got: $out)"
[[ "$(trailer LEASE <<<"$out")" == drain-thing ]] || fail "LEASE trailer (got: $out)"
[[ "$(cat "$GH_WRITE_LOG")" == "issue edit 40 --repo owner/repo --add-assignee @me" ]] || fail "one assign write (got: $(cat "$GH_WRITE_LOG"))"
[[ "$(sed -n 2p "$POOL_LOG")" == "acquire drain-thing agent-3" ]] || fail "acquire is strict on the free slot (got: $(cat "$POOL_LOG"))"

# --- claim: assignee changed since pick → taken, nothing written ---------------------------------
reset; echo "someone" > "$FIX/assignees.txt"
rc=0; out="$(bash "$DRAIN" claim 40 drain-thing 2>/dev/null)" || rc=$?
[[ "$rc" -eq 4 && "$(trailer CLAIM <<<"$out")" == taken ]] || fail "an assignee at re-read is taken, exit 4 (rc=$rc: $out)"
[[ ! -s "$GH_WRITE_LOG" && ! -s "$POOL_LOG" ]] || fail "taken writes nothing and never reads the pool"

# --- claim: no free slot → no_slot before any assign ---------------------------------------------
reset; printf '%s' "$NO_FREE" > "$FIX/porcelain.txt"
rc=0; out="$(bash "$DRAIN" claim 40 drain-thing 2>/dev/null)" || rc=$?
[[ "$rc" -eq 3 && "$(trailer CLAIM <<<"$out")" == no_slot ]] || fail "only stale/locked slots is no_slot, exit 3 (rc=$rc: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "no_slot must not assign (got: $(cat "$GH_WRITE_LOG"))"
! grep -q '^acquire' "$POOL_LOG" || fail "no_slot must never acquire (a stale reclaim)"

# --- claim: acquire fails after assign → unassign ------------------------------------------------
reset; echo 1 > "$FIX/acquire.exit"
rc=0; out="$(bash "$DRAIN" claim 40 drain-thing 2>/dev/null)" || rc=$?
[[ "$rc" -eq 5 && "$(trailer CLAIM <<<"$out")" == acquire_failed ]] || fail "acquire failure exits 5 (rc=$rc: $out)"
[[ "$(sed -n 2p "$GH_WRITE_LOG")" == "issue edit 40 --repo owner/repo --remove-assignee @me" ]] || fail "acquire failure unassigns (got: $(cat "$GH_WRITE_LOG"))"
! grep -q '^SLOT=' <<<"$out" || fail "no SLOT on acquire failure"

echo "PASS test_drain_pick.sh"
