#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"
source "$POOL"
MERGE_JOURNAL="$TMP/journal"
MERGE_RUN_START=$EPOCHSECONDS
utc_before="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
journal_event $'ev"ent' $'ph\\ase' 'sec=-3' $'detail=a\tb\nc"d\\e' 'empty='
grep -q '"event":"event","phase":"phase","sec":-3,"detail":"abcde","empty":""}' "$MERGE_JOURNAL"
utc_after="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
grep -Fq "\"ts\":\"$utc_before\"" "$MERGE_JOURNAL" || grep -Fq "\"ts\":\"$utc_after\"" "$MERGE_JOURNAL"
mkdir -p "$TMP/suite/scripts/tests"
printf 'exit 0\n' > "$TMP/suite/scripts/tests/test_a.sh"
printf 'exit 7\n' > "$TMP/suite/scripts/tests/test_b.sh"
printf 'touch "%s"\n' "$TMP/should-not-run" > "$TMP/suite/scripts/tests/test_c.sh"
rc=0
cmd_run_script_tests "$TMP/suite" > "$TMP/output" || rc=$?
[[ "$rc" == 1 && ! -e "$TMP/should-not-run" ]]
grep -Eq '^SCRIPT_TEST_FILE=test_a.sh SECONDS=[0-9]+ EXIT=0$' "$TMP/output"
grep -Eq '^SCRIPT_TEST_FILE=test_b.sh SECONDS=[0-9]+ EXIT=7$' "$TMP/output"
grep -Eq '^SCRIPT_TEST_TOTAL_SECONDS=[0-9]+$' "$TMP/output"
grep -Eq '"event":"script-test","phase":"script-tests","file":"test_b.sh","sec":[0-9]+,"exit":7' "$MERGE_JOURNAL"
echo 'PASS: journal character and number preservation; suite timing retains fail-fast exit'
