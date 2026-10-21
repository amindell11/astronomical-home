#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh
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
echo 'PASS: journal character and number preservation'
