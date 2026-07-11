#!/usr/bin/env bash
set -euo pipefail

# Anchor to the primary worktree even when invoked from inside an agent-N
# worktree: --show-toplevel is CWD-dependent, and a worktree-local
# .worktree-pool/locks holds dead leases from prior tasks (revise then pushes
# to a prior task's branch — the WRONG-BRANCH hazard).
ROOT="$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
LOCK_ROOT="${WORKTREE_POOL_LOCK_ROOT:-$ROOT/.worktree-pool/locks}"
# Locks go stale by AGE, not by a live pid: each agent shell here is ephemeral,
# so the acquiring pid is long dead by the next call. A lock older than the TTL
# with no unpushed work in its slot is reclaimable. Override for tests.
LOCK_TTL_SECONDS="${WORKTREE_POOL_LOCK_TTL:-43200}"
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

  submit <slot> [base_ref] [-- unity_test_agent.ps1 args...]
      Run tests, push to a task-specific remote branch (task/<lease>),
      and create PR — but keep the lock so the agent can respond to
      review feedback.  Test args after -- are passed to
      unity_test_agent.ps1.

  merge <slot> [base_ref] [-- unity_test_agent.ps1 args...]
      Gated squash-merge of the slot's open PR. If base (default:
      origin/main) moved since the slot branch last contained it, merge
      base in, re-run tests, and push before merging — so the tested
      tree is the tree that lands on main. The ONLY sanctioned merge
      path; do not call 'gh pr merge' directly.

  finalize <slot> [base_ref]
      After PR is merged: reset slot branch to base ref (default:
      origin/main), clean the worktree, and release the lock.

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
  scripts/agent_worktree_pool.sh revise agent-1 -- -Mode EditMode -ScopeType Feature -ScopeName camera
  scripts/agent_worktree_pool.sh submit agent-1 origin/main -- -Mode Both -ScopeType Workspace
  scripts/agent_worktree_pool.sh merge agent-1
  scripts/agent_worktree_pool.sh finalize agent-1 origin/main
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

lease_for() {
  local slot="$1"
  # Durable source of truth: the worktree's own git config (survives lock-dir
  # loss, so submit/revise can recover the lease "by id"). Fall back to the
  # lock dir for locks written before this binding existed.
  local path cfg ldir
  path="$(slot_path "$slot" 2>/dev/null || true)"
  if [[ -n "$path" ]]; then
    cfg="$(git -C "$path" config --worktree --get worktree-pool.lease 2>/dev/null || true)"
    if [[ -n "$cfg" ]]; then
      echo "$cfg"
      return 0
    fi
  fi
  ldir="$(lock_dir_for "$slot")"
  cat "$ldir/lease" 2>/dev/null || true
}

task_branch_for() {
  local slot="$1"
  local ldir tb lease
  ldir="$(lock_dir_for "$slot")"
  tb="$(cat "$ldir/task_branch" 2>/dev/null || true)"
  if [[ -n "$tb" ]]; then
    echo "$tb"
    return 0
  fi
  # Derive deterministically from the lease so a missing task_branch file never
  # forces a fall back to the bare slot name (which rebases onto ancient
  # origin/agent-N — the documented REVISE HAZARD).
  lease="$(lease_for "$slot")"
  if [[ -n "$lease" ]]; then
    echo "task/$lease"
  fi
  # Always succeed: a bare failing test as the last line would poison
  # `x="$(task_branch_for ...)"` under `set -e` in callers.
  return 0
}

write_lock() {
  local slot="$1" lease="$2" path="$3"
  local ldir
  ldir="$(lock_dir_for "$slot")"
  printf '%s\n' "$lease" > "$ldir/lease"
  printf '%s\n' "$$" > "$ldir/pid"
  date -u +"%Y-%m-%dT%H:%M:%SZ" > "$ldir/timestamp"
  if [[ -n "$path" ]]; then
    # Worktree-scoped, never the repo-shared .git/config: all slots share that
    # file, so a plain `git config` write here clobbers every other slot's
    # lease and their submit/revise pushes to the wrong task branch (the
    # cross-slot LEASE RACE). The unqualified --unset keeps the shared config
    # clear of the key so a stale cross-slot value can never be read back.
    git -C "$path" config extensions.worktreeConfig true 2>/dev/null || true
    git -C "$path" config --worktree worktree-pool.lease "$lease" 2>/dev/null || true
    git -C "$path" config --unset worktree-pool.lease 2>/dev/null || true
  fi
}

lock_age_seconds() {
  local ldir="$1"
  local ts_file="$ldir/timestamp"
  [[ -f "$ts_file" ]] || { echo 999999999; return 0; }
  local ts now
  ts="$(date -u -d "$(cat "$ts_file")" +%s 2>/dev/null || echo 0)"
  now="$(date -u +%s)"
  echo $(( now - ts ))
}

# Reachable from some remote branch => already pushed => safe to reset/reclaim.
is_head_pushed() {
  local path="$1"
  local remotes
  remotes="$(git -C "$path" branch -r --contains HEAD 2>/dev/null | tr -d ' ' | grep -v '^$' || true)"
  [[ -n "$remotes" ]]
}

# A slot is clobber-safe when its worktree has no uncommitted changes and no
# local commits that aren't already on a remote branch.
slot_is_clobber_safe() {
  local path="$1" base="${2:-origin/main}"
  local dirty ahead
  dirty="$(git -C "$path" status --porcelain 2>/dev/null | wc -l | tr -d ' ')"
  [[ "${dirty:-0}" -eq 0 ]] || return 1
  ahead="$(git -C "$path" rev-list --count "$base"..HEAD 2>/dev/null || echo 0)"
  [[ "${ahead:-0}" -eq 0 ]] && return 0
  is_head_pushed "$path"
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
      local lease pid ts tb
      lease="$(cat "$ldir/lease" 2>/dev/null || true)"
      pid="$(cat "$ldir/pid" 2>/dev/null || true)"
      ts="$(cat "$ldir/timestamp" 2>/dev/null || true)"
      tb="$(cat "$ldir/task_branch" 2>/dev/null || true)"
      echo "$slot | LOCKED | $path | lease=${lease:-unknown} pid=${pid:-unknown} at=${ts:-unknown}${tb:+ branch=$tb}"
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
      write_lock "$slot" "$lease" "$path"
      echo "SLOT=$slot PATH=$path"
      return 0
    fi
    # Locked. Reclaim only if the lock is past its TTL AND the slot holds no
    # unpushed work (never clobber a dead lock's WIP -- the CLOBBER HAZARD).
    local age
    age="$(lock_age_seconds "$ldir")"
    if [[ "$age" -gt "$LOCK_TTL_SECONDS" ]]; then
      if slot_is_clobber_safe "$path"; then
        rm -rf "$ldir"
        if mkdir "$ldir" 2>/dev/null; then
          write_lock "$slot" "$lease" "$path"
          echo "Reclaimed stale lock on $slot (age ${age}s > TTL ${LOCK_TTL_SECONDS}s)" >&2
          echo "SLOT=$slot PATH=$path"
          return 0
        fi
      else
        echo "Skipping $slot: stale lock (age ${age}s) but slot holds unpushed work; leaving locked" >&2
      fi
    fi
  done < <(slots_tsv)

  echo "No free slots" >&2
  return 1
}

cmd_release() {
  local slot="$1"
  local ldir path
  ldir="$(lock_dir_for "$slot")"
  path="$(slot_path "$slot" 2>/dev/null || true)"
  if [[ -n "$path" ]]; then
    git -C "$path" config --worktree --unset worktree-pool.lease 2>/dev/null || true
    git -C "$path" config --unset worktree-pool.lease 2>/dev/null || true
  fi
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
  local force="${3:-}"
  local path
  path="$(slot_path "$slot")"

  git -C "$path" fetch origin

  # Guard: never reset --hard over unpushed work unless explicitly forced.
  if [[ "$force" != "--force" ]] && ! slot_is_clobber_safe "$path" "$base"; then
    local ahead dirty
    ahead="$(git -C "$path" rev-list --count "$base"..HEAD 2>/dev/null || echo 0)"
    dirty="$(git -C "$path" status --porcelain 2>/dev/null | wc -l | tr -d ' ')"
    echo "REFUSING to prepare $slot: it holds unpushed work" >&2
    echo "  ($ahead commit(s) ahead of $base, $dirty uncommitted change(s))." >&2
    echo "  Preserve it first, e.g.:" >&2
    echo "    git -C $path push origin HEAD:refs/heads/task/<lease>" >&2
    echo "  then re-run with --force:" >&2
    echo "    $0 prepare $slot $base --force" >&2
    return 1
  fi

  git -C "$path" checkout "$slot"
  git -C "$path" reset --hard "$base"
  git -C "$path" clean -fd
  # Ignored, so clean leaves it: purge pre-ROOT-anchor leftovers that an old
  # script copy running inside the worktree could still resolve as live locks.
  rm -rf "$path/.worktree-pool"

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

This PR was created via \`scripts/agent_worktree_pool.sh create-pr\`.
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

cmd_submit() {
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

  # Ensure slot branch is checked out (do NOT reset — preserve agent's work)
  local path
  path="$(slot_path "$slot")"
  git -C "$path" checkout "$slot"

  cmd_run_tests "$slot" "${test_args[@]}"

  # Derive a task-specific remote branch from the lease id
  local lease task_branch
  lease="$(lease_for "$slot")"
  if [[ -z "$lease" ]]; then
    lease="task-$(date +%Y%m%d-%H%M%S)"
  fi
  task_branch="task/$lease"

  # Store task branch in lock dir (recreate it if a stale-reclaim removed the
  # dir, so submit never dies with "task_branch: No such file or directory").
  local ldir
  ldir="$(lock_dir_for "$slot")"
  mkdir -p "$ldir"
  printf '%s\n' "$task_branch" > "$ldir/task_branch"

  # Push local slot branch to the task-specific remote branch
  git -C "$path" push -u origin "$slot:refs/heads/$task_branch" >/dev/null

  # Create PR from the task branch
  command -v gh >/dev/null 2>&1 || {
    echo "gh CLI not found in PATH" >&2
    return 1
  }

  git -C "$ROOT" fetch origin "$base_branch" >/dev/null 2>&1 || true

  local existing
  existing="$(gh pr list --head "$task_branch" --base "$base_branch" --state open --json url --jq '.[0].url' 2>/dev/null || true)"
  if [[ -n "$existing" ]]; then
    echo "$slot PR already open: $existing"
  else
    local title body
    title="$(git -C "$path" log --format=%s -n 1 "origin/$base_branch..$slot" 2>/dev/null || true)"
    [[ -n "$title" ]] || title="agent update: $task_branch"

    body=$(cat <<EOF
## Summary
Automated PR from warm worktree slot.

- slot: $slot
- lease: $lease
- branch: $task_branch

This PR was created via \`scripts/agent_worktree_pool.sh submit\`.
EOF
)

    local url
    url="$(gh pr create --base "$base_branch" --head "$task_branch" --title "$title" --body "$body")"
    echo "$slot PR created: $url"
  fi

  echo ""
  echo "PR submitted for $slot (branch: $task_branch). Lock kept —"
  echo "use 'revise' for feedback, then 'finalize' once the PR is merged."
}

cmd_merge() {
  local slot="$1"
  shift || true

  local base_ref="origin/main"
  if [[ -n "${1:-}" && ${1:-} != "--" ]]; then
    base_ref="$1"
    shift
  fi
  [[ ${1:-} != "--" ]] || shift
  local test_args=("$@")

  command -v gh >/dev/null 2>&1 || {
    echo "gh CLI not found in PATH" >&2
    return 1
  }

  local path task_branch base_branch
  path="$(slot_path "$slot")"
  task_branch="$(task_branch_for "$slot")"
  base_branch="${base_ref#origin/}"
  if [[ -z "$task_branch" ]]; then
    echo "merge: no task branch known for $slot (no lease/task_branch)." >&2
    return 1
  fi

  local pr
  pr="$(pr_number_for_slot "$task_branch" "$base_branch")"
  if [[ -z "$pr" || "$pr" == "null" ]]; then
    echo "merge: no open PR found for $task_branch -> $base_branch" >&2
    return 1
  fi

  git -C "$path" fetch origin "$base_branch"
  git -C "$path" checkout "$slot"
  # Gate against the freshly-fetched remote-tracking ref: a bare local name
  # (e.g. 'main') can lag the remote and silently skip the re-test.
  base_ref="origin/$base_branch"

  # If base moved, the branch's last test run predates the merged tree: two PRs
  # each green on their own base can still break main together (no textual
  # conflict, so git merges both silently). Re-test the merged tree before landing.
  if git -C "$path" merge-base --is-ancestor "$base_ref" "$slot"; then
    echo "$base_ref already contained in $slot — last test run covered the merge result."
  else
    echo "$base_ref moved since $slot last synced: merging it in and re-running tests."
    if ! git -C "$path" merge --no-edit "$base_ref"; then
      git -C "$path" merge --abort || true
      echo "merge: conflict merging $base_ref into $slot — resolve in the worktree," >&2
      echo "  'revise' to test+push, then re-run merge." >&2
      return 1
    fi
    cmd_run_tests "$slot" "${test_args[@]}"
    git -C "$path" push origin "$slot:refs/heads/$task_branch"
  fi

  # GitHub recomputes mergeability asynchronously after the gate's push; a
  # merge call inside that window fails "not mergeable" — brief retries ride
  # it out.
  local attempt merged=0
  for attempt in 1 2 3 4 5; do
    if gh pr merge "$pr" --squash --delete-branch=false; then
      merged=1
      break
    fi
    echo "merge: PR #$pr not mergeable yet (attempt $attempt/5) — retrying in 3s..."
    sleep 3
  done
  if [[ "$merged" -ne 1 ]]; then
    echo "merge: gh pr merge failed for PR #$pr after 5 attempts." >&2
    return 1
  fi
  echo ""
  echo "PR #$pr squash-merged. Next: finalize the slot and sync local main:"
  echo "  ./scripts/agent_worktree_pool.sh finalize $slot $base_ref"
}

cmd_finalize() {
  local slot="$1"
  local base_ref="${2:-origin/main}"

  # Clean up remote task branch (PR is merged, branch is no longer needed)
  local task_branch
  task_branch="$(task_branch_for "$slot")"
  if [[ -n "$task_branch" ]]; then
    git -C "$ROOT" push origin --delete "$task_branch" 2>/dev/null || true
  fi

  # Post-merge reset is intentional discard: the slot's task-branch commits are
  # subsumed into main (squash) and the remote task branch was just deleted, so
  # force past the unpushed-work guard.
  cmd_prepare "$slot" "$base_ref" --force
  cmd_release "$slot"

  echo "Finalized $slot: reset to $base_ref and released lock."
}

cmd_review_comments() {
  local slot="$1"
  local base="${2:-main}"

  command -v gh >/dev/null 2>&1 || {
    echo "gh CLI not found in PATH" >&2
    return 1
  }

  # Look up PR by task branch first, fall back to slot name
  local head_branch
  head_branch="$(task_branch_for "$slot")"
  [[ -n "$head_branch" ]] || head_branch="$slot"

  local pr
  pr="$(pr_number_for_slot "$head_branch" "$base")"
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

  local path task_branch
  path="$(slot_path "$slot")"
  task_branch="$(task_branch_for "$slot")"

  # Never fall back to the bare slot name: origin/agent-N is an ancient,
  # unrelated branch, and rebasing onto it replays 100+ commits (REVISE HAZARD).
  if [[ -z "$task_branch" ]]; then
    echo "revise: no task branch known for $slot (no lease/task_branch)." >&2
    echo "  Push manually instead: git -C $path push origin $slot:refs/heads/task/<lease>" >&2
    return 1
  fi

  git -C "$path" fetch origin
  git -C "$path" checkout "$slot"

  if git -C "$path" rev-parse "origin/$task_branch" >/dev/null 2>&1; then
    git -C "$path" pull --rebase origin "$task_branch"
  fi

  cmd_run_tests "$slot" "${test_args[@]}"

  git -C "$path" push origin "$slot:refs/heads/$task_branch"
  echo "Revised and pushed $slot -> $task_branch"
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
    submit)
      [[ $# -ge 1 ]] || { echo "submit requires <slot> [base_ref] [-- test_args...]" >&2; exit 1; }
      cmd_submit "$@"
      ;;
    merge)
      [[ $# -ge 1 ]] || { echo "merge requires <slot> [base_ref] [-- test_args...]" >&2; exit 1; }
      cmd_merge "$@"
      ;;
    finalize)
      [[ $# -ge 1 ]] || { echo "finalize requires <slot> [base_ref]" >&2; exit 1; }
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
