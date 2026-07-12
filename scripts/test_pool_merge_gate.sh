#!/usr/bin/env bash
set -euo pipefail

# Regression for the merge gate's tested-tree proof: a failed test run after base integration must force a re-test on retry (a base-merge commit alone is never test evidence).

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/agent_worktree_pool.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export RUNNER_LOG="$TMP/runner.log"
export RUNNER_EXIT_FILE="$TMP/runner.exit"
export GH_MERGE_LOG="$TMP/gh-merge.log"
: > "$RUNNER_LOG"
: > "$GH_MERGE_LOG"
echo 0 > "$RUNNER_EXIT_FILE"

STUB_BIN="$TMP/bin"
mkdir -p "$STUB_BIN"

cat > "$STUB_BIN/powershell.exe" <<'EOF'
#!/usr/bin/env bash
echo "run $*" >> "$RUNNER_LOG"
exit "$(cat "$RUNNER_EXIT_FILE")"
EOF
chmod +x "$STUB_BIN/powershell.exe"

cat > "$STUB_BIN/gh" <<'EOF'
#!/usr/bin/env bash
case "$1 $2" in
  "pr list") [[ "$*" == *"--json number"* ]] && echo 7 ;;
  "pr create") echo "https://example.test/pr/7" ;;
  "pr merge") echo "$*" >> "$GH_MERGE_LOG" ;;
esac
exit 0
EOF
chmod +x "$STUB_BIN/gh"

export PATH="$STUB_BIN:$PATH"
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"

fail() { echo "FAIL: $1" >&2; exit 1; }
runner_runs() { grep -c '^run' "$RUNNER_LOG" || true; }
gh_merges() { grep -c 'squash' "$GH_MERGE_LOG" || true; }
slot_tree() { git -C "$TMP/agent-1" rev-parse 'agent-1^{tree}'; }
recorded_tree() { cat "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_tree" 2>/dev/null || true; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
git -C "$TMP/primary" config user.email pool-test@example.test
git -C "$TMP/primary" config user.name "Pool Test"
echo base > "$TMP/primary/file.txt"
git -C "$TMP/primary" add file.txt
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
cd "$TMP/primary"

"$POOL" acquire merge-gate-test agent-1 >/dev/null

echo change > "$TMP/agent-1/feature.txt"
git -C "$TMP/agent-1" add feature.txt
git -C "$TMP/agent-1" commit -qm feature

"$POOL" submit agent-1 origin/main >/dev/null
[[ "$(runner_runs)" == 1 ]] || fail "submit should run tests once (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "submit should record the tested tree"

echo moved > "$TMP/primary/main.txt"
git -C "$TMP/primary" add main.txt
git -C "$TMP/primary" commit -qm "base moves"
git -C "$TMP/primary" push -q origin main

echo 1 > "$RUNNER_EXIT_FILE"
"$POOL" merge agent-1 >/dev/null 2>&1 && fail "merge must fail when the post-integration test run fails"
[[ "$(runner_runs)" == 2 ]] || fail "failed merge attempt should have run tests (got $(runner_runs))"
[[ "$(gh_merges)" == 0 ]] || fail "failed test run must not reach gh pr merge"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "failed run must not record the merged tree as tested"

echo 0 > "$RUNNER_EXIT_FILE"
"$POOL" merge agent-1 >/dev/null
[[ "$(runner_runs)" == 3 ]] || fail "retry after failed run must re-run tests, not trust the base-merge commit (got $(runner_runs))"
[[ "$(gh_merges)" == 1 ]] || fail "retry with passing tests should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "passing run should record the merged tree"

"$POOL" merge agent-1 >/dev/null
[[ "$(runner_runs)" == 3 ]] || fail "proven tree should skip the re-run (got $(runner_runs))"
[[ "$(gh_merges)" == 2 ]] || fail "fast path should still merge (got $(gh_merges))"

echo "PASS: merge gate tested-tree proof"
