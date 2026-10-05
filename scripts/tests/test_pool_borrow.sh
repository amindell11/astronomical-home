#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# borrow / return: borrow takes a free slot, never a stale one, skips a free slot holding unpushed
# work, takes over a slot already holding its lease unless the access coordinator shows a live Unity
# owner there, and records no task branch; return refuses a slot not holding the lease, touching
# nothing, and resets a slot left at a deleted branch's head before releasing it. powershell.exe is
# a stub answering the coordinator's Status read; every other call fails closed.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
pool() { bash "$POOL" "$@"; }
pool_fn() { (source "$POOL"; "$@"); }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT
fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix" WORKTREE_POOL_LOCK_ROOT="$TMP/locks"
LOCKS="$WORKTREE_POOL_LOCK_ROOT"
mkdir -p "$FIX" "$TMP/bin"
# $FIX/owner is the Status read's answer: none, owner <lease>, or fail.
cat > "$TMP/bin/powershell.exe" <<'EOF'
#!/usr/bin/env bash
if [[ "$*" != *'"-Action", "Status", "-ProjectPath"'* ]]; then
  echo "powershell stub: unmodelled call: $*" >&2
  exit 97
fi
echo "$POOL_PROJECT_PATH" >> "$FIX/asked"
[[ "$(cat "$FIX/owner")" != fail ]] || exit 1
cat "$FIX/owner"
EOF
chmod +x "$TMP/bin/powershell.exe"
export PATH="$TMP/bin:$PATH"

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary" 2>/dev/null
cd "$TMP/primary"
git config user.email pool-test@example.test
git config user.name "Pool Test"
printf 'results/\n.worktree-pool/\n' > .gitignore
echo base > a.txt
git add . && git commit -qm init && git push -q origin main
for n in 1 2 3; do git worktree add -q -b "agent-$n" "$TMP/agent-$n" main; done

lease_of() { cat "$LOCKS/$1.lock/lease" 2>/dev/null || true; }
locked() { [[ -d "$LOCKS/$1.lock" ]]; }
head_of() { git -C "$TMP/$1" rev-parse HEAD; }
age_lock() { echo 2020-01-01T00:00:00Z | tee "$LOCKS/$1.lock/timestamp" > "$LOCKS/$1.lock/last_use"; }
# borrow <lease>: runs it, stdout to $out, exit code to $rc
borrow() { rc=0; out="$(pool borrow "$1" 2>"$TMP/err")" || rc=$?; }
echo none > "$FIX/owner"

# --- usage --------------------------------------------------------------------------------------
for bad in "borrow" "borrow a b" "borrow a/b" "return" "return agent-1" "return ../x verify"; do
  rc=0; pool $bad > /dev/null 2>&1 || rc=$?
  [[ "$rc" -eq 1 ]] || fail "'$bad' should exit 1 (got $rc)"
done

# --- borrow: a free slot, skipping one holding unpushed work ------------------------------------
pool acquire other-1 agent-1 >/dev/null
echo wip > "$TMP/agent-2/wip.txt"
borrow verify
[[ "$rc" -eq 0 && "$out" == "SLOT=agent-3 PATH=$(pool_fn slot_path agent-3)" ]] || fail "borrow takes the first clean free slot (rc=$rc: $out; $(cat "$TMP/err"))"
grep -q "skipping agent-2 — it is free but holds unpushed work" "$TMP/err" || fail "the skipped slot is named (got: $(cat "$TMP/err"))"
! locked agent-2 && [[ -e "$TMP/agent-2/wip.txt" ]] || fail "the skipped slot is released untouched"
[[ "$(lease_of agent-3)" == verify ]] || fail "the borrowed slot holds the lease"
[[ ! -e "$LOCKS/agent-3.lock/task_branch" ]] || fail "borrow records no task branch"
[[ ! -e "$FIX/asked" ]] || fail "a fresh borrow asks the coordinator nothing"
[[ "$(head_of agent-3)" == "$(git rev-parse origin/main)" ]] || fail "fixture: agent-3 starts at main"

# --- borrow: records no task branch, so land's preflight sees no PR head on the slot -------------
git checkout -q -b task/p5 && echo five > p5.txt && git add p5.txt && git commit -qm p5 && git push -q origin task/p5
p5="$(git rev-parse HEAD)"
git checkout -q --detach
pool prepare agent-3 "$p5" >/dev/null 2>&1
[[ "$(pool_fn slot_table | grep '^agent-3')" == $'agent-3\037verify\037task/verify' ]] \
  || fail "a borrowed slot at a PR head shows only its own lease's branch (got: $(pool_fn slot_table | grep '^agent-3' | cat -A))"

# --- borrow: takes over its own lease unless Unity is live on that slot ---------------------------
borrow verify
[[ "$rc" -eq 0 && "$out" == "SLOT=agent-3 PATH=$(pool_fn slot_path agent-3)" ]] || fail "borrow takes over the slot holding its lease (rc=$rc: $out; $(cat "$TMP/err"))"
[[ "$(head_of agent-3)" == "$p5" ]] || fail "a takeover leaves the tree where it was"
[[ "$(cat "$FIX/asked")" == "$(pool_fn slot_path agent-3)/src/Asteroids3D" ]] || fail "the takeover asks about that slot's project (got: $(cat "$FIX/asked"))"
echo "owner unity-tests-agent-3-abc" > "$FIX/owner"
borrow verify
[[ "$rc" -eq 1 && -z "$out" ]] || fail "a live owner refuses the takeover (rc=$rc: $out)"
grep -q "Unity is live on its project (owner unity-tests-agent-3-abc)" "$TMP/err" || fail "the refusal names the owner (got: $(cat "$TMP/err"))"
echo fail > "$FIX/owner"
borrow verify
[[ "$rc" -eq 1 ]] && grep -q "could not ask the access coordinator" "$TMP/err" || fail "an unreadable coordinator refuses (rc=$rc)"
! locked agent-2 && [[ "$(lease_of agent-3)" == verify ]] || fail "a refused takeover takes no other slot and keeps the lease"
echo none > "$FIX/owner"

# --- borrow: never a stale slot ----------------------------------------------------------------------
rm "$TMP/agent-2/wip.txt"
pool acquire stale-2 agent-2 >/dev/null 2>&1
age_lock agent-2
borrow verify-2
[[ "$rc" -eq 1 ]] && grep -q "no free slot for verify-2" "$TMP/err" || fail "with only a stale slot left, borrow refuses (rc=$rc: $out)"
[[ "$(lease_of agent-2)" == stale-2 ]] || fail "borrow never reclaims a stale slot"
pool release agent-2 >/dev/null

# --- return: refuses a slot not holding the lease, touching nothing ---------------------------------
before="$(head_of agent-3)"
for args in "agent-1 verify" "agent-2 verify" "agent-3 other"; do
  rc=0; pool return $args > "$TMP/out" 2> "$TMP/err" || rc=$?
  [[ "$rc" -eq 1 ]] && grep -q "nothing was reset" "$TMP/err" || fail "return $args refuses (rc=$rc: $(cat "$TMP/err"))"
done
[[ "$(head_of agent-3)" == "$before" && "$(lease_of agent-3)" == verify && "$(lease_of agent-1)" == other-1 ]] \
  || fail "a refused return leaves every slot as it was"

# --- return: resets a slot left at a deleted branch's head, then releases it ------------------------
git push -q origin --delete task/p5
git -C "$TMP/agent-3" fetch -q --prune origin
if pool prepare agent-3 origin/main > /dev/null 2>&1; then fail "fixture: plain prepare refuses a deleted branch's head"; fi
pool return agent-3 verify > /dev/null 2>"$TMP/err" || fail "return resets and releases its slot ($(cat "$TMP/err"))"
[[ "$(head_of agent-3)" == "$(git rev-parse origin/main)" ]] || fail "the returned slot sits at origin/main"
! locked agent-3 || fail "the returned slot is released"

echo "PASS test_pool_borrow.sh"
