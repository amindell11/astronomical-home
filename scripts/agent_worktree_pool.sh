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

  create-pr <slot> [base]
      Push slot branch and create a PR with gh (default base: main).
      If an open PR already exists for that head/base, prints URL.

  create-pool-prs [base]
      Create PRs for all agent-* slots that are ahead of base.

  finalize <slot> [base_ref] [-- unity_test_agent.ps1 args...]
      End-to-end helper: prepare slot, run tests, create PR, release lock.
      Test args after -- are passed to unity_test_agent.ps1.

  review-comments <slot> [base]
      Show open PR URL and unresolved review threads/comments for slot.

  revise <slot> [-- unity_test_agent.ps1 args...]
      Update existing slot branch for PR feedback: pull --rebase, run tests,
      then push branch updates (no reset to main).

Examples:
  scripts/agent_worktree_pool.sh status
  scripts/agent_worktree_pool.sh acquire task-123
  scripts/agent_worktree_pool.sh prepare agent-1 origin/main
  scripts/agent_worktree_pool.sh run-tests agent-1 -Mode EditMode -ScopeType Smoke
  scripts/agent_worktree_pool.sh create-pr agent-1
  scripts/agent_worktree_pool.sh create-pool-prs
  scripts/agent_worktree_pool.sh review-comments agent-1
  scripts/agent_worktree_pool.sh revise agent-1 -- -Mode Smoke -ScopeType Feature -ScopeName camera
  scripts/agent_worktree_pool.sh finalize agent-1 origin/main -- -Mode Smoke
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

repo_slug() {
  local url
  url="$(git -C "$ROOT" config --get remote.origin.url)"
  if [[ "$url" =~ ^git@github.com:([^/]+)/([^/.]+)(\.git)?$ ]]; then
    echo "${BASH_REMATCH[1]}/${BASH_REMATCH[2]}"
  elif [[ "$url" =~ ^https?://github.com/([^/]+)/([^/.]+)(\.git)?$ ]]; then
    echo "${BASH_REMATCH[1]}/${BASH_REMATCH[2]}"
  else
    echo ""
  fi
}

pr_number_for_slot() {
  local slot="$1"
  local base="${2:-main}"
  gh pr list --head "$slot" --base "$base" --state open --json number --jq '.[0].number' 2>/dev/null || true
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

cmd_create_pr() {
  local slot="$1"
  local base="${2:-main}"

  command -v gh >/dev/null 2>&1 || {
    echo "gh CLI not found in PATH" >&2
    return 1
  }

  git -C "$ROOT" fetch origin "$base" >/dev/null 2>&1 || true

  local ahead
  ahead="$(git -C "$ROOT" rev-list --count "$base..$slot" 2>/dev/null || echo 0)"
  if [[ "$ahead" -eq 0 ]]; then
    echo "Skipping $slot: no commits ahead of $base"
    return 0
  fi

  git -C "$ROOT" push -u origin "$slot" >/dev/null

  local existing
  existing="$(gh pr list --head "$slot" --base "$base" --state open --json url --jq '.[0].url' 2>/dev/null || true)"
  if [[ -n "$existing" ]]; then
    echo "$slot PR already open: $existing"
    return 0
  fi

  local title body
  title="$(git -C "$ROOT" log --format=%s -n 1 "$base..$slot" 2>/dev/null || true)"
  [[ -n "$title" ]] || title="agent update: $slot"

  body=$(cat <<EOF
## Summary
Automated PR from warm worktree slot.

- slot: $slot
- branch: $slot

This PR was created via `scripts/agent_worktree_pool.sh create-pr`.
EOF
)

  local url
  url="$(gh pr create --base "$base" --head "$slot" --title "$title" --body "$body")"
  echo "$slot PR created: $url"
}

cmd_create_pool_prs() {
  local base="${1:-main}"
  while IFS=$'\t' read -r slot _path; do
    cmd_create_pr "$slot" "$base"
  done < <(slots_tsv)
}

cmd_finalize() {
  local slot="$1"
  local base_ref="${2:-origin/main}"
  shift 2 || true

  local test_args=()
  if [[ ${1:-} == "--" ]]; then
    shift
    test_args=("$@")
  elif [[ $# -gt 0 ]]; then
    test_args=("$@")
  fi

  local base_branch
  base_branch="${base_ref#origin/}"

  cmd_prepare "$slot" "$base_ref"
  cmd_run_tests "$slot" "${test_args[@]}"
  cmd_create_pr "$slot" "$base_branch"
  cmd_release "$slot"
}

cmd_review_comments() {
  local slot="$1"
  local base="${2:-main}"

  command -v gh >/dev/null 2>&1 || {
    echo "gh CLI not found in PATH" >&2
    return 1
  }

  local pr
  pr="$(pr_number_for_slot "$slot" "$base")"
  if [[ -z "$pr" || "$pr" == "null" ]]; then
    echo "No open PR found for slot=$slot base=$base"
    return 1
  fi

  local slug owner repo
  slug="$(repo_slug)"
  owner="${slug%%/*}"
  repo="${slug##*/}"
  if [[ -z "$owner" || -z "$repo" ]]; then
    echo "Could not derive owner/repo from origin remote" >&2
    return 1
  fi

  echo "PR #$pr ($(gh pr view "$pr" --json url --jq '.url'))"
  echo
  echo "Unresolved review threads:"

  local unresolved
  unresolved="$(gh api graphql \
    -F owner="$owner" \
    -F repo="$repo" \
    -F number="$pr" \
    -f query='query($owner:String!, $repo:String!, $number:Int!) { repository(owner:$owner, name:$repo) { pullRequest(number:$number) { reviewThreads(first:100) { nodes { isResolved isOutdated path line comments(first:20) { nodes { author { login } body url } } } } } } }' \
    --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved == false) | "- " + (.path // "(no-path)") + ":" + ((.line // 0)|tostring) + "\n  " + (.comments.nodes[-1].author.login // "unknown") + ": " + ((.comments.nodes[-1].body // "") | gsub("\n"; " ")) + "\n  " + (.comments.nodes[-1].url // "")' 2>/dev/null || true)"

  if [[ -z "$unresolved" ]]; then
    echo "(none)"
  else
    echo "$unresolved"
  fi

  echo
  echo "Conversation comments:"
  gh pr view "$pr" --comments
}

cmd_revise() {
  local slot="$1"
  shift || true

  local test_args=()
  if [[ ${1:-} == "--" ]]; then
    shift
    test_args=("$@")
  elif [[ $# -gt 0 ]]; then
    test_args=("$@")
  fi

  local path
  path="$(slot_path "$slot")"

  git -C "$path" fetch origin
  git -C "$path" checkout "$slot"
  git -C "$path" pull --rebase origin "$slot"

  cmd_run_tests "$slot" "${test_args[@]}"

  git -C "$path" push origin "$slot"
  echo "Revised and pushed $slot"
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
    create-pr)
      [[ $# -ge 1 ]] || { echo "create-pr requires <slot> [base]" >&2; exit 1; }
      cmd_create_pr "$@"
      ;;
    create-pool-prs)
      cmd_create_pool_prs "$@"
      ;;
    finalize)
      [[ $# -ge 1 ]] || { echo "finalize requires <slot> [base_ref] [-- test_args...]" >&2; exit 1; }
      cmd_finalize "$@"
      ;;
    review-comments)
      [[ $# -ge 1 ]] || { echo "review-comments requires <slot> [base]" >&2; exit 1; }
      cmd_review_comments "$@"
      ;;
    revise)
      [[ $# -ge 1 ]] || { echo "revise requires <slot> [-- test_args...]" >&2; exit 1; }
      cmd_revise "$@"
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
