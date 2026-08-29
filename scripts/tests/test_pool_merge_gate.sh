#!/usr/bin/env bash
set -euo pipefail

# Regression for the merge gate's proof chain: proof binds to the landing tree,
# failed runs stop the PR path, inert deltas skip the full suite, and the phase
# journal records the ladder for both outcomes.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
# Via bash: the pool script is tracked non-executable (mode 100644), so direct exec fails on Unix checkouts.
pool() { bash "$POOL" "$@"; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export RUNNER_LOG="$TMP/runner.log"
export RUNNER_EXIT_FILE="$TMP/runner.exit"
export RESHARPER_LOG="$TMP/resharper.log"
export RESHARPER_EXIT_FILE="$TMP/resharper.exit"
export GH_MERGE_LOG="$TMP/gh-merge.log"
: > "$RUNNER_LOG"
: > "$RESHARPER_LOG"
: > "$GH_MERGE_LOG"
echo 0 > "$RUNNER_EXIT_FILE"
echo 0 > "$RESHARPER_EXIT_FILE"

export STUB_BIN="$TMP/bin"
mkdir -p "$STUB_BIN"

export GOLDEN_SUMMARY="$SCRIPT_DIR/fixtures/full-coverage-summary.json"

# Stub only the test runner; other powershell invocations (inert_diff.ps1) fall through to the real binary. The stub derives the summary JSON from its args so the coverage predicate sees full vs scoped runs.
cat > "$STUB_BIN/powershell.exe" <<'EOF'
#!/usr/bin/env bash
if [[ "$*" == *resharper_ratchet.ps1* ]]; then
  echo "run $*" >> "$RESHARPER_LOG"
  exit "$(cat "$RESHARPER_EXIT_FILE")"
fi
if [[ "$*" != *unity_test_agent.ps1* ]]; then
  real="$(type -pa powershell.exe | grep -vF "$STUB_BIN" | head -n 1)"
  [[ -n "$real" ]] || { echo "stub: no real powershell.exe for: $*" >&2; exit 1; }
  exec "$real" "$@"
fi
echo "run $*" >> "$RUNNER_LOG"
if [[ "${RUNNER_MUTATE_TRACKED:-0}" == 1 ]]; then
  sed -i 's/UNITY_POST_PROCESSING_STACK_V2$/UNITY_POST_PROCESSING_STACK_V2;SENTIS_ANALYTICS_ENABLED/' src/Asteroids3D/ProjectSettings/ProjectSettings.asset
fi
mode=Both scope=Workspace filter="" category="" assemblies="" transport_line="" outdir=""
args=("$@")
for ((i = 0; i < ${#args[@]} - 1; i++)); do
  case "${args[i]}" in
    -Mode) mode="${args[i+1]}" ;;
    -ScopeType) scope="${args[i+1]}" ;;
    -TestFilter) filter="${args[i+1]}" ;;
    -TestCategory) category="${args[i+1]}" ;;
    -AssemblyNames) assemblies="${args[i+1]}" ;;
    -OutDir) outdir="${args[i+1]}" ;;
  esac
done
[[ -n "$outdir" ]] || { echo "stub: pool must pass -OutDir" >&2; exit 1; }
if [[ " $* " == *" -Routed "* ]]; then transport_line='"transport": "routed",'; fi
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
mkdir -p "$outdir"
# Golden mode replays a real unity_test_agent.ps1 summary, so the gate's coverage predicate is tested against the runner's actual schema.
if [[ -n "${RUNNER_GOLDEN_SUMMARY:-}" ]]; then
  sed "s#__PROJECT_PATH__#$(printf '%s' "$project" | sed 's/\\/\\\\/g')#" "$GOLDEN_SUMMARY" > "$outdir/latest-summary.json"
  exit "$ec"
fi
cat > "$outdir/latest-summary.json" <<JSON
{
  $transport_line
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
resharper_runs() { grep -c '^run' "$RESHARPER_LOG" || true; }
gh_merges() { grep -c 'squash' "$GH_MERGE_LOG" || true; }
slot_tree() { git -C "$TMP/agent-1" rev-parse 'agent-1^{tree}'; }
recorded_tree() { cat "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_tree" 2>/dev/null || true; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
git -C "$TMP/primary" config user.email pool-test@example.test
git -C "$TMP/primary" config user.name "Pool Test"
echo base > "$TMP/primary/file.txt"
printf 'results/\n' > "$TMP/primary/.gitignore"
mkdir -p "$TMP/primary/.config" "$TMP/primary/scripts"
mkdir -p "$TMP/primary/src/Asteroids3D/ProjectSettings"
printf '{}\n' > "$TMP/primary/.config/dotnet-tools.json"
printf '    Standalone: UNITY_POST_PROCESSING_STACK_V2\n' > "$TMP/primary/src/Asteroids3D/ProjectSettings/ProjectSettings.asset"
for file in agent_worktree_pool.sh resharper-unity.DotSettings resharper_ratchet.ps1 sync_unity_solution.ps1; do
  printf 'stub\n' > "$TMP/primary/scripts/$file"
done
git -C "$TMP/primary" add file.txt .gitignore .config scripts src/Asteroids3D/ProjectSettings/ProjectSettings.asset
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
cd "$TMP/primary"

pool acquire merge-gate-test agent-1 >/dev/null

echo change > "$TMP/agent-1/feature.txt"
git -C "$TMP/agent-1" add feature.txt
git -C "$TMP/agent-1" commit -qm feature

runs_before="$(runner_runs)"
if pool submit agent-1 origin/main --title "test PR" --body "test body" --bogus >/dev/null 2>&1; then fail "submit must reject unknown --flags before the -- separator"; fi
[[ "$(runner_runs)" == "$runs_before" ]] || fail "rejected submit must not start a test run (got $(runner_runs))"

# The golden summary is a real unity_test_agent.ps1 payload: proof arming here IS the schema assertion.
runs_before="$(runner_runs)"
resharper_before="$(resharper_runs)"
RUNNER_GOLDEN_SUMMARY=1 pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "golden-summary submit should run tests once (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "the golden runner summary must arm merge-grade proof"

echo tweak >> "$TMP/agent-1/feature.txt"
git -C "$TMP/agent-1" add feature.txt
git -C "$TMP/agent-1" commit -qm "feature tweak"

export RUNNER_MUTATE_TRACKED=1
runs_before="$(runner_runs)"
resharper_before="$(resharper_runs)"
pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
unset RUNNER_MUTATE_TRACKED
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "submit should run tests once (got $(runner_runs))"
[[ "$(resharper_runs)" == $((resharper_before + 1)) ]] || fail "submit should run the ReSharper ratchet once (got $(resharper_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "submit should record the tested tree"
[[ -z "$(git -C "$TMP/agent-1" status --porcelain)" ]] || fail "submit should restore tracked Unity test mutations"
[[ "$(cat "$TMP/agent-1/src/Asteroids3D/ProjectSettings/ProjectSettings.asset")" == "    Standalone: UNITY_POST_PROCESSING_STACK_V2" ]] \
  || fail "submit should restore the known analytics define churn"

echo moved > "$TMP/primary/main.txt"
git -C "$TMP/primary" add main.txt
git -C "$TMP/primary" commit -qm "base moves"
git -C "$TMP/primary" push -q origin main

echo 1 > "$RUNNER_EXIT_FILE"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null 2>&1 && fail "merge must fail when the post-integration test run fails"
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "failed merge attempt should have run tests (got $(runner_runs))"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "failed test run must not reach gh pr merge"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "failed run must not record the merged tree as tested"

echo 0 > "$RUNNER_EXIT_FILE"
runs_before="$(runner_runs)"
resharper_before="$(resharper_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "retry after failed run must re-run tests, not trust the base-merge commit (got $(runner_runs))"
[[ "$(resharper_runs)" == $((resharper_before + 1)) ]] || fail "passing landing tree should run the ReSharper ratchet (got $(resharper_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "retry with passing tests should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "passing run should record the merged tree"

runs_before="$(runner_runs)"
resharper_before="$(resharper_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "proven tree should skip the re-run (got $(runner_runs))"
[[ "$(resharper_runs)" == "$resharper_before" ]] || fail "proven ReSharper tree/base pair should skip the re-run (got $(resharper_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "fast path should still merge (got $(gh_merges))"

last_run_line() { grep '^run' "$RUNNER_LOG" | tail -n 1; }
scope_field() { sed -n "s/^$1=//p" "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_scope" 2>/dev/null | head -n 1; }

# Scoped submit records NO proof; the gate then runs the full suite.
echo change2 > "$TMP/agent-1/feature2.txt"
git -C "$TMP/agent-1" add feature2.txt
git -C "$TMP/agent-1" commit -qm "feature 2"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool submit agent-1 origin/main --title "test PR" --body "test body" -- -Mode EditMode -ScopeType Feature -ScopeName camera >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "scoped submit should still run tests (got $(runner_runs))"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "scoped submit must not record merge-grade proof"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 2)) ]] || fail "merge after scoped submit must run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-ScopeType'; then fail "gate run after scoped submit should be the unfiltered full suite"; fi
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "merge after gate full run should merge (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "gate full run should record proof"

# revise --no-test pushes but runs nothing and records no proof.
echo hygiene >> "$TMP/agent-1/feature2.txt"
git -C "$TMP/agent-1" add feature2.txt
git -C "$TMP/agent-1" commit -qm "hygiene edit"
proof_before="$(recorded_tree)"
runs_before="$(runner_runs)"
pool revise agent-1 --no-test >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "revise --no-test must not run tests (got $(runner_runs))"
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
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "full submit should run tests (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "full submit should record proof"
full_tree="$(recorded_tree)"
runs_before="$(runner_runs)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "merge on fully-proven tree must skip the re-run (got $(runner_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "proven-tree merge should still merge (got $(gh_merges))"

# md/doc-only delta after full proof: proof extends with NO runner invocation.
echo notes > "$TMP/agent-1/NOTES.md"
mkdir -p "$TMP/agent-1/doc"
echo design > "$TMP/agent-1/doc/design.md"
git -C "$TMP/agent-1" add NOTES.md doc/design.md
git -C "$TMP/agent-1" commit -qm "docs only"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "docs-only delta must not invoke the runner (got $(runner_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "docs-only delta should merge (got $(gh_merges))"
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
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "comment-only delta should invoke the runner exactly once (got $(runner_runs))"
last_run_line | grep -q -- '-Mode EditMode' || fail "comment-only refresh must run EditMode"
last_run_line | grep -q -- '-ScopeType Smoke' || fail "comment-only refresh must run the Smoke scope"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "comment-only delta should merge (got $(gh_merges))"
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
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "code delta must run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-ScopeType Smoke'; then fail "code delta must not downgrade to the smoke refresh"; fi
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "code delta merge should complete (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "code delta gate run should record proof"
[[ "$(scope_field kind)" == "full-run" ]] || fail "code delta gate run should re-anchor as full-run"

# Dirty worktree: proof-bearing runs refuse to start (the runner tests the working tree, not HEAD).
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
echo "class Stray { }" > "$TMP/agent-1/stray.cs"
if pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null 2>&1; then fail "submit must refuse a dirty worktree"; fi
[[ "$(runner_runs)" == "$runs_before" ]] || fail "dirty submit must not invoke the runner (got $(runner_runs))"
if pool merge agent-1 >/dev/null 2>&1; then fail "merge must refuse a dirty worktree"; fi
[[ "$(runner_runs)" == "$runs_before" ]] || fail "dirty merge must not invoke the runner (got $(runner_runs))"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "dirty merge must not reach gh pr merge (got $(gh_merges))"
rm "$TMP/agent-1/stray.cs"

# A summary from the wrong project must not arm proof.
echo change3 > "$TMP/agent-1/feature3.txt"
git -C "$TMP/agent-1" add feature3.txt
git -C "$TMP/agent-1" commit -qm "feature 3"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
STUB_PROJECT_PATH="/definitely/not/this/project" pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "wrong-project submit should still run tests (got $(runner_runs))"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "wrong-project summary must not arm proof"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 2)) ]] || fail "merge after wrong-project submit must re-run the full suite (got $(runner_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "wrong-project recovery merge should complete (got $(gh_merges))"
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
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "caller-probe submit should run tests (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "caller-probe full submit should arm proof"
sed -i 's/reworded comment/reworded again/' "$TMP/agent-1/code.cs"
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "comment-only edit under caller-info"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 2)) ]] || fail "comment-only edit under caller-info must run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-ScopeType Smoke'; then fail "caller-info must disable the smoke downgrade"; fi
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "caller-info merge should complete (got $(gh_merges))"
[[ "$(scope_field kind)" == "full-run" ]] || fail "caller-info gate run should record full-run provenance"

# The markdown fast path is unaffected by caller-info attributes.
echo changelog > "$TMP/agent-1/CHANGES.md"
git -C "$TMP/agent-1" add CHANGES.md
git -C "$TMP/agent-1" commit -qm "md under caller-info"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "md-only delta must stay run-free under caller-info (got $(runner_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "md-only merge under caller-info should complete (got $(gh_merges))"
[[ "$(scope_field kind)" == "inherit-doc" ]] || fail "md-only delta should extend proof"

# Non-markdown files under doc/ are not inert.
mkdir -p "$TMP/agent-1/doc"
echo "Write-Host tool" > "$TMP/agent-1/doc/tool.ps1"
git -C "$TMP/agent-1" add doc/tool.ps1
git -C "$TMP/agent-1" commit -qm "script under doc/"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "doc/tool.ps1 must force the full suite (got $(runner_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "doc-script merge should complete (got $(gh_merges))"
[[ "$(scope_field kind)" == "full-run" ]] || fail "doc-script gate run should record full-run provenance"

# A ReSharper failure blocks submit before the task branch is pushed.
mkdir -p "$TMP/agent-1/src/Asteroids3D/Assets/Scripts"
echo "class RatchetFailure { }" > "$TMP/agent-1/src/Asteroids3D/Assets/Scripts/RatchetFailure.cs"
git -C "$TMP/agent-1" add src/Asteroids3D/Assets/Scripts/RatchetFailure.cs
git -C "$TMP/agent-1" commit -qm "ratchet failure"
remote_before="$(git -C "$TMP/origin.git" rev-parse refs/heads/task/merge-gate-test)"
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
echo 1 > "$RESHARPER_EXIT_FILE"
if pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null 2>&1; then fail "submit must fail when the ReSharper ratchet fails"; fi
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "ReSharper-failing submit should still run tests first (got $(runner_runs))"
[[ "$(git -C "$TMP/origin.git" rev-parse refs/heads/task/merge-gate-test)" == "$remote_before" ]] \
  || fail "ReSharper-failing submit must not push the task branch"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "ReSharper-failing submit must not reach gh pr merge"

# --- merge gate journal ------------------------------------------------------
journal_for() { ls -1t "$TMP/primary/.worktree-pool/merge-runs/agent-1-"*.jsonl 2>/dev/null | head -n 1; }
phase_order() { sed -n 's/.*"event":"phase-start","phase":"\([^"]*\)".*/\1/p' "$(journal_for)" | tr '\n' ' '; }
run_status() { sed -n 's/.*"event":"run-end".*"status":"\([^"]*\)".*/\1/p' "$(journal_for)"; }

# The failing-ReSharper submit above left the ratchet armed; disarm for a clean merge.
echo 0 > "$RESHARPER_EXIT_FILE"
pool merge agent-1 >/dev/null
[[ -n "$(journal_for)" ]] || fail "merge must write a journal"
[[ "$(phase_order)" == "preflight fetch base-merge proof-check tests resharper push gh-merge " ]] \
  || fail "journal should record the full phase ladder (got '$(phase_order)')"
[[ "$(run_status)" == "merged" ]] || fail "successful merge should close the journal as merged (got $(run_status))"

# Every phase-end carries a duration and its budget — that pairing IS the profiling data.
ends="$(grep -c '"event":"phase-end"' "$(journal_for)")"
[[ "$ends" == 8 ]] || fail "every started phase should also end (got $ends)"
grep -q '"phase":"tests","sec":[0-9]*,"status":"ok","budget":1200' "$(journal_for)" \
  || fail "phase-end should carry sec + status + budget"

# A failed merge must close the open phase rather than leave it dangling, and must
# name the phase that died.
echo journal-fail > "$TMP/agent-1/journal_fail.txt"
git -C "$TMP/agent-1" add journal_fail.txt
git -C "$TMP/agent-1" commit -qm "journal failure case"
echo 1 > "$RUNNER_EXIT_FILE"
pool merge agent-1 >/dev/null 2>&1 && fail "merge with a failing run must fail"
echo 0 > "$RUNNER_EXIT_FILE"
[[ "$(run_status)" == "failed" ]] || fail "failed merge should close the journal as failed (got $(run_status))"
grep -q '"event":"phase-end","phase":"tests".*"status":"failed"' "$(journal_for)" \
  || fail "the phase that died should be marked failed"

# merge-progress reads a run it did not start, and --oneline stays silent once the
# run is over (the dashboard shows in-flight merges only).
progress="$(pool merge-progress agent-1)"
[[ "$progress" == *"XX  tests"* ]] || fail "merge-progress should surface the failed phase (got: $progress)"
[[ "$progress" == *"failed in"* ]] || fail "merge-progress should report the run outcome"
[[ -z "$(pool merge-progress agent-1 --oneline)" ]] || fail "--oneline must print nothing for a finished run"

# With the lock dir gone (post-finalize), the newest run file still resolves.
rm -f "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/merge_run"
[[ "$(pool merge-progress agent-1)" == *"XX  tests"* ]] || fail "merge-progress should fall back to the newest run file"

# An unknown slot is a clean no-op, not an error.
pool merge-progress agent-nonexistent | grep -q "no merge run recorded" \
  || fail "merge-progress on a slot with no runs should say so"

# A routed (warm-editor) summary must not arm proof even when full-shaped; the gate re-runs cold.
runs_before="$(runner_runs)"
merges_before="$(gh_merges)"
echo routed-change > "$TMP/agent-1/routed_feature.txt"
git -C "$TMP/agent-1" add routed_feature.txt
git -C "$TMP/agent-1" commit -qm "routed feature"
pool submit agent-1 origin/main --title "test PR" --body "test body" -- -Routed >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "routed submit should run tests once (got $(runner_runs), had $runs_before)"
[[ "$(recorded_tree)" != "$(slot_tree)" ]] || fail "a transport=routed summary must not arm merge proof"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == $((runs_before + 2)) ]] || fail "merge after routed submit must re-run the full suite (got $(runner_runs))"
if last_run_line | grep -q -- '-Routed'; then fail "the gate re-run must be cold (no -Routed)"; fi
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "routed-recovery merge should complete (got $(gh_merges))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "the gate cold run should arm proof after a routed summary"

# A landing diff touching scripts/ runs the script suite; a red suite blocks the merge.
export PROBE_MARKER="$TMP/script-suite-runs"
: > "$PROBE_MARKER"
export PROBE_EXIT_FILE="$TMP/probe.exit"
echo 0 > "$PROBE_EXIT_FILE"
mkdir -p "$TMP/agent-1/scripts/tests"
cat > "$TMP/agent-1/scripts/tests/test_probe.sh" <<'PROBE'
#!/usr/bin/env bash
echo probe >> "$PROBE_MARKER"
exit "$(cat "$PROBE_EXIT_FILE")"
PROBE
git -C "$TMP/agent-1" add scripts/tests/test_probe.sh
git -C "$TMP/agent-1" commit -qm "add script test probe"
merges_before="$(gh_merges)"
echo 1 > "$PROBE_EXIT_FILE"
pool merge agent-1 >/dev/null 2>&1 && fail "a red script suite must fail the merge"
[[ "$(grep -c probe "$PROBE_MARKER")" -ge 1 ]] || fail "scripts/ delta must trigger the script suite"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "red script suite must not reach gh pr merge"
grep -q '"phase":"script-tests".*"status":"failed"' "$(journal_for)" \
  || fail "the journal should name script-tests as the phase that died"

# The non-hermetic skiplist is visible in gate output, so de-listing it is a deliberate act.
cat > "$TMP/agent-1/scripts/tests/test_unity_access.ps1" <<'NONHERMETIC'
exit 1
NONHERMETIC
git -C "$TMP/agent-1" add scripts/tests/test_unity_access.ps1
git -C "$TMP/agent-1" commit -qm "add non-hermetic suite member"
echo 0 > "$PROBE_EXIT_FILE"
pool merge agent-1 > "$TMP/merge.out"
grep -q "SKIP: test_unity_access.ps1 — non-hermetic" "$TMP/merge.out" \
  || fail "the gate must print the non-hermetic SKIP line"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "green script suite should merge (got $(gh_merges))"

echo "PASS: merge gate tested-tree proof + ReSharper proof + scope-aware proof + inert fast path + routed-summary refusal + phase journal + scripts/ suite trigger"
