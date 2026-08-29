#!/usr/bin/env bash
set -euo pipefail

# Anchor to the primary worktree: --show-toplevel is CWD-dependent, and a worktree-local lock dir holds dead leases (the WRONG-BRANCH hazard).
ROOT="$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCK_ROOT="${WORKTREE_POOL_LOCK_ROOT:-$ROOT/.worktree-pool/locks}"
# Locks go stale by AGE, not pid — each agent shell is ephemeral, so the acquiring pid is dead by the next call. TTL override for tests.
LOCK_TTL_SECONDS="${WORKTREE_POOL_LOCK_TTL:-43200}"
mkdir -p "$LOCK_ROOT"

usage() {
  cat <<'EOF'
Usage: scripts/agent_worktree_pool.sh <command> [args]

Commands:
  status [--porcelain]
      List agent-* worktree slots and lock status. Plain output is human-only
      and carries no contract.

      --porcelain is THE pool's read interface - the only sanctioned way for
      another script to learn slot state. One record per slot, KEY=value one
      per line, records separated by a blank line (git's own --porcelain
      shape; values may contain spaces, keys never do):

        slot=agent-1            always
        state=free|locked|stale always; stale = locked past the lock TTL
        path=<abs-path>         always
        lease=<lease-id>        locked/stale slots that have a lease
        task_branch=task/<lease>  when one is recorded or derivable; this is
                                the branch a PR for the slot is opened FROM
        age_seconds=<int>       locked/stale only
        locked_by_pid=<pid>     locked/stale only, informational: pool locks
                                go stale by AGE, never by pid liveness
        locked_at=<iso8601>     locked/stale only

      Keys may be added; consumers must ignore unknown keys and tolerate any
      optional key being absent.

  acquire [lease_id] [slot]
      Lock and return an available slot. Auto-pick prefers genuinely
      free slots over stale-lock reclaims. Naming a slot is strict:
      if it isn't free (or safely reclaimable) acquire FAILS — no
      silent fallback to auto-pick.
      Output: SLOT=<name> PATH=<abs-path>

  release <slot>
      Release slot lock (e.g., agent-1).

  prepare <slot> [base_ref]
      Reset slot branch/worktree to base ref (default: origin/main)
      while preserving ignored dirs (e.g., Unity Library/).

  run-tests <slot> [unity_test_agent.ps1 args...]
      Run Unity tests in that slot with standardized outDir:
      results/unity-tests-agent

  run-resharper <slot> [base_ref]
      Run the Unity-aware ReSharper changed-line ratchet against base_ref
      (default: origin/main).

  run-script-tests [dir]
      Run every scripts/tests/test_*.sh (bash) and test_*.ps1
      (powershell.exe) under dir (default: the primary worktree). Prints
      one PASS/FAIL line per file and stops at the first failure.
      Non-hermetic files are SKIPped unless
      SCRIPT_TESTS_INCLUDE_NONHERMETIC=1. Exit 0 = all green. The merge
      gate runs this when the landing diff touches scripts/.

  create-pr <slot> [base] --title "<text>" (--body "<text>" | --body-file <path>)
      Push the slot's work to its task branch (task/<lease>, recorded
      for merge/revise like submit) and create a PR with gh (default
      base: main) — submit without the test run. An explicit --title
      and exactly one of --body/--body-file are REQUIRED — the PR must
      describe the change, not echo the last commit subject. If an open
      PR already exists for that head/base, prints URL.

  create-pool-prs [base]
      Create PRs for all agent-* slots that are ahead of base.
      (Each PR needs its own --title/--body, so this fails per slot
      until invoked via create-pr with explicit flags.)

  submit <slot> [base_ref] --title "<text>" (--body "<text>" | --body-file <path>) [-- unity_test_agent.ps1 args...]
      Run tests and the ReSharper ratchet, push to a task-specific remote
      branch (task/<lease>), and create PR — but keep the lock so the agent
      can respond to review feedback. An explicit --title and exactly one of
      --body/--body-file are REQUIRED. Test args after -- are passed to
      unity_test_agent.ps1. Only a passing FULL run (-Mode Both,
      -ScopeType Workspace, unfiltered) records merge-grade proof;
      scoped runs still open the PR but the merge gate will re-test.

  merge-progress <slot> [--oneline]
      Render that slot's merge gate journal: per-phase wall clock, which
      phase is open and for how long, and any phase over its budget. Reads
      the live run if one is in flight, else the slot's most recent. Safe
      to call from any session, including one that did not start the merge.
      --oneline prints one compact line for a merge still in flight and
      nothing otherwise (what worktree_dashboard.sh consumes).

  merge <slot> [base_ref] [-- unity_test_agent.ps1 args...]
      Gated squash-merge of the slot's open PR. Merges base (default:
      origin/main) in if it moved, then re-runs the full suite unless
      the exact resulting tree has recorded full-coverage proof — so
      the tree that lands on main is a tree that actually passed
      everything. Deltas since the proven tree that are markdown-only
      (*.md) extend the proof without a run; C# comment/whitespace-only
      deltas take an EditMode Smoke compile refresh instead of the
      full suite. Runs test the working tree, so submit/revise/merge
      refuse to start a proof-bearing run on a dirty worktree. The ONLY
      sanctioned merge path; it also requires the exact landing tree to pass
      the ReSharper ratchet, plus the scripts/tests suite when the landing
      diff touches scripts/. Do not call 'gh pr merge' directly.

  finalize <slot> [base_ref]
      After PR is merged: reset slot branch to base ref (default:
      origin/main), clean the worktree, and release the lock.

  review-comments <slot> [base]
      Show open PR URL and unresolved review threads/comments for slot.

  revise <slot> [--no-test] [-- unity_test_agent.ps1 args...]
      Update existing slot branch for PR feedback: pull --rebase, run tests
      unless --no-test, run the ReSharper ratchet, then push branch updates
      (no reset to main). With --no-test, record no test proof; the merge gate
      then runs the single full suite on the exact landing tree.

Examples:
  scripts/agent_worktree_pool.sh status
  scripts/agent_worktree_pool.sh acquire task-123
  scripts/agent_worktree_pool.sh acquire task-123 agent-4
  scripts/agent_worktree_pool.sh prepare agent-1 origin/main
  scripts/agent_worktree_pool.sh run-tests agent-1 -Mode EditMode -ScopeType Smoke
  scripts/agent_worktree_pool.sh run-resharper agent-1 origin/main
  scripts/agent_worktree_pool.sh create-pr agent-1 --title "feat(x): add y" --body "## Summary\n..."
  scripts/agent_worktree_pool.sh create-pool-prs
  scripts/agent_worktree_pool.sh review-comments agent-1
  scripts/agent_worktree_pool.sh revise agent-1 -- -Mode EditMode -ScopeType Feature -ScopeName camera
  scripts/agent_worktree_pool.sh revise agent-1 --no-test
  scripts/agent_worktree_pool.sh submit agent-1 origin/main --title "fix(nav): clamp turn rate" --body-file pr_body.md -- -Mode Both -ScopeType Workspace
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
  # Worktree git config is the durable lease source (survives lock-dir loss); the lock dir is the legacy fallback.
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
  # Derive from the lease so a missing task_branch file never falls back to the bare slot name (rebases onto ancient origin/agent-N — the REVISE HAZARD).
  lease="$(lease_for "$slot")"
  if [[ -n "$lease" ]]; then
    echo "task/$lease"
  fi
  # Always succeed: a failing last line would poison callers' command substitution under set -e.
  return 0
}

# Every PR-opener mints/records here so merge/revise (task_branch_for) resolve the pushed head.
ensure_task_branch() {
  local slot="$1"
  local lease task_branch ldir
  lease="$(lease_for "$slot")"
  if [[ -z "$lease" ]]; then
    lease="task-$(date +%Y%m%d-%H%M%S)"
  fi
  task_branch="task/$lease"
  # A stale-reclaim may have removed the lock dir; the task_branch write must not die.
  ldir="$(lock_dir_for "$slot")"
  mkdir -p "$ldir"
  printf '%s\n' "$task_branch" > "$ldir/task_branch"
  echo "$task_branch"
}

SUMMARY_REL="results/unity-tests-agent/latest-summary.json"

# Stale-summary hazard: an older run's summary could vouch for a run that never wrote one; proof-recording callers clear it before the runner starts.
clear_run_summary() {
  local path="$1"
  rm -f "$path/$SUMMARY_REL"
}

# The runner stamps the coverage verdict (unity_test_agent.ps1 -> summary.coverage); this reads that
# one field and checks only the thing the pool owns - that the summary is about THIS slot's project.
# No re-derivation of the runner's selection semantics lives here (script-contracts.md sec.3).
COVERAGE_READER='
$ErrorActionPreference = "Stop"
function Canon($p) { return "$p".Replace("\", "/").TrimEnd("/").ToLower() }
try { $s = Get-Content -LiteralPath $env:POOL_SUMMARY_JSON -Raw | ConvertFrom-Json } catch { Write-Output "partial|summary unreadable"; exit 0 }
$expected = Canon $env:POOL_EXPECTED_PROJECT
if ($expected -eq "" -or (Canon $s.projectPath) -ne $expected) { Write-Output ("partial|projectPath=" + $s.projectPath + " (expected " + $expected + ")"); exit 0 }
$coverage = $s.PSObject.Properties["coverage"]
if ($null -eq $coverage -or $null -eq $coverage.Value) { Write-Output "partial|summary has no coverage field (pre-stamp run or foreign producer)"; exit 0 }
$verdict = "$($coverage.Value.verdict)".Trim().ToLower()
$reason = "$($coverage.Value.reason)"
if ($verdict -ne "full" -and $verdict -ne "partial") { Write-Output ("partial|coverage.verdict=" + $verdict + " is not a verdict"); exit 0 }
Write-Output ($verdict + "|" + $reason)
'

# Prints "full|<detail>" or "partial|<reason>"; a missing, unreadable, unstamped or wrong-project summary is all partial (fail closed).
summary_coverage() {
  local summary="$1" expected_project="$2" out=""
  [[ -f "$summary" ]] || { echo "partial|no summary at $summary"; return 0; }
  out="$(POOL_SUMMARY_JSON="$summary" POOL_EXPECTED_PROJECT="$expected_project" powershell.exe -NoProfile -Command "$COVERAGE_READER" 2>/dev/null || true)"
  case "$out" in full\|*|partial\|*) printf '%s\n' "$out"; return 0 ;; esac
  echo "partial|coverage field unreadable (powershell.exe gave no verdict)"
  return 0
}

write_tested_scope() {
  local ldir="$1" tree="$2" kind="$3" anchor="$4" detail="$5"
  {
    printf 'tree=%s\n' "$tree"
    printf 'kind=%s\n' "$kind"
    printf 'anchor=%s\n' "$anchor"
    printf 'detail=%s\n' "$detail"
    printf 'recordedAt=%s\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  } > "$ldir/tested_scope"
}

# Merge-grade proof = exact tree hash + provenance of a passing FULL run (Mode Both, ScopeType Workspace, unfiltered), recorded only after the runner exits 0; anything narrower records nothing so the gate re-tests (fail closed). A local base-merge commit alone is never evidence.
record_tested_tree() {
  local slot="$1" path="$2"
  local ldir verdict detail tree
  ldir="$(lock_dir_for "$slot")"
  mkdir -p "$ldir"
  verdict="$(summary_coverage "$path/$SUMMARY_REL" "$path/src/Asteroids3D")"
  detail="${verdict#*|}"
  verdict="${verdict%%|*}"
  if [[ "$verdict" != "full" ]]; then
    echo "No merge-grade proof recorded ($detail); the merge gate will run the full suite."
    return 0
  fi
  tree="$(git -C "$path" rev-parse 'HEAD^{tree}')"
  printf '%s\n' "$tree" > "$ldir/tested_tree"
  write_tested_scope "$ldir" "$tree" "full-run" "$tree" "$detail"
}

tested_tree_for() {
  local slot="$1"
  cat "$(lock_dir_for "$slot")/tested_tree" 2>/dev/null || true
}

tested_scope_field() {
  local slot="$1" key="$2"
  sed -n "s/^${key}=//p" "$(lock_dir_for "$slot")/tested_scope" 2>/dev/null | head -n 1
}

# Only a provenance-corroborated tree counts: a bare tested_tree (legacy scoped-run recordings) is not merge evidence.
verified_proof_tree() {
  local slot="$1" tree
  tree="$(tested_tree_for "$slot")"
  if [[ -n "$tree" && "$(tested_scope_field "$slot" tree)" == "$tree" ]]; then
    echo "$tree"
  fi
  return 0
}

extend_proof() {
  local slot="$1" tree="$2" kind="$3" prior_tree="$4"
  local ldir anchor
  ldir="$(lock_dir_for "$slot")"
  anchor="$(tested_scope_field "$slot" anchor)"
  [[ -n "$anchor" ]] || anchor="$prior_tree"
  mkdir -p "$ldir"
  printf '%s\n' "$tree" > "$ldir/tested_tree"
  write_tested_scope "$ldir" "$tree" "$kind" "$anchor" "inherited from fully-tested tree $prior_tree"
}

# The runner tests the WORKING TREE, so proof for the committed tree is a lie unless they match; also catches the recurring "submit doesn't commit" mistake.
require_clean_slot() {
  local slot="$1" path="$2" action="$3"
  local dirty
  dirty="$(git -C "$path" status --porcelain 2>/dev/null || echo "status-failed")"
  [[ -z "$dirty" ]] && return 0
  echo "$action: $slot worktree has uncommitted/untracked changes — tests would cover a tree that is not the committed one. Commit (or clean) first:" >&2
  printf '%s\n' "$dirty" | head -n 20 >&2
  return 1
}

# CallerLineNumber/CallerArgumentExpression et al. make comment/whitespace edits behavior-visible (line shifts, argument text); any use in Assets disables the .cs inert path for the merge.
caller_info_attrs_present() {
  local path="$1" tree="$2"
  local rc=0
  git -C "$path" grep -l -E 'CallerLineNumber|CallerArgumentExpression|CallerMemberName|CallerFilePath' "$tree" -- 'src/Asteroids3D/Assets/*.cs' >/dev/null 2>&1 || rc=$?
  [[ "$rc" -ne 1 ]]
}

# Inert = provably unable to change compiled behavior: markdown (*.md) freely; modified .cs only when the string-literal-aware normalizer proves comment/whitespace-only. Everything else is code (fail closed).
classify_diff_since_proof() {
  local path="$1" old_tree="$2" new_tree="$3"
  git -C "$path" rev-parse --verify -q "$old_tree^{tree}" >/dev/null 2>&1 || { echo "code"; return 0; }
  git -C "$path" rev-parse --verify -q "$new_tree^{tree}" >/dev/null 2>&1 || { echo "code"; return 0; }
  local diff_output diff_rc=0
  diff_output="$(git -C "$path" diff --no-renames --name-status "$old_tree" "$new_tree" 2>/dev/null)" || diff_rc=$?
  [[ "$diff_rc" -eq 0 ]] || { echo "code"; return 0; }
  local classification="doc" status file
  while IFS=$'\t' read -r status file; do
    [[ -n "$file" ]] || continue
    case "$file" in
      '"'*) echo "code"; return 0 ;;
      *.md) ;;
      *.cs)
        [[ "$status" == "M" ]] || { echo "code"; return 0; }
        cs_diff_is_comment_only "$path" "$old_tree" "$new_tree" "$file" || { echo "code"; return 0; }
        classification="comment"
        ;;
      *) echo "code"; return 0 ;;
    esac
  done <<< "$diff_output"
  if [[ "$classification" == "comment" ]] && caller_info_attrs_present "$path" "$new_tree"; then
    echo "Caller-info attributes present in Assets — .cs comment-only fast path disabled for this merge." >&2
    classification="code"
  fi
  echo "$classification"
}

cs_diff_is_comment_only() {
  local path="$1" old_tree="$2" new_tree="$3" file="$4"
  local normalizer="$SCRIPT_DIR/inert_diff.ps1"
  [[ -f "$normalizer" ]] || return 1
  local old_blob new_blob rc=0
  old_blob="$(mktemp)"
  new_blob="$(mktemp)"
  git -C "$path" show "$old_tree:$file" > "$old_blob" 2>/dev/null || rc=1
  git -C "$path" show "$new_tree:$file" > "$new_blob" 2>/dev/null || rc=1
  if [[ "$rc" -eq 0 ]]; then
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$normalizer" -OldPath "$old_blob" -NewPath "$new_blob" >/dev/null 2>&1 || rc=1
  fi
  rm -f "$old_blob" "$new_blob"
  return "$rc"
}

write_lock() {
  local slot="$1" lease="$2" path="$3"
  local ldir
  ldir="$(lock_dir_for "$slot")"
  printf '%s\n' "$lease" > "$ldir/lease"
  printf '%s\n' "$$" > "$ldir/pid"
  date -u +"%Y-%m-%dT%H:%M:%SZ" > "$ldir/timestamp"
  if [[ -n "$path" ]]; then
    # Worktree-scoped, never the repo-shared .git/config: a plain write there clobbers every slot's lease (cross-slot LEASE RACE); the unqualified --unset keeps the shared key clear.
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

# Clobber-safe: no uncommitted changes and no local commits absent from every remote branch.
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

# The pool's read interface: one blank-line-separated record per slot, KEY=value per line (git's own
# --porcelain shape, and the only shape safe for paths with spaces). Both `status` renderings are
# adapters over this - nothing else may read the lock dir or re-derive a lease.
collect_slot_records() {
  local slot path ldir lease tb pid ts age state
  while IFS=$'\t' read -r slot path; do
    ldir="$(lock_dir_for "$slot")"
    printf 'slot=%s\n' "$slot"
    if [[ -d "$ldir" ]]; then
      # Lease/branch are locked-slot keys: a free slot's leftover worktree config is not a claim.
      lease="$(lease_for "$slot")"
      tb="$(task_branch_for "$slot")"
      pid="$(cat "$ldir/pid" 2>/dev/null || true)"
      ts="$(cat "$ldir/timestamp" 2>/dev/null || true)"
      age="$(lock_age_seconds "$ldir")"
      state="locked"
      [[ "$age" -gt "$LOCK_TTL_SECONDS" ]] && state="stale"
      printf 'state=%s\n' "$state"
      printf 'path=%s\n' "$path"
      [[ -n "$lease" ]] && printf 'lease=%s\n' "$lease"
      [[ -n "$tb" ]] && printf 'task_branch=%s\n' "$tb"
      printf 'age_seconds=%s\n' "$age"
      [[ -n "$pid" ]] && printf 'locked_by_pid=%s\n' "$pid"
      [[ -n "$ts" ]] && printf 'locked_at=%s\n' "$ts"
    else
      printf 'state=free\n'
      printf 'path=%s\n' "$path"
    fi
    printf '\n'
  done < <(slots_tsv)
}

cmd_status() {
  local any=0 slot="" state="" path="" lease="" tb="" pid="" ts="" line key value
  # One collection pass feeds both renderings; a record ends at its blank line.
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ -n "$line" ]]; then
      key="${line%%=*}"
      value="${line#*=}"
      case "$key" in
        slot) slot="$value" ;;
        state) state="$value" ;;
        path) path="$value" ;;
        lease) lease="$value" ;;
        task_branch) tb="$value" ;;
        locked_by_pid) pid="$value" ;;
        locked_at) ts="$value" ;;
      esac
      continue
    fi
    [[ -n "$slot" ]] || continue
    any=1
    if [[ "$state" == "free" ]]; then
      echo "$slot | FREE   | $path"
    else
      local label="LOCKED"
      [[ "$state" == "stale" ]] && label="STALE "
      echo "$slot | $label | $path | lease=${lease:-unknown} pid=${pid:-unknown} at=${ts:-unknown}${tb:+ branch=$tb}"
    fi
    slot=""; state=""; path=""; lease=""; tb=""; pid=""; ts=""
  done < <(collect_slot_records)

  if [[ "$any" -eq 0 ]]; then
    echo "No agent-* worktrees found."
    exit 1
  fi
}

try_lock_slot() {
  local slot="$1" lease="$2" path="$3"
  local ldir
  ldir="$(lock_dir_for "$slot")"
  mkdir "$ldir" 2>/dev/null || return 1
  write_lock "$slot" "$lease" "$path"
  echo "SLOT=$slot PATH=$path"
}

# Reclaim only past-TTL locks whose slot holds no unpushed work (never clobber a dead lock's WIP — the CLOBBER HAZARD).
try_reclaim_slot() {
  local slot="$1" lease="$2" path="$3"
  local ldir age tomb stamp_before stamp_after
  ldir="$(lock_dir_for "$slot")"
  age="$(lock_age_seconds "$ldir")"
  [[ "$age" -gt "$LOCK_TTL_SECONDS" ]] || return 1
  if ! slot_is_clobber_safe "$path"; then
    echo "Skipping $slot: stale lock (age ${age}s) but slot holds unpushed work; leaving locked" >&2
    return 1
  fi
  # Single winner: the stale dir is renamed aside and only one racer's rename can succeed.
  # A rename that landed on a rival's already-fresh lock (stamp differs from the one age-checked)
  # is put back — reclaim must never clobber a live lock.
  # Tombstones are dead state; only sweep ones far too old to belong to an in-flight racer.
  find "$(dirname "$ldir")" -maxdepth 1 -name "$(basename "$ldir").tomb.*" -mmin +60 -exec rm -rf {} + 2>/dev/null || true
  stamp_before="$(cat "$ldir/timestamp" 2>/dev/null || true)"
  tomb="${ldir}.tomb.$$-$(date +%s%N)"
  mv "$ldir" "$tomb" 2>/dev/null || return 1
  stamp_after="$(cat "$tomb/timestamp" 2>/dev/null || true)"
  if [[ "$stamp_after" != "$stamp_before" ]]; then
    [[ -d "$ldir" ]] || mv "$tomb" "$ldir" 2>/dev/null || true
    return 1
  fi
  rm -rf "$tomb"
  mkdir "$ldir" 2>/dev/null || return 1
  write_lock "$slot" "$lease" "$path"
  echo "Reclaimed stale lock on $slot (age ${age}s > TTL ${LOCK_TTL_SECONDS}s)" >&2
  echo "SLOT=$slot PATH=$path"
}

cmd_acquire() {
  local lease="${1:-task-$(date +%Y%m%d-%H%M%S)}"
  local wanted="${2:-}"

  # A named slot is strict: the caller chose it for state the pool can't see — silently handing back a different slot recreates the surprise naming was meant to remove.
  if [[ -n "$wanted" ]]; then
    local path
    path="$(slot_path "$wanted")" || { echo "acquire: unknown slot '$wanted'" >&2; return 1; }
    try_lock_slot "$wanted" "$lease" "$path" && return 0
    try_reclaim_slot "$wanted" "$lease" "$path" && return 0
    echo "acquire: $wanted unavailable (lease=$(lease_for "$wanted")); no fallback when a slot is named." >&2
    return 1
  fi

  # Free slots first; reclaiming a stale lock crosses another session's expectations, so it is a fallback pass, never interleaved.
  local slot path
  while IFS=$'\t' read -r slot path; do
    try_lock_slot "$slot" "$lease" "$path" && return 0
  done < <(slots_tsv)
  while IFS=$'\t' read -r slot path; do
    try_reclaim_slot "$slot" "$lease" "$path" && return 0
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
  # Ignored, so clean leaves it: purge worktree-local .worktree-pool so a stale script copy can't resolve it as live locks.
  rm -rf "$path/.worktree-pool"

  echo "Prepared $slot at $path -> $base"
}

cmd_run_tests() {
  local slot="$1"
  shift || true
  # run-tests forwards args straight to the runner; a leading '--' (the submit/revise separator) would reach PowerShell as an ambiguous empty parameter, so drop it with a hint.
  if [[ "${1:-}" == "--" ]]; then
    echo "run-tests: ignoring stray '--' — run-tests forwards test args directly; '--' is only for submit/revise." >&2
    shift
  fi
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

restore_tracked_unity_changes() {
  local path="$1" action="$2" changes names numstat content known=0
  changes="$(git -C "$path" status --porcelain --untracked-files=no 2>/dev/null)"
  [[ -n "$changes" ]] || return 0
  names="$(git -C "$path" diff --name-only)"
  numstat="$(git -C "$path" diff --numstat -- src/Asteroids3D/ProjectSettings/ProjectSettings.asset)"
  content="$(git -C "$path" diff --unified=0 -- src/Asteroids3D/ProjectSettings/ProjectSettings.asset | grep -E '^[+-][[:space:]]+Standalone:' || true)"
  if [[ "$names" == "src/Asteroids3D/ProjectSettings/ProjectSettings.asset" ]] &&
     [[ "$numstat" == $'1\t1\tsrc/Asteroids3D/ProjectSettings/ProjectSettings.asset' ]] &&
     [[ "$(printf '%s\n' "$content" | grep -Ec '^[+-][[:space:]]+Standalone: UNITY_POST_PROCESSING_STACK_V2(;SENTIS_ANALYTICS_ENABLED)?$')" -eq 2 ]]; then
    known=1
  fi
  git -C "$path" restore --worktree --source=HEAD -- .
  if [[ "$known" -ne 1 ]]; then
    echo "$action changed unexpected tracked files:" >&2
    printf '%s\n' "$changes" | head -n 20 >&2
    return 1
  fi
}

cmd_run_tests_clean() {
  local slot="$1" path exit_code=0
  shift || true
  path="$(slot_path "$slot")"
  cmd_run_tests "$slot" "$@" || exit_code=$?
  restore_tracked_unity_changes "$path" "Unity test run"
  require_clean_slot "$slot" "$path" "Unity test run" || return 1
  [[ "$exit_code" -eq 0 ]] || return "$exit_code"
}

resharper_fingerprint() {
  local path="$1" file hashes=""
  for file in \
    .config/dotnet-tools.json \
    scripts/agent_worktree_pool.sh \
    scripts/resharper-unity.DotSettings \
    scripts/resharper_ratchet.ps1 \
    scripts/sync_unity_solution.ps1; do
    [[ -f "$path/$file" ]] || { echo "missing:$file"; return 0; }
    hashes+="$(git -C "$path" hash-object "$path/$file"):$file"$'\n'
  done
  printf '%s' "$hashes" | git hash-object --stdin
}

record_resharper_proof() {
  local slot="$1" path="$2" base_ref="$3"
  local ldir tree base_tree fingerprint
  ldir="$(lock_dir_for "$slot")"
  tree="$(git -C "$path" rev-parse 'HEAD^{tree}')"
  base_tree="$(git -C "$path" rev-parse "$base_ref^{tree}")"
  fingerprint="$(resharper_fingerprint "$path")"
  mkdir -p "$ldir"
  {
    printf 'tree=%s\n' "$tree"
    printf 'baseTree=%s\n' "$base_tree"
    printf 'fingerprint=%s\n' "$fingerprint"
    printf 'recordedAt=%s\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  } > "$ldir/resharper_proof"
}

resharper_proof_matches() {
  local slot="$1" path="$2" base_ref="$3"
  local proof tree base_tree fingerprint
  proof="$(lock_dir_for "$slot")/resharper_proof"
  [[ -f "$proof" ]] || return 1
  tree="$(git -C "$path" rev-parse 'HEAD^{tree}')"
  base_tree="$(git -C "$path" rev-parse "$base_ref^{tree}")"
  fingerprint="$(resharper_fingerprint "$path")"
  [[ "$(sed -n 's/^tree=//p' "$proof" | head -n 1)" == "$tree" ]] || return 1
  [[ "$(sed -n 's/^baseTree=//p' "$proof" | head -n 1)" == "$base_tree" ]] || return 1
  [[ "$(sed -n 's/^fingerprint=//p' "$proof" | head -n 1)" == "$fingerprint" ]]
}

cmd_run_resharper() {
  local slot="$1" base_ref="${2:-origin/main}"
  local path
  path="$(slot_path "$slot")"
  require_clean_slot "$slot" "$path" "run-resharper" || return 1
  if resharper_proof_matches "$slot" "$path" "$base_ref"; then
    echo "Tree already passed the ReSharper ratchet against $base_ref — skipping re-run."
    return 0
  fi
  (
    cd "$path"
    powershell.exe -NoProfile -ExecutionPolicy Bypass \
      -File "./scripts/resharper_ratchet.ps1" \
      -BaseRef "$base_ref" \
      -OutDir "results/resharper-ratchet"
  )
  require_clean_slot "$slot" "$path" "run-resharper" || return 1
  record_resharper_proof "$slot" "$path" "$base_ref"
}

cmd_run_script_tests() {
  local dir="${1:-$ROOT}"
  local tests_dir="$dir/scripts/tests" file base rc=0 ran=0
  # A name here is skipped because its state escapes a temp dir, so another session can turn it red.
  # Empty is the goal state (test_unity_access.ps1 left in #454 by injecting its state+primary root).
  local nonhermetic=" "
  if [[ ! -d "$tests_dir" ]]; then
    echo "run-script-tests: no $tests_dir — nothing to run." >&2
    return 0
  fi
  for file in "$tests_dir"/test_*.sh "$tests_dir"/test_*.ps1; do
    [[ -f "$file" ]] || continue
    base="$(basename "$file")"
    if [[ "${SCRIPT_TESTS_INCLUDE_NONHERMETIC:-0}" != 1 && "$nonhermetic" == *" $base "* ]]; then
      echo "SKIP: $base — non-hermetic (its state escapes a temp dir); runs with SCRIPT_TESTS_INCLUDE_NONHERMETIC=1"
      continue
    fi
    ran=1
    rc=0
    case "$file" in
      *.sh) bash "$file" || rc=$? ;;
      *.ps1) powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$file" || rc=$? ;;
    esac
    if [[ "$rc" -eq 0 ]]; then
      echo "PASS $base"
    else
      echo "FAIL $base (exit $rc)"
      return 1
    fi
  done
  [[ "$ran" -eq 1 ]] || echo "run-script-tests: no test files under $tests_dir." >&2
}

# 0 = touched, 1 = untouched, 2 = the diff could not be computed. Fail closed: a
# swallowed git error would read as "no scripts/ change" and skip the gate.
landing_diff_touches_scripts() {
  local path="$1" base_ref="$2" head_ref="$3" changed rc=0
  changed="$(git -C "$path" diff --name-only "$base_ref" "$head_ref" -- scripts)" || rc=$?
  if [[ "$rc" -ne 0 ]]; then
    echo "merge: could not compute the landing diff $base_ref..$head_ref (git exit $rc)." >&2
    return 2
  fi
  [[ -n "$changed" ]]
}

require_pr_title_body() {
  local cmd="$1" title="$2" body="$3" body_file="$4"
  if [[ -z "$title" ]]; then
    echo "$cmd: missing required --title \"<text>\"" >&2
    return 1
  fi
  if [[ -n "$body" && -n "$body_file" ]]; then
    echo "$cmd: --body and --body-file are mutually exclusive — pass exactly one" >&2
    return 1
  fi
  if [[ -z "$body" && -z "$body_file" ]]; then
    echo "$cmd: missing required --body \"<text>\" or --body-file <path>" >&2
    return 1
  fi
  if [[ -n "$body_file" && ! -f "$body_file" ]]; then
    echo "$cmd: --body-file not found: $body_file" >&2
    return 1
  fi
}

resolve_pr_body() {
  local body="$1" body_file="$2"
  if [[ -n "$body_file" ]]; then
    cat "$body_file"
  else
    printf '%s' "$body"
  fi
}

cmd_create_pr() {
  local slot="$1"
  shift || true

  local base="main"
  if [[ -n "${1:-}" && "${1:-}" != --* ]]; then
    base="$1"
    shift
  fi

  local title="" body="" body_file=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --title)
        [[ -n "${2:-}" ]] || { echo "create-pr: --title requires a value" >&2; return 1; }
        title="$2"; shift 2 ;;
      --body)
        [[ -n "${2:-}" ]] || { echo "create-pr: --body requires a value" >&2; return 1; }
        body="$2"; shift 2 ;;
      --body-file)
        [[ -n "${2:-}" ]] || { echo "create-pr: --body-file requires a path" >&2; return 1; }
        body_file="$2"; shift 2 ;;
      *)
        echo "create-pr: unknown argument: $1" >&2
        return 1 ;;
    esac
  done
  require_pr_title_body "create-pr" "$title" "$body" "$body_file" || return 1

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

  local task_branch
  task_branch="$(ensure_task_branch "$slot")"

  git -C "$ROOT" push -u origin "$slot:refs/heads/$task_branch" >/dev/null

  local existing
  existing="$(gh pr list --head "$task_branch" --base "$base" --state open --json url --jq '.[0].url' 2>/dev/null || true)"
  if [[ -n "$existing" ]]; then
    echo "$slot PR already open: $existing"
    return 0
  fi

  local url
  url="$(gh pr create --base "$base" --head "$task_branch" --title "$title" --body "$(resolve_pr_body "$body" "$body_file")")"
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
  shift || true

  local base_ref="origin/main"
  if [[ -n "${1:-}" && "${1:-}" != --* ]]; then
    base_ref="$1"
    shift
  fi

  local title="" body="" body_file=""
  local test_args=()
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --title)
        [[ -n "${2:-}" ]] || { echo "submit: --title requires a value" >&2; return 1; }
        title="$2"; shift 2 ;;
      --body)
        [[ -n "${2:-}" ]] || { echo "submit: --body requires a value" >&2; return 1; }
        body="$2"; shift 2 ;;
      --body-file)
        [[ -n "${2:-}" ]] || { echo "submit: --body-file requires a path" >&2; return 1; }
        body_file="$2"; shift 2 ;;
      --)
        shift
        test_args=("$@")
        break ;;
      --*)
        echo "submit: unknown flag '$1' before '--' — test-runner args go after '--'" >&2
        return 1 ;;
      *)
        test_args+=("$1"); shift ;;
    esac
  done
  # Validate PR flags before the test run so a missing flag fails in seconds, not after a full suite.
  require_pr_title_body "submit" "$title" "$body" "$body_file" || return 1

  local base_branch
  base_branch="${base_ref#origin/}"

  # Ensure slot branch is checked out (do NOT reset — preserve agent's work)
  local path
  path="$(slot_path "$slot")"
  git -C "$path" checkout "$slot"
  require_clean_slot "$slot" "$path" "submit" || return 1

  clear_run_summary "$path"
  cmd_run_tests_clean "$slot" "${test_args[@]}"
  record_tested_tree "$slot" "$path"
  cmd_run_resharper "$slot" "$base_ref"

  local task_branch
  task_branch="$(ensure_task_branch "$slot")"

  git -C "$path" push -u origin "$slot:refs/heads/$task_branch" >/dev/null

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
    local url
    url="$(gh pr create --base "$base_branch" --head "$task_branch" --title "$title" --body "$(resolve_pr_body "$body" "$body_file")")"
    echo "$slot PR created: $url"
  fi

  echo ""
  echo "PR submitted for $slot (branch: $task_branch). Lock kept —"
  echo "use 'revise' for feedback, then 'finalize' once the PR is merged."
}

# ---- Merge gate journal ------------------------------------------------------
# Stdout is bound to the launching session; the journal is not. Any agent can
# render a merge it did not start, and the phase timings are the profiling data.
MERGE_RUNS_DIR="${WORKTREE_POOL_MERGE_RUNS_DIR:-$ROOT/.worktree-pool/merge-runs}"

# Wall-clock budgets in seconds. PROVISIONAL — placeholders until real gate runs
# are collected; over-budget only ever warns, because a slow gate that still
# passes must still land.
merge_phase_budget() {
  case "$1" in
    preflight) echo 30 ;;
    fetch) echo 60 ;;
    base-merge) echo 60 ;;
    proof-check) echo 15 ;;
    tests) echo 1200 ;;
    resharper) echo 300 ;;
    script-tests) echo 420 ;;
    push) echo 90 ;;
    gh-merge) echo 90 ;;
    *) echo 0 ;;
  esac
}

MERGE_JOURNAL=""
MERGE_JOURNAL_PID=""
MERGE_RUN_START=0
MERGE_PHASE=""
MERGE_PHASE_START=0

# Values are ours (phase names, hashes, PR numbers, short status words); drop the
# two characters that would need escaping rather than emit invalid JSON.
json_scrub() {
  printf '%s' "$1" | tr -d '"\\' | tr -d '[:cntrl:]'
}

# Journalling must never be able to fail a merge.
journal_line() {
  [[ -n "$MERGE_JOURNAL" ]] || return 0
  printf '%s\n' "$1" >> "$MERGE_JOURNAL" 2>/dev/null || true
}

journal_event() {
  [[ -n "$MERGE_JOURNAL" ]] || return 0
  local event="$1" phase="$2"
  shift 2
  local now frag="" kv key val
  now="$(date +%s)"
  for kv in "$@"; do
    key="${kv%%=*}"
    val="${kv#*=}"
    if [[ "$val" =~ ^-?[0-9]+$ ]]; then
      frag+="$(printf ',"%s":%s' "$(json_scrub "$key")" "$val")"
    else
      frag+="$(printf ',"%s":"%s"' "$(json_scrub "$key")" "$(json_scrub "$val")")"
    fi
  done
  journal_line "$(printf '{"ts":"%s","t":%s,"event":"%s","phase":"%s"%s}' \
    "$(date -u +"%Y-%m-%dT%H:%M:%SZ")" "$((now - MERGE_RUN_START))" \
    "$(json_scrub "$event")" "$(json_scrub "$phase")" "$frag")"
}

merge_journal_open() {
  local slot="$1" base_ref="$2" ldir
  MERGE_RUN_START="$(date +%s)"
  MERGE_JOURNAL_PID="$BASHPID"
  mkdir -p "$MERGE_RUNS_DIR" 2>/dev/null || return 0
  # $$ disambiguates two runs opening in the same second; the truncation below would eat the earlier journal.
  MERGE_JOURNAL="$MERGE_RUNS_DIR/$slot-$(date -u +"%Y%m%d-%H%M%S")-$$.jsonl"
  : > "$MERGE_JOURNAL" 2>/dev/null || { MERGE_JOURNAL=""; return 0; }
  # Readers follow this pointer; nothing outside cmd_merge reconstructs the path.
  ldir="$(lock_dir_for "$slot")"
  mkdir -p "$ldir" 2>/dev/null && printf '%s\n' "$MERGE_JOURNAL" > "$ldir/merge_run" 2>/dev/null || true
  journal_event run-start "" "slot=$slot" "base=$base_ref" "pid=$$" "epoch=$MERGE_RUN_START"
}

# Reaching the next phase is itself proof the previous one succeeded, so a begin
# closes the open phase and no call site has to pair them.
merge_phase_begin() {
  merge_phase_end ok
  MERGE_PHASE="$1"
  MERGE_PHASE_START="$(date +%s)"
  journal_event phase-start "$MERGE_PHASE"
}

merge_phase_end() {
  [[ -n "$MERGE_PHASE" ]] || return 0
  local status="${1:-ok}" sec
  sec=$(( $(date +%s) - MERGE_PHASE_START ))
  journal_event phase-end "$MERGE_PHASE" "sec=$sec" "status=$status" "budget=$(merge_phase_budget "$MERGE_PHASE")"
  MERGE_PHASE=""
}

merge_journal_note() {
  journal_event note "$MERGE_PHASE" "msg=$1"
}

merge_journal_finish() {
  local code="${1:-0}" status="merged"
  [[ -n "$MERGE_JOURNAL" ]] || return 0
  # Some bash builds run an inherited EXIT trap when a ( ) subshell exits; only
  # the shell that opened the journal may close it.
  [[ "$BASHPID" == "$MERGE_JOURNAL_PID" ]] || return 0
  if [[ "$code" -eq 0 ]]; then
    merge_phase_end ok
  else
    merge_phase_end failed
    status="failed"
  fi
  journal_event run-end "" "sec=$(( $(date +%s) - MERGE_RUN_START ))" "status=$status" "exit=$code"
  echo ""
  merge_journal_render "$MERGE_JOURNAL"
}

# Parses only what this file's emitter writes: flat objects, scalar values, no
# escapes (json_scrub guarantees it) — so awk suffices and the gate needs no JSON
# interpreter to explain itself.
MERGE_RENDER_AWK='
function fld(line, key,   re, i, s) {
  re = "\"" key "\":"
  i = index(line, re)
  if (i == 0) return ""
  s = substr(line, i + length(re))
  if (substr(s, 1, 1) == "\"") { s = substr(s, 2); return substr(s, 1, index(s, "\"") - 1) }
  match(s, /^-?[0-9]+/)
  return substr(s, 1, RLENGTH)
}
function fmt(s,   h, m) {
  s = int(s + 0)
  if (s < 60) return s "s"
  m = int(s / 60); s = s % 60
  if (m < 60) return m "m" sprintf("%02ds", s)
  h = int(m / 60); m = m % 60
  return h "h" sprintf("%02dm", m)
}
function pct(sec, budget) { return sprintf("%+d%%", int((sec - budget) * 100 / budget)) }
/"event":"run-start"/ {
  slot = fld($0, "slot"); base = fld($0, "base")
  startTs = fld($0, "ts"); startEpoch = fld($0, "epoch") + 0
}
/"event":"phase-start"/ { openPhase = fld($0, "phase"); openAt = fld($0, "t") + 0 }
/"event":"phase-end"/ {
  p = fld($0, "phase")
  if (!(p in sec)) order[++n] = p
  sec[p] = fld($0, "sec") + 0; bud[p] = fld($0, "budget") + 0; stat[p] = fld($0, "status")
  openPhase = ""
}
/"event":"note"/ {
  p = fld($0, "phase"); m = fld($0, "msg")
  note[p] = (p in note) ? note[p] "; " m : m
}
/"event":"run-end"/ {
  ended = 1; runSec = fld($0, "sec") + 0; runStatus = fld($0, "status"); openPhase = ""
}
END {
  if (startTs == "") {
    if (!oneline) print "  (journal empty)"
    exit
  }
  if (openPhase != "" && nowEpoch > 0 && startEpoch > 0) openElapsed = (nowEpoch - startEpoch) - openAt
  # One line, live runs only: the dashboard wants "what is this slot doing now".
  if (oneline) {
    if (openPhase == "" || ended) exit
    line = openPhase " " fmt(openElapsed) " OPEN"
    if (openBudget > 0 && openElapsed > openBudget) line = line " (over budget " fmt(openBudget) ")"
    print line
    exit
  }
  hdr = slot " merge -> " base "   started " startTs
  if (ended) hdr = hdr "   " runStatus " in " fmt(runSec)
  else if (nowEpoch > 0 && startEpoch > 0) hdr = hdr "   RUNNING " fmt(nowEpoch - startEpoch)
  print hdr
  warned = 0
  for (i = 1; i <= n; i++) {
    p = order[i]
    line = sprintf("  %s  %-12s %8s", (stat[p] == "failed") ? "XX" : "ok", p, fmt(sec[p]))
    if (bud[p] > 0 && sec[p] > bud[p]) {
      line = line sprintf("   OVER BUDGET %s (%s)", fmt(bud[p]), pct(sec[p], bud[p]))
      warned++
    }
    if (p in note) line = line "   " note[p]
    print line
  }
  if (openPhase != "" && nowEpoch > 0 && startEpoch > 0) {
    elapsed = openElapsed
    line = sprintf("  >>  %-12s %8s   OPEN", openPhase, fmt(elapsed))
    if (openBudget > 0 && elapsed > openBudget) {
      line = line sprintf(" - OVER BUDGET %s (%s)", fmt(openBudget), pct(elapsed, openBudget))
      warned++
    }
    else if (openBudget > 0) line = line sprintf(" - budget %s", fmt(openBudget))
    if (openPhase in note) line = line "   " note[openPhase]
    print line
  }
  if (warned > 0) printf "  %d phase(s) over budget.\n", warned
}
'

# Empty unless a phase-start is the last event — i.e. a phase is still open.
merge_journal_open_phase() {
  local last
  last="$(grep -E '"event":"phase-(start|end)"|"event":"run-end"' "$1" 2>/dev/null | tail -n 1 || true)"
  case "$last" in
    *'"event":"phase-start"'*) printf '%s' "$last" | sed -n 's/.*"phase":"\([^"]*\)".*/\1/p' ;;
    *) printf '' ;;
  esac
}

merge_journal_render() {
  local journal="$1" oneline="${2:-0}" open_phase open_budget
  [[ -f "$journal" ]] || return 0
  open_phase="$(merge_journal_open_phase "$journal")"
  open_budget="$(merge_phase_budget "${open_phase:-none}")"
  awk -v nowEpoch="$(date +%s)" -v openBudget="$open_budget" -v oneline="$oneline"     "$MERGE_RENDER_AWK" "$journal"
}

# Resolve a slot's journal: the live pointer first, else the newest run file (the
# pointer dies with the lock dir at finalize, the run history does not).
merge_journal_for_slot() {
  local slot="$1" ldir journal
  ldir="$(lock_dir_for "$slot")"
  journal="$(cat "$ldir/merge_run" 2>/dev/null || true)"
  if [[ -z "$journal" || ! -f "$journal" ]]; then
    journal="$(ls -1t "$MERGE_RUNS_DIR/$slot-"*.jsonl 2>/dev/null | head -n 1 || true)"
  fi
  printf '%s' "$journal"
}

cmd_merge_progress() {
  local slot="$1" mode="${2:-}" journal path open_phase log age
  journal="$(merge_journal_for_slot "$slot")"
  if [[ -z "$journal" || ! -f "$journal" ]]; then
    [[ "$mode" == "--oneline" ]] || echo "merge-progress: no merge run recorded for $slot."
    return 0
  fi
  if [[ "$mode" == "--oneline" ]]; then
    merge_journal_render "$journal" 1
    return 0
  fi
  merge_journal_render "$journal"
  echo "  journal: $journal"

  # Separates a hung editor from a slow suite far better than a pid check.
  open_phase="$(merge_journal_open_phase "$journal")"
  [[ "$open_phase" == "tests" ]] || return 0
  path="$(slot_path "$slot" 2>/dev/null || true)"
  [[ -n "$path" ]] || return 0
  log="$(ls -1t "$path/results/unity-tests-agent/"*.log 2>/dev/null | head -n 1 || true)"
  if [[ -z "$log" ]]; then
    echo "  unity: no editor log yet under $path/results/unity-tests-agent/"
    return 0
  fi
  age=$(( $(date +%s) - $(stat -c %Y "$log" 2>/dev/null || echo 0) ))
  echo "  unity: $(basename "$log") last written ${age}s ago"
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

  merge_journal_open "$slot" "$base_ref"
  # Fires on every exit path, including a set -e abort, so no failure leaves the
  # journal with a phase open forever.
  trap 'merge_journal_finish "$?"' EXIT
  merge_phase_begin preflight

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
  merge_journal_note "PR #$pr $task_branch -> $base_branch"

  merge_phase_begin fetch
  git -C "$path" fetch origin "$base_branch"
  git -C "$path" checkout "$slot"
  require_clean_slot "$slot" "$path" "merge" || return 1
  # Gate against the freshly-fetched remote-tracking ref: a bare local name (e.g. 'main') can lag the remote and silently skip the re-test.
  base_ref="origin/$base_branch"

  # If base moved, integrate it first: two PRs each green on their own base can still break main together with no textual conflict.
  merge_phase_begin base-merge
  if ! git -C "$path" merge-base --is-ancestor "$base_ref" "$slot"; then
    echo "$base_ref moved since $slot last synced: merging it in."
    merge_journal_note "$base_ref moved - integrating"
    if ! git -C "$path" merge --no-edit "$base_ref"; then
      git -C "$path" merge --abort || true
      echo "merge: conflict merging $base_ref into $slot — resolve in the worktree," >&2
      echo "  'revise' to test+push, then re-run merge." >&2
      merge_journal_note "conflict merging $base_ref"
      return 1
    fi
  else
    merge_journal_note "already current with $base_ref"
  fi

  # Skip the re-test only on provenance-corroborated FULL-suite proof for this exact tree: scoped runs never count, and ancestry alone is not evidence — a base-merge commit survives a failed test run, and a retry must re-test it.
  merge_phase_begin proof-check
  local current_tree proof_tree
  current_tree="$(git -C "$path" rev-parse "$slot^{tree}")"
  proof_tree="$(verified_proof_tree "$slot")"
  if [[ -n "$proof_tree" && "$proof_tree" == "$current_tree" ]]; then
    echo "Tree $current_tree already passed the full suite — skipping re-run."
    merge_phase_begin tests
    merge_journal_note "skipped - tree already fully proven"
  elif [[ -n "$proof_tree" ]]; then
    case "$(classify_diff_since_proof "$path" "$proof_tree" "$current_tree")" in
      doc)
        echo "Markdown-only delta since fully-tested tree $proof_tree — extending proof without a run."
        merge_phase_begin tests
        merge_journal_note "skipped - markdown-only delta, proof extended"
        extend_proof "$slot" "$current_tree" "inherit-doc" "$proof_tree"
        ;;
      comment)
        echo "C# comment/whitespace-only delta since fully-tested tree $proof_tree — compile-level smoke refresh."
        merge_phase_begin tests
        merge_journal_note "comment-only delta - EditMode smoke refresh"
        clear_run_summary "$path"
        cmd_run_tests_clean "$slot" -Mode EditMode -ScopeType Smoke
        extend_proof "$slot" "$current_tree" "inherit-smoke" "$proof_tree"
        ;;
      *)
        echo "Code delta since fully-tested tree $proof_tree — running the full suite before merge."
        merge_phase_begin tests
        merge_journal_note "code delta since proof - full suite"
        clear_run_summary "$path"
        cmd_run_tests_clean "$slot" "${test_args[@]}"
        record_tested_tree "$slot" "$path"
        ;;
    esac
  else
    echo "No full-suite proof for tree $current_tree — running the full suite before merge."
    merge_phase_begin tests
    merge_journal_note "no proof for landing tree - full suite"
    clear_run_summary "$path"
    cmd_run_tests_clean "$slot" "${test_args[@]}"
    record_tested_tree "$slot" "$path"
  fi
  if [[ "$(verified_proof_tree "$slot")" != "$current_tree" ]]; then
    echo "merge: no full-coverage proof for landing tree $current_tree (scoped gate args?); not merging." >&2
    return 1
  fi
  merge_phase_begin resharper
  cmd_run_resharper "$slot" "$base_ref"

  local scripts_diff_rc=0
  landing_diff_touches_scripts "$path" "$base_ref" "$slot" || scripts_diff_rc=$?
  [[ "$scripts_diff_rc" -ne 2 ]] || return 1
  # Depth is bounded: the suite runs the SLOT's scripts/tests, and a test fixture's slot carries none.
  if [[ "$scripts_diff_rc" -eq 0 ]]; then
    merge_phase_begin script-tests
    merge_journal_note "landing diff touches scripts/ - running the script suite"
    cmd_run_script_tests "$path"
  fi

  merge_phase_begin push
  # Unconditional: gh merges the REMOTE branch, so any local-only commits must be on it before the squash.
  git -C "$path" push origin "$slot:refs/heads/$task_branch"

  # GitHub recomputes mergeability asynchronously after the gate's push; a merge call inside that window fails "not mergeable" — brief retries ride it out.
  merge_phase_begin gh-merge
  local attempt merged=0
  for attempt in 1 2 3 4 5; do
    if gh pr merge "$pr" --squash --delete-branch=false; then
      merged=1
      break
    fi
    echo "merge: PR #$pr not mergeable yet (attempt $attempt/5) — retrying in 3s..."
    merge_journal_note "not mergeable yet, attempt $attempt/5"
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

  local task_branch
  task_branch="$(task_branch_for "$slot")"
  if [[ -n "$task_branch" ]]; then
    git -C "$ROOT" push origin --delete "$task_branch" 2>/dev/null || true
  fi

  # --force: post-merge reset is intentional discard — the squash subsumed the slot's commits and the remote task branch is gone.
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

  local no_test=0
  if [[ ${1:-} == "--no-test" ]]; then
    no_test=1
    shift
  fi

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

  # Never fall back to the bare slot name: rebasing onto ancient origin/agent-N replays 100+ commits (REVISE HAZARD).
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

  require_clean_slot "$slot" "$path" "revise" || return 1

  if [[ "$no_test" -eq 1 ]]; then
    echo "Skipping tests (--no-test): no proof recorded; the merge gate will test the landing tree."
  else
    clear_run_summary "$path"
    cmd_run_tests_clean "$slot" "${test_args[@]}"
    record_tested_tree "$slot" "$path"
  fi
  cmd_run_resharper "$slot" origin/main

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
    status)
      if [[ "${1:-}" == "--porcelain" ]]; then collect_slot_records; else cmd_status; fi
      ;;
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
    run-resharper)
      [[ $# -ge 1 ]] || { echo "run-resharper requires <slot> [base_ref]" >&2; exit 1; }
      cmd_run_resharper "$@"
      ;;
    run-script-tests)
      cmd_run_script_tests "$@"
      ;;
    create-pr)
      [[ $# -ge 1 ]] || { echo "create-pr requires <slot> [base] --title \"<text>\" (--body \"<text>\" | --body-file <path>)" >&2; exit 1; }
      cmd_create_pr "$@"
      ;;
    create-pool-prs)
      cmd_create_pool_prs "$@"
      ;;
    submit)
      [[ $# -ge 1 ]] || { echo "submit requires <slot> [base_ref] --title \"<text>\" (--body \"<text>\" | --body-file <path>) [-- test_args...]" >&2; exit 1; }
      cmd_submit "$@"
      ;;
    merge)
      [[ $# -ge 1 ]] || { echo "merge requires <slot> [base_ref] [-- test_args...]" >&2; exit 1; }
      cmd_merge "$@"
      ;;
    merge-progress)
      [[ $# -ge 1 ]] || { echo "merge-progress requires <slot> [--oneline]" >&2; exit 1; }
      cmd_merge_progress "$@"
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
      [[ $# -ge 1 ]] || { echo "revise requires <slot> [--no-test] [-- test_args...]" >&2; exit 1; }
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
