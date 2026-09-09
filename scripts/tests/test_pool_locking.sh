#!/usr/bin/env bash
set -euo pipefail

# Regression for the pool's locking contracts: auto-pick prefers free slots over
# stale reclaims, a named slot never falls back, reclaim is TTL-gated and refuses
# to clobber unpushed work, release clears both lease homes, and prepare refuses
# a slot holding unpushed work.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
# Via bash: the pool script is tracked non-executable (mode 100644), so direct exec fails on Unix checkouts.
pool() { bash "$POOL" "$@"; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"

fail() { echo "FAIL: $1" >&2; exit 1; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
git -C "$TMP/primary" config user.email pool-test@example.test
git -C "$TMP/primary" config user.name "Pool Test"
echo base > "$TMP/primary/file.txt"
printf 'results/\n' > "$TMP/primary/.gitignore"
git -C "$TMP/primary" add file.txt .gitignore
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
git -C "$TMP/primary" worktree add -q -b agent-2 "$TMP/agent-2" main
cd "$TMP/primary"

lock_dir() { printf '%s/%s.lock' "$WORKTREE_POOL_LOCK_ROOT" "$1"; }
acquired_slot() { sed -n 's/^SLOT=\([^ ]*\).*/\1/p' | head -n 1; }
# Reclaim is age-gated, and the acquiring pid is dead by the next call; back-date the stamp instead.
age_lock() { date -u -d "@$(( $(date -u +%s) - $2 ))" +"%Y-%m-%dT%H:%M:%SZ" > "$(lock_dir "$1")/timestamp"; }

# --- acquire ordering: a free slot beats a reclaimable stale one --------------
pool acquire lease-one agent-1 >/dev/null
age_lock agent-1 100
got="$(WORKTREE_POOL_LOCK_TTL=60 pool acquire lease-two 2>/dev/null | acquired_slot)"
[[ "$got" == "agent-2" ]] || fail "auto-pick must take the free agent-2 before reclaiming stale agent-1 (got '$got')"
[[ "$(cat "$(lock_dir agent-1)/lease")" == "lease-one" ]] || fail "auto-pick must leave the stale lock's lease intact"

# --- named slot is strict: no fallback ---------------------------------------
if pool acquire lease-three agent-1 >/dev/null 2>&1; then fail "acquire must fail on a busy named slot"; fi
[[ "$(cat "$(lock_dir agent-1)/lease")" == "lease-one" ]] || fail "a failed named acquire must not overwrite the holder's lease"
if pool acquire lease-three agent-9 >/dev/null 2>&1; then fail "acquire must fail on an unknown slot"; fi

# --- TTL gate: the same lock is unreclaimable under a long TTL, reclaimable under a short one ---
if WORKTREE_POOL_LOCK_TTL=600 pool acquire lease-four agent-1 >/dev/null 2>&1; then
  fail "a lock younger than the TTL must not be reclaimed"
fi
got="$(WORKTREE_POOL_LOCK_TTL=60 pool acquire lease-four agent-1 2>/dev/null | acquired_slot)"
[[ "$got" == "agent-1" ]] || fail "a past-TTL clobber-safe lock should be reclaimed (got '$got')"
[[ "$(cat "$(lock_dir agent-1)/lease")" == "lease-four" ]] || fail "reclaim should install the new lease"

# --- clobber safety: unpushed work refuses reclaim ----------------------------
echo wip > "$TMP/agent-1/wip.txt"
git -C "$TMP/agent-1" add wip.txt
git -C "$TMP/agent-1" commit -qm "unpushed work"
age_lock agent-1 100
if WORKTREE_POOL_LOCK_TTL=60 pool acquire lease-five agent-1 >/dev/null 2>&1; then
  fail "reclaim must refuse a slot holding unpushed commits"
fi
[[ "$(cat "$(lock_dir agent-1)/lease")" == "lease-four" ]] || fail "a refused reclaim must leave the lock untouched"
echo dirty > "$TMP/agent-2/dirty.txt"
age_lock agent-2 100
if WORKTREE_POOL_LOCK_TTL=60 pool acquire lease-five agent-2 >/dev/null 2>&1; then
  fail "reclaim must refuse a dirty slot"
fi

# --- release ------------------------------------------------------------------
pool release agent-2 | grep -q "Released agent-2" || fail "release should report the release"
[[ ! -d "$(lock_dir agent-2)" ]] || fail "release must remove the lock dir"
[[ -z "$(git -C "$TMP/agent-2" config --worktree --get worktree-pool.lease 2>/dev/null || true)" ]] \
  || fail "release must clear the durable git-config lease"
pool release agent-2 | grep -q "was not locked" || fail "releasing a free slot should be a clean no-op"

# --- prepare refuses unpushed work unless forced ------------------------------
if pool prepare agent-1 origin/main >/dev/null 2>&1; then fail "prepare must refuse a slot holding unpushed work"; fi
[[ -f "$TMP/agent-1/wip.txt" ]] || fail "a refused prepare must not touch the worktree"
pool prepare agent-1 origin/main --force >/dev/null
[[ ! -f "$TMP/agent-1/wip.txt" ]] || fail "prepare --force should reset the slot to base"

# --- reclaim contention: two racers, exactly one winner (#453) ----------------
pool release agent-1 >/dev/null
for iteration in 1 2 3 4 5; do
  pool acquire "lease-six-$iteration" agent-1 >/dev/null
  age_lock agent-1 100
  out_a="$TMP/race-a.out"
  out_b="$TMP/race-b.out"
  WORKTREE_POOL_LOCK_TTL=60 pool acquire "race-a-$iteration" agent-1 > "$out_a" 2>/dev/null &
  pid_a=$!
  WORKTREE_POOL_LOCK_TTL=60 pool acquire "race-b-$iteration" agent-1 > "$out_b" 2>/dev/null &
  pid_b=$!
  wait "$pid_a" || true
  wait "$pid_b" || true
  winners="$(cat "$out_a" "$out_b" | grep -c '^SLOT=' || true)"
  [[ "$winners" == 1 ]] || fail "exactly one racer may reclaim a stale lock (iteration $iteration, got $winners)"
  [[ -d "$(lock_dir agent-1)" ]] || fail "the winning racer must leave agent-1 locked (iteration $iteration)"
  pool release agent-1 >/dev/null
done

echo "PASS: pool locking — acquire ordering + named strictness + TTL reclaim + clobber safety + release + prepare refusal + reclaim contention"
