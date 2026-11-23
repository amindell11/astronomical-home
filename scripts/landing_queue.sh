#!/usr/bin/env bash
set -euo pipefail

# Landing queue: brings auto-merge PRs up to date with main one at a time, so a main move re-runs
# the hosted suite on the front PR only. GitHub auto-merge does the merging once land-check passes;
# this script never merges, never resolves a conflict and never decides who may merge.
# Run by .github/workflows/landing-queue.yml from main's copy.
#
# Usage: landing_queue.sh step
#        landing_queue.sh reset <pr>
#   The queue: open, non-draft PRs into main from a task/* branch of this repository with
#   GitHub auto-merge enabled, lowest PR number first.
#   step   one pass. While a PR carries queue:updating (its branch was updated and its suite
#          dispatched), it is the front and nothing else moves:
#            - BEHIND or DIRTY again: the label comes off and the pass picks afresh;
#            - merge-proof/headless or land-check failure on its head, or the label older than
#              UPDATE_TIMEOUT_MIN: queue:updating → queue:stuck, with a PR comment;
#            - headless success with no land-check success yet: dispatches land-check;
#            - otherwise it waits.
#          With no front, the first BEHIND member without queue:stuck or queue:conflict whose
#          land-check on its head is success, or pending with description `behind` (ready but for
#          main), gets GitHub's update-branch, then the headless suite dispatched on its branch
#          (pushes under GITHUB_TOKEN start no workflow) and queue:updating. An update GitHub
#          refuses for a conflict labels the PR queue:conflict, comments, and the next one is tried.
#   reset  takes the queue's labels off <pr>; the workflow runs it on each push to a PR branch,
#          so a pushed fix re-enters the queue.
# Env: GH_TOKEN, GITHUB_REPOSITORY (owner/repo), UPDATE_TIMEOUT_MIN (default 120).
# Exit: 0 done · 1 infra (a gh call failed) · 2 usage.
# Stdout: one trailer, QUEUE=idle | wait:<pr> | updated:<pr> | stuck:<pr> | landcheck:<pr> |
#         reset:<pr>; conflict:<pr> lines may come before it. Prose to stderr.

UPDATING=queue:updating
STUCK=queue:stuck
CONFLICT=queue:conflict
UPDATE_TIMEOUT_MIN="${UPDATE_TIMEOUT_MIN:-120}"
REPO="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is required}"

usage() { echo "Usage: landing_queue.sh step | reset <pr>" >&2; exit 2; }
infra() { echo "landing_queue: $1" >&2; exit 1; }
say() { echo "landing_queue: $1" >&2; }

# number, branch, head sha, mergeStateStatus, comma-joined labels; tab-separated, lowest first.
members() {
  gh pr list --repo "$REPO" --base main --state open --limit 100 \
    --json number,headRefName,headRefOid,isDraft,isCrossRepository,autoMergeRequest,mergeStateStatus,labels \
    --jq 'map(select(.autoMergeRequest != null and (.isDraft | not) and (.isCrossRepository | not)
                     and (.headRefName | startswith("task/"))))
          | sort_by(.number) | .[]
          | [.number, .headRefName, .headRefOid, .mergeStateStatus, ([.labels[].name] | join(","))] | @tsv'
}

# status <sha> <context>: "<state>\t<description>" of the newest status, empty when none.
status() {
  gh api "repos/$REPO/commits/$1/status" \
    --jq ".statuses[] | select(.context == \"$2\") | [.state, .description] | @tsv" | head -n 1
}

has_label() { [[ ",$1," == *",$2,"* ]]; }
label() { gh pr edit "$1" --repo "$REPO" --add-label "$2" >/dev/null || infra "could not label #$1 $2"; }
unlabel() { gh pr edit "$1" --repo "$REPO" --remove-label "$2" >/dev/null || infra "could not unlabel #$1 $2"; }
comment() { gh pr comment "$1" --repo "$REPO" --body "$2" >/dev/null || infra "could not comment on #$1"; }

labelled_minutes_ago() {
  local at
  at="$(gh api --paginate "repos/$REPO/issues/$1/events" \
    --jq ".[] | select(.event == \"labeled\" and .label.name == \"$UPDATING\") | .created_at" | tail -n 1)" \
    || infra "could not read #$1's events"
  [[ -n "$at" ]] || { echo 0; return; }
  echo $(( ($(date -u +%s) - $(date -u -d "$at" +%s)) / 60 ))
}

stick() {
  local pr="$1" why="$2"
  unlabel "$pr" "$UPDATING"
  label "$pr" "$STUCK"
  comment "$pr" "Landing queue: $why. The queue skips this PR until its branch gets a new push."
  echo "QUEUE=stuck:$pr"
}

# Returns 0 when the pass is over (the front is waiting or was handled), 1 when there is no front.
tend_front() {
  local pr branch sha state labels headless land
  while IFS=$'\t' read -r pr branch sha state labels; do
    [[ -n "$pr" ]] || continue
    has_label "$labels" "$UPDATING" || continue
    if [[ "$state" == BEHIND || "$state" == DIRTY ]]; then
      say "#$pr is $state again; picking afresh."
      unlabel "$pr" "$UPDATING"
      return 1
    fi
    headless="$(status "$sha" merge-proof/headless)" || infra "could not read #$pr's statuses"
    land="$(status "$sha" land-check)" || infra "could not read #$pr's statuses"
    case "${headless%%$'\t'*}" in
      failure|error) stick "$pr" "the headless suite failed on \`${sha:0:7}\` after the branch update"; return 0 ;;
    esac
    if [[ "${land%%$'\t'*}" == failure ]]; then
      stick "$pr" "land-check failed on \`${sha:0:7}\` (${land#*$'\t'})"
      return 0
    fi
    if [[ "$(labelled_minutes_ago "$pr")" -ge "$UPDATE_TIMEOUT_MIN" ]]; then
      stick "$pr" "it has not landed ${UPDATE_TIMEOUT_MIN} minutes after its branch update (land-check: ${land:-none})"
      return 0
    fi
    if [[ "${headless%%$'\t'*}" == success && "${land%%$'\t'*}" != success ]]; then
      gh workflow run land-check.yml --repo "$REPO" --ref main -f pr="$pr" >/dev/null \
        || infra "could not dispatch land-check for #$pr"
      echo "QUEUE=landcheck:$pr"
      return 0
    fi
    echo "QUEUE=wait:$pr"
    return 0
  done <<< "$QUEUE_ROWS"
  return 1
}

# update <pr> <branch> <sha>: 0 updated, 1 conflict.
update() {
  local pr="$1" branch="$2" sha="$3" err head i
  if ! err="$(gh api -X PUT "repos/$REPO/pulls/$pr/update-branch" -f expected_head_sha="$sha" 2>&1)"; then
    [[ "$err" == *conflict* ]] || infra "update-branch on #$pr failed: $err"
    label "$pr" "$CONFLICT"
    comment "$pr" "Landing queue: \`$branch\` conflicts with main, so the queue cannot update it. Merge main into it and push; the queue picks it up again."
    echo "QUEUE=conflict:$pr"
    return 1
  fi
  for i in $(seq 1 24); do
    head="$(gh api "repos/$REPO/pulls/$pr" --jq .head.sha)" || infra "could not read #$pr's head"
    [[ "$head" == "$sha" ]] || break
    sleep 5
  done
  [[ "$head" != "$sha" ]] || infra "#$pr's head did not move within 2 minutes of update-branch"
  gh workflow run headless-suite.yml --repo "$REPO" --ref "$branch" >/dev/null \
    || infra "could not dispatch the headless suite on $branch"
  label "$pr" "$UPDATING"
  echo "QUEUE=updated:$pr"
}

cmd_step() {
  local pr branch sha state labels land l
  for l in "$UPDATING" "$STUCK" "$CONFLICT"; do
    gh label create "$l" --repo "$REPO" --force --color ededed \
      --description "Landing queue state (scripts/landing_queue.sh)" >/dev/null || infra "could not create label $l"
  done
  QUEUE_ROWS="$(members)" || infra "could not list the queue"
  tend_front && return 0
  while IFS=$'\t' read -r pr branch sha state labels; do
    [[ -n "$pr" && "$state" == BEHIND ]] || continue
    if has_label "$labels" "$STUCK" || has_label "$labels" "$CONFLICT"; then continue; fi
    land="$(status "$sha" land-check)" || infra "could not read #$pr's statuses"
    [[ "$land" == success$'\t'* || "$land" == $'pending\tbehind' ]] || continue
    update "$pr" "$branch" "$sha" && return 0
  done <<< "$QUEUE_ROWS"
  echo "QUEUE=idle"
}

cmd_reset() {
  local pr="$1" labels l
  labels="$(gh pr view "$pr" --repo "$REPO" --json labels --jq '[.labels[].name] | join(",")')" \
    || infra "could not read #$pr's labels"
  for l in "$UPDATING" "$STUCK" "$CONFLICT"; do
    if has_label "$labels" "$l"; then unlabel "$pr" "$l"; fi
  done
  echo "QUEUE=reset:$pr"
}

case "${1:-}" in
  step) [[ $# -eq 1 ]] || usage; cmd_step ;;
  reset) [[ $# -eq 2 && "$2" =~ ^[1-9][0-9]*$ ]] || usage; cmd_reset "$2" ;;
  *) usage ;;
esac
