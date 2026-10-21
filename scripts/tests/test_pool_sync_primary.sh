#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# Regression for 'sync-primary': the primary fast-forwards past owner edits main doesn't touch,
# replays edits main touches in other hunks, and refuses with the primary byte-identical on a
# conflict, a staged replay, an untracked collision, a wrong branch, divergence or a held lock.
# Every case runs under a global config with the owner's pull.rebase and rebase.autostash.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"

TMP="$(mktemp -d)"
holder=""
trap 'touch "$TMP/release"; if [[ -n "$holder" ]]; then wait "$holder" 2>/dev/null || true; fi; rm -rf "$TMP"' EXIT
fail() { echo "FAIL: $1" >&2; [[ ! -f "$TMP/out" ]] || cat "$TMP/out" "$TMP/err" >&2; exit 1; }

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks" WORKTREE_POOL_PRIMARY_SYNC_WAIT_SECONDS=1
export GIT_AUTHOR_NAME="Pool Test" GIT_AUTHOR_EMAIL=pool-test@example.test
export GIT_COMMITTER_NAME="Pool Test" GIT_COMMITTER_EMAIL=pool-test@example.test
export GIT_CONFIG_GLOBAL="$TMP/gitconfig"
git config --file "$GIT_CONFIG_GLOBAL" pull.rebase true
git config --file "$GIT_CONFIG_GLOBAL" rebase.autostash true
# Quiets the LF/CRLF warnings only; the system's autocrlf conversion still runs.
git config --file "$GIT_CONFIG_GLOBAL" core.safecrlf false

# A fresh origin, primary P and mover M (the only clone that pushes) per case.
n=0
universe() {
  n=$((n + 1))
  U="$TMP/u$n" O="$TMP/u$n/origin.git" P="$TMP/u$n/primary" M="$TMP/u$n/mover"
  git init -q --bare -b main "$O"
  git clone -q "$O" "$P" 2>/dev/null
  seq 1 40 > "$P/f.txt"
  echo g > "$P/g.txt"
  echo '*.sh text eol=lf' > "$P/.gitattributes"
  if [[ "${1:-}" == --with-pool-copy ]]; then mkdir -p "$P/scripts" && cp "$POOL" "$P/scripts/agent_worktree_pool.sh"; fi
  git -C "$P" add -A && git -C "$P" commit -qm base && git -C "$P" push -q origin HEAD:main
  git clone -q "$O" "$M" 2>/dev/null
}
main_edits() { sed -i "$2" "$M/$1" && git -C "$M" commit -qam "main edits $1" && git -C "$M" push -q origin HEAD:main; }
main_adds() { echo "$2" > "$M/$1" && git -C "$M" add "$1" && git -C "$M" commit -qm "main adds $1" && git -C "$M" push -q origin HEAD:main; }

sync_from() {
  rc=0
  (cd "$1" && bash "${2:-$POOL}" sync-primary) > "$TMP/out" 2> "$TMP/err" || rc=$?
}
expect() {
  local verdict="$1" code="$2" what="$3"
  [[ "$rc" -eq "$code" && "$(cat "$TMP/out")" == "PRIMARY_SYNC=$verdict" ]] || fail "$what: PRIMARY_SYNC=$verdict, exit $code (exit $rc)"
}
state() {
  git -C "$P" rev-parse HEAD
  git -C "$P" status --porcelain
  echo '--- diff'
  git -C "$P" diff
  echo '--- diff --cached'
  git -C "$P" diff --cached
}
expect_refused() {
  local reason="$1" what="$2" before
  before="$(state)"
  sync_from "$P"
  expect "refused:$reason" 1 "$what"
  [[ "$(state)" == "$before" ]] || fail "$what: the primary must be byte-identical"
}
at_main() { [[ "$(git -C "$P" rev-parse HEAD)" == "$(git -C "$O" rev-parse main)" ]]; }
backups() { git -C "$P" for-each-ref --format='%(refname)' refs/primary-sync/; }

universe
sync_from "$P"
expect current 0 "an up-to-date primary"

universe
main_edits f.txt 's/^5$/five-main/'
sync_from "$P"
expect ok 0 "a clean primary behind main"
at_main || fail "a clean primary fast-forwards to main"

universe
echo owner > "$P/g.txt"
main_edits f.txt 's/^5$/five-main/'
sync_from "$P"
expect ok 0 "an edit to a file main leaves alone"
at_main || fail "the fast path reaches main"
[[ "$(cat "$P/g.txt")" == owner ]] || fail "the fast path keeps the owner's edit"
[[ -z "$(backups)" ]] || fail "the fast path pins no backup ref"

universe
sed -i 's/^35$/thirty-five-owner/' "$P/f.txt"
main_edits f.txt 's/^5$/five-main/'
sync_from "$P"
expect replayed 0 "an edit in another hunk of a file main changes"
at_main || fail "the replay reaches main"
grep -q '^five-main' "$P/f.txt" && grep -q '^thirty-five-owner' "$P/f.txt" || fail "the replay keeps both edits"
[[ "$(git -C "$P" diff --name-only)" == f.txt && -z "$(git -C "$P" diff --cached)" ]] || fail "the owner's edit stays unstaged"
[[ "$(backups | wc -l)" -eq 1 ]] || fail "the replay pins one backup ref"
grep -q '^thirty-five-owner' <(git -C "$P" show "$(backups):f.txt") || fail "the backup holds the owner's edit"
[[ -z "$(git -C "$P" stash list)" ]] || fail "the replay leaves the stash list empty"

universe
sed -i 's/^5$/five-owner/' "$P/f.txt"
main_edits f.txt 's/^5$/five-main/'
expect_refused conflict "an edit in the hunk main changes"
grep -q '^  f.txt: conflict markers at lines [0-9]*-[0-9]*$' "$TMP/err" || fail "stderr names the file and its conflicting lines"

universe
rm "$P/g.txt"
main_edits g.txt 's/^g$/g-main/'
expect_refused conflict "the owner deleted a file main modifies"
grep -q '^  g.txt: ' "$TMP/err" || fail "stderr names the deleted file"

universe
echo mine > "$P/new.txt"
main_adds new.txt theirs
expect_refused untracked "an untracked file where main adds one"
grep -qx '  new.txt' "$TMP/err" || fail "stderr lists the untracked file"
[[ "$(cat "$P/new.txt")" == mine ]] || fail "the untracked file keeps its content"

universe
sed -i 's/^35$/thirty-five-owner/' "$P/f.txt"
git -C "$P" add f.txt
main_edits f.txt 's/^5$/five-main/'
expect_refused staged "a staged edit when a replay is needed"

universe
echo staged > "$P/g.txt"
git -C "$P" add g.txt
main_edits f.txt 's/^5$/five-main/'
sync_from "$P"
expect ok 0 "a staged edit on the fast path"
at_main || fail "the fast path with a staged edit reaches main"
[[ "$(git -C "$P" diff --cached --name-only)" == g.txt ]] || fail "the fast path leaves the staged edit staged"

universe
git -C "$P" checkout -q -b other
main_edits f.txt 's/^5$/five-main/'
expect_refused branch "a primary on another branch"

universe
echo local >> "$P/g.txt"
git -C "$P" commit -qam "local commit"
expect_refused diverged "a local commit origin lacks"

universe
git -C "$P" worktree add -q --detach "$U/linked"
main_edits f.txt 's/^5$/five-main/'
sync_from "$U/linked"
expect ok 0 "a run from a linked worktree"
at_main || fail "a run from a linked worktree syncs the primary"

# The running in-tree copy is replaced mid-run; bash must exit before reading the new bytes.
# Git for Windows swaps in a new file, so this case guards against in-place rewrites.
universe --with-pool-copy
sed -i 's/^35$/thirty-five-owner/' "$P/f.txt"
sed -i 's/^5$/five-main/' "$M/f.txt"
echo 'echo "read past the dispatch tail" >&2; exit 42' >> "$M/scripts/agent_worktree_pool.sh"
git -C "$M" commit -qam "main changes the pool script" && git -C "$M" push -q origin HEAD:main
sync_from "$P" "$P/scripts/agent_worktree_pool.sh"
expect replayed 0 "the in-tree pool script updating itself"
grep -q 'read past the dispatch tail' "$P/scripts/agent_worktree_pool.sh" || fail "the sync replaces the running script"
! grep -q 'read past the dispatch tail' "$TMP/err" || fail "bash exits before reading the replaced script"

# The sync lock: a holder that outlasts the wait refuses the sync.
universe
main_edits f.txt 's/^5$/five-main/'
(cd "$P" && bash "$POOL" lock primary-sync -- bash -c ': > "$1/held"; until [[ -e "$1/release" ]]; do sleep .05; done' _ "$TMP") &
holder=$!
until [[ -e "$TMP/held" ]]; do kill -0 "$holder" 2>/dev/null || fail "fixture: the lock holder exited early"; sleep .05; done
expect_refused busy "a sync while another holds the lock"
touch "$TMP/release"
wait "$holder"
holder=""
rm -f "$TMP/held" "$TMP/release"

# A file saved during the autostash window (here by a post-merge hook) fails the post-check.
universe
sed -i 's/^35$/thirty-five-owner/' "$P/f.txt"
main_edits f.txt 's/^5$/five-main/'
printf '#!/bin/sh\necho saved-mid-sync >> g.txt\n' > "$P/.git/hooks/post-merge"
chmod +x "$P/.git/hooks/post-merge"
sync_from "$P"
expect failed:mismatch 2 "a file saved mid-sync"
grep -q "pinned at $(backups)" "$TMP/err" || fail "a mismatch names the backup ref"

universe
sed -i 's/^40$/forty-owner/' "$P/f.txt"
first=""
for i in $(seq 1 21); do
  main_edits f.txt "s/^$i\$/main-$i/"
  sync_from "$P"
  expect replayed 0 "replay $i of 21"
  [[ -n "$first" ]] || first="$(backups)"
done
[[ "$(backups | wc -l)" -eq 20 ]] || fail "21 replays keep the newest 20 backup refs ($(backups | wc -l))"
! backups | grep -qx "$first" || fail "the oldest backup ref is the one pruned"
grep -q '^forty-owner' "$P/f.txt" && grep -q '^main-21' "$P/f.txt" || fail "21 replays keep both sides"

echo "PASS test_pool_sync_primary.sh"
