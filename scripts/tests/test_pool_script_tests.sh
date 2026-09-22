#!/usr/bin/env bash
set -euo pipefail

# run-script-tests takes <slot> like every sibling verb, and a slot whose suite cannot run
# (no scripts/tests, or none with test files) fails instead of reporting success.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"

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

pool() { bash "$POOL" "$@"; }

expect_reject() {
  local why="$1"; shift
  local out rc=0
  out="$(pool "$@" 2>&1)" || rc=$?
  [[ "$rc" -ne 0 ]] || fail "$why (command succeeded)"
  printf '%s' "$out"
}

out="$(expect_reject "run-script-tests must require a slot" run-script-tests)"
[[ "$out" == *"requires <slot>"* ]] || fail "a missing slot should print the usage line (got: $out)"

out="$(expect_reject "run-script-tests must reject an unknown slot" run-script-tests nope)"
[[ "$out" == *"unknown slot 'nope'"* ]] || fail "an unknown slot should be named (got: $out)"

out="$(expect_reject "run-script-tests must reject a directory path" run-script-tests "$TMP/agent-1")"
[[ "$out" == *"unknown slot"* ]] || fail "a path is not a slot (got: $out)"

out="$(expect_reject "a slot without scripts/tests must fail" run-script-tests agent-1)"
[[ "$out" == *"scripts/tests is missing"* ]] || fail "a missing tests dir should be named (got: $out)"

mkdir -p "$TMP/agent-1/scripts/tests"
out="$(expect_reject "a slot whose scripts/tests holds no test files must fail" run-script-tests agent-1)"
[[ "$out" == *"no test files under"* ]] || fail "an empty tests dir should be named (got: $out)"

export PROBE_MARKER="$TMP/probe-runs"
: > "$PROBE_MARKER"
cat > "$TMP/agent-1/scripts/tests/test_probe.sh" <<'PROBE'
#!/usr/bin/env bash
echo probe >> "$PROBE_MARKER"
PROBE
out="$(pool run-script-tests agent-1 2>&1)" || fail "a real slot with a green suite should exit 0 (got: $out)"
[[ "$(grep -c probe "$PROBE_MARKER")" -eq 1 ]] || fail "the slot's suite must run exactly once"
[[ "$out" == *"SCRIPT_TEST_FILE=test_probe.sh"* ]] || fail "the per-file trailer should name the probe (got: $out)"
[[ "$out" == *"PASS test_probe.sh"* ]] || fail "a green probe should print PASS (got: $out)"

echo "PASS: run-script-tests resolves <slot>, refuses unknown slots and paths, and fails on a missing or empty suite"
