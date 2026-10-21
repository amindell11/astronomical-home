#!/usr/bin/env bash
set -euo pipefail

# Decision reconcile (arc #830 Slice-5): projects an issue's or PR's open decision onto the board's
# `Decision` text field, which the decisions view lists. The decision lives on the item, in its
# comments and PR state, and `drain_pick.sh decision` reads it; this script is the field's only
# writer. It adds an item to the board only to set the field, never removes one, and writes
# nothing when the field already matches.
#
# Usage: decision_reconcile.sh <n> [--dry-run]
#        decision_reconcile.sh --all [--dry-run]
#   <n>        one issue or PR.
#   --all      every open issue and PR, and every board item whose `Decision` is set (a row an
#              unseen event left stale).
#   --dry-run  reads only: every write is printed as a WRITE= line and run nowhere.
# Env:  GH_TOKEN (drain_pick.sh's REST reads and the open-item list) · PROJECTS_TOKEN (board reads
#       and writes; classic PAT, `project` scope only — GITHUB_TOKEN cannot touch a user-owned
#       project) · GITHUB_REPOSITORY (owner/repo; default `gh repo view`) · GITHUB_STEP_SUMMARY
#       (optional: the report is appended there).
# Exit: 0 done · 1 infra (gh or drain_pick.sh failed, or PROJECTS_TOKEN absent on a live run) ·
#       2 usage.
# Stdout trailers, one per line, stable — one block per item, in number order:
#   ITEM=<n>  DECISION=<line>|none  (drain_pick.sh decision's verdict)
#   WRITE=<board.sh primitive> <its arguments>  (one per write, applied or — dry run — printed)
# then the closing trailers: ITEMS=<count>  WRITES=<count>  DRY_RUN=<0|1>
# Prose to stderr.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/board.sh
. "$SCRIPT_DIR/lib/board.sh"

usage() { echo "Usage: decision_reconcile.sh (<n> | --all) [--dry-run]" >&2; exit 2; }
infra() { echo "decision_reconcile: $1" >&2; exit 1; }
say() { echo "decision_reconcile: $1" >&2; }

ITEM="" DRY_RUN=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --all) [[ -z "$ITEM" ]] || usage; ITEM=all; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    *) [[ -z "$ITEM" && "$1" =~ ^[0-9]+$ ]] || usage; ITEM="$1"; shift ;;
  esac
done
[[ -n "$ITEM" ]] || usage

REPO="${GITHUB_REPOSITORY:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"
[[ -n "${PROJECTS_TOKEN:-}" || "$DRY_RUN" -eq 1 ]] || infra "PROJECTS_TOKEN is not set (board reads and writes need it)"

SUMMARY_LINES=()
ITEMS=0 WRITES=0 WROTE=""
# write <board.sh primitive> <args>…: prints the WRITE line, then (live) runs it under
# PROJECTS_TOKEN with its stdout in WROTE.
write() {
  echo "WRITE=$*"
  SUMMARY_LINES+=("- \`$*\`")
  WRITES=$((WRITES + 1))
  WROTE=""
  [[ "$DRY_RUN" -eq 0 ]] || return 0
  WROTE="$(GH_TOKEN="$PROJECTS_TOKEN" "$@")" || infra "board write failed: $*"
}

reconcile() {
  local n="$1" out line item
  out="$(bash "$SCRIPT_DIR/drain_pick.sh" decision "$n")" || infra "drain_pick.sh decision $n failed"
  [[ "$out" == "DECISION=$n "* ]] || infra "drain_pick.sh decision $n printed no verdict: $out"
  line="${out#DECISION=$n }"
  echo "ITEM=$n"
  echo "DECISION=$line"
  ITEMS=$((ITEMS + 1))
  [[ "$line" != none ]] || line=""
  if [[ -n "${PROJECTS_TOKEN:-}" ]]; then
    GH_TOKEN="$PROJECTS_TOKEN" board_item "$REPO" "$n" Decision || infra "board read of #$n failed"
  else
    BOARD_NODE="<node-id>" BOARD_ITEM="" BOARD_TEXT=""
    say "PROJECTS_TOKEN absent: #$n's board row unknown, printing the writes for an item not on the board"
  fi
  [[ "$line" != "$BOARD_TEXT" ]] || return 0
  if [[ -z "$line" ]]; then
    write board_clear "$BOARD_ITEM" "$BOARD_DECISION_FIELD_ID"
    return
  fi
  item="$BOARD_ITEM"
  if [[ -z "$item" ]]; then
    write board_add "$BOARD_NODE"
    item="${WROTE:-<item-id>}"
  fi
  write board_set_text "$item" "$BOARD_DECISION_FIELD_ID" "$line"
}

if [[ "$ITEM" == all ]]; then
  numbers="$(gh api --paginate "repos/$REPO/issues?state=open&per_page=100" --jq '.[].number')" \
    || infra "gh api (open issues and PRs) failed"
  if [[ -n "${PROJECTS_TOKEN:-}" ]]; then
    numbers+=$'\n'"$(GH_TOKEN="$PROJECTS_TOKEN" board_texts "$REPO" Decision)" || infra "board read of set rows failed"
  else
    say "PROJECTS_TOKEN absent: board rows with a Decision set are not read"
  fi
  for n in $(sort -nu <<<"$numbers"); do reconcile "$n"; done
else
  reconcile "$ITEM"
fi

echo "ITEMS=$ITEMS"
echo "WRITES=$WRITES"
echo "DRY_RUN=$DRY_RUN"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "## Decision reconcile ($ITEM)"
    echo "- Items: $ITEMS · Writes: $WRITES · Dry run: $DRY_RUN"
    printf '%s\n' "${SUMMARY_LINES[@]:-- no write}"
  } >> "$GITHUB_STEP_SUMMARY"
fi
