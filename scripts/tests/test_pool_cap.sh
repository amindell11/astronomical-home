#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# The pool is agent-1 through agent-8. A worktree on agent-9 is not a slot.
# When every recognized slot is leased, auto-pick parks the longest-idle one
# that has no Unity editor, and leaves a slot a Unity editor is using.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
pool() { bash "$POOL" "$@"; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"
export WORKTREE_POOL_UNITY_OWNER=none

fail() { echo "FAIL: $1" >&2; exit 1; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
git -C "$TMP/primary" config user.email pool-test@example.test
git -C "$TMP/primary" config user.name "Pool Test"
echo base > "$TMP/primary/file.txt"
git -C "$TMP/primary" add file.txt
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
git -C "$TMP/primary" worktree add -q -b agent-2 "$TMP/agent-2" main
git -C "$TMP/primary" worktree add -q -b agent-9 "$TMP/agent-9" main
cd "$TMP/primary"

lock_dir() { printf '%s/%s.lock' "$WORKTREE_POOL_LOCK_ROOT" "$1"; }
acquired_slot() { sed -n 's/^SLOT=\([^ ]*\).*/\1/p' | head -n 1; }
age_lock() { date -u -d "@$(( $(date -u +%s) - $2 ))" +"%Y-%m-%dT%H:%M:%SZ" > "$(lock_dir "$1")/last_use"; }

if pool status --porcelain | grep -q '^slot=agent-9$'; then
  fail "agent-9 must not be a pool slot"
fi

pool acquire lease-old agent-1 >/dev/null
pool acquire lease-new agent-2 >/dev/null
echo wip > "$TMP/agent-1/wip.txt"
echo other > "$TMP/agent-2/other.txt"
age_lock agent-1 5000
age_lock agent-2 10

if WORKTREE_POOL_UNITY_OWNER=editor pool acquire lease-third >/dev/null 2>"$TMP/busy.err"; then
  fail "acquire must not park a slot a Unity editor is using"
fi
[[ "$(cat "$(lock_dir agent-1)/lease")" == "lease-old" ]] || fail "a refused park changed agent-1"
git rev-parse -q --verify refs/heads/held/lease-old >/dev/null && fail "a refused park wrote a held branch"
grep -q "Unity editor" "$TMP/busy.err" || fail "a refused park should say the slot is in Unity"

if pool acquire lease-named agent-2 >/dev/null 2>&1; then
  fail "a named acquire must not park some other slot"
fi
[[ "$(cat "$(lock_dir agent-2)/lease")" == "lease-new" ]] || fail "a named acquire changed agent-2"

got="$(pool acquire lease-third 2>"$TMP/park.err" | acquired_slot)"
[[ "$got" == "agent-1" ]] || fail "auto-pick must park the longest-idle slot (got '$got')"
[[ "$(cat "$(lock_dir agent-1)/lease")" == "lease-third" ]] || fail "the parked slot should hold the new lease"
[[ "$(cat "$(lock_dir agent-2)/lease")" == "lease-new" ]] || fail "the newer slot should keep its lease"
[[ "$(git show held/lease-old:wip.txt)" == "wip" ]] || fail "the parked work should keep the dirty file"
git rev-parse --verify refs/remotes/origin/held/lease-old >/dev/null 2>&1 && fail "an automatic park must stay local"
[[ ! -e "$TMP/agent-1/wip.txt" ]] || fail "the parked slot should be prepared"
grep -q "parking agent-1" "$TMP/park.err" || fail "acquire should say which slot it parked"

echo "PASS: pool cap"
