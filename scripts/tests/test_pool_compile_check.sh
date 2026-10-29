#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# compile-check runs the slot's own unity_compile_check.ps1 against the slot's Unity project and a
# per-slot state dir under the compile-check root, prints COMPILE_CHECK=<verdict> and exits with the
# script's code. run-tests runs it first: only a fail stops the run, before the runner starts.
# Stub scripts in the slot stand in for the check and the Unity runner.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
WIN_TMP="$(cygpath -m "$TMP")"

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"
export WORKTREE_POOL_COMPILE_CHECK_ROOT="$WIN_TMP/compile-check"
export PROBE_LOG="$WIN_TMP/probe.log"

fail() { echo "FAIL: $1" >&2; exit 1; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
git -C "$TMP/primary" config user.email pool-test@example.test
git -C "$TMP/primary" config user.name "Pool Test"
printf 'base\n' > "$TMP/primary/file.txt"
git -C "$TMP/primary" add file.txt
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
cd "$TMP/primary"

mkdir -p "$TMP/agent-1/scripts"
cat > "$TMP/agent-1/scripts/unity_compile_check.ps1" <<'EOF'
param([string]$ProjectPath, [string]$StateDir, [switch]$Refresh)
Add-Content -LiteralPath $env:PROBE_LOG -Value "check refresh=$($Refresh.IsPresent) project=$ProjectPath state=$StateDir"
[Console]::Out.WriteLine($env:PROBE_VERDICT)
exit [int]$env:PROBE_EXIT
EOF
cat > "$TMP/agent-1/scripts/unity_test_agent.ps1" <<'EOF'
Add-Content -LiteralPath $env:PROBE_LOG -Value "runner $args"
exit 0
EOF

# run <verdict> <exit> <pool args...>: sets OUT, RC and LOG for the assertions that follow.
run() {
  export PROBE_VERDICT="$1" PROBE_EXIT="$2"
  shift 2
  : > "$TMP/probe.log"
  RC=0
  OUT="$(bash "$POOL" "$@" 2>&1)" || RC=$?
  LOG="$(cat "$TMP/probe.log")"
}

run pass 0 compile-check agent-1
[[ "$RC" -eq 0 && "$OUT" == *"COMPILE_CHECK=pass SECONDS="* ]] || fail "a pass prints its trailer and exits 0 (rc=$RC: $OUT)"
[[ "$LOG" == *"refresh=False project="*"/agent-1/src/Asteroids3D state=$WIN_TMP/compile-check/agent-1"* ]] \
  || fail "the check gets the slot's Unity project and its per-slot state dir (got: $LOG)"

run pass 0 compile-check agent-1 --refresh
[[ "$LOG" == *"refresh=True"* ]] || fail "--refresh reaches the script as -Refresh (got: $LOG)"

run stale 3 compile-check agent-1
[[ "$RC" -eq 3 && "$OUT" == *"COMPILE_CHECK=stale"* ]] || fail "a stale verdict exits 3 (rc=$RC: $OUT)"

run pass 0 compile-check agent-1 --bogus
[[ "$RC" -eq 2 && "$OUT" == *"requires <slot> [--refresh]"* && -z "$LOG" ]] || fail "an unknown flag is refused before the check runs (rc=$RC: $OUT)"

run pass 0 compile-check nope
[[ "$RC" -eq 2 && "$OUT" == *"unknown slot 'nope'"* && "$OUT" == *"COMPILE_CHECK=error"* ]] || fail "an unknown slot is an error, exit 2 (rc=$RC: $OUT)"

run fail 1 run-tests agent-1 -Mode EditMode
[[ "$RC" -eq 2 && "$OUT" == *"COMPILE_CHECK=fail"* ]] || fail "run-tests exits 2 on a compile failure (rc=$RC: $OUT)"
[[ "$LOG" != *runner* ]] || fail "run-tests never starts the runner after a compile failure (got: $LOG)"

for verdict in "pass 0" "stale 3" "error 2"; do
  read -r name code <<<"$verdict"
  run "$name" "$code" run-tests agent-1 -Mode EditMode
  [[ "$RC" -eq 0 && "$OUT" == *"COMPILE_CHECK=$name"* ]] || fail "run-tests goes on after a $name verdict (rc=$RC: $OUT)"
  [[ "$LOG" == *"runner -OutDir results/unity-tests-agent -Mode EditMode"* ]] || fail "after a $name verdict the runner gets its args (got: $LOG)"
done

echo "PASS: compile-check verb and run-tests preflight"
