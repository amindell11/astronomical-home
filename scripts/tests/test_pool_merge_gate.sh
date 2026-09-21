#!/usr/bin/env bash
set -euo pipefail

# Regression for the merge gate's proof chain: proof binds to the landing tree,
# failed runs stop the PR path, inert deltas skip the full suite, the phase
# journal records the ladder for both outcomes, remote proof is accepted only
# from a green merge-proof/headless status stamping the landing tree, and an owed run with no
# named producer goes local or hosted on the memory admission verdict.

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
export GH_DISPATCH_LOG="$TMP/gh-dispatch.log"
export GH_STATUS_SEQ="$TMP/gh-status.seq"
export GH_RUN_SEQ="$TMP/gh-run.seq"
export ADMISSION_SEQ="$TMP/admission.seq"
export ADMISSION_LOG="$TMP/admission.log"
: > "$ADMISSION_LOG"
# Every local-run case below models an admitted boot; the fallback block rescripts it.
echo boot_admitted > "$ADMISSION_SEQ"
: > "$RUNNER_LOG"
: > "$RESHARPER_LOG"
: > "$GH_MERGE_LOG"
: > "$GH_DISPATCH_LOG"
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
# Memory admission answers from a scripted sequence (one line per query, the last sticks) and fails CLOSED when none is set.
if [[ "$*" == *BootAdmission* ]]; then
  echo "query" >> "$ADMISSION_LOG"
  [[ -s "$ADMISSION_SEQ" ]] || { echo "stub: no admission answer scripted" >&2; exit 97; }
  answer="$(head -n 1 "$ADMISSION_SEQ")"
  [[ "$(wc -l < "$ADMISSION_SEQ")" -le 1 ]] || sed -i 1d "$ADMISSION_SEQ"
  [[ "$answer" != query_error ]] || { echo "coordinator stub: state file locked" >&2; exit 1; }
  echo "$answer"
  exit 0
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
# The runner owns the coverage verdict, so the stub stamps it the way unity_test_agent.ps1 does.
coverage_verdict=full
coverage_reason="mode=Both scopeType=Workspace excludeCategory=RequiresGraphics"
if [[ -n "$transport_line" ]]; then coverage_verdict=partial; coverage_reason="transport=routed (warm-editor run; merge-grade proof requires a cold-process run)"
elif [[ "$status" != passed ]]; then coverage_verdict=partial; coverage_reason="status=$status"
elif [[ "$mode" != Both ]]; then coverage_verdict=partial; coverage_reason="mode=$mode"
elif [[ "$scope" != Workspace ]]; then coverage_verdict=partial; coverage_reason="scopeType=$scope"
elif [[ -n "$filter" ]]; then coverage_verdict=partial; coverage_reason="testFilter set"
elif [[ -n "$category" ]]; then coverage_verdict=partial; coverage_reason="testCategory set"
elif [[ -n "$assemblies" ]]; then coverage_verdict=partial; coverage_reason="assemblyNames set"
fi
if [[ "${RUNNER_OMIT_COVERAGE:-0}" == 1 ]]; then coverage_block=""; else coverage_block=",
  \"coverage\": { \"verdict\": \"$coverage_verdict\", \"reason\": \"$coverage_reason\" }"; fi
# Golden mode replays a real unity_test_agent.ps1 summary, so the gate's coverage read is tested against the runner's actual schema.
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
  }$coverage_block
}
JSON
exit "$ec"
EOF
chmod +x "$STUB_BIN/powershell.exe"

# Fails CLOSED: a call the stub does not model is an error, never an empty answer the gate could read as "no status".
# The *_SEQ files script successive answers (already --jq shaped), one line per call; the last line sticks.
cat > "$STUB_BIN/gh" <<'EOF'
#!/usr/bin/env bash
next_answer() {
  local file="$1" default="$2"
  if [[ ! -s "$file" ]]; then printf '%s\n' "$default"; return 0; fi
  head -n 1 "$file"
  [[ "$(wc -l < "$file")" -le 1 ]] || sed -i 1d "$file"
}
case "$1 $2" in
  "pr list") [[ "$*" == *"--json number"* ]] && echo 7 ;;
  "pr create") echo "https://example.test/pr/7" ;;
  "pr merge") echo "$*" >> "$GH_MERGE_LOG" ;;
  "api repos/pool-test/repo/commits/"*)
    [[ "${GH_API_FAIL:-0}" != 1 ]] || { echo "gh stub: HTTP 502" >&2; exit 1; }
    next_answer "$GH_STATUS_SEQ" $'absent\037\037' ;;
  "run list") next_answer "$GH_RUN_SEQ" $'none\t' ;;
  "workflow run") echo "$*" >> "$GH_DISPATCH_LOG" ;;
  *) echo "gh stub: unmodelled call: $*" >&2; exit 97 ;;
esac
exit 0
EOF
chmod +x "$STUB_BIN/gh"

export PATH="$STUB_BIN:$PATH"
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"

fail() { echo "FAIL: $1" >&2; exit 1; }
runner_runs() { grep -c '^run' "$RUNNER_LOG" || true; }
resharper_runs() { grep -c '^run' "$RESHARPER_LOG" || true; }
admission_queries() { grep -c '^query' "$ADMISSION_LOG" || true; }
admission() { printf '%s\n' "$@" > "$ADMISSION_SEQ"; }
gh_merges() { grep -c 'squash' "$GH_MERGE_LOG" || true; }
slot_tree() { git -C "$TMP/agent-1" rev-parse 'agent-1^{tree}'; }
recorded_tree() { cat "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_tree" 2>/dev/null || true; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
# repo_slug reads a GitHub URL off origin; insteadOf keeps the transport on the local bare repo.
git -C "$TMP/primary" config remote.origin.url "https://github.com/pool-test/repo.git"
git -C "$TMP/primary" config "url.$TMP/origin.git.insteadOf" "https://github.com/pool-test/repo.git"
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
RUNNER_GOLDEN_SUMMARY=1 pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "golden-summary submit should run tests once (got $(runner_runs))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "the golden runner summary must arm merge-grade proof"

# Fail-closed on the one trusted field: an otherwise full-shaped summary with no coverage stamp
# (older run, foreign producer) is partial, and the gate re-tests.
rm -rf "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_tree" "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_scope"
RUNNER_OMIT_COVERAGE=1 pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ -z "$(recorded_tree)" ]] || fail "an unstamped summary must not arm merge proof"
pool submit agent-1 origin/main --title "test PR" --body "test body" >/dev/null
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "a stamped full summary must arm proof after an unstamped one"

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
queries_before="$(admission_queries)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "proven tree should skip the re-run (got $(runner_runs))"
[[ "$(admission_queries)" == "$queries_before" ]] || fail "a proven tree owes no run, so memory admission must not be asked"
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
queries_before="$(admission_queries)"
pool merge agent-1 >/dev/null
[[ "$(runner_runs)" == "$runs_before" ]] || fail "docs-only delta must not invoke the runner (got $(runner_runs))"
[[ "$(admission_queries)" == "$queries_before" ]] || fail "a docs-only delta owes no run, so memory admission must not be asked"
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
[[ "$(phase_order)" == "preflight fetch base-merge proof-check tests resharper push base-recheck gh-merge " ]] \
  || fail "journal should record the full phase ladder (got '$(phase_order)')"
[[ "$(run_status)" == "merged" ]] || fail "successful merge should close the journal as merged (got $(run_status))"

# Every phase-end carries a duration and its budget — that pairing IS the profiling data.
ends="$(grep -c '"event":"phase-end"' "$(journal_for)")"
[[ "$ends" == 9 ]] || fail "every started phase should also end (got $ends)"
grep -q '"phase":"tests","sec":[0-9]*,"status":"ok","budget":480' "$(journal_for)" \
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

# The skiplist is empty since #454: the coordinator suite is an ordinary suite member now, so a red
# one fails the merge instead of printing a SKIP line and passing.
cat > "$TMP/agent-1/scripts/tests/test_unity_access.ps1" <<'COORDINATOR'
exit 1
COORDINATOR
git -C "$TMP/agent-1" add scripts/tests/test_unity_access.ps1
git -C "$TMP/agent-1" commit -qm "add red coordinator suite member"
echo 0 > "$PROBE_EXIT_FILE"
pool merge agent-1 > "$TMP/merge.out" 2>&1 && fail "a red test_unity_access.ps1 must fail the merge"
grep -q "SKIP: test_unity_access.ps1" "$TMP/merge.out" \
  && fail "test_unity_access.ps1 must no longer be skipped"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "a red coordinator suite must not reach gh pr merge"

git -C "$TMP/agent-1" rm -q scripts/tests/test_unity_access.ps1
git -C "$TMP/agent-1" commit -qm "drop red coordinator suite member"
pool merge agent-1 > "$TMP/merge.out"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "green script suite should merge (got $(gh_merges))"

# --- remote proof ------------------------------------------------------------
TASK_BRANCH="task/merge-gate-test"
RUN_URL="https://github.com/pool-test/repo/actions/runs"
slot_sha() { git -C "$TMP/agent-1" rev-parse agent-1; }
remote_tip() { git -C "$TMP/primary" ls-remote origin "refs/heads/$TASK_BRANCH" | cut -f1; }
dispatches() { grep -c 'workflow run' "$GH_DISPATCH_LOG" || true; }
proof_kind() { sed -n 's/^kind=//p' "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/tested_scope"; }
new_commit() {
  echo "$1" > "$TMP/agent-1/$1.txt"
  git -C "$TMP/agent-1" add "$1.txt"
  git -C "$TMP/agent-1" commit -qm "$1"
}
push_slot() { git -C "$TMP/agent-1" push -q origin "agent-1:refs/heads/$TASK_BRANCH"; }
statuses() { printf '%s\n' "$@" > "$GH_STATUS_SEQ"; }
runs() { printf '%s\n' "$@" > "$GH_RUN_SEQ"; }
green() { printf 'success\037tree=%s total=5 passed=5 skipped=0\037%s/%s' "$(slot_tree)" "$RUN_URL" "$1"; }
# Refusals must come from the liveness rules, not from minutes of real waiting.
gate_merge() {
  WORKTREE_POOL_REMOTE_POLL_SECONDS=1 WORKTREE_POOL_REMOTE_NO_RUN_SECONDS="${NO_RUN:-120}" \
    WORKTREE_POOL_REMOTE_QUEUED_SECONDS="${QUEUED:-180}" pool merge agent-1 "$@" > "$TMP/merge.out" 2>&1
}
remote_merge() { gate_merge --remote; }
expect_output() { grep -q -- "$1" "$TMP/merge.out" || { cat "$TMP/merge.out" >&2; fail "$2"; }; }

# A green status already on the landing commit is proof: no run of either kind.
new_commit remote-existing
push_slot
statuses "$(green 41)"
runs_before="$(runner_runs)"; merges_before="$(gh_merges)"; queries_before="$(admission_queries)"
pool merge agent-1 > "$TMP/merge.out" 2>&1 || { cat "$TMP/merge.out" >&2; fail "existing remote proof should merge"; }
[[ "$(admission_queries)" == "$queries_before" ]] || fail "existing remote proof owes no run, so memory admission must not be asked"
[[ "$(runner_runs)" == "$runs_before" ]] || fail "existing remote proof must skip the local run (got $(runner_runs))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "existing remote proof should reach gh pr merge"
[[ "$(proof_kind)" == "remote-run" ]] || fail "remote proof must be recorded as kind=remote-run (got $(proof_kind))"
[[ "$(recorded_tree)" == "$(slot_tree)" ]] || fail "remote proof must record the landing tree"
expect_output "remote proof, skipping the run" "the gate should say it used remote proof"
[[ "$(phase_order)" == *"proof-check tests resharper"* ]] || fail "a skipped run keeps the default ladder (got '$(phase_order)')"

# Fail closed, default path: each unusable status names its reason and the gate turns to the local run.
# The runner is red here so each case stops at the tests phase; one green fallback closes the block.
expect_local_fallback() {
  local reason="$1" label="$2"
  runs_before="$(runner_runs)"
  if pool merge agent-1 > "$TMP/merge.out" 2>&1; then fail "$label: fixture runner is red, merge must fail"; fi
  expect_output "$reason" "$label: the gate must say why there is no remote proof"
  [[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "$label: must fall back to one local run (got $(runner_runs))"
}
new_commit remote-fail-closed
push_slot
merges_before="$(gh_merges)"
echo 1 > "$RUNNER_EXIT_FILE"
statuses "$(printf 'success\037tree=%040d total=5 passed=5 skipped=0\037%s/42' 0 "$RUN_URL")"
expect_local_fallback "stamps tree 0000000000000000000000000000000000000000, the landing tree is $(slot_tree)" "tree mismatch"
statuses "$(printf 'success\037all green, trust me\037%s/42' "$RUN_URL")"
expect_local_fallback "names no tree ('all green, trust me')" "unparsable trailer"
statuses "$(printf 'neutral\037whatever\037%s/42' "$RUN_URL")"
expect_local_fallback "is 'neutral', not success" "unknown state"
statuses $'absent\037\037'
expect_local_fallback "is 'absent', not success" "absent status"
statuses "$(green 42)"
GH_API_FAIL=1 expect_local_fallback "could not read the merge-proof/headless status" "gh error"
new_commit remote-unpushed
expect_local_fallback "is not on GitHub yet" "unpushed landing commit"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "no fail-closed case may reach gh pr merge"
echo 0 > "$RUNNER_EXIT_FILE"
pool merge agent-1 > "$TMP/merge.out" 2>&1 || { cat "$TMP/merge.out" >&2; fail "the local run should still merge"; }
[[ "$(proof_kind)" == "full-run" ]] || fail "fallback proof must come from the local run (got $(proof_kind))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "fallback should merge on local proof"

# --remote takes no runner args.
if pool merge agent-1 --remote -- -Mode EditMode > "$TMP/merge.out" 2>&1; then fail "--remote must refuse test-runner args"; fi
expect_output "--remote takes no test-runner args" "--remote arg refusal must say why"

# --remote, red verdict: refuse at once and name the rerun recovery.
new_commit remote-red
push_slot
statuses "$(printf 'failure\037headless suite failed - see run\037%s/43' "$RUN_URL")"
runs "$(printf 'completed\t43')"
runs_before="$(runner_runs)"; merges_before="$(gh_merges)"; dispatches_before="$(dispatches)"
if remote_merge; then fail "--remote must refuse a failure status"; fi
expect_output "is 'failure' (headless suite failed - see run)" "a red verdict must be quoted"
expect_output "gh run rerun 43" "a red verdict must name the rerun recovery"
[[ "$(dispatches)" == "$dispatches_before" ]] || fail "a red verdict must not dispatch a new run"
[[ "$(runner_runs)" == "$runs_before" ]] || fail "--remote must never run the local suite"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "a red verdict must not reach gh pr merge"
grep -q '"phase":"remote-proof".*"status":"failed"' "$(journal_for)" || fail "the journal should name remote-proof as the phase that died"

# An empty description must not shift the run URL out of its field.
statuses "$(printf 'failure\037\037%s/43' "$RUN_URL")"
runs $'none\t'
if remote_merge; then fail "--remote must refuse a failure status with no description"; fi
expect_output "gh run rerun 43" "the run id must survive an empty description"
expect_output "Run: $RUN_URL/43" "the run URL must survive an empty description"
runs "$(printf 'completed\t43')"

statuses "$(printf 'error\037headless suite cancelled\037%s/43' "$RUN_URL")"
if remote_merge; then fail "--remote must refuse an error status"; fi
expect_output "is 'error' (headless suite cancelled)" "an error verdict must be quoted"

# After the rerun: pending with a live run is waited on, not re-dispatched.
statuses "$(printf 'pending\037headless suite running\037%s/43' "$RUN_URL")" "$(printf 'pending\037headless suite running\037%s/43' "$RUN_URL")" "$(green 43)"
runs "$(printf 'in_progress\t43')"
merges_before="$(gh_merges)"; queries_before="$(admission_queries)"
remote_merge || { cat "$TMP/merge.out" >&2; fail "--remote should merge once the rerun goes green"; }
[[ "$(dispatches)" == "$dispatches_before" ]] || fail "a live run must not be re-dispatched"
[[ "$(runner_runs)" == "$runs_before" ]] || fail "--remote must never run the local suite"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "green rerun should reach gh pr merge"
[[ "$(proof_kind)" == "remote-run" ]] || fail "--remote proof must be kind=remote-run (got $(proof_kind))"
[[ "$(phase_order)" == "preflight fetch base-merge proof-check resharper remote-proof script-tests push base-recheck gh-merge " ]] \
  || fail "--remote runs the ratchet before remote-proof and opens it once (got '$(phase_order)')"
[[ "$(admission_queries)" == "$queries_before" ]] || fail "--remote names the producer, so memory admission must not be asked"

# --remote with the landing commit not on GitHub: the gate pushes it, and the push is the trigger.
new_commit remote-push
statuses "$(printf 'pending\037headless suite running\037%s/44' "$RUN_URL")" "$(green 44)"
runs "$(printf 'in_progress\t44')"
merges_before="$(gh_merges)"; resharper_before="$(resharper_runs)"
[[ "$(remote_tip)" != "$(slot_sha)" ]] || fail "fixture: the landing commit should start unpushed"
remote_merge || { cat "$TMP/merge.out" >&2; fail "--remote should push, wait, and merge"; }
[[ "$(resharper_runs)" == $((resharper_before + 1)) ]] || fail "--remote must invoke the ratchet exactly once (got $(resharper_runs))"
[[ "$(remote_tip)" == "$(slot_sha)" ]] || fail "--remote must push the landing commit"
[[ "$(dispatches)" == "$dispatches_before" ]] || fail "a push triggers the workflow; no dispatch"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "pushed --remote run should merge"

# --remote with the commit already pushed and no verdict coming: dispatch on the task branch.
new_commit remote-dispatch
push_slot
statuses $'absent\037\037' $'absent\037\037' "$(green 45)"
runs $'none\t' "$(printf 'in_progress\t45')"
merges_before="$(gh_merges)"
remote_merge || { cat "$TMP/merge.out" >&2; fail "--remote should dispatch, wait, and merge"; }
[[ "$(dispatches)" == $((dispatches_before + 1)) ]] || fail "pushed + no verdict must dispatch once (got $(dispatches))"
grep -q -- "workflow run headless-suite.yml --ref $TASK_BRANCH" "$GH_DISPATCH_LOG" || fail "dispatch must target the task branch"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "dispatched --remote run should merge"

# Liveness refusals.
new_commit remote-no-run
push_slot
statuses $'absent\037\037'
runs $'none\t'
merges_before="$(gh_merges)"
if NO_RUN=0 remote_merge; then fail "--remote must refuse when no run ever appears"; fi
expect_output "no headless-suite run exists for $(slot_sha)" "the no-run refusal must name the commit"
expect_output "gh workflow run headless-suite.yml --ref $TASK_BRANCH" "the no-run refusal must name the recovery"

runs $'none\t' "$(printf 'queued\t46')"
if QUEUED=0 remote_merge; then fail "--remote must refuse a run stuck queued"; fi
expect_output "run 46 has sat queued" "the queued refusal must name the run"

statuses "$(printf 'pending\037headless suite running\037%s/46' "$RUN_URL")"
runs "$(printf 'completed\t46')"
if remote_merge; then fail "--remote must refuse pending with no live run"; fi
expect_output "is pending but no headless-suite run is live" "the dead-pending refusal must say so"
expect_output "gh run rerun 46" "the dead-pending refusal must name the recovery"

runs "$(printf 'in_progress\t46')"
GH_API_FAIL=1 remote_merge && fail "--remote must refuse when GitHub cannot be asked"
expect_output "could not ask GitHub about $(slot_sha)" "the gh-error refusal must say so"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "no liveness refusal may reach gh pr merge"

# A green status whose trailer stamps another tree is still no proof after the wait.
statuses "$(printf 'success\037tree=%040d total=5 passed=5 skipped=0\037%s/46' 0 "$RUN_URL")"
if remote_merge; then fail "--remote must refuse a green status for another tree"; fi
expect_output "stamps tree 0000000000000000000000000000000000000000" "the post-wait tree check must say why"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "a wrong-tree verdict must not reach gh pr merge"
statuses "$(green 46)"
remote_merge || { cat "$TMP/merge.out" >&2; fail "fixture: clear the pending landing commit"; }

# Under --remote a comment-only delta is a code delta: hosted run, no local smoke boot.
git -C "$TMP/agent-1" rm -q src/Asteroids3D/Assets/CallerProbe.cs
git -C "$TMP/agent-1" commit -qm "drop caller-info probe"
statuses $'absent\037\037'
pool merge agent-1 >/dev/null
sed -i 's/reworded again/reworded for remote/' "$TMP/agent-1/code.cs"
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "comment-only edit for --remote"
statuses "$(printf 'pending\037headless suite running\037%s/47' "$RUN_URL")" "$(green 47)"
runs "$(printf 'in_progress\t47')"
runs_before="$(runner_runs)"
remote_merge || { cat "$TMP/merge.out" >&2; fail "comment-only --remote merge should complete"; }
[[ "$(runner_runs)" == "$runs_before" ]] || fail "--remote must not smoke-boot a comment-only delta (got $(runner_runs))"
[[ "$(proof_kind)" == "remote-run" ]] || fail "comment-only --remote proof must be a full remote run (got $(proof_kind))"

# --- automatic producer choice from memory admission ---------------------------
# One owed run serves every refusal: each stops at proof-check, before any run or wait.
new_commit admission-fallback
runs_before="$(runner_runs)"; merges_before="$(gh_merges)"; resharper_before="$(resharper_runs)"
admission query_error
if gate_merge; then fail "a failed admission query must refuse the merge"; fi
expect_output "coordinator stub: state file locked" "the query-error refusal must pass the coordinator's stderr through"
expect_output "gave no memory admission verdict" "the query-error refusal must say so"
expect_output "merge agent-1 --remote" "the query-error refusal must name the hosted chooser"
expect_output "merge agent-1 -- <runner args>" "the query-error refusal must name the local chooser"
admission boot_perhaps
if gate_merge; then fail "an unknown admission status must refuse the merge"; fi
expect_output "memory admission status 'boot_perhaps' is not one the gate knows" "the unknown-status refusal must quote the status"
: > "$ADMISSION_SEQ"
if gate_merge; then fail "fixture: an unscripted admission answer must fail closed"; fi
grep -q '"phase":"proof-check".*"status":"failed"' "$(journal_for)" || fail "admission refusals must die in proof-check"
[[ "$(runner_runs)" == "$runs_before" && "$(resharper_runs)" == "$resharper_before" && "$(gh_merges)" == "$merges_before" ]] \
  || fail "an admission refusal must not run tests, the ratchet, or gh pr merge"

# Runner args name the local producer: no query, even when the answer would be no.
admission boot_not_admitted
queries_before="$(admission_queries)"
echo 1 > "$RUNNER_EXIT_FILE"
if gate_merge -- -Mode Both -ScopeType Workspace; then fail "fixture runner is red, merge must fail"; fi
echo 0 > "$RUNNER_EXIT_FILE"
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail "runner args must take the local run (got $(runner_runs))"
[[ "$(admission_queries)" == "$queries_before" ]] || fail "runner args name the producer, so memory admission must not be asked"

# Not admitted: the hosted run, ratchet first and once, no local boot for tests.
statuses "$(printf 'pending\037headless suite running\037%s/49' "$RUN_URL")" "$(green 49)"
runs "$(printf 'in_progress\t49')"
runs_before="$(runner_runs)"
gate_merge || { cat "$TMP/merge.out" >&2; fail "boot_not_admitted should merge on the hosted run"; }
expect_output "boot_not_admitted) — the test run goes to the hosted headless suite" "the gate must say why it went remote"
expect_output "faces the same memory pressure" "the gate must warn about the ratchet's boot"
[[ "$(admission_queries)" == $((queries_before + 1)) ]] || fail "an owed run with no named producer asks memory admission once (got $(admission_queries))"
[[ "$(runner_runs)" == "$runs_before" ]] || fail "boot_not_admitted must not run the local suite (got $(runner_runs))"
[[ "$(resharper_runs)" == $((resharper_before + 1)) ]] || fail "the hosted path must invoke the ratchet exactly once (got $(resharper_runs))"
[[ "$(proof_kind)" == "remote-run" ]] || fail "the automatic hosted run must record kind=remote-run (got $(proof_kind))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "the automatic hosted run should reach gh pr merge"
[[ "$(phase_order)" == "preflight fetch base-merge proof-check resharper remote-proof script-tests push base-recheck gh-merge " ]] \
  || fail "the automatic hosted path takes the remote ladder (got '$(phase_order)')"
grep -q 'memory admission boot_not_admitted - hosted run' "$(journal_for)" || fail "the journal must note the verdict and the chosen producer"

# Not admitted + comment-only delta: a code delta on the hosted path, no smoke boot.
sed -i 's/reworded for remote/reworded for fallback/' "$TMP/agent-1/code.cs"
git -C "$TMP/agent-1" add code.cs
git -C "$TMP/agent-1" commit -qm "comment-only edit under boot_not_admitted"
statuses "$(printf 'pending\037headless suite running\037%s/50' "$RUN_URL")" "$(green 50)"
runs "$(printf 'in_progress\t50')"
gate_merge || { cat "$TMP/merge.out" >&2; fail "comment-only delta under boot_not_admitted should merge on the hosted run"; }
[[ "$(runner_runs)" == "$runs_before" ]] || fail "boot_not_admitted must not smoke-boot a comment-only delta (got $(runner_runs))"
[[ "$(proof_kind)" == "remote-run" ]] || fail "comment-only delta under boot_not_admitted must be a full remote run (got $(proof_kind))"
admission boot_admitted

# Base moving while the gate works is caught before gh-merge; no auto-loop.
cat > "$TMP/agent-1/scripts/tests/test_probe.sh" <<'PROBE'
#!/usr/bin/env bash
echo probe >> "$PROBE_MARKER"
[[ -z "${PROBE_HOOK:-}" ]] || bash -c "$PROBE_HOOK"
exit "$(cat "$PROBE_EXIT_FILE")"
PROBE
git -C "$TMP/agent-1" add scripts/tests/test_probe.sh
git -C "$TMP/agent-1" commit -qm "probe can move base mid-gate"
statuses $'absent\037\037'
merges_before="$(gh_merges)"
export PROBE_HOOK="echo mid-gate > '$TMP/primary/mid_gate.txt' && git -C '$TMP/primary' add mid_gate.txt && git -C '$TMP/primary' commit -qm 'base moves mid-gate' && git -C '$TMP/primary' push -q origin main"
pool merge agent-1 > "$TMP/merge.out" 2>&1 && fail "merge must refuse when base moved during the gate"
unset PROBE_HOOK
expect_output "base moved during the merge gate — re-run 'merge agent-1'" "the base re-check must say what to do"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "a moved base must not reach gh pr merge"
grep -q '"phase":"base-recheck".*"status":"failed"' "$(journal_for)" || fail "the journal should name base-recheck as the phase that died"
pool merge agent-1 >/dev/null
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail "re-running merge after a moved base should integrate and merge"

# A landing diff touching .github/ can edit the workflow that proves it: remote proof is refused on both paths.
mkdir -p "$TMP/agent-1/.github/workflows"
echo "name: edited" > "$TMP/agent-1/.github/workflows/headless-suite.yml"
git -C "$TMP/agent-1" add .github
git -C "$TMP/agent-1" commit -qm "edit the proving workflow"
push_slot
statuses "$(green 48)"
merges_before="$(gh_merges)"
if remote_merge; then fail "--remote must refuse a landing diff touching .github/"; fi
expect_output "--remote refused — the landing diff touches .github/" "the .github refusal must say why"
[[ "$(gh_merges)" == "$merges_before" ]] || fail "the .github refusal must not reach gh pr merge"
# Not admitted AND remote proof barred: no producer is left, and the refusal names both reasons and the way out.
admission boot_not_admitted
runs_before="$(runner_runs)"
if gate_merge; then fail "boot_not_admitted with a .github/ landing diff must refuse"; fi
expect_output "memory admission would refuse a batch Unity boot (boot_not_admitted), and the landing diff touches .github/" "the refusal must name both reasons"
expect_output "merge agent-1 -- -AllowLowMemory'. It covers the TEST boot only" "the refusal must name the approved-boot way out and its limit"
[[ "$(runner_runs)" == "$runs_before" && "$(gh_merges)" == "$merges_before" ]] || fail "the double refusal must not run tests or reach gh pr merge"
admission boot_admitted
runs_before="$(runner_runs)"
pool merge agent-1 > "$TMP/merge.out" 2>&1 || { cat "$TMP/merge.out" >&2; fail ".github landing diff should merge on the local run"; }
expect_output "the landing diff touches .github/, so this merge needs the local run" "the default path must say why it ignored the green status"
[[ "$(runner_runs)" == $((runs_before + 1)) ]] || fail ".github landing diff must run the local suite (got $(runner_runs))"
[[ "$(proof_kind)" == "full-run" ]] || fail ".github landing diff must merge on local proof (got $(proof_kind))"
[[ "$(gh_merges)" == $((merges_before + 1)) ]] || fail ".github landing diff should merge on local proof"
# With the landing tree already proven no run is needed, so --remote has nothing to refuse.
remote_merge || { cat "$TMP/merge.out" >&2; fail "--remote on an already-proven .github landing tree should merge"; }

echo "PASS: merge gate tested-tree proof + ReSharper proof + scope-aware proof + inert fast path + routed-summary refusal + phase journal + scripts/ suite trigger + remote proof (accept, fail-closed, --remote liveness, base re-check, .github refusal) + memory-admission producer choice"
