#!/usr/bin/env bash
set -euo pipefail

# run-script-tests takes <slot> like every sibling verb, and a slot whose suite cannot run
# (no scripts/tests, or none with test files) fails instead of reporting success. The .ps1
# lane runs beside the .sh lane, and a red file fails the suite only after every file ran.

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

# Lanes: the .sh file waits for the .ps1 file's marker, so a green run proves the lanes overlap.
LANE_DIR="$(cygpath -m "$TMP/lanes")"
export LANE_DIR
mkdir -p "$LANE_DIR"
tests="$TMP/agent-1/scripts/tests"
cat > "$tests/test_a_lane.sh" <<'LANE'
touch "$LANE_DIR/a.ran"
for _ in $(seq 1 600); do [[ -f "$LANE_DIR/d.ran" ]] && { echo sh-a-out; exit 0; }; sleep 0.1; done
echo "the .ps1 lane never ran beside this file" >&2
exit 1
LANE
lane_ps1() { printf 'New-Item -ItemType File -Force -Path "$env:LANE_DIR/%s.ran" | Out-Null\nWrite-Output "ps1-%s-out"\nexit %s\n' "$1" "$1" "$2" > "$tests/$3"; }
lane_ps1 d 0 test_d_lane.ps1
line_of() { grep -n -F -- "$1" <<<"$out" | head -n 1 | cut -d: -f1; }

out="$(pool run-script-tests agent-1 2>&1)" || fail "both lanes green should exit 0 (got: $out)"
# The .ps1 file finished first, yet its block prints after the .sh block, each block contiguous.
prev=0
for line in sh-a-out SCRIPT_TEST_FILE=test_a_lane.sh "PASS test_a_lane.sh" \
  ps1-d-out SCRIPT_TEST_FILE=test_d_lane.ps1 "PASS test_d_lane.ps1" SCRIPT_TEST_TOTAL_SECONDS=; do
  n="$(line_of "$line")"
  [[ -n "$n" && "$n" -gt "$prev" ]] || fail "blocks must print .sh then .ps1, each in order, total last ('$line' misplaced; got: $out)"
  prev="$n"
done

# A red file in either lane fails the suite only after every file in both lanes has run.
rm -f "$LANE_DIR"/*.ran
printf 'touch "$LANE_DIR/b.ran"\nexit 3\n' > "$tests/test_b_red.sh"
printf 'touch "$LANE_DIR/c.ran"\n' > "$tests/test_c_after.sh"
lane_ps1 e 4 test_e_red.ps1
lane_ps1 f 0 test_f_after.ps1
rc=0
out="$(pool run-script-tests agent-1 2>&1)" || rc=$?
[[ "$rc" -ne 0 ]] || fail "a red file in either lane must fail the suite (got: $out)"
for name in a b c d e f; do
  [[ -f "$LANE_DIR/$name.ran" ]] || fail "every file must run despite red files ($name never ran; got: $out)"
done
for trailer in "test_a_lane.sh EXIT=0" "test_b_red.sh EXIT=3" "test_c_after.sh EXIT=0" \
  "test_d_lane.ps1 EXIT=0" "test_e_red.ps1 EXIT=4" "test_f_after.ps1 EXIT=0"; do
  grep -qE "^SCRIPT_TEST_FILE=${trailer% *} SECONDS=[0-9]+ ${trailer#* }$" <<<"$out" \
    || fail "each file keeps its own trailer ($trailer missing; got: $out)"
done
[[ "$out" == *"FAIL test_b_red.sh (exit 3)"* && "$out" == *"FAIL test_e_red.ps1 (exit 4)"* ]] \
  || fail "each red file names its own failure (got: $out)"

echo "PASS: run-script-tests resolves <slot>, refuses unknown slots and paths, fails on a missing or empty suite, runs the .ps1 lane beside the .sh lane in a stable print order, and fails only after every file ran"
