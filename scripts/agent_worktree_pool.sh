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
  status
      List agent-* worktree slots and lock status.

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
      the ReSharper ratchet. Do not call 'gh pr merge' directly.

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

FULL_COVERAGE_PY='
import json, sys

def canon_path(p):
    return str(p or "").replace("\\", "/").rstrip("/").lower()

def main():
    try:
        with open(sys.argv[1], encoding="utf-8-sig") as f:
            summary = json.load(f)
    except Exception:
        print("partial|summary unreadable")
        return
    expected_project = canon_path(sys.argv[2])
    sel = summary.get("selection")
    if not isinstance(sel, dict):
        print("partial|selection missing")
        return
    must_be_empty = ["testFilter", "testCategory", "assemblyNames", "orderedTestListFile", "rerunFailedFrom"]
    for key in must_be_empty + ["scopeType", "excludeCategory"]:
        if key not in sel:
            print("partial|selection.%s missing" % key)
            return
    # A fully-green run with ignored tests reports NUnit result "Skipped:Ignored" -> per-run status "unknown", so gate on failed==0/total>0 instead of the status label.
    def green_run(run):
        if not isinstance(run, dict) or run.get("status") in ("failed", "infra_error"):
            return False
        try:
            return int(run.get("failed")) == 0 and int(run.get("total")) > 0
        except (TypeError, ValueError):
            return False
    runs = summary.get("runs")
    runs_ok = isinstance(runs, list) and len(runs) > 0
    passed_platforms = set()
    if runs_ok:
        for run in runs:
            if not green_run(run):
                runs_ok = False
                break
            passed_platforms.add(run.get("platform"))
    exclude = {c.strip().lower() for c in str(sel.get("excludeCategory") or "").split(";") if c.strip()}
    checks = [
        (summary.get("status") == "passed", "status=%s" % summary.get("status")),
        (summary.get("mode") == "Both", "mode=%s" % summary.get("mode")),
        (expected_project != "" and canon_path(summary.get("projectPath")) == expected_project,
         "projectPath=%s (expected %s)" % (summary.get("projectPath"), expected_project)),
        (runs_ok and {"EditMode", "PlayMode"} <= passed_platforms, "runs lack passed EditMode+PlayMode"),
        (str(sel.get("scopeType") or "").lower() == "workspace", "scopeType=%s" % sel.get("scopeType")),
        (exclude <= {"requiresgraphics"}, "excludeCategory=%s" % sel.get("excludeCategory")),
    ] + [(not str(sel.get(k) or "").strip(), "%s set" % k) for k in must_be_empty]
    for ok, why in checks:
        if not ok:
            print("partial|" + why)
            return
    print("full|mode=Both scopeType=Workspace excludeCategory=%s" % (sel.get("excludeCategory") or ""))

main()
'

FULL_COVERAGE_PS='
function Canon($p) { return "$p".Replace("\", "/").TrimEnd("/").ToLower() }
function Blank($v) { return [string]::IsNullOrWhiteSpace([string]$v) }
try { $s = Get-Content -LiteralPath $env:POOL_SUMMARY_JSON -Raw | ConvertFrom-Json } catch { Write-Output "partial|summary unreadable"; exit 0 }
$expected = Canon $env:POOL_EXPECTED_PROJECT
$sel = $s.selection
$mustBeEmpty = @("testFilter", "testCategory", "assemblyNames", "orderedTestListFile", "rerunFailedFrom")
$why = $null
if ($null -eq $sel) { $why = "selection missing" }
if ($null -eq $why) {
  $selKeys = @($sel.PSObject.Properties.Name)
  foreach ($key in ($mustBeEmpty + @("scopeType", "excludeCategory"))) {
    if ($selKeys -notcontains $key) { $why = "selection.$key missing"; break }
  }
}
if ($null -eq $why) {
  $runs = @($s.runs)
  $passedPlatforms = @()
  $runsOk = $runs.Count -gt 0
  foreach ($run in $runs) {
    if ($null -eq $run) { $runsOk = $false; break }
    $st = [string]$run.status
    if ($st -eq "failed" -or $st -eq "infra_error") { $runsOk = $false; break }
    $failedN = -1
    $totalN = 0
    if (-not [int]::TryParse([string]$run.failed, [ref]$failedN)) { $runsOk = $false; break }
    if (-not [int]::TryParse([string]$run.total, [ref]$totalN)) { $runsOk = $false; break }
    if ($failedN -ne 0 -or $totalN -le 0) { $runsOk = $false; break }
    $passedPlatforms += [string]$run.platform
  }
  $bad = @("$($sel.excludeCategory)".Split(";") | ForEach-Object { $_.Trim().ToLower() } | Where-Object { $_ -and $_ -ne "requiresgraphics" })
  if ($s.status -ne "passed") { $why = "status=" + $s.status }
  elseif ($s.mode -cne "Both") { $why = "mode=" + $s.mode }
  elseif ($expected -eq "" -or (Canon $s.projectPath) -ne $expected) { $why = "projectPath=" + $s.projectPath + " (expected " + $expected + ")" }
  elseif (-not ($runsOk -and $passedPlatforms -ccontains "EditMode" -and $passedPlatforms -ccontains "PlayMode")) { $why = "runs lack passed EditMode+PlayMode" }
  elseif ("$($sel.scopeType)".ToLower() -ne "workspace") { $why = "scopeType=" + $sel.scopeType }
  elseif ($bad.Count -gt 0) { $why = "excludeCategory=" + $sel.excludeCategory }
  else {
    foreach ($key in $mustBeEmpty) {
      if (-not (Blank $sel.$key)) { $why = "$key set"; break }
    }
  }
}
if ($null -ne $why) { Write-Output ("partial|" + $why) }
else { Write-Output ("full|mode=Both scopeType=Workspace excludeCategory=" + $sel.excludeCategory) }
'

# Prints "full|<detail>" or "partial|<reason>"; missing/unparseable summaries and dead parsers are all partial (fail closed).
summary_coverage() {
  local summary="$1" expected_project="$2" out="" interp
  [[ -f "$summary" ]] || { echo "partial|no summary at $summary"; return 0; }
  # A Windows Store python3 stub satisfies command -v yet fails on invocation; only a well-formed verdict counts, else fall through.
  for interp in python3 python; do
    command -v "$interp" >/dev/null 2>&1 || continue
    out="$("$interp" -c "$FULL_COVERAGE_PY" "$summary" "$expected_project" 2>/dev/null || true)"
    case "$out" in full\|*|partial\|*) printf '%s\n' "$out"; return 0 ;; esac
  done
  out="$(POOL_SUMMARY_JSON="$summary" POOL_EXPECTED_PROJECT="$expected_project" powershell.exe -NoProfile -Command "$FULL_COVERAGE_PS" 2>/dev/null || true)"
  case "$out" in full\|*|partial\|*) printf '%s\n' "$out"; return 0 ;; esac
  echo "partial|no working JSON parser (tried python3, python, powershell.exe)"
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
  local ldir age
  ldir="$(lock_dir_for "$slot")"
  age="$(lock_age_seconds "$ldir")"
  [[ "$age" -gt "$LOCK_TTL_SECONDS" ]] || return 1
  if ! slot_is_clobber_safe "$path"; then
    echo "Skipping $slot: stale lock (age ${age}s) but slot holds unpushed work; leaving locked" >&2
    return 1
  fi
  rm -rf "$ldir"
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
  local path="$1" action="$2" changes names content known=0
  changes="$(git -C "$path" status --porcelain --untracked-files=no 2>/dev/null)"
  [[ -n "$changes" ]] || return 0
  names="$(git -C "$path" diff --name-only)"
  content="$(git -C "$path" diff --unified=0 -- src/Asteroids3D/ProjectSettings/ProjectSettings.asset | grep -E '^[+-][[:space:]]+Standalone:' || true)"
  if [[ "$names" == "src/Asteroids3D/ProjectSettings/ProjectSettings.asset" ]] &&
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
  require_clean_slot "$slot" "$path" "merge" || return 1
  # Gate against the freshly-fetched remote-tracking ref: a bare local name (e.g. 'main') can lag the remote and silently skip the re-test.
  base_ref="origin/$base_branch"

  # If base moved, integrate it first: two PRs each green on their own base can still break main together with no textual conflict.
  if ! git -C "$path" merge-base --is-ancestor "$base_ref" "$slot"; then
    echo "$base_ref moved since $slot last synced: merging it in."
    if ! git -C "$path" merge --no-edit "$base_ref"; then
      git -C "$path" merge --abort || true
      echo "merge: conflict merging $base_ref into $slot — resolve in the worktree," >&2
      echo "  'revise' to test+push, then re-run merge." >&2
      return 1
    fi
  fi

  # Skip the re-test only on provenance-corroborated FULL-suite proof for this exact tree: scoped runs never count, and ancestry alone is not evidence — a base-merge commit survives a failed test run, and a retry must re-test it.
  local current_tree proof_tree
  current_tree="$(git -C "$path" rev-parse "$slot^{tree}")"
  proof_tree="$(verified_proof_tree "$slot")"
  if [[ -n "$proof_tree" && "$proof_tree" == "$current_tree" ]]; then
    echo "Tree $current_tree already passed the full suite — skipping re-run."
  elif [[ -n "$proof_tree" ]]; then
    case "$(classify_diff_since_proof "$path" "$proof_tree" "$current_tree")" in
      doc)
        echo "Markdown-only delta since fully-tested tree $proof_tree — extending proof without a run."
        extend_proof "$slot" "$current_tree" "inherit-doc" "$proof_tree"
        ;;
      comment)
        echo "C# comment/whitespace-only delta since fully-tested tree $proof_tree — compile-level smoke refresh."
        clear_run_summary "$path"
        cmd_run_tests_clean "$slot" -Mode EditMode -ScopeType Smoke
        extend_proof "$slot" "$current_tree" "inherit-smoke" "$proof_tree"
        ;;
      *)
        echo "Code delta since fully-tested tree $proof_tree — running the full suite before merge."
        clear_run_summary "$path"
        cmd_run_tests_clean "$slot" "${test_args[@]}"
        record_tested_tree "$slot" "$path"
        ;;
    esac
  else
    echo "No full-suite proof for tree $current_tree — running the full suite before merge."
    clear_run_summary "$path"
    cmd_run_tests_clean "$slot" "${test_args[@]}"
    record_tested_tree "$slot" "$path"
  fi
  if [[ "$(verified_proof_tree "$slot")" != "$current_tree" ]]; then
    echo "merge: no full-coverage proof for landing tree $current_tree (scoped gate args?); not merging." >&2
    return 1
  fi
  cmd_run_resharper "$slot" "$base_ref"
  # Unconditional: gh merges the REMOTE branch, so any local-only commits must be on it before the squash.
  git -C "$path" push origin "$slot:refs/heads/$task_branch"

  # GitHub recomputes mergeability asynchronously after the gate's push; a merge call inside that window fails "not mergeable" — brief retries ride it out.
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
    run-resharper)
      [[ $# -ge 1 ]] || { echo "run-resharper requires <slot> [base_ref]" >&2; exit 1; }
      cmd_run_resharper "$@"
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
