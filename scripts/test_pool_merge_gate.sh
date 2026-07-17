#!/usr/bin/env bash
set -euo pipefail

# Regression for the merge gate's proof chain: only passing FULL runs on a clean worktree record merge-grade proof, a failed run after base integration forces a re-test on retry, and inert deltas (*.md / .cs comment-only) extend proof without burning a full suite.

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
# pwd -W (Windows-style, git-bash) matches the canonical worktree path the pool script derives; plain $PWD is the mismatched MSYS view.
project="${STUB_PROJECT_PATH:-$(pwd -W 2>/dev/null || pwd)/src/Asteroids3D}"
failed=0
[[ "$ec" == 0 ]] || failed=1
if [[ "$mode" == "Both" ]]; then
  runs="{\"platform\": \"EditMode\", \"status\": \"$status\", \"total\": 10, \"failed\": $failed}, {\"platform\": \"PlayMode\", \"status\": \"$status\", \"total\": 10, \"failed\": $failed}"
else
  runs="{\"platform\": \"$mode\", \"status\": \"$status\", \"total\": 10, \"failed\": $failed}"
fi
mkdir -p results/unity-tests-agent
cat > results/unity-tests-agent/latest-summary.json <<JSON
{
  "mode": "$mode",
  "status": "$status",
  "projectPath": "$project",
  "runs": [ $runs ],
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
printf 'results/\n' > "$TMP/primary/.gitignore"
git -C "$TMP/primary" add file.txt .gitignore
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
cd "$TMP/primary"

pool acquire merge-gate-test agent-1 >/dev/null

echo change > "$TMP/agent-1/feature.txt"
git -C "$TMP/agent-1" add feature.txt
git -C "$TMP/agent-1" commit -qm feature

if pool submit agent-1 origin/main --title "test PR" --body "test body" --bogus >/dev/null 2>&1; then fail "submit must reject unknown --flags before the -- separator"; fi
[[ "$(runner_runs)" == 0 ]] || fail "rejected submit must not start a test run (got $(runner_runs))"

pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
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
pool submit agent-1 origin/main --title "test PR" --body "test body" -- -Mode EditMode -ScopeType Feature -ScopeName camera >/dev/null
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
    string url = "http://example.test";
    void Run() { }
}
CS
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "add code.cs"
pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
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
    void Run() { }
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
    void Run() { }
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

# Dirty worktree: proof-bearing runs refuse to start (the runner tests the working tree, not HEAD).
echo "class Stray { }" > "$TMP/agent-1/stray.cs"
if pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null 2>&1; then fail "submit must refuse a dirty worktree"; fi
[[ "$(runner_runs)" == 8 ]] || fail "dirty submit must not invoke the runner (got $(runner_runs))"
if pool merge agent-1 >/dev/null 2>&1; then fail "merge must refuse a dirty worktree"; fi
[[ "$(runner_runs)" == 8 ]] || fail "dirty merge must not invoke the runner (got $(runner_runs))"
[[ "$(gh_merges)" == 7 ]] || fail "dirty merge must not reach gh pr merge (got $(gh_merges))"
rm "$TMP/agent-1/stray.cs"

# A summary from the wrong project must not arm proof.
echo change3 > "$TMP/agent-1/feature3.txt"
git -C "$TMP/agent-1" add feature3.txt
git -C "$TMP/agent-1" commit -qm "feature 3"
STUB_PROJECT_PATH="/definitely/not/this/project" pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == 9 ]] || fail "wrong-project submit should still run tests (got $(runner_runs))"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "wrong-project summary must not arm proof"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 10 ]] || fail "merge after wrong-project submit must re-run the full suite (got $(runner_runs))"
[[ "$(gh_merges)" == 8 ]] || fail "wrong-project recovery merge should complete (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "gate full run should arm proof after wrong-project summary"

# Caller-info attributes in Assets disable the .cs comment-only fast path (line/argument-text sensitive).
mkdir -p "$TMP/agent-1/src/Asteroids3D/Assets"
cat > "$TMP/agent-1/src/Asteroids3D/Assets/CallerProbe.cs" <<'CS'
using System.Runtime.CompilerServices;
class CallerProbe {
    static void Log(string message, [CallerLineNumber] int line = 0) { }
}
CS
git -C "$TMP/agent-1" add src/Asteroids3D/Assets/CallerProbe.cs
git -C "$TMP/agent-1" commit -qm "plant caller-info attribute"
pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == 11 ]] || fail "caller-probe submit should run tests (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "caller-probe full submit should arm proof"
sed -i 's/reworded comment/reworded again/' "$TMP/agent-1/code.cs"
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "comment-only edit under caller-info"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 12 ]] || fail "comment-only edit under caller-info must run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-ScopeType Smoke'; then fail "caller-info must disable the smoke downgrade"; fi
[[ "$(gh_merges)" == 9 ]] || fail "caller-info merge should complete (got $(gh_merges))"
[[ "$(scope_field kind)" == "full-run" ]] || fail "caller-info gate run should record full-run provenance"

# The markdown fast path is unaffected by caller-info attributes.
echo changelog > "$TMP/agent-1/CHANGES.md"
git -C "$TMP/agent-1" add CHANGES.md
git -C "$TMP/agent-1" commit -qm "md under caller-info"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 12 ]] || fail "md-only delta must stay run-free under caller-info (got $(runner_runs))"
[[ "$(gh_merges)" == 10 ]] || fail "md-only merge under caller-info should complete (got $(gh_merges))"
[[ "$(scope_field kind)" == "inherit-doc" ]] || fail "md-only delta should extend proof"

# Non-markdown files under doc/ are not inert.
mkdir -p "$TMP/agent-1/doc"
echo "Write-Host tool" > "$TMP/agent-1/doc/tool.ps1"
git -C "$TMP/agent-1" add doc/tool.ps1
git -C "$TMP/agent-1" commit -qm "script under doc/"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == 13 ]] || fail "doc/tool.ps1 must force the full suite (got $(runner_runs))"
[[ "$(gh_merges)" == 11 ]] || fail "doc-script merge should complete (got $(gh_merges))"
[[ "$(scope_field kind)" == "full-run" ]] || fail "doc-script gate run should record full-run provenance"

echo "PASS: merge gate tested-tree proof + scope-aware proof + inert fast path"
