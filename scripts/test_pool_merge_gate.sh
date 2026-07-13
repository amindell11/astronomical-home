#!/usr/bin/env bash
set -euo pipefail

# Regression for the merge gate's proof chain: only passing FULL runs record merge-grade proof, a failed run after base integration forces a re-test on retry, and inert deltas (docs-only / .cs comment-only) extend proof without burning a full suite.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/agent_worktree_pool.sh"
# Via bash: the pool script is tracked non-executable (mode 100644), so direct exec fails on Unix checkouts.
pool() { bash "$POOL" "$@"; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export RUNNER_LOG="$TMP/runner.log"
export RUNNER_EXIT_FILE="$TMP/runner.exit"
export GH_MERGE_LOG="$TMP/gh-merge.log"
: > "$RUNNER_LOG"
: > "$GH_MERGE_LOG"
echo 0 > "$RUNNER_EXIT_FILE"

export STUB_BIN="$TMP/bin"
mkdir -p "$STUB_BIN"

# Stub only the test runner; other powershell invocations (inert_diff.ps1) fall through to the real binary. The stub derives the summary JSON from its args so the coverage predicate sees full vs scoped runs.
cat > "$STUB_BIN/powershell.exe" <<'EOF'
#!/usr/bin/env bash
if [[ "$*" != *unity_test_agent.ps1* ]]; then
  real="$(type -pa powershell.exe | grep -vF "$STUB_BIN" | head -n 1)"
  [[ -n "$real" ]] || { echo "stub: no real powershell.exe for: $*" >&2; exit 1; }
  exec "$real" "$@"
fi
echo "run $*" >> "$RUNNER_LOG"
mode=Both scope=Workspace filter="" category="" assemblies=""
args=("$@")
for ((i = 0; i < ${#args[@]} - 1; i++)); do
  case "${args[i]}" in
    -Mode) mode="${args[i+1]}" ;;
    -ScopeType) scope="${args[i+1]}" ;;
    -TestFilter) filter="${args[i+1]}" ;;
    -TestCategory) category="${args[i+1]}" ;;
    -AssemblyNames) assemblies="${args[i+1]}" ;;
  esac
done
ec="$(cat "$RUNNER_EXIT_FILE")"
status=passed
[[ "$ec" == 0 ]] || status=failed
mkdir -p results/unity-tests-agent
cat > results/unity-tests-agent/latest-summary.json <<JSON
{
  "mode": "$mode",
  "status": "$status",
  "selection": {
    "scopeType": "$scope",
    "scopeName": "",
    "testFilter": "$filter",
    "testCategory": "$category",
    "excludeCategory": "RequiresGraphics",
    "assemblyNames": "$assemblies",
    "orderedTestListFile": "",
    "rerunFailedFrom": ""
  }
}
JSON
exit "$ec"
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

pool acquire merge-gate-test agent-1 >/dev/null

echo change > "$TMP/agent-1/feature.txt"
git -C "$TMP/agent-1" add feature.txt
git -C "$TMP/agent-1" commit -qm feature

pool submit agent-1 origin/main >/dev/null
[[ "$(runner_runs)" == 1 ]] || fail "submit should run tests once (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "submit should record the tested tree"

echo moved > "$TMP/primary/main.txt"
git -C "$TMP/primary" add main.txt
git -C "$TMP/primary" commit -qm "base moves"
git -C "$TMP/primary" push -q origin main

echo 1 > "$RUNNER_EXIT_FILE"
pool merge agent-1 >/dev/null 2>&1 && fail "merge must fail when the post-integration test run fails"
[[ "$(runner_runs)" == 2 ]] || fail "failed merge attempt should have run tests (got $(runner_runs))"
[[ "$(gh_merges)" == 0 ]] || fail "failed test run must not reach gh pr merge"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "failed run must not record the merged tree as tested"

echo 0 > "$RUNNER_EXIT_FILE"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 3 ]] || fail "retry after failed run must re-run tests, not trust the base-merge commit (got $(runner_runs))"
[[ "$(gh_merges)" == 1 ]] || fail "retry with passing tests should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "passing run should record the merged tree"

pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 3 ]] || fail "proven tree should skip the re-run (got $(runner_runs))"
[[ "$(gh_merges)" == 2 ]] || fail "fast path should still merge (got $(gh_merges))"

last_run_line() { grep '^run' "$RUNNER_LOG" | tail -n 1; }
scope_field() { sed -n "s/^$1=//p" "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_scope" 2>/dev/null | head -n 1; }

# Scoped submit records NO proof; the gate then runs the full suite.
echo change2 > "$TMP/agent-1/feature2.txt"
git -C "$TMP/agent-1" add feature2.txt
git -C "$TMP/agent-1" commit -qm "feature 2"
pool submit agent-1 origin/main -- -Mode EditMode -ScopeType Feature -ScopeName camera >/dev/null
[[ "$(runner_runs)" == 4 ]] || fail "scoped submit should still run tests (got $(runner_runs))"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "scoped submit must not record merge-grade proof"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 5 ]] || fail "merge after scoped submit must run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-ScopeType'; then fail "gate run after scoped submit should be the unfiltered full suite"; fi
[[ "$(gh_merges)" == 3 ]] || fail "merge after gate full run should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "gate full run should record proof"

# revise --no-test pushes but runs nothing and records no proof.
echo hygiene >> "$TMP/agent-1/feature2.txt"
git -C "$TMP/agent-1" add feature2.txt
git -C "$TMP/agent-1" commit -qm "hygiene edit"
proof_before="$(recorded_tree)"
pool revise agent-1 --no-test >/dev/null
[[ "$(runner_runs)" == 5 ]] || fail "revise --no-test must not run tests (got $(runner_runs))"
[[ "$(recorded_tree)" == "$proof_before" ]] || fail "revise --no-test must not touch recorded proof"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "revise --no-test must not claim proof for the new tree"
[[ "$(git -C "$TMP/origin.git" rev-parse refs/heads/task/merge-gate-test)" == "$(git -C "$TMP/agent-1" rev-parse agent-1)" ]] \
  || fail "revise --no-test must still push the branch"

# Full submit + unmoved base: merge skips the re-run.
cat > "$TMP/agent-1/code.cs" <<'CS'
class Gate {
    // seed comment
    string url = "http://example.test"; /* block */
    void Run() { }
}
CS
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "add code.cs"
pool submit agent-1 origin/main >/dev/null
[[ "$(runner_runs)" == 6 ]] || fail "full submit should run tests (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "full submit should record proof"
full_tree="$(recorded_tree)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 6 ]] || fail "merge on fully-proven tree must skip the re-run (got $(runner_runs))"
[[ "$(gh_merges)" == 4 ]] || fail "proven-tree merge should still merge (got $(gh_merges))"

# md/doc-only delta after full proof: proof extends with NO runner invocation.
echo notes > "$TMP/agent-1/NOTES.md"
mkdir -p "$TMP/agent-1/doc"
echo design > "$TMP/agent-1/doc/design.md"
git -C "$TMP/agent-1" add NOTES.md doc/design.md
git -C "$TMP/agent-1" commit -qm "docs only"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 6 ]] || fail "docs-only delta must not invoke the runner (got $(runner_runs))"
[[ "$(gh_merges)" == 5 ]] || fail "docs-only delta should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "docs-only delta should extend proof to the landing tree"
[[ "$(scope_field kind)" == "inherit-doc" ]] || fail "docs-only extension should record inherit-doc provenance"
[[ "$(scope_field anchor)" == "$full_tree" ]] || fail "docs-only extension must stay anchored to the full run"

# .cs comment-only delta after full proof: one EditMode/Smoke refresh, not the full suite.
cat > "$TMP/agent-1/code.cs" <<'CS'
class Gate {
    // reworded comment
    string url = "http://example.test";
    void Run() {
    }
}
CS
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "comment-only edit"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 7 ]] || fail "comment-only delta should invoke the runner exactly once (got $(runner_runs))"
last_run_line | grep -q -- '-Mode EditMode' || fail "comment-only refresh must run EditMode"
last_run_line | grep -q -- '-ScopeType Smoke' || fail "comment-only refresh must run the Smoke scope"
[[ "$(gh_merges)" == 6 ]] || fail "comment-only delta should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "comment-only delta should extend proof to the landing tree"
[[ "$(scope_field kind)" == "inherit-smoke" ]] || fail "comment-only extension should record inherit-smoke provenance"
[[ "$(scope_field anchor)" == "$full_tree" ]] || fail "comment-only extension must stay anchored to the full run"

# Real .cs code change after full proof: full suite runs.
cat > "$TMP/agent-1/code.cs" <<'CS'
class Gate {
    // reworded comment
    string url = "http://other.example";
    void Run() {
    }
}
CS
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "real code change"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 8 ]] || fail "code delta must run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-ScopeType Smoke'; then fail "code delta must not downgrade to the smoke refresh"; fi
[[ "$(gh_merges)" == 7 ]] || fail "code delta merge should complete (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "code delta gate run should record proof"
[[ "$(scope_field kind)" == "full-run" ]] || fail "code delta gate run should re-anchor as full-run"

echo "PASS: merge gate tested-tree proof + scope-aware proof + inert fast path"
