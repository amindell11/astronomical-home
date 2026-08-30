#!/usr/bin/env bash
set -euo pipefail

# Worktree Dashboard - quick overview of all agent worktrees.
# Usage: ./scripts/worktree_dashboard.sh [--watch]
#
# Optional env vars:
#   WORKTREE_DASHBOARD_FETCH=1   Refresh origin before rendering.
#   WORKTREE_DASHBOARD_PRS=1     Include open PR URLs via gh.
#   WORKTREE_DASHBOARD_STATUS=1  Scan each worktree for local file changes.

# Same CWD-invariant anchor as agent_worktree_pool.sh — a --show-toplevel
# root read from inside an agent-N worktree misses the primary's locks and
# shows occupied slots as FREE.
ROOT="$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
if [[ -n "${BASH_SOURCE[0]:-}" ]]; then
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
else
  SCRIPT_DIR="$(git rev-parse --show-toplevel)/scripts"
fi
UNITY_ACCESS_SCRIPT="$SCRIPT_DIR/unity_access.ps1"
POOL_SCRIPT="$SCRIPT_DIR/agent_worktree_pool.sh"
SHOW_PRS="${WORKTREE_DASHBOARD_PRS:-0}"
DO_FETCH="${WORKTREE_DASHBOARD_FETCH:-0}"
SHOW_STATUS="${WORKTREE_DASHBOARD_STATUS:-0}"

if [[ -t 1 || "${1:-}" == "--watch" ]]; then
  RED='\033[0;31m'
  GREEN='\033[0;32m'
  YELLOW='\033[0;33m'
  BLUE='\033[0;34m'
  CYAN='\033[0;36m'
  DIM='\033[2m'
  BOLD='\033[1m'
  NC='\033[0m'
else
  RED='' GREEN='' YELLOW='' BLUE='' CYAN='' DIM='' BOLD='' NC=''
fi

to_shell_path() {
  local path="$1"

  if [[ "$path" =~ ^[A-Za-z]:[\\/].* ]]; then
    if command -v wslpath >/dev/null 2>&1; then
      wslpath -u "$path"
      return
    fi
    if command -v cygpath >/dev/null 2>&1; then
      cygpath -u "$path"
      return
    fi
  fi

  printf '%s\n' "$path"
}

divider() {
  printf '%s\n' "-------------------------------------------------------------"
}

header() {
  echo ""
  printf "${BOLD}${CYAN}  WORKTREE DASHBOARD${NC}  ${DIM}$(date '+%H:%M:%S')${NC}\n"
  divider
}

branch_status_summary() {
  local path="$1"
  local shell_path changed_count
  shell_path="$(to_shell_path "$path")"

  changed_count="$(git -C "$shell_path" status --short 2>/dev/null | wc -l | tr -d ' ')"
  if [[ "$changed_count" -gt 0 ]]; then
    printf "${YELLOW}%s files${NC}" "$changed_count"
  else
    printf "${GREEN}clean${NC}"
  fi
}

branch_status_files() {
  local path="$1"
  local shell_path changed_count
  shell_path="$(to_shell_path "$path")"
  changed_count="$(git -C "$shell_path" status --short 2>/dev/null | wc -l | tr -d ' ')"

  if [[ "$changed_count" -le 0 ]]; then
    return
  fi

  printf "    ${DIM}files:${NC}\n"
  git -C "$shell_path" status --short 2>/dev/null | head -5 | while read -r line; do
    printf "      ${DIM}%s${NC}\n" "$line"
  done
  if [[ "$changed_count" -gt 5 ]]; then
    printf "      ${DIM}... and %d more${NC}\n" "$((changed_count - 5))"
  fi
}

slot_info() {
  local slot="$1"
  local path="$2"
  local state="$3"
  local lease="$4"
  local tb="$5"

  local status_icon status_text lease_info="" task_branch_info=""

  case "$state" in
    free)
      status_icon="${GREEN}o${NC}"
      status_text="${GREEN}FREE${NC}"
      ;;
    stale)
      status_icon="${YELLOW}o${NC}"
      status_text="${YELLOW}STALE${NC}"
      lease_info="${YELLOW}${lease:-?}${NC}"
      task_branch_info="${tb:+${BLUE}-> $tb${NC}}"
      ;;
    *)
      status_icon="${RED}o${NC}"
      status_text="${RED}LOCKED${NC}"
      lease_info="${YELLOW}${lease:-?}${NC}"
      task_branch_info="${tb:+${BLUE}-> $tb${NC}}"
      ;;
  esac

  local commit_msg ahead behind changed_summary
  commit_msg="$(git -C "$ROOT" log --format='%s' -n 1 "$slot" 2>/dev/null || echo '(no commits)')"
  commit_msg="${commit_msg:0:50}"
  ahead="$(git -C "$ROOT" rev-list --count origin/main.."$slot" 2>/dev/null || echo '?')"
  behind="$(git -C "$ROOT" rev-list --count "$slot"..origin/main 2>/dev/null || echo '?')"

  if [[ "$SHOW_STATUS" == "1" ]]; then
    changed_summary="$(branch_status_summary "$path")"
  else
    changed_summary="${DIM}skipped${NC}"
  fi

  # The pool script owns the journal format; ask it, never parse it here.
  local merge_info=""
  if [[ -f "$POOL_SCRIPT" ]]; then
    merge_info="$(bash "$POOL_SCRIPT" merge-progress "$slot" --oneline 2>/dev/null || true)"
  fi

  local pr_info=""
  if [[ "$SHOW_PRS" == "1" && -n "$tb" ]] && command -v gh >/dev/null 2>&1; then
    local tb_check="$tb"
    local pr_url
    pr_url="$(gh pr list --head "$tb_check" --base main --state open --json url --jq '.[0].url' 2>/dev/null || true)"
    if [[ -n "$pr_url" && "$pr_url" != "null" ]]; then
      pr_info="${CYAN}PR: $pr_url${NC}"
    fi
  fi

  printf "\n  ${status_icon} ${BOLD}%-10s${NC} %b\n" "$slot" "$status_text"
  printf "    ${DIM}path:${NC}    %s\n" "$path"
  printf "    ${DIM}branch:${NC}  %s" "$slot"
  [[ -n "$task_branch_info" ]] && printf "  %b" "$task_branch_info"
  echo ""
  printf "    ${DIM}commits:${NC} +${GREEN}%s${NC} -${RED}%s${NC} vs main    ${DIM}changed:${NC} %b\n" "$ahead" "$behind" "$changed_summary"
  printf "    ${DIM}latest:${NC}  %s\n" "$commit_msg"
  [[ -n "$lease_info" ]] && printf "    ${DIM}lease:${NC}   %b\n" "$lease_info"
  [[ -n "$merge_info" ]] && printf "    ${DIM}merge:${NC}   ${YELLOW}[merging] %s${NC}\n" "$merge_info"
  [[ -n "$pr_info" ]] && printf "    %b\n" "$pr_info"

  if [[ "$SHOW_STATUS" == "1" ]]; then
    branch_status_files "$path"
  fi
}

main_info() {
  local branch commit_msg changed_summary
  branch="$(git -C "$ROOT" branch --show-current 2>/dev/null || echo 'detached')"
  commit_msg="$(git -C "$ROOT" log --format='%s' -n 1 2>/dev/null || echo '(no commits)')"
  commit_msg="${commit_msg:0:50}"

  if [[ "$SHOW_STATUS" == "1" ]]; then
    changed_summary="$(branch_status_summary "$ROOT")"
  else
    changed_summary="${DIM}skipped${NC}"
  fi

  printf "\n  ${BLUE}*${NC} ${BOLD}%-10s${NC} ${BLUE}MAIN${NC}\n" "$branch"
  printf "    ${DIM}path:${NC}    %s\n" "$ROOT"
  printf "    ${DIM}changed:${NC} %b\n" "$changed_summary"
  printf "    ${DIM}latest:${NC}  %s\n" "$commit_msg"
}

# The coordinator owns its state; ask its JSON channel through the sanctioned client and render here.
# Its human Status layout carries no contract, so no display may be scraped out of it.
UNITY_ACCESS_RENDER='
. (Join-Path $env:DASHBOARD_SCRIPT_DIR "unity_access_client.ps1")
$call = Invoke-UnityAccessCoordinator -CoordinatorArgs @("-Action", "Status")
$s = $call.result
if ($null -eq $s) { "unity access: coordinator gave no answer"; exit 0 }
$owners = @($s.owners)
if ($owners.Count -eq 0) { "Unity projects: all free" }
foreach ($o in $owners) { "Unity owner: $($o.slot) $($o.mode) lease=$($o.lease) pid=$($o.processId) project=$($o.projectPath)" }
if ($null -ne $s.boot) { "Boot lane: held by lease=$($s.boot.lease)" } elseif ($s.bootWedged) { "Boot lane: WEDGED" } else { "Boot lane: free" }
$q = @($s.queue)
if ($q.Count -gt 0) { "Queue: " + (($q | ForEach-Object { "$($_.position):$($_.slot)" }) -join ", ") } else { "Queue: empty" }
foreach ($b in @($s.blockers)) { "Blocker: $($b.kind) pid=$($b.processId) project=$($b.projectPath)" }
'

run_dashboard() {
  header
  main_info

  if [[ -f "$UNITY_ACCESS_SCRIPT" ]] && command -v powershell.exe >/dev/null 2>&1; then
    echo ""
    DASHBOARD_SCRIPT_DIR="$SCRIPT_DIR" powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$UNITY_ACCESS_RENDER" 2>/dev/null | sed 's/^/  /' || true
  fi

  if [[ "$DO_FETCH" == "1" ]]; then
    git -C "$ROOT" fetch origin --quiet 2>/dev/null || true
  fi

  # Slot state comes from the pool's porcelain read interface; nothing here reads the lock dir.
  local any=0 slot="" path="" state="" lease="" tb="" line key value
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ -n "$line" ]]; then
      key="${line%%=*}"
      value="${line#*=}"
      case "$key" in
        slot) slot="$value" ;;
        path) path="$value" ;;
        state) state="$value" ;;
        lease) lease="$value" ;;
        task_branch) tb="$value" ;;
      esac
      continue
    fi
    [[ -n "$slot" ]] || continue
    any=1
    slot_info "$slot" "$path" "$state" "$lease" "$tb"
    slot=""; path=""; state=""; lease=""; tb=""
  done < <(bash "$POOL_SCRIPT" status --porcelain 2>/dev/null || true)

  if [[ "$any" -eq 0 ]]; then
    printf "\n  ${DIM}No agent-* worktrees found.${NC}\n"
  fi

  echo ""
  divider
  printf "${DIM}  tip: set WORKTREE_DASHBOARD_FETCH=1 to refresh origin before rendering${NC}\n"
  printf "${DIM}  tip: set WORKTREE_DASHBOARD_PRS=1 to include PR URLs via gh${NC}\n"
  printf "${DIM}  tip: set WORKTREE_DASHBOARD_STATUS=1 to scan each worktree for file changes${NC}\n"
  printf "${DIM}  tip: run 'lazygit' and press 'w' to browse all worktrees${NC}\n"
  echo ""
}

if [[ "${1:-}" == "--watch" ]]; then
  # Buffer each frame and overwrite in place — clear-before-draw flashes blank.
  clear
  while true; do
    frame="$(run_dashboard)"
    frame="${frame//$'\n'/$'\033[K\n'}"   # erase per-line residue when lines shrink
    printf '\033[H%s\033[K\n\033[J' "$frame"
    sleep 5
  done
else
  run_dashboard
fi
