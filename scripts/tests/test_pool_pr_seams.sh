#!/usr/bin/env bash
set -euo pipefail

# Regression for the shared PR seams (#456): one flag grammar for create-pr/submit, a PR lookup
# that refuses a missing head branch, and the single-owner Unity churn classifier the pool shells
# out to. Every case here fails before any network or gh call, so the suite stays hermetic.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
CHURN="$SCRIPT_DIR/../lib/unity_churn.ps1"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"

fail() { echo "FAIL: $1" >&2; exit 1; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
git -C "$TMP/primary" config user.email pool-test@example.test
git -C "$TMP/primary" config user.name "Pool Test"
mkdir -p "$TMP/primary/src/Asteroids3D/ProjectSettings"
printf 'base\n' > "$TMP/primary/file.txt"
printf '  Standalone: UNITY_POST_PROCESSING_STACK_V2\n' \
  > "$TMP/primary/src/Asteroids3D/ProjectSettings/ProjectSettings.asset"
git -C "$TMP/primary" add -A
git -C "$TMP/primary" commit -qm init
git -C "$TMP/primary" push -q origin main
git -C "$TMP/primary" worktree add -q -b agent-1 "$TMP/agent-1" main
cd "$TMP/primary"

pool() { bash "$POOL" "$@"; }

# --- flag grammar: both commands reject the same shapes, and reject them before gh -----------
expect_reject() {
  local why="$1"; shift
  local out rc=0
  out="$(pool "$@" 2>&1)" || rc=$?
  [[ "$rc" -ne 0 ]] || fail "$why (command succeeded)"
  printf '%s' "$out"
}

out="$(expect_reject "create-pr must require --title" create-pr agent-1 --body "b")"
[[ "$out" == *"missing required --title"* ]] || fail "create-pr should name the missing --title (got: $out)"

out="$(expect_reject "create-pr must reject --body with --body-file" \
  create-pr agent-1 --title t --body b --body-file "$TMP/primary/file.txt")"
[[ "$out" == *"mutually exclusive"* ]] || fail "create-pr should reject --body + --body-file (got: $out)"

out="$(expect_reject "create-pr must reject an unknown flag" create-pr agent-1 --title t --body b --nope)"
[[ "$out" == *"unknown argument: --nope"* ]] || fail "create-pr should name the unknown flag (got: $out)"

out="$(expect_reject "create-pr must reject test args" create-pr agent-1 --title t --body b -- -Mode EditMode)"
[[ "$out" == *"'--' is not accepted"* ]] || fail "create-pr takes no test-runner args (got: $out)"

out="$(expect_reject "submit must reject an unknown flag before --" submit agent-1 --title t --body b --nope)"
[[ "$out" == *"test-runner args go after '--'"* ]] || fail "submit should point at '--' (got: $out)"

# The regression this seam exists for: a bare word was silently collected as a test arg.
out="$(expect_reject "submit must reject a bare positional before --" submit agent-1 --title t --body b -Mode EditMode)"
[[ "$out" == *"unexpected argument '-Mode'"* ]] \
  || fail "submit should refuse a bare positional before '--' (got: $out)"

out="$(expect_reject "submit must require a body" submit agent-1 --title t -- -Mode EditMode)"
[[ "$out" == *"missing required --body"* ]] || fail "submit should validate flags before running tests (got: $out)"

# --- PR lookup refuses a missing head branch ---------------------------------------------
# Sourced, not executed: the guard at the foot of the pool script leaves the functions defined.
set +u
# shellcheck disable=SC1090
source "$POOL"
set -u
if pr_number_for_pushed_head "" main >/dev/null 2>&1; then
  fail "pr_number_for_pushed_head must refuse an empty head branch rather than listing every PR"
fi

# --- churn classifier is the single owner of the restore allowlist ------------------------
churn() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$CHURN" -WorktreePath "$1"; }

[[ "$(churn "$TMP/primary")" == *'"knownChurn":true'* ]] || fail "a clean worktree is not unexpected churn"

printf '  Standalone: UNITY_POST_PROCESSING_STACK_V2;SENTIS_ANALYTICS_ENABLED\n' \
  > "$TMP/primary/src/Asteroids3D/ProjectSettings/ProjectSettings.asset"
[[ "$(churn "$TMP/primary")" == *'"knownChurn":true'* ]] || fail "the analytics define flip is the known churn"

printf 'edited\n' > "$TMP/primary/file.txt"
[[ "$(churn "$TMP/primary")" == *'"knownChurn":false'* ]] || fail "a real edit alongside the churn is NOT allowlisted"

git -C "$TMP/primary" restore --worktree -- .
printf 'edited\n' > "$TMP/primary/file.txt"
[[ "$(churn "$TMP/primary")" == *'"knownChurn":false'* ]] || fail "an unrelated tracked edit is not allowlisted"

echo "PASS: pool PR seams — shared flag grammar + head-branch PR lookup + single-owner churn classifier"
