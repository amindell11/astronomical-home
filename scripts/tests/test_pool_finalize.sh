#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
mkdir -p "$TMP/bin"
export REAL_GIT="$(command -v git)"
cat > "$TMP/bin/git" <<'EOF'
#!/usr/bin/env bash
if [[ "${FAIL_RESET:-0}" == 1 && " $* " == *' reset --hard '* ]]; then
  echo 'injected reset failure' >&2
  exit 73
fi
exec "$REAL_GIT" "$@"
EOF
chmod +x "$TMP/bin/git"
cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
case "$1 $2" in
  'pr list')
    [[ "$*" == *--state\ all* ]] || { echo 7; exit 0; }
    [[ " $* " == *" --head task/lifecycle --base main "* ]] || { echo 'wrong PR identity' >&2; exit 1; }
    [[ "${PR_API_FAIL:-0}" == 0 ]] || exit 1
    printf '%s\t%s\t%s\n' "${PR_STATE:-OPEN}" "$PR_HEAD" "$MERGE_SHA" ;;

esac
EOF
chmod +x "$TMP/bin/gh"
export PATH="$TMP/bin:$PATH"
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"
git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
cd "$TMP/primary"
git config user.email test@example.test
git config user.name Test
echo base > file.txt
git add . && git commit -qm base && git push -q origin main
git worktree add -q -b agent-1 "$TMP/agent-1" main
bash "$POOL" acquire lifecycle agent-1 >/dev/null
echo feature > "$TMP/agent-1/feature.txt"
git -C "$TMP/agent-1" add . && git -C "$TMP/agent-1" commit -qm feature
export PR_HEAD="$(git -C "$TMP/agent-1" rev-parse HEAD)"
export MERGE_SHA="$(git rev-parse HEAD)"
git -C "$TMP/agent-1" push -q origin HEAD:task/lifecycle
echo dirty >> "$TMP/agent-1/file.txt"
# Exercise the public processes and the reported && chain, with errexit enabled.
set +e
bash -ec 'bash "$POOL" merge agent-1 && { echo FINALIZE_EXECUTED; bash "$POOL" finalize agent-1; }' > "$TMP/chain.log" 2>&1
code=$?
set -e
[[ "$code" != 0 ]] || fail "dirty merge chain exited zero"
! grep -q FINALIZE_EXECUTED "$TMP/chain.log" || fail "dirty merge invoked chained finalize"
grep -q 'uncommitted/untracked' "$TMP/chain.log" || fail "chain missed dirty-tree refusal"
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$PR_HEAD" ]] || fail "chain reset commits"
[[ -d "$TMP/locks/agent-1.lock" ]] || fail "chain released lease"
echo 'PASS: dirty merge exits nonzero and && prevents finalize'
set +e
bash "$POOL" finalize agent-1 > "$TMP/finalize.log" 2>&1
code=$?
set -e
[[ "$code" != 0 ]] || fail "finalize discarded open PR work"
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$PR_HEAD" ]] || fail "finalize reset commits"
grep -q dirty "$TMP/agent-1/file.txt" || fail "finalize erased dirty work"
[[ -n "$(git ls-remote origin refs/heads/task/lifecycle)" ]] || fail "finalize deleted branch"
[[ -d "$TMP/locks/agent-1.lock" ]] || fail "finalize released lease"
echo 'PASS: finalize preserves open PR work'

refuse_finalize() {
  local head branch lease status
  head="$(git -C "$TMP/agent-1" rev-parse HEAD)"
  branch="$(git ls-remote origin refs/heads/task/lifecycle)"
  lease="$(cat "$TMP/locks/agent-1.lock/lease")"
  status="$(git -C "$TMP/agent-1" status --porcelain)"
  if bash "$POOL" finalize agent-1 > "$TMP/finalize.log" 2>&1; then fail "$1 accepted finalize"; fi
  if [[ -n "${2:-}" ]]; then grep -q "$2" "$TMP/finalize.log" || { cat "$TMP/finalize.log"; fail "$1 refused for wrong reason"; }; fi
  [[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$head" ]] || fail "$1 reset commits"
  [[ "$(git ls-remote origin refs/heads/task/lifecycle)" == "$branch" ]] || fail "$1 changed remote branch"
  [[ "$(cat "$TMP/locks/agent-1.lock/lease")" == "$lease" ]] || fail "$1 changed lease"
  [[ "$(git -C "$TMP/agent-1" status --porcelain)" == "$status" ]] || fail "$1 changed dirty work"
  echo "PASS: $1 preserves slot and remote branch"
}
export PR_STATE=MERGED
refuse_finalize 'merged PR with dirty work' 'uncommitted/untracked'
git -C "$TMP/agent-1" checkout -- file.txt
export PR_STATE=OPEN
refuse_finalize 'open PR' 'merged PR evidence unavailable'
export PR_STATE=CLOSED
refuse_finalize 'closed unmerged PR' 'merged PR evidence unavailable'
export PR_STATE=MERGED PR_API_FAIL=1
refuse_finalize 'failed API'
export PR_API_FAIL=0
saved_head="$PR_HEAD"
export PR_HEAD=''
refuse_finalize 'missing PR head' 'merged PR evidence unavailable'
export PR_HEAD="$saved_head"
# Squash commits differ from PR heads; GitHub preserves the merged head as evidence.
git merge --squash agent-1 >/dev/null && git commit -qm squash
export MERGE_SHA="$(git rev-parse HEAD)"
refuse_finalize 'merge absent from remote base' 'does not contain'
git push -q origin main
echo later > "$TMP/agent-1/later.txt"
git -C "$TMP/agent-1" add . && git -C "$TMP/agent-1" commit -qm later
refuse_finalize 'later local commit' 'HEAD differs'
git -C "$TMP/agent-1" push -q origin HEAD:task/lifecycle
git -C "$TMP/agent-1" reset -q --hard "$PR_HEAD"
refuse_finalize 'later remote commit' 'changed after'
git push -q --force origin "$PR_HEAD:refs/heads/task/lifecycle"
bash "$POOL" finalize agent-1 > "$TMP/finalize.log" 2>&1 || { cat "$TMP/finalize.log"; fail 'verified merged work refused'; }
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$MERGE_SHA" ]] || fail 'success did not reset to merged base'
[[ -z "$(git ls-remote origin refs/heads/task/lifecycle)" ]] || fail 'success retained remote branch'
[[ ! -d "$TMP/locks/agent-1.lock" ]] || fail 'success retained lease'
echo 'PASS: verified merged work finalizes'

bash "$POOL" acquire lifecycle agent-1 >/dev/null
git -C "$TMP/agent-1" reset -q --hard "$PR_HEAD"
set +e
FAIL_RESET=1 bash "$POOL" finalize agent-1 > "$TMP/finalize.log" 2>&1
code=$?
set -e
[[ "$code" == 73 ]] || { cat "$TMP/finalize.log"; fail 'prepare reset failure was swallowed'; }
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$PR_HEAD" ]] || fail 'failed reset changed HEAD'
[[ -d "$TMP/locks/agent-1.lock" ]] || fail 'failed reset released lease'
echo 'PASS: prepare failure propagates and retains lease'
bash "$POOL" finalize agent-1 > "$TMP/finalize.log" 2>&1 || { cat "$TMP/finalize.log"; fail 'already deleted remote branch refused'; }
[[ "$(git -C "$TMP/agent-1" rev-parse HEAD)" == "$MERGE_SHA" ]] || fail 'absent branch cleanup did not reset'
[[ ! -d "$TMP/locks/agent-1.lock" ]] || fail 'absent branch cleanup retained lease'
echo 'PASS: verified merged work finalizes with remote branch already deleted'
