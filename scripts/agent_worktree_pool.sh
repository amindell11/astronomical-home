#!/usr/bin/env bash
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
LOCK_ROOT="$ROOT/.worktree-pool/locks"
mkdir -p "$LOCK_ROOT"

usage() {
  cat <<'EOF'
Usage: scripts/agent_worktree_pool.sh <command> [args]

Commands:
  status
      List agent-* worktree slots and lock status.

  acquire [lease_id]
      Lock and return an available slot.
      Output: SLOT=<name> PATH=<abs-path>

  release <slot>
      Release slot lock (e.g., agent-1).

  prepare <slot> [base_ref]
      Reset slot branch/worktree to base ref (default: origin/main)
      while preserving ignored dirs (e.g., Unity Library/).

  run-tests <slot> [unity_test_agent.ps1 args...]
      Run Unity tests in that slot with standardized outDir:
      results/unity-tests-agent

Examples:
  scripts/agent_worktree_pool.sh status
  scripts/agent_worktree_pool.sh acquire task-123
  scripts/agent_worktree_pool.sh prepare agent-1 origin/main
  scripts/agent_worktree_pool.sh run-tests agent-1 -Mode EditMode -ScopeType Smoke
  scripts/agent_worktree_pool.sh release agent-1
EOF
}

slots_tsv() {
  git -C "$ROOT" worktree list --porcelain | awk '
    /^worktree / {
      path = substr($0, 10)
      next
    }
    /^branch refs\/heads\/agent-[0-9]+$/ {
      branch = $0
      sub(/^branch refs\/heads\//, "", branch)
      print branch "\t" path
    }
  ' | sort -V
}

slot_path() {
  local slot="$1"
  slots_tsv | awk -F'\t' -v s="$slot" '$1 == s { print $2; found=1 } END { if (!found) exit 1 }'
}

lock_dir_for() {
  local slot="$1"
  printf '%s/%s.lock' "$LOCK_ROOT" "$slot"
}

cmd_status() {
  local any=0
  while IFS=$'\t' read -r slot path; do
    any=1
    local ldir
    ldir="$(lock_dir_for "$slot")"
    if [[ -d "$ldir" ]]; then
      local lease pid ts
      lease="$(cat "$ldir/lease" 2>/dev/null || true)"
      pid="$(cat "$ldir/pid" 2>/dev/null || true)"
      ts="$(cat "$ldir/timestamp" 2>/dev/null || true)"
      echo "$slot | LOCKED | $path | lease=${lease:-unknown} pid=${pid:-unknown} at=${ts:-unknown}"
    else
      echo "$slot | FREE   | $path"
    fi
  done < <(slots_tsv)

  if [[ "$any" -eq 0 ]]; then
    echo "No agent-* worktrees found."
    exit 1
  fi
}

cmd_acquire() {
  local lease="${1:-task-$(date +%Y%m%d-%H%M%S)}"
  while IFS=$'\t' read -r slot path; do
    local ldir
    ldir="$(lock_dir_for "$slot")"
    if mkdir "$ldir" 2>/dev/null; then
      printf '%s\n' "$lease" > "$ldir/lease"
      printf '%s\n' "$$" > "$ldir/pid"
      date -u +"%Y-%m-%dT%H:%M:%SZ" > "$ldir/timestamp"
      echo "SLOT=$slot PATH=$path"
      return 0
    fi
  done < <(slots_tsv)

  echo "No free slots" >&2
  return 1
}

cmd_release() {
  local slot="$1"
  local ldir
  ldir="$(lock_dir_for "$slot")"
  if [[ -d "$ldir" ]]; then
    rm -rf "$ldir"
    echo "Released $slot"
  else
    echo "$slot was not locked"
  fi
}

cmd_prepare() {
  local slot="$1"
  local base="${2:-origin/main}"
  local path
  path="$(slot_path "$slot")"

  git -C "$path" fetch origin
  git -C "$path" checkout "$slot"
  git -C "$path" reset --hard "$base"
  git -C "$path" clean -fd

  echo "Prepared $slot at $path -> $base"
}

cmd_run_tests() {
  local slot="$1"
  shift || true
  local path
  path="$(slot_path "$slot")"

  (
    cd "$path"
    powershell.exe -NoProfile -ExecutionPolicy Bypass \
      -File "./scripts/unity_test_agent.ps1" \
      -OutDir "results/unity-tests-agent" \
      "$@"
  )
}

main() {
  if [[ $# -lt 1 ]]; then
    usage
    exit 1
  fi

  local cmd="$1"
  shift || true

  case "$cmd" in
    status) cmd_status ;;
    acquire) cmd_acquire "$@" ;;
    release)
      [[ $# -ge 1 ]] || { echo "release requires <slot>" >&2; exit 1; }
      cmd_release "$1"
      ;;
    prepare)
      [[ $# -ge 1 ]] || { echo "prepare requires <slot> [base_ref]" >&2; exit 1; }
      cmd_prepare "$@"
      ;;
    run-tests)
      [[ $# -ge 1 ]] || { echo "run-tests requires <slot> [args...]" >&2; exit 1; }
      cmd_run_tests "$@"
      ;;
    -h|--help|help) usage ;;
    *)
      echo "Unknown command: $cmd" >&2
      usage
      exit 1
      ;;
  esac
}

main "$@"
