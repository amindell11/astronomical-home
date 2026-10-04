#!/usr/bin/env bash
set -euo pipefail

# Anchor to the primary worktree: --show-toplevel is CWD-dependent, and a worktree-local lock dir holds dead leases (the WRONG-BRANCH hazard).
ROOT="$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCK_ROOT="${WORKTREE_POOL_LOCK_ROOT:-$ROOT/.worktree-pool/locks}"
# Locks go stale by IDLE TIME since the slot's last use, not pid — each agent shell is ephemeral, so the acquiring pid is dead by the next call. TTL override for tests.
LOCK_TTL_SECONDS="${WORKTREE_POOL_LOCK_TTL:-43200}"
mkdir -p "$LOCK_ROOT"

# ---- Section map -------------------------------------------------------------
#   Config & anchors          ROOT/LOCK_ROOT/TTL (above)
#   Usage                     usage - the published command interface
#   Slot & lease resolution   slots_tsv .. ensure_task_branch
#   Run summary & proof       RUN_OUTDIR_REL, summary_coverage, tested_tree/tested_scope, require_clean_slot
#   Inert-delta classification caller_info_attrs_present, classify_diff_since_proof, cs_diff_is_comment_only
#   Locks                     with_flock + cmd_lock, write_lock, mark_slot_used, lock_age_seconds, clobber safety
#   Status                    collect_slot_records + collect_held_records (porcelain), cmd_status
#   Acquire / release / prepare
#   Hold / resume             held_snapshot, cmd_hold, cmd_resume
#   Unity test runs           cmd_run_tests, restore_tracked_unity_changes, cmd_run_tests_clean
#   ReSharper ratchet         fingerprint + proof, cmd_run_resharper
#   Script tests              cmd_run_script_tests, landing_diff_touches
#   PR opening                flag grammar, gh helpers, push_and_open_pr, cmd_create_pr, cmd_submit
#   Merge gate journal        budgets, journal events, awk renderer, cmd_merge_progress
#   Merge gate                merge turn + turn tickets, cmd_merge (takes the turn), merge_gate (runs under it)
#   Borrow / return           borrow_slot (land's too), cmd_borrow, return_slot
#   Land                      cmd_land: preflight, slot borrow, bounded merge gate attempts
#   Docs-only landing         cmd_land_docs, land_docs_push (runs under the merge turn)
#   Finalize / review / revise
#   Dispatch                  main
# ------------------------------------------------------------------------------

# ---- Usage -------------------------------------------------------------------
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
        state=free|locked|stale always; stale = locked, and not used for
                                longer than the lock TTL
        path=<abs-path>         always
        lease=<lease-id>        locked/stale slots that have a lease
        task_branch=task/<lease>  when one is recorded or derivable; this is
                                the branch a PR for the slot is opened FROM
        age_seconds=<int>       locked/stale only; seconds since last_used_at
        locked_by_pid=<pid>     locked/stale only, informational: pool locks
                                go stale by idle time, never by pid liveness
        locked_at=<iso8601>     locked/stale only; when the lease was acquired
        last_used_at=<iso8601>  locked/stale only; the last acquire, resume or
                                working command on the slot: prepare,
                                run-tests, run-resharper, run-script-tests,
                                create-pr, submit, revise, review-comments,
                                merge. status and merge-progress are reads
                                and never count as use. A lock with no
                                last-use stamp reports its locked_at.

      After the slot records, one record per held lease (see hold), read
      from local held/* branches and origin/held/* remote-tracking refs as
      of the last fetch. It leads with held= instead of slot=:

        held=<lease>            always
        branch=held/<lease>     always
        left_slot=<slot>        the slot the work was held from, when recorded
        held_at=<iso8601>       when the snapshot was taken
        pushed=0|1              origin/held/<lease> exists as of the last fetch

      Keys may be added; consumers must ignore unknown keys and tolerate any
      optional key being absent.

  acquire [lease_id] [slot]
      Lock and return an available slot. Auto-pick prefers genuinely
      free slots over stale-lock reclaims. Naming a slot is strict:
      if it isn't free (or safely reclaimable) acquire FAILS — no
      silent fallback to auto-pick.
      A free slot is handed back prepared at origin/main (as prepare,
      keeping ignored dirs); when prepare refuses (the slot holds unpushed
      work) acquire releases the slot untouched and fails - auto-pick does
      not move on to another slot. A reclaimed stale slot is not prepared.
      Output: SLOT=<name> PATH=<abs-path>
      Lease mutation uses Perl flock on a stable per-slot .mutation file.
      Concurrent mutation returns nonzero. The OS lock releases when its
      last inheriting process exits.
      A working command (see last_used_at) records its use under the same
      lock before it runs, waiting up to 30s for it; still held, the command
      exits 1 without running.
      Never delete .mutation files: existing holders must share the same file.

  release <slot>
      Release slot lock (e.g., agent-1). Uses the same mutation lock and Perl
      requirement as acquire; contention exits nonzero without releasing.

  prepare <slot> [base_ref]
      Reset slot branch/worktree to base ref (default: origin/main)
      while preserving ignored dirs (e.g., Unity Library/).

  hold <slot> [--local]
      Free a slot whose work waits on the user, keeping the work. Snapshots
      HEAD plus the dirty tree (tracked edits and untracked non-ignored
      files) as one commit whose only parent is HEAD, on branch
      held/<lease>, pushed to origin unless --local; then prepares the slot
      to origin/main and releases it. task/<lease> is never touched.
      Holds the slot's merge gate flock throughout, so it refuses while a
      merge gate runs on the slot and no gate starts mid-hold. Also refuses
      a free or lease-less slot, and a held/<lease> that exists locally or
      on origin as of the last fetch. Local merge and
      ReSharper proof live in the lock and are lost; the merge gate re-proves.
      Output: HELD=<lease> RESUME="agent_worktree_pool.sh resume <lease>"

  resume <lease> [slot]
      Put held work back on a slot. Reads local held/<lease>, else
      origin/held/<lease>. Acquires <slot> (strict, as acquire, but never
      prepared), else the slot the work left if free, else any; refuses a
      slot holding unpushed work.
      Resets the slot branch to the held HEAD and restores the snapshot as
      uncommitted changes (staged edits come back unstaged), then deletes
      held/<lease> locally and on origin.
      Output: SLOT=<name> PATH=<abs-path> RESUMED=<lease>
      Exit 1 after that line means the work is restored but held/<lease>
      could not be deleted everywhere.

  lock <name> [--wait <seconds>] -- <cmd...>
      Run <cmd> holding an exclusive machine-wide lock named <name> (a flock
      on <name>.lock under the lock root, the same primitive the slot
      mutation and merge gate locks use). Waits up to --wait seconds
      (default 30) for another holder to finish. <name> is [A-Za-z0-9._-]+.
      Exit: <cmd>'s own exit code; 75 when the lock is still held after the
      wait (stderr names the lock file); 1 on a usage error.

  run-tests <slot> [unity_test_agent.ps1 args...]
      Run Unity tests in that slot with standardized outDir:
      results/unity-tests-agent

  run-resharper <slot> [base_ref]
      Run the Unity-aware ReSharper changed-line ratchet against base_ref
      (default: origin/main).

  run-script-tests <slot>
      Run every scripts/tests/test_*.sh (bash) and test_*.ps1
      (powershell.exe) in that slot's worktree: the .sh files in one lane,
      the .ps1 files in a second lane beside it. Every file runs; each
      file's output is buffered and printed once the suite ends, .sh files
      then .ps1 files, each ending in one PASS/FAIL line. Any failure exits
      1 after the whole suite. An unknown slot, a missing scripts/tests,
      or one with no test files also exits 1.
      Trailers: SCRIPT_TEST_FILE=<name> SECONDS=<wall seconds> EXIT=<child exit>
      per file; SCRIPT_TEST_TOTAL_SECONDS=<suite wall seconds>.
      During a merge, journal event script-test (phase script-tests) carries
      file, sec and exit for each completed file.
      Non-hermetic files are SKIPped unless
      SCRIPT_TESTS_INCLUDE_NONHERMETIC=1. Exit 0 = all green. A covers
      line entry that matches no file exits 1 before any file runs. The
      first line names the files that run; during a merge, journal event
      script-selection carries mode, reason, files and unlisted. The merge
      gate runs this when the landing diff touches scripts/, handing it
      the landing range so only the test files that range selects run
      (doc/agents/script-contracts.md sec.4).

  create-pr <slot> [base] --title "<text>" (--body "<text>" | --body-file <path>)
      Push the slot's work to its task branch (task/<lease>, recorded
      for merge/revise like submit) and create a PR with gh (default
      base: main) — submit without the test run. An explicit --title
      and exactly one of --body/--body-file are REQUIRED — the PR must
      describe the change, not echo the last commit subject. If an open
      PR already exists for that head/base, prints URL. Exits 2 before
      anything runs when the body negates a closing keyword ("does not
      close #N"): GitHub still closes #N (scripts/lib/negated_close.py).

  submit <slot> [base_ref] --title "<text>" (--body "<text>" | --body-file <path>) [-- unity_test_agent.ps1 args...]
      Run tests and the ReSharper ratchet, push to a task-specific remote
      branch (task/<lease>), and create PR — but keep the lock so the agent
      can respond to review feedback. An explicit --title and exactly one of
      --body/--body-file are REQUIRED, and the body passes create-pr's
      exit-2 check. Test args after -- are passed to
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

  merge <slot> [base_ref] [--remote] [-- unity_test_agent.ps1 args...]
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
      Either path accepts remote proof on the landing commit: a green
      merge-proof/headless status whose trailer stamps the landing tree, and
      for the ReSharper ratchet a green merge-proof/resharper status (the
      hosted ratchet) stamping the landing tree AND the gate's base tree.
      A base other than main never matches, so it takes the local ratchet.
      When a run is needed, its producer is chosen in this order:
        --remote            hosted headless suite; memory admission not asked
        -- <runner args>    local run; memory admission not asked
        neither             the access coordinator's BootAdmission verdict:
                            boot_admitted -> local run, boot_not_admitted ->
                            hosted suite, anything else -> refused
      A hosted run is refused when the landing diff touches .github/, since
      such a PR can edit the workflow that proves it; with boot_not_admitted
      too, only a user-approved 'merge <slot> -- -AllowLowMemory' lands it.
      Hosted ladder (... proof-check remote-proof resharper ...): one hosted
      run posts both statuses, the gate waits for both, and the resharper
      phase accepts the hosted ratchet with no local ratchet run and no Unity
      boot here; without an acceptable merge-proof/resharper it refuses, and
      one stamping another baseTree means main moved: re-run
      'merge <slot> --remote'.
      When the landing diff touches scripts/, the script suite starts at the
      end of proof-check and runs beside the test run or hosted wait and the
      ratchet; the script-tests phase joins it. It runs only the test files
      whose covers line the landing diff touches (script-suite selection,
      doc/agents/script-contracts.md sec.4).
      One gate per slot: a second 'merge' on a slot whose gate is running is
      refused at once (flock on the slot's .merge file under the lock root).
      One gate at a time, pool-wide (the merge turn): after the slot's lock
      a gate takes the 'merge-turn' lock (see lock) and holds it from before
      its fetch through gh pr merge, so no other gate on this machine moves
      base under it. A gate that finds the turn taken waits in journal phase
      turn-wait, which comes first in every ladder. Waiting gates take the
      turn in arrival order: each records its arrival as a turn ticket
      (<slot>.turn-ticket under the lock root) and holds a flock on it
      while it waits. A ticket counts only while that flock is held, so a
      waiter that died never blocks the line, and a re-run gate arrives
      anew, at the back. merge-progress shows a waiter's place in the line,
      and the slot it waits behind when a gate holds the turn. A gate
      waiting for the turn already holds its slot's .merge lock, so 'merge'
      and 'hold' on that slot refuse.
      A waiter gives up only on a stuck holder: once it has watched one
      holder keep the turn for WORKTREE_POOL_MERGE_TURN_WAIT_SECONDS
      (default 3600) it exits 75; stderr names the lock file and the holder
      slot, or says the holder is not a merge gate. Each change of holder
      restarts that count, so a line that keeps moving times nobody out.
      Any other push to base takes the same turn, e.g. land-docs, or:
        lock merge-turn --wait 3600 -- <sync and push cmd>
      Such a caller holds no ticket: it takes the turn whenever it is free.
      The turn is machine-local: a base move from anywhere else is still
      caught just before gh pr merge ("base moved during the merge gate").
      gh pr merge names the landing commit (--match-head-commit), so a push
      to the PR after the gate's own push is refused, not landed.
      Stdout trailer, once per gate that started: GATE=merged, or
      GATE=refused:<reason>, the reason one of
        base-moved     base moved during the gate (re-run)
        no-verdict     the hosted run gave no verdict in time
        hosted-error   a merge-proof status is error, or pending with no
                       live run (the run was cancelled or timed out)
        gh             a GitHub read failed, or gh pr merge failed 5 times
        failure        a merge-proof status is failure (red, or a dead runner)
        conflict       base does not merge into the slot
        turn-held      exit 75, above
        unauthorized   land's authorize phase (see land)
        github         the hosted path refused a .github/ landing diff
      or else the name of the gate phase that refused. A gate refused at
      the slot's .merge lock never started, and prints none.

  borrow <lease>
      Lease a slot for a pass of unattended work (the verify task's is
      'borrow verify'), without moving its tree. Reuses the slot already
      holding <lease>, which a dead pass left, unless the access
      coordinator shows a live Unity owner on that slot's project; else
      claims the first free slot, skipping one holding unpushed work.
      Never reclaims a stale slot and records no task branch.
      Output: SLOT=<name> PATH=<abs-path>. Exit 1: no free slot, a live
      owner, the coordinator unreadable, or a usage error.

  return <slot> <lease>
      Give back a borrowed slot: refuses, touching nothing, unless <slot>
      holds <lease>; then prepares it at origin/main with --force (a PR
      head whose branch is deleted would make plain prepare refuse it for
      everyone) and releases it. Exit 0 returned, 1 refused or a usage
      error, 75 a merge gate running on the slot (nothing touched); any
      other code is a failed prepare's, and the slot keeps <lease>.

  land <pr>
      Land an open PR that no slot holds, on the user's recorded
      instruction (scripts/drain_pick.sh instruct); every fact about the
      PR's instruction, review and merge order comes from
      'drain_pick.sh land-facts'. Preflight, before any slot or the merge
      turn is taken, refuses: a PR not open against main, a draft, a head
      that is not a task/* branch of this repository (the hosted suite
      runs only there), an owed-local checklist that is open or malformed,
      no recorded instruction, an unresolved review thread, a live
      '## Merge order' constraint naming an open PR, a slot other than a
      land-<pr> slot holding the PR's head branch (that slot's session
      merges it with 'merge <slot>'), and no free slot. land never
      reclaims a stale slot and skips a free one holding unpushed work; it
      reuses a land-<pr> slot a dead run left.
      The slot is leased as land-<pr>, recorded with the PR's head branch
      as its task branch, and checked out at the PR head from origin. The
      merge gate then runs on the hosted path (as --remote: no Unity boot,
      memory admission not asked) from no local proof, with one more
      phase after base-merge, authorize: it refuses unless the recorded
      instruction's commit covers the landing tree, i.e. merging that
      commit with base (git merge-tree) gives the landing tree, or a tree
      that differs from it only by an inert (doc or comment) delta. A
      conflicting merge covers nothing. A landing diff touching .github/
      is refused (GATE reason github); land it with 'merge <slot>'.
      After the merge the slot is finalized; after a refusal it is reset
      to origin/main and released.
      At most 3 merge gate attempts per call, and only after base-moved,
      no-verdict, hosted-error or gh; before a hosted-error retry land
      re-dispatches the hosted headless suite on the PR's branch and waits
      for its first status. Nothing counts attempts across calls.
      The auto-merge class is computed in shadow and authorizes nothing
      (WORKTREE_POOL_AUTO_MERGE_CLASS=1, for tests, lets class membership
      stand in for a covering instruction). It holds when the owed verdict
      is none, the landing diff touches neither scripts/ nor .github/,
      both merge-proof statuses were accepted on the landing commit, and
      Codex's completed review covers the landing tree with no unresolved
      review thread.
      Trailers, at most once each: CLASS=in | CLASS=out:<condition>, the
      first failing of owed, paths, hosted, review (printed once the gate
      passes every check before its push); then GATE= as for merge, its
      reason also one of the preflight reasons not-open, base, draft,
      head-branch, owed-open, owed-malformed, facts (land-facts printed
      no verdict), no-instruction, unresolved, merge-order-malformed,
      after:<pr>, slot:<slot>, no-free-slot, gate-running (another land of
      this PR holds its slot), checkout, or error (the gate printed no
      trailer). Exit: 0 merged; 1 refused, or a usage error.

  land-docs
      The docs-only landing (agent-worktree-pr-loop skill): push the HEAD
      of the worktree it runs in straight to main, with no PR, when every
      path it changes is under doc/ or ends in .md. Run it from the
      worktree holding the commits: a slot, or a cloud session's checkout.
      Holding the merge turn as 'lock merge-turn' does (no ticket, waiting
      up to WORKTREE_POOL_MERGE_TURN_WAIT_SECONDS), it fetches origin,
      rebases HEAD onto origin/main, checks the paths origin/main..HEAD
      changes (a rename counts as a delete and an add), and pushes HEAD to
      main without force.
      Stdout trailer: LAND_DOCS=landed <sha>, or LAND_DOCS=refused:<reason>,
      the reason one of
        rebase     HEAD did not rebase onto origin/main: a conflict (the
                   rebase is aborted) or a dirty tree
        empty      HEAD changes nothing on origin/main
        paths      a changed path is outside doc/ and *.md (stderr lists them)
        push       the push failed, e.g. main moved from another clone
        turn-held  the merge turn was still held after the wait
      A failed fetch prints no trailer. Exit: 0 landed; 75 turn-held;
      1 any other refusal, or a usage error.

  finalize <slot> [base_ref]
      After PR is merged: reset slot branch to base ref (default:
      origin/main), delete the task branch, and release the lock. Requires a
      clean slot at the merged PR head, the merge commit in the fetched base,
      and no later remote task-branch commits. Missing or failed evidence
      exits nonzero before cleanup; preserve additional work before retrying.

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
  scripts/agent_worktree_pool.sh review-comments agent-1
  scripts/agent_worktree_pool.sh revise agent-1 -- -Mode EditMode -ScopeType Feature -ScopeName camera
  scripts/agent_worktree_pool.sh revise agent-1 --no-test
  scripts/agent_worktree_pool.sh submit agent-1 origin/main --title "fix(nav): clamp turn rate" --body-file pr_body.md -- -Mode Both -ScopeType Workspace
  scripts/agent_worktree_pool.sh merge agent-1
  scripts/agent_worktree_pool.sh land 812
  scripts/agent_worktree_pool.sh finalize agent-1 origin/main
  scripts/agent_worktree_pool.sh release agent-1
  scripts/agent_worktree_pool.sh hold agent-1 --local
  scripts/agent_worktree_pool.sh resume task-123
EOF
}

# ---- Slot & lease resolution -----------------------------------------------
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

# ---- Run summary & merge-grade proof ---------------------------------------
# The pool tells the runner where to write (-OutDir), so the pool may read that directory back.
# Everything under it - summary name, editor logs - is the RUNNER's layout: derive paths from this
# one variable, never re-spell the directory at a read site.
RUN_OUTDIR_REL="results/unity-tests-agent"
SUMMARY_REL="$RUN_OUTDIR_REL/latest-summary.json"

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

# First value of KEY= in a KEY=value record file; empty when absent.
record_field() {
  local file="$1" key="$2" line
  [[ -r "$file" ]] || return 2
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    if [[ "$line" == "$key="* ]]; then
      printf '%s\n' "${line#"$key="}"
      return 0
    fi
  done < "$file"
}

tested_scope_field() {
  local slot="$1" key="$2"
  record_field "$(lock_dir_for "$slot")/tested_scope" "$key"
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

# ---- Inert-delta classification --------------------------------------------
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

# ---- Locks -----------------------------------------------------------------
LOCK_BUSY_EXIT=75

# Runs <cmd...> under an exclusive flock on <file>; still held after <wait>s → LOCK_BUSY_EXIT, printing any <busy_msg>.
with_flock() {
  local file="$1" wait="$2" busy_msg="$3"
  shift 3
  command -v perl >/dev/null 2>&1 || { echo 'Pool locking requires Perl flock support.' >&2; return 1; }
  # The execed command inherits the lock; launcher death cannot expose a surviving child.
  # POOL_FLOCK_FDS lets a background child close every lock fd, so none outlives its holder.
  perl -e '
    use strict;
    use warnings;
    use Fcntl qw(LOCK_EX LOCK_NB F_SETFD);
    my ($path, $wait, $busy, $busy_exit) = splice @ARGV, 0, 4;
    open my $lock, ">>", $path or die "Pool lock $path: $!\n";
    my $deadline = time + $wait;
    until (flock($lock, LOCK_EX | LOCK_NB)) {
      if (time >= $deadline) {
        print STDERR "$busy\n" if length $busy;
        exit $busy_exit;
      }
      select(undef, undef, undef, 0.1);
    }
    fcntl($lock, F_SETFD, 0) or die "Pool lock inheritance: $!\n";
    $ENV{POOL_FLOCK_FDS} = join " ", split(" ", $ENV{POOL_FLOCK_FDS} // ""), fileno($lock);
    exec @ARGV or die "Pool lock exec: $!\n";
  ' "$file" "$wait" "$busy_msg" "$LOCK_BUSY_EXIT" bash -c 'source "$1"; shift; "$@"' \
    pool-mutation "$SCRIPT_DIR/agent_worktree_pool.sh" "$@"
}

with_slot_mutation() {
  local slot="$1"
  shift
  with_flock "$LOCK_ROOT/$slot.mutation" 0 "" "$@"
}

cmd_lock() {
  local usage_line="lock requires <name> [--wait <seconds>] -- <cmd...>" name="${1:-}" wait=30
  [[ "$name" =~ ^[A-Za-z0-9._-]+$ ]] || { echo "$usage_line" >&2; return 1; }
  shift
  if [[ "${1:-}" == --wait ]]; then
    [[ "${2:-}" =~ ^[0-9]+$ ]] || { echo "$usage_line" >&2; return 1; }
    wait="$2"
    shift 2
  fi
  [[ "${1:-}" == -- && $# -ge 2 ]] || { echo "$usage_line" >&2; return 1; }
  shift
  # `command` runs <cmd> itself even when it shares a name with a pool function.
  with_flock "$LOCK_ROOT/$name.lock" "$wait" \
    "lock: $name is still held after ${wait}s ($LOCK_ROOT/$name.lock)" command "$@"
}

write_lock() {
  local slot="$1" lease="$2" path="$3"
  local ldir
  ldir="$(lock_dir_for "$slot")"
  printf '%s\n' "$lease" > "$ldir/lease"
  printf '%s\n' "$$" > "$ldir/pid"
  date -u +"%Y-%m-%dT%H:%M:%SZ" > "$ldir/timestamp"
  cp "$ldir/timestamp" "$ldir/last_use"
  if [[ -n "$path" ]]; then
    # Worktree-scoped, never the repo-shared .git/config: a plain write there clobbers every slot's lease (cross-slot LEASE RACE); the unqualified --unset keeps the shared key clear.
    git -C "$path" config extensions.worktreeConfig true 2>/dev/null || true
    git -C "$path" config --worktree worktree-pool.lease "$lease" 2>/dev/null || true
    git -C "$path" config --unset worktree-pool.lease 2>/dev/null || true
  fi
}

# Reclaim decides under the slot's mutation lock, so this write takes it too.
mark_slot_used() {
  local slot="$1"
  # Working commands also run on free slots, which have no lock to stamp.
  [[ -d "$(lock_dir_for "$slot")" ]] || return 0
  with_flock "$LOCK_ROOT/$slot.mutation" 30 \
    "$slot's lease is still being changed after 30s ($LOCK_ROOT/$slot.mutation); its use was not recorded." \
    stamp_last_use "$slot" || exit 1
}

stamp_last_use() {
  local ldir
  ldir="$(lock_dir_for "$1")"
  # A release can land while this waits for the mutation lock.
  [[ -d "$ldir" ]] || return 0
  date -u +"%Y-%m-%dT%H:%M:%SZ" > "$ldir/last_use"
}

# A lock with no last-use stamp counts from its acquire time.
lock_last_use() {
  local ldir="$1" stamp="$1/last_use"
  [[ -s "$stamp" ]] || stamp="$ldir/timestamp"
  cat "$stamp" 2>/dev/null || true
}

lock_age_seconds() {
  local ldir="$1"
  local used ts now
  used="$(lock_last_use "$ldir")"
  [[ -n "$used" ]] || { echo 999999999; return 0; }
  ts="$(date -u -d "$used" +%s 2>/dev/null || echo 0)"
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

# A local held/* branch keeps HEAD reachable through a reset, pushed or not (hold --local).
is_head_held() {
  local path="$1"
  [[ -n "$(git -C "$path" for-each-ref --contains HEAD --format='%(refname)' refs/heads/held/ 2>/dev/null)" ]]
}

slot_is_clobber_safe() {
  local path="$1" base="${2:-origin/main}"
  local dirty ahead
  dirty="$(git -C "$path" status --porcelain 2>/dev/null | wc -l | tr -d ' ')"
  [[ "${dirty:-0}" -eq 0 ]] || return 1
  ahead="$(git -C "$path" rev-list --count "$base"..HEAD 2>/dev/null || echo 0)"
  [[ "${ahead:-0}" -eq 0 ]] && return 0
  is_head_pushed "$path" || is_head_held "$path"
}

# ---- Status ----------------------------------------------------------------
# The pool's read interface: blank-line-separated records (slots, then held leases), KEY=value per line (git's own
# --porcelain shape, and the only shape safe for paths with spaces). Both `status` renderings are
# adapters over this - nothing else may read the lock dir or re-derive a lease.
collect_slot_records() {
  local slot path ldir lease tb pid ts used age state
  while IFS=$'\t' read -r slot path; do
    ldir="$(lock_dir_for "$slot")"
    printf 'slot=%s\n' "$slot"
    if [[ -d "$ldir" ]]; then
      # Lease/branch are locked-slot keys: a free slot's leftover worktree config is not a claim.
      lease="$(lease_for "$slot")"
      tb="$(task_branch_for "$slot")"
      pid="$(cat "$ldir/pid" 2>/dev/null || true)"
      ts="$(cat "$ldir/timestamp" 2>/dev/null || true)"
      used="$(lock_last_use "$ldir")"
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
      [[ -n "$used" ]] && printf 'last_used_at=%s\n' "$used"
    else
      printf 'state=free\n'
      printf 'path=%s\n' "$path"
    fi
    printf '\n'
  done < <(slots_tsv)
}

# The local branch wins when both exist; an origin-only lease was held from another clone.
collect_held_records() {
  local lease branch ref pushed left at
  while IFS= read -r lease; do
    [[ -n "$lease" ]] || continue
    branch="held/$lease"
    pushed=0
    git -C "$ROOT" rev-parse -q --verify "refs/remotes/origin/$branch" >/dev/null && pushed=1
    ref="refs/heads/$branch"
    git -C "$ROOT" rev-parse -q --verify "$ref" >/dev/null || ref="refs/remotes/origin/$branch"
    left="$(held_trailer "$ref" Held-Slot)"
    at="$(TZ=UTC git -C "$ROOT" log -1 --date=format-local:%Y-%m-%dT%H:%M:%SZ --format=%cd "$ref")"
    printf 'held=%s\nbranch=%s\n' "$lease" "$branch"
    [[ -n "$left" ]] && printf 'left_slot=%s\n' "$left"
    printf 'held_at=%s\npushed=%s\n\n' "$at" "$pushed"
  done < <(git -C "$ROOT" for-each-ref --format='%(refname)' refs/heads/held/ refs/remotes/origin/held/ \
    | sed -e 's#^refs/heads/held/##' -e 's#^refs/remotes/origin/held/##' | sort -u)
}

cmd_status() {
  local any=0 slot="" state="" path="" lease="" tb="" pid="" ts="" held="" left="" pushed="" line key value
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
        held) held="$value" ;;
        left_slot) left="$value" ;;
        pushed) pushed="$value" ;;
      esac
      continue
    fi
    if [[ -n "$held" ]]; then
      local where="local"
      [[ "$pushed" == 1 ]] && where="pushed"
      echo "held/$held | HELD   | left=${left:-unknown} $where"
      held=""; left=""; pushed=""
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
  done < <(collect_slot_records; collect_held_records)

  if [[ "$any" -eq 0 ]]; then
    echo "No agent-* worktrees found."
    exit 1
  fi
}

# ---- Acquire / release / prepare -------------------------------------------
try_lock_slot() { with_slot_mutation "$1" claim_free_slot "$@"; }

claim_free_slot() {
  local slot="$1" lease="$2" path="$3"
  local ldir
  ldir="$(lock_dir_for "$slot")"
  mkdir "$ldir" 2>/dev/null || return 1
  write_lock "$slot" "$lease" "$path"
  echo "SLOT=$slot PATH=$path"
}

# Reclaim only past-TTL locks whose slot holds no unpushed work (never clobber a dead lock's WIP — the CLOBBER HAZARD).
try_reclaim_slot() { with_slot_mutation "$1" reclaim_stale_slot "$@"; }

reclaim_stale_slot() {
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
  mkdir "$ldir"
  write_lock "$slot" "$lease" "$path"
  echo "Reclaimed stale lock on $slot (age ${age}s > TTL ${LOCK_TTL_SECONDS}s)" >&2
  echo "SLOT=$slot PATH=$path"
}

# A released slot keeps its old tree, so a free slot is only handed out prepared.
claim_prepared_slot() {
  local slot="$1" lease="$2" path="$3" out
  out="$(try_lock_slot "$slot" "$lease" "$path")" || return 1
  # A child process keeps prepare's errexit, which a failure-handling context would switch off.
  if ! bash "$SCRIPT_DIR/agent_worktree_pool.sh" prepare "$slot" origin/main >&2; then
    with_slot_mutation "$slot" release_slot "$slot" "$lease" >&2 || true
    echo "acquire: $slot is free but could not be prepared, so it was released untouched." >&2
    exit 1
  fi
  echo "$out"
}

# resume claims lock-only: it resets the slot to its held snapshot itself.
lock_slot() {
  local lease="$1" wanted="$2" claim="$3"

  # A named slot is strict: the caller chose it for state the pool can't see — silently handing back a different slot recreates the surprise naming was meant to remove.
  if [[ -n "$wanted" ]]; then
    local path
    path="$(slot_path "$wanted")" || { echo "acquire: unknown slot '$wanted'" >&2; return 1; }
    "$claim" "$wanted" "$lease" "$path" && return 0
    try_reclaim_slot "$wanted" "$lease" "$path" && return 0
    echo "acquire: $wanted unavailable (lease=$(lease_for "$wanted")); no fallback when a slot is named." >&2
    return 1
  fi

  # Free slots first; reclaiming a stale lock crosses another session's expectations, so it is a fallback pass, never interleaved.
  local slot path
  while IFS=$'\t' read -r slot path; do
    "$claim" "$slot" "$lease" "$path" && return 0
  done < <(slots_tsv)
  while IFS=$'\t' read -r slot path; do
    try_reclaim_slot "$slot" "$lease" "$path" && return 0
  done < <(slots_tsv)

  echo "No free slots" >&2
  return 1
}

cmd_acquire() { lock_slot "${1:-task-$(date +%Y%m%d-%H%M%S)}" "${2:-}" claim_prepared_slot; }

cmd_release() { with_slot_mutation "$1" release_slot "$@"; }

# An expected lease makes it compare-and-release: a slot reclaimed meanwhile keeps its new holder.
release_slot() {
  local slot="$1" expected="${2:-}"
  local ldir path current
  ldir="$(lock_dir_for "$slot")"
  path="$(slot_path "$slot" 2>/dev/null || true)"
  if [[ -n "$expected" ]]; then
    current="$(lease_for "$slot")"
    if [[ "$current" != "$expected" ]]; then
      echo "$slot now holds lease '${current:-none}', not '$expected'; left locked." >&2
      return 1
    fi
  fi
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
    echo "  Work that must not be pushed (held for approval): hold it, which also frees the slot:" >&2
    echo "    $0 hold $slot --local" >&2
    echo "  Work that may be pushed but waits on the user: $0 hold $slot" >&2
    echo "  Work that belongs on its task branch: push it, then re-run with --force:" >&2
    echo "    git -C $path push origin HEAD:refs/heads/task/<lease>" >&2
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

# ---- Hold / resume -----------------------------------------------------------
# Resume reads trailers, not a local file, so an origin-only held branch is the whole record.
held_trailer() {
  local ref="$1" key="$2" value
  value="$(git -C "$ROOT" log -1 --format="%(trailers:key=$key,valueonly)" "$ref")"
  printf '%s' "${value%%$'\n'*}"
}

# Built in a copy of the index so the slot's own index and worktree stay untouched until prepare.
held_snapshot() {
  local path="$1" lease="$2" slot="$3"
  local index tmp_index tree rc=0
  # Callers run this in $( ), where errexit is off: every step feeds rc.
  index="$(git -C "$path" rev-parse --path-format=absolute --git-path index)" || return 1
  tmp_index="$(mktemp)" || return 1
  cp "$index" "$tmp_index" || rc=$?
  [[ "$rc" -ne 0 ]] || GIT_INDEX_FILE="$tmp_index" git -C "$path" add -A || rc=$?
  [[ "$rc" -ne 0 ]] || tree="$(GIT_INDEX_FILE="$tmp_index" git -C "$path" write-tree)" || rc=$?
  rm -f "$tmp_index"
  [[ "$rc" -eq 0 ]] || return "$rc"
  printf 'hold: %s\n\nHeld-Lease: %s\nHeld-Slot: %s\n' "$lease" "$lease" "$slot" \
    | git -C "$path" commit-tree "$tree" -p HEAD
}

# Runs under the slot's .merge flock (see main): no merge gate is live, and none starts mid-hold.
cmd_hold() {
  local slot="$1" mode="${2:-}"
  local path lease branch snap where="origin" self="$SCRIPT_DIR/agent_worktree_pool.sh"
  case "$mode" in
    ""|--local) ;;
    *) echo "hold: unknown argument '$mode' (hold <slot> [--local])" >&2; return 1 ;;
  esac
  path="$(slot_path "$slot")" || { echo "hold: unknown slot '$slot'" >&2; return 1; }
  [[ -d "$(lock_dir_for "$slot")" ]] || { echo "hold: $slot is not locked; there is no work to hold." >&2; return 1; }
  lease="$(lease_for "$slot")"
  [[ -n "$lease" ]] || { echo "hold: $slot has no lease, and held work is keyed on it." >&2; return 1; }

  branch="held/$lease"
  if git -C "$ROOT" rev-parse -q --verify "refs/heads/$branch" >/dev/null \
    || git -C "$ROOT" rev-parse -q --verify "refs/remotes/origin/$branch" >/dev/null; then
    echo "hold: $branch already exists here or on origin; resume it ('$self resume $lease') before holding $slot again." >&2
    return 1
  fi
  snap="$(held_snapshot "$path" "$lease" "$slot")"
  git -C "$ROOT" update-ref "refs/heads/$branch" "$snap" ""

  if [[ "$mode" == "--local" ]]; then
    where="this clone only"
  elif ! git -C "$path" push -q --force-with-lease="refs/heads/$branch:" origin "$snap:refs/heads/$branch"; then
    git -C "$ROOT" update-ref -d "refs/heads/$branch" "$snap"
    echo "hold: pushing $branch failed, so $slot is untouched. For work that must not be pushed: $self hold $slot --local" >&2
    return 1
  fi

  # The snapshot is on its branch, so this reset destroys nothing.
  cmd_prepare "$slot" origin/main --force >&2
  with_slot_mutation "$slot" release_slot "$slot" "$lease" >&2 || {
    echo "hold: $slot was prepared but not released; the work is safe on $branch." >&2
    return 1
  }
  echo "Held $slot's work on $branch ($where); $slot is prepared and free." >&2
  echo "HELD=$lease RESUME=\"agent_worktree_pool.sh resume $lease\""
}

cmd_resume() {
  local lease="$1" wanted="${2:-}"
  local branch ref snap base hint hint_path out="" slot path rc=0
  branch="held/$lease"
  ref="refs/heads/$branch"
  if ! git -C "$ROOT" rev-parse -q --verify "$ref" >/dev/null; then
    ref="refs/remotes/origin/$branch"
    git -C "$ROOT" fetch -q origin "+refs/heads/$branch:$ref" 2>/dev/null || {
      echo "resume: no held work for '$lease': $branch is not local and could not be fetched from origin." >&2
      return 1
    }
  fi
  snap="$(git -C "$ROOT" rev-parse "$ref^{commit}")"
  base="$(git -C "$ROOT" rev-parse "$snap^1")"

  if [[ -n "$wanted" ]]; then
    out="$(lock_slot "$lease" "$wanted" try_lock_slot)" || return 1
  else
    hint="$(held_trailer "$snap" Held-Slot)"
    if [[ -n "$hint" ]] && hint_path="$(slot_path "$hint" 2>/dev/null)"; then
      out="$(try_lock_slot "$hint" "$lease" "$hint_path")" || out=""
    fi
    [[ -n "$out" ]] || out="$(lock_slot "$lease" "" try_lock_slot)" || return 1
  fi
  slot="${out#SLOT=}"
  slot="${slot%% PATH=*}"
  path="${out#* PATH=}"

  if ! slot_is_clobber_safe "$path"; then
    with_slot_mutation "$slot" release_slot "$slot" "$lease" >&2 || true
    echo "resume: $slot holds unpushed or uncommitted work, so it was released untouched; name another slot." >&2
    return 1
  fi
  git -C "$path" checkout -q "$slot"
  git -C "$path" reset -q --hard "$snap"
  git -C "$path" reset -q "$base"

  if git -C "$ROOT" rev-parse -q --verify "refs/heads/$branch" >/dev/null; then
    git -C "$ROOT" update-ref -d "refs/heads/$branch" "$snap" || rc=1
  fi
  if git -C "$ROOT" rev-parse -q --verify "refs/remotes/origin/$branch" >/dev/null; then
    git -C "$ROOT" push -q --force-with-lease="refs/heads/$branch:$snap" origin --delete "$branch" || rc=1
  fi
  if [[ "$rc" -ne 0 ]]; then
    echo "resume: the work is restored on $slot, but $branch could not be deleted everywhere; delete it once checked." >&2
  fi
  echo "SLOT=$slot PATH=$path RESUMED=$lease"
  return "$rc"
}

# ---- Unity test runs -------------------------------------------------------
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
      -OutDir "$RUN_OUTDIR_REL" \
      "$@"
  )
}

restore_tracked_unity_changes() {
  local path="$1" action="$2" changes verdict diff
  changes="$(git -C "$path" status --porcelain --untracked-files=no 2>/dev/null)"
  [[ -n "$changes" ]] || return 0

  # The analytics-churn allowlist has ONE owner (scripts/lib/unity_churn.ps1); a second copy of the
  # regex here is exactly the drift the classifier exists to prevent. Both the verdict and the diff
  # are captured BEFORE the restore - the restore is what destroys the evidence this error reports.
  diff="$(git -C "$path" diff --unified=0 2>/dev/null || true)"
  verdict="$(powershell.exe -NoProfile -ExecutionPolicy Bypass \
    -File "$SCRIPT_DIR/lib/unity_churn.ps1" -WorktreePath "$path" 2>/dev/null || true)"

  git -C "$path" restore --worktree --source=HEAD -- .

  case "$verdict" in
    *'"knownChurn":true'*) return 0 ;;
  esac
  echo "$action changed unexpected tracked files:" >&2
  printf '%s\n' "$changes" | head -n 20 >&2
  echo "--- diff, captured before the restore ---" >&2
  printf '%s\n' "$diff" | head -n 60 >&2
  return 1
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

# Proof comes only from this run: no stale summary may vouch for the tree.
run_tests_for_proof() {
  local slot="$1" path="$2"
  shift 2
  clear_run_summary "$path"
  cmd_run_tests_clean "$slot" "$@"
  record_tested_tree "$slot" "$path"
}

# ---- ReSharper ratchet -----------------------------------------------------
resharper_fingerprint() {
  local path="$1" file hash hashes="" i=0 files=(
    .config/dotnet-tools.json
    scripts/agent_worktree_pool.sh
    scripts/resharper-unity.DotSettings
    scripts/resharper_ratchet.ps1
    scripts/sync_unity_solution.ps1)
  for file in "${files[@]}"; do
    [[ -f "$path/$file" ]] || { echo "missing:$file"; return 0; }
  done
  while IFS= read -r hash; do
    hashes+="$hash:${files[i++]}"$'\n'
  done < <(git -C "$path" hash-object "${files[@]/#/$path/}")
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
  [[ "$(record_field "$proof" tree)" == "$tree" ]] || return 1
  [[ "$(record_field "$proof" baseTree)" == "$base_tree" ]] || return 1
  [[ "$(record_field "$proof" fingerprint)" == "$fingerprint" ]]
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

# ---- Script tests ----------------------------------------------------------
# run-script-tests trailers: SCRIPT_TEST_FILE=<name> SECONDS=<wall seconds> EXIT=<child exit>;
# SCRIPT_TEST_TOTAL_SECONDS=<suite wall seconds>. Every selected file runs; any failure exits 1.
# Internal: callers supply a resolved worktree path and, from the merge gate, the landing range
# (<base> <head>) that script-suite selection reads. A subshell body keeps its EXIT trap off the caller.
cmd_run_script_tests() (
  local dir="$1" base_ref="${2:-}" head_ref="${3:-}"
  local tests_dir="$dir/scripts/tests" file base rc=0 suite_started=$SECONDS buf sh_lane ps1_lane
  local mode=all reason=no-diff changed entry hit diff_out
  local -a found=() sh_files=() ps1_files=() names=() unlisted=() changes=() entries=()
  local -A covers_of=() picked=()
  # A name here is skipped because its state escapes a temp dir, so another session can turn it red.
  # Empty is the goal state (test_unity_access.ps1 left in #454 by injecting its state+primary root).
  local nonhermetic=" "
  # A changed path here runs every file: tests load these without listing them on a covers line.
  # scripts/tests/* means its non-test files (fixtures); a changed test file selects only itself.
  local -a shared=("scripts/lib/*" "scripts/tests/*")
  if [[ ! -d "$tests_dir" ]]; then
    echo "run-script-tests: $tests_dir is missing — the suite did not run." >&2
    return 1
  fi
  for file in "$tests_dir"/test_*.sh "$tests_dir"/test_*.ps1; do
    [[ -f "$file" ]] || continue
    base="${file##*/}"
    if [[ "${SCRIPT_TESTS_INCLUDE_NONHERMETIC:-0}" != 1 && "$nonhermetic" == *" $base "* ]]; then
      echo "SKIP: $base — non-hermetic (its state escapes a temp dir); runs with SCRIPT_TESTS_INCLUDE_NONHERMETIC=1"
      continue
    fi
    found+=("$file")
  done
  if (( ${#found[@]} == 0 )); then
    echo "SCRIPT_TEST_TOTAL_SECONDS=$((SECONDS - suite_started))"
    echo "run-script-tests: no test files under $tests_dir — the suite did not run." >&2
    return 1
  fi
  for file in "${found[@]}"; do
    base="${file##*/}"
    read_covers_line "$file" || continue
    covers_of[$base]="$SCRIPT_TEST_COVERS"
    read -ra entries <<< "$SCRIPT_TEST_COVERS"
    for entry in "${entries[@]}"; do
      if ! compgen -G "$dir/$entry" > /dev/null; then
        echo "run-script-tests: $base covers '$entry', which matches no file in the tree — fix its covers line; the suite did not run." >&2
        return 1
      fi
    done
  done
  if [[ -n "$base_ref" ]]; then
    diff_out="$(git -C "$dir" diff --name-only --no-renames "$base_ref" "$head_ref" -- scripts)" || {
      rc=$?
      echo "run-script-tests: could not compute the landing diff $base_ref..$head_ref (git exit $rc) — the suite did not run." >&2
      return 1
    }
    mapfile -t changes <<< "$diff_out"
    mode=selected reason=diff
    for file in "${found[@]}"; do
      base="${file##*/}"
      [[ -v "covers_of[$base]" ]] || { mode=all reason="undeclared:$base"; break; }
    done
  fi
  if [[ "$mode" == selected ]]; then
    for changed in "${changes[@]}"; do
      [[ -n "$changed" ]] || continue
      if [[ "$changed" =~ ^scripts/tests/test_[^/]*$ ]]; then
        picked[${changed##*/}]=1
        continue
      fi
      for entry in "${shared[@]}"; do
        # shellcheck disable=SC2053  # the entry is a glob pattern
        [[ "$changed" == $entry ]] && { mode=all reason="shared:$changed"; break 2; }
      done
      hit=0
      for base in "${!covers_of[@]}"; do
        read -ra entries <<< "${covers_of[$base]}"
        for entry in "${entries[@]}"; do
          # shellcheck disable=SC2053  # the entry is a glob pattern
          [[ "$changed" == $entry ]] && { picked[$base]=1; hit=1; break; }
        done
      done
      (( hit )) || unlisted+=("$changed")
    done
  fi
  [[ "$mode" == selected ]] || unlisted=()
  for file in "${found[@]}"; do
    base="${file##*/}"
    [[ "$mode" == all || -v "picked[$base]" ]] || continue
    names+=("$base")
    case "$file" in
      *.sh) sh_files+=("$file") ;;
      *) ps1_files+=("$file") ;;
    esac
  done
  echo "Script-suite selection: ${#names[@]} of ${#found[@]} files run (mode=$mode reason=$reason)${names[*]:+: ${names[*]}}${unlisted[*]:+; no covers line lists: ${unlisted[*]}}"
  journal_event script-selection script-tests "mode=$mode" "reason=$reason" "files=${names[*]}" "unlisted=${unlisted[*]}"
  if (( ${#names[@]} == 0 )); then
    echo "SCRIPT_TEST_TOTAL_SECONDS=$((SECONDS - suite_started))"
    return 0
  fi
  buf="$(mktemp -d)"
  # Bash pairs with PowerShell only: same-runtime lanes contend on process-spawn cost.
  run_script_test_lane "$buf" "${sh_files[@]}" &
  sh_lane=$!
  run_script_test_lane "$buf" "${ps1_files[@]}" &
  ps1_lane=$!
  # Armed after the fork: some bash builds run an inherited EXIT trap on lane exit.
  trap "rm -rf '$buf'" EXIT
  wait "$sh_lane" || rc=1
  wait "$ps1_lane" || rc=1
  for base in "${names[@]}"; do
    cat "$buf/$base"
  done
  echo "SCRIPT_TEST_TOTAL_SECONDS=$((SECONDS - suite_started))"
  return "$rc"
)

# Sets SCRIPT_TEST_COVERS to the file's covers-line entries; 1 = none in its first 10 lines.
read_covers_line() {
  local line n=0
  while (( n++ < 10 )) && IFS= read -r line; do
    line="${line%$'\r'}"
    if [[ "$line" == "# covers:"* ]]; then
      SCRIPT_TEST_COVERS="${line#"# covers:"}"
      return 0
    fi
  done < "$1"
  return 1
}

# Runs its files in order; each file's output, trailer and verdict land in <buf>/<name>.
run_script_test_lane() {
  local buf="$1" file base rc sec started failed=0
  shift
  for file in "$@"; do
    base="$(basename "$file")"
    rc=0
    started=$SECONDS
    # A file must not read the runner's stdin: a pipe nobody closes would hang it.
    case "$file" in
      *.sh) bash "$file" < /dev/null > "$buf/$base" 2>&1 || rc=$? ;;
      *.ps1) powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$file" < /dev/null > "$buf/$base" 2>&1 || rc=$? ;;
    esac
    sec=$((SECONDS - started))
    journal_event script-test script-tests "file=$base" "sec=$sec" "exit=$rc"
    echo "SCRIPT_TEST_FILE=$base SECONDS=$sec EXIT=$rc" >> "$buf/$base"
    if [[ "$rc" -eq 0 ]]; then
      echo "PASS $base" >> "$buf/$base"
    else
      echo "FAIL $base (exit $rc)" >> "$buf/$base"
      failed=1
    fi
  done
  return "$failed"
}

# Own process group lets a refusal stop the suite, which must hold none of the gate's locks.
SCRIPT_SUITE_PID=""
SCRIPT_SUITE_LOG=""

start_script_suite() {
  local dir="$1" base_ref="$2" head_ref="$3" fd
  SCRIPT_SUITE_LOG="$(mktemp)"
  set -m
  (
    for fd in ${POOL_FLOCK_FDS:-}; do eval "exec ${fd}>&-"; done
    unset POOL_FLOCK_FDS
    cmd_run_script_tests "$dir" "$base_ref" "$head_ref"
  ) > "$SCRIPT_SUITE_LOG" 2>&1 &
  SCRIPT_SUITE_PID=$!
  set +m
}

join_script_suite() {
  local rc=0
  wait "$SCRIPT_SUITE_PID" || rc=$?
  SCRIPT_SUITE_PID=""
  cat "$SCRIPT_SUITE_LOG"
  rm -f "$SCRIPT_SUITE_LOG"
  return "$rc"
}

stop_script_suite() {
  [[ -n "$SCRIPT_SUITE_PID" ]] || return 0
  local attempt
  # Under Git Bash a child caught mid-exec can miss one group signal, so repeat until the group is empty.
  for attempt in 1 2 3 4 5 6 7 8 9 10; do
    kill -TERM -- "-$SCRIPT_SUITE_PID" 2>/dev/null || break
    sleep 0.2
  done
  wait "$SCRIPT_SUITE_PID" 2>/dev/null || true
  SCRIPT_SUITE_PID=""
  rm -f "$SCRIPT_SUITE_LOG"
}

# 0 = touched, 1 = untouched, 2 = the diff could not be computed. Fail closed: a
# swallowed git error would read as "no change under <dir>" and skip the gate.
landing_diff_touches() {
  local path="$1" base_ref="$2" head_ref="$3" dir="$4" changed rc=0
  changed="$(git -C "$path" diff --name-only "$base_ref" "$head_ref" -- "$dir")" || rc=$?
  if [[ "$rc" -ne 0 ]]; then
    echo "merge: could not compute the landing diff $base_ref..$head_ref (git exit $rc)." >&2
    return 2
  fi
  [[ -n "$changed" ]]
}

# ---- PR opening ------------------------------------------------------------
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

require_gh() {
  command -v gh >/dev/null 2>&1 || {
    echo "gh CLI not found in PATH" >&2
    return 1
  }
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

# PRs are opened from the minted task branch, never the bare agent-N slot branch: --head "agent-2"
# finds nothing (or, worse, a stale PR someone once opened from the slot itself). Callers pass
# task_branch_for's answer.
pr_number_for_pushed_head() {
  local head_branch="$1"
  local base="${2:-main}"
  if [[ -z "$head_branch" ]]; then
    echo "pr_number_for_pushed_head: no head branch — the slot has no recorded task branch." >&2
    return 1
  fi
  gh pr list --head "$head_branch" --base "$base" --state open --json number --jq '.[0].number' 2>/dev/null || true
}

# One flag grammar for every PR-opening command. Results land in PR_TITLE / PR_BODY /
# PR_BODY_FILE / PR_TEST_ARGS rather than stdout: a command substitution could not carry the
# test-arg array through intact.
# $1 = command name (error prefix), $2 = 1 when trailing '-- <test args>' is accepted.
parse_pr_flags() {
  local cmd="$1" accept_test_args="$2"
  shift 2
  PR_TITLE=""; PR_BODY=""; PR_BODY_FILE=""; PR_TEST_ARGS=()
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --title)
        [[ -n "${2:-}" ]] || { echo "$cmd: --title requires a value" >&2; return 1; }
        PR_TITLE="$2"; shift 2 ;;
      --body)
        [[ -n "${2:-}" ]] || { echo "$cmd: --body requires a value" >&2; return 1; }
        PR_BODY="$2"; shift 2 ;;
      --body-file)
        [[ -n "${2:-}" ]] || { echo "$cmd: --body-file requires a path" >&2; return 1; }
        PR_BODY_FILE="$2"; shift 2 ;;
      --)
        if [[ "$accept_test_args" != 1 ]]; then
          echo "$cmd: '--' is not accepted; $cmd takes no test-runner args." >&2
          return 1
        fi
        shift
        PR_TEST_ARGS=("$@")
        break ;;
      --*)
        if [[ "$accept_test_args" == 1 ]]; then
          echo "$cmd: unknown flag '$1' before '--' — test-runner args go after '--'" >&2
        else
          echo "$cmd: unknown argument: $1" >&2
        fi
        return 1 ;;
      *)
        # A bare word is never a test arg: silently swallowing one hides a typo'd flag from the run.
        echo "$cmd: unexpected argument '$1' — test-runner args go after '--'" >&2
        return 1 ;;
    esac
  done
  require_pr_title_body "$cmd" "$PR_TITLE" "$PR_BODY" "$PR_BODY_FILE" || return 1
  require_no_negated_close "$cmd"
}

# GitHub's keyword parser ignores negation: "does not close #N" still closes #N on merge.
require_no_negated_close() {
  local cmd="$1" rc=0
  if [[ -n "$PR_BODY_FILE" ]]; then
    python3 "$SCRIPT_DIR/lib/negated_close.py" < "$PR_BODY_FILE" || rc=$?
  else
    python3 "$SCRIPT_DIR/lib/negated_close.py" <<<"$PR_BODY" || rc=$?
  fi
  [[ "$rc" -eq 0 ]] || echo "$cmd: refusing the PR body — reword the lines above; no PR was created" >&2
  return "$rc"
}

# Push the slot branch to its minted task branch and open the PR (or report the open one).
push_and_open_pr() {
  local repo_path="$1" slot="$2" base_branch="$3" task_branch="$4"

  git -C "$repo_path" push -u origin "$slot:refs/heads/$task_branch" >/dev/null
  git -C "$ROOT" fetch origin "$base_branch" >/dev/null 2>&1 || true

  local existing
  existing="$(gh pr list --head "$task_branch" --base "$base_branch" --state open --json url --jq '.[0].url' 2>/dev/null || true)"
  if [[ -n "$existing" ]]; then
    echo "$slot PR already open: $existing"
    return 0
  fi

  # gh reads the file itself: inlining a large body overflows Windows' ~32 KB command line.
  local body_args=(--body "$PR_BODY")
  [[ -z "$PR_BODY_FILE" ]] || body_args=(--body-file "$PR_BODY_FILE")
  local url
  url="$(gh pr create --base "$base_branch" --head "$task_branch" --title "$PR_TITLE" "${body_args[@]}")"
  echo "$slot PR created: $url"
}

cmd_create_pr() {
  local slot="$1"
  shift || true

  local base="main"
  if [[ -n "${1:-}" && "${1:-}" != --* ]]; then
    base="$1"
    shift
  fi

  parse_pr_flags "create-pr" 0 "$@" || return
  require_gh || return 1

  git -C "$ROOT" fetch origin "$base" >/dev/null 2>&1 || true

  local ahead
  ahead="$(git -C "$ROOT" rev-list --count "$base..$slot" 2>/dev/null || echo 0)"
  if [[ "$ahead" -eq 0 ]]; then
    echo "Skipping $slot: no commits ahead of $base"
    return 0
  fi

  local task_branch
  task_branch="$(ensure_task_branch "$slot")"
  push_and_open_pr "$ROOT" "$slot" "$base" "$task_branch"
}

cmd_submit() {
  local slot="$1"
  shift || true

  local base_ref="origin/main"
  if [[ -n "${1:-}" && "${1:-}" != --* ]]; then
    base_ref="$1"
    shift
  fi

  # Preflight: flags and tooling are checked before the test run so a missing one fails in
  # seconds, not after a full suite.
  parse_pr_flags "submit" 1 "$@" || return
  require_gh || return 1

  local base_branch
  base_branch="${base_ref#origin/}"

  # Ensure slot branch is checked out (do NOT reset — preserve agent's work)
  local path
  path="$(slot_path "$slot")"
  git -C "$path" checkout "$slot"
  require_clean_slot "$slot" "$path" "submit" || return 1

  run_tests_for_proof "$slot" "$path" "${PR_TEST_ARGS[@]}"
  cmd_run_resharper "$slot" "$base_ref"

  local task_branch
  task_branch="$(ensure_task_branch "$slot")"
  push_and_open_pr "$path" "$slot" "$base_branch" "$task_branch"

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
    turn-wait) echo 900 ;;
    preflight) echo 10 ;;
    fetch) echo 15 ;;
    base-merge) echo 15 ;;
    authorize) echo 20 ;;
    proof-check) echo 5 ;;
    tests) echo 480 ;;
    remote-proof) echo 900 ;;
    resharper) echo 360 ;;
    script-tests) echo 1200 ;;
    push) echo 30 ;;
    base-recheck) echo 15 ;;
    gh-merge) echo 20 ;;
    *) echo 0 ;;
  esac
}

MERGE_JOURNAL=""
MERGE_JOURNAL_PID=""
MERGE_RUN_START=0
MERGE_PHASE=""
MERGE_PHASE_START=0

# Journal fields omit quotes, backslashes and control characters.
json_scrub() {
  local scrubbed="${!1}"
  scrubbed="${scrubbed//\"/}"
  scrubbed="${scrubbed//\\/}"
  printf -v "$1" '%s' "${scrubbed//[[:cntrl:]]/}"
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
  local now stamp line field frag="" kv key val
  printf -v now '%(%s)T' -1
  TZ=UTC printf -v stamp '%(%Y-%m-%dT%H:%M:%SZ)T' "$now"
  json_scrub event
  json_scrub phase
  for kv in "$@"; do
    key="${kv%%=*}"
    val="${kv#*=}"
    json_scrub key
    if [[ "$val" =~ ^-?[0-9]+$ ]]; then
      printf -v field ',"%s":%s' "$key" "$val"
    else
      json_scrub val
      printf -v field ',"%s":"%s"' "$key" "$val"
    fi
    frag+="$field"
  done
  printf -v line '{"ts":"%s","t":%s,"event":"%s","phase":"%s"%s}' \
    "$stamp" "$((now - MERGE_RUN_START))" "$event" "$phase" "$frag"
  journal_line "$line"
}

merge_journal_open() {
  local slot="$1" base_ref="$2" ldir
  MERGE_RUN_START=$EPOCHSECONDS
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

# with_flock's exec drops shell state, so the journal and open phase cross as arguments.
merge_journal_adopt() {
  MERGE_JOURNAL="$1"
  MERGE_RUN_START="$2"
  MERGE_PHASE="$3"
  MERGE_PHASE_START="$4"
  MERGE_JOURNAL_PID="$BASHPID"
}

# Reaching the next phase is itself proof the previous one succeeded, so a begin
# closes the open phase and no call site has to pair them.
merge_phase_begin() {
  merge_phase_end ok
  MERGE_PHASE="$1"
  MERGE_PHASE_START=$EPOCHSECONDS
  journal_event phase-start "$MERGE_PHASE"
}

merge_phase_end() {
  [[ -n "$MERGE_PHASE" ]] || return 0
  local status="${1:-ok}" sec
  sec=$(( EPOCHSECONDS - MERGE_PHASE_START ))
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
  journal_event run-end "" "sec=$(( EPOCHSECONDS - MERGE_RUN_START ))" "status=$status" "exit=$code"
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
    if (openDetail != "") line = line " " openDetail
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
    if (openDetail != "") line = line "   " openDetail
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
  local journal="$1" oneline="${2:-0}" slot="${3:-}" open_phase open_budget open_detail="" holder place
  [[ -f "$journal" ]] || return 0
  open_phase="$(merge_journal_open_phase "$journal")"
  open_budget="$(merge_phase_budget "${open_phase:-none}")"
  # Read live rather than journaled: the turn changes hands and the line moves while a gate waits.
  if [[ "$open_phase" == turn-wait ]]; then
    holder="$(merge_turn_holder)"
    place="$(merge_turn_place "$slot")"
    [[ -z "$holder" ]] || open_detail="behind $holder"
    [[ -z "$place" ]] || open_detail+="${open_detail:+, }place $place in line"
  fi
  awk -v nowEpoch="$(date +%s)" -v openBudget="$open_budget" -v oneline="$oneline" -v openDetail="$open_detail" \
    "$MERGE_RENDER_AWK" "$journal"
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
    merge_journal_render "$journal" 1 "$slot"
    return 0
  fi
  merge_journal_render "$journal" 0 "$slot"
  echo "  journal: $journal"

  # Separates a hung editor from a slow suite far better than a pid check.
  open_phase="$(merge_journal_open_phase "$journal")"
  [[ "$open_phase" == "tests" ]] || return 0
  path="$(slot_path "$slot" 2>/dev/null || true)"
  [[ -n "$path" ]] || return 0
  log="$(ls -1t "$path/$RUN_OUTDIR_REL/"*.log 2>/dev/null | head -n 1 || true)"
  if [[ -z "$log" ]]; then
    echo "  unity: no editor log yet under $path/$RUN_OUTDIR_REL/"
    return 0
  fi
  age=$(( $(date +%s) - $(stat -c %Y "$log" 2>/dev/null || echo 0) ))
  echo "  unity: $(basename "$log") last written ${age}s ago"
}

# ---- Remote proof ----------------------------------------------------------
# The hosted headless suite stamps its verdict as a commit status; the gate trusts that one
# field and checks only what it owns: the stamped tree is the landing tree (script-contracts.md sec.3).
REMOTE_PROOF_CONTEXT="merge-proof/headless"
REMOTE_RESHARPER_CONTEXT="merge-proof/resharper"
REMOTE_PROOF_WORKFLOW="headless-suite.yml"
REMOTE_NO_RUN_SECONDS="${WORKTREE_POOL_REMOTE_NO_RUN_SECONDS:-120}"
REMOTE_QUEUED_SECONDS="${WORKTREE_POOL_REMOTE_QUEUED_SECONDS:-180}"
# Coupled to timeout-minutes in .github/workflows/headless-suite.yml: the hosted job may run this long.
REMOTE_RUN_SECONDS="${WORKTREE_POOL_REMOTE_RUN_SECONDS:-1200}"
REMOTE_POLL_SECONDS="${WORKTREE_POOL_REMOTE_POLL_SECONDS:-15}"

# Prints "<state><US><description><US><target_url>" — US (\x1f), since tab-splitting collapses an empty
# description — for the NEWEST status in the context (the API lists
# a context's whole history, newest first); state "absent" when there is none. Non-zero = GitHub could not be asked.
remote_status() {
  local sha="$1" context="$2" slug
  slug="$(repo_slug)"
  if [[ -z "$slug" ]]; then
    echo "merge: cannot derive the GitHub repo from remote.origin.url." >&2
    return 1
  fi
  gh api "repos/$slug/commits/$sha/statuses?per_page=100" --jq \
    "[.[] | select(.context == \"$context\")][0] // {state: \"absent\"} | [.state, .description // \"\", .target_url // \"\"] | join(\"\")"
}

# Prints "<status>\t<run id>" for run <id> when given, else for the newest headless-suite run on a commit;
# status "none" when there is none.
remote_run() {
  local sha="$1" id="${2:-}"
  if [[ -n "$id" ]]; then
    gh run view "$id" --json databaseId,status --jq '[.status, .databaseId] | @tsv'
    return
  fi
  gh run list --workflow "$REMOTE_PROOF_WORKFLOW" --commit "$sha" --limit 1 --json databaseId,status --jq \
    '.[0] // {status: "none", databaseId: ""} | [.status, .databaseId] | @tsv'
}

run_id_from_url() {
  local url="$1" id
  [[ "$url" == */actions/runs/* ]] || return 0
  id="${url##*/actions/runs/}"
  printf '%s' "${id%%[!0-9]*}"
}

# A green status in <context> on the commit whose trailer stamps every given <key>=<tree> prints
# "<description> run=<url>". Everything else is no proof (fail closed): returns 1 with the reason on stdout.
verified_remote_status() {
  local sha="$1" context="$2"
  shift 2
  local status state description url stamp key
  if ! status="$(remote_status "$sha" "$context")"; then
    echo "no remote proof: could not read the $context status of $sha from GitHub"
    return 1
  fi
  IFS=$'\x1f' read -r state description url <<< "$status"
  if [[ "$state" != "success" ]]; then
    echo "no remote proof: $context on $sha is '${state:-unreadable}', not success"
    return 1
  fi
  for stamp in "$@"; do
    key="${stamp%%=*}"
    if [[ ! "$description" =~ (^|[[:space:]])$key=([0-9a-f]{40})([[:space:]]|$) ]]; then
      echo "no remote proof: the $context trailer on $sha names no $key ('$description')"
      return 1
    fi
    if [[ "${BASH_REMATCH[2]}" != "${stamp#*=}" ]]; then
      echo "no remote proof: the $context trailer on $sha stamps $key ${BASH_REMATCH[2]}, the landing $key is ${stamp#*=}"
      return 1
    fi
  done
  printf '%s run=%s\n' "$description" "$url"
}

accept_remote_proof() {
  local slot="$1" sha="$2" tree="$3"
  local evidence ldir
  evidence="$(verified_remote_status "$sha" "$REMOTE_PROOF_CONTEXT" "tree=$tree")" || { echo "$evidence"; return 1; }
  ldir="$(lock_dir_for "$slot")"
  mkdir -p "$ldir"
  printf '%s\n' "$tree" > "$ldir/tested_tree"
  write_tested_scope "$ldir" "$tree" "remote-run" "$tree" "$evidence"
}

accept_remote_resharper_proof() {
  local slot="$1" path="$2" sha="$3" base_ref="$4"
  verified_remote_status "$sha" "$REMOTE_RESHARPER_CONTEXT" \
    "tree=$(git -C "$path" rev-parse 'HEAD^{tree}')" "baseTree=$(git -C "$path" rev-parse "$base_ref^{tree}")" || return 1
  record_resharper_proof "$slot" "$path" "$base_ref"
}

# Liveness-based wait: the two statuses are the verdicts, run liveness only says whether they are coming.
# The run is read BEFORE the statuses so a run that completes between the reads has already posted.
wait_for_remote_verdict() {
  local slot="$1" sha="$2" task_branch="$3"
  local started=$SECONDS phase_status="" phase_since=$SECONDS
  local run run_status run_id context status state description url owed named run_ref=""
  while :; do
    if ! run="$(remote_run "$sha" "$run_ref")"; then
      GATE_REASON=gh
      echo "merge: could not ask GitHub about $sha — no remote proof; not merging." >&2
      return 1
    fi
    IFS=$'\t' read -r run_status run_id <<< "$run"
    owed="" named=""
    for context in "$REMOTE_PROOF_CONTEXT" "$REMOTE_RESHARPER_CONTEXT"; do
      if ! status="$(remote_status "$sha" "$context")"; then
        GATE_REASON=gh
        echo "merge: could not ask GitHub about $sha — no remote proof; not merging." >&2
        return 1
      fi
      IFS=$'\x1f' read -r state description url <<< "$status"
      case "$state" in
        success) ;;
        failure|error)
          GATE_REASON=failure
          [[ "$state" == failure ]] || GATE_REASON=hosted-error
          [[ -n "$run_id" ]] || run_id="$(run_id_from_url "$url")"
          echo "merge: $context on $sha is '$state' ($description) — not merging." >&2
          echo "  Run: $url" >&2
          echo "  Red tests or ratchet findings: fix, 'revise', re-run merge. A run that died before the suite started (runner/infra):" >&2
          echo "  'gh run rerun $run_id' re-posts both verdicts on the same commit; then re-run 'merge $slot --remote'." >&2
          return 1 ;;
        # The job posts headless pending first, so the first pending context names the newest run.
        pending) owed=pending; [[ -n "$named" ]] || named="$(run_id_from_url "$url")" ;;
        absent) owed="${owed:-absent}" ;;
        *)
          echo "merge: $context on $sha has unknown state '$state' — no remote proof; not merging." >&2
          return 1 ;;
      esac
    done
    [[ -n "$owed" ]] || return 0
    # The runs listing lags a run's own statuses: judge a pending status by the run it names, read first.
    if [[ -n "$named" && "$named" != "$run_id" ]]; then
      run_ref="$named"
      continue
    fi
    state="$owed"
    [[ "$run_status" == "$phase_status" ]] || { phase_status="$run_status"; phase_since=$SECONDS; }
    case "$run_status" in
      in_progress)
        if (( SECONDS - phase_since > REMOTE_RUN_SECONDS )); then
          GATE_REASON=no-verdict
          echo "merge: headless-suite run $run_id has been in progress over ${REMOTE_RUN_SECONDS}s with no verdict — not merging. 'gh run cancel $run_id', then 'gh run rerun $run_id'." >&2
          return 1
        fi ;;
      queued|requested|waiting|pending)
        if (( SECONDS - phase_since > REMOTE_QUEUED_SECONDS )); then
          GATE_REASON=no-verdict
          echo "merge: headless-suite run $run_id has sat queued over ${REMOTE_QUEUED_SECONDS}s — not merging. Re-run 'merge $slot --remote' to keep waiting, or pin the local run with 'merge $slot -- -Mode Both -ScopeType Workspace'." >&2
          return 1
        fi ;;
      *)
        if [[ "$state" == "pending" ]]; then
          GATE_REASON=hosted-error
          echo "merge: a merge-proof status on $sha is pending but no headless-suite run is live — no verdict is coming; not merging." >&2
          echo "  'gh run rerun ${run_id:-<run-id>}' (or 'gh workflow run $REMOTE_PROOF_WORKFLOW --ref $task_branch'), then re-run 'merge $slot --remote'." >&2
          return 1
        fi
        if (( SECONDS - started > REMOTE_NO_RUN_SECONDS )); then
          GATE_REASON=no-verdict
          echo "merge: no headless-suite run exists for $sha ${REMOTE_NO_RUN_SECONDS}s after the gate asked for one — not merging." >&2
          echo "  'gh workflow run $REMOTE_PROOF_WORKFLOW --ref $task_branch', then re-run 'merge $slot --remote'." >&2
          return 1
        fi ;;
    esac
    sleep "$REMOTE_POLL_SECONDS"
  done
}

# Causes a hosted run for the landing commit unless a verdict is already there or coming, then waits.
run_remote_for_proof() {
  local slot="$1" path="$2" sha="$3" task_branch="$4"
  local remote_tip run status
  remote_tip="$(git -C "$path" ls-remote origin "refs/heads/$task_branch" | cut -f1)"
  if [[ "$remote_tip" != "$sha" ]]; then
    echo "Pushing landing commit $sha to $task_branch — the push starts the hosted headless suite."
    git -C "$path" push origin "$slot:refs/heads/$task_branch"
  else
    if ! run="$(remote_run "$sha")" || ! status="$(remote_status "$sha" "$REMOTE_PROOF_CONTEXT")"; then
      GATE_REASON=gh
      echo "merge: could not ask GitHub about $sha — no remote proof; not merging." >&2
      return 1
    fi
    run="${run%%$'\t'*}"
    if [[ "${status%%$'\x1f'*}" == "absent" && ( "$run" == "none" || "$run" == "completed" ) ]]; then
      echo "Landing commit $sha is already on $task_branch with no verdict — dispatching the hosted headless suite."
      gh workflow run "$REMOTE_PROOF_WORKFLOW" --ref "$task_branch"
    fi
  fi
  wait_for_remote_verdict "$slot" "$sha" "$task_branch"
}

# The access coordinator owns memory admission; the gate reads its status word, measuring nothing (script-contracts.md sec.3).
ADMISSION_READER='
. (Join-Path $env:POOL_SCRIPT_DIR "unity_access_client.ps1")
$call = Invoke-UnityAccessCoordinator -CoordinatorArgs @("-Action", "BootAdmission", "-Mode", "batch")
if ($call.stderr) { [Console]::Error.WriteLine($call.stderr) }
if ($call.exitCode -ne 0 -or $null -eq $call.result) { [Console]::Error.WriteLine("BootAdmission exit=" + $call.exitCode + " stdout=" + $call.stdout); exit 1 }
[Console]::Error.WriteLine("memory admission: " + $call.stdout)
Write-Output "$($call.result.status)"
'

# Prints the batch-boot admission status word; non-zero = the coordinator could not be asked.
boot_admission_status() {
  local out
  out="$(POOL_SCRIPT_DIR="$SCRIPT_DIR" powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ADMISSION_READER")" || return 1
  printf '%s\n' "${out//$'\r'/}"
}

# ---- Merge gate ------------------------------------------------------------
# 'lock merge-turn' takes this same file, so any other push to base waits its turn.
MERGE_TURN_LOCK="$LOCK_ROOT/merge-turn.lock"
MERGE_TURN_HOLDER="$LOCK_ROOT/merge-turn.holder"
MERGE_TURN_WAIT_SECONDS="${WORKTREE_POOL_MERGE_TURN_WAIT_SECONDS:-3600}"
MERGE_TURN_POLL_SECONDS="${WORKTREE_POOL_MERGE_TURN_POLL_SECONDS:-0.1}"

# The stamp tells two holds by one slot apart, for a waiter timing a single holder.
merge_turn_publish() { printf '%s %s\n' "$1" "$EPOCHSECONDS" > "$MERGE_TURN_HOLDER"; }

# Empty when the turn is free or a 'lock merge-turn' caller holds it.
merge_turn_holder() { cut -d' ' -f1 "$MERGE_TURN_HOLDER" 2>/dev/null || true; }

# The line is the live turn tickets (<slot>.turn-ticket, one arrival time each) in arrival order.
# A ticket is live while its gate holds its flock, so a probe that takes that flock found it dead.
MERGE_TURN_LINE_PL='
  use strict;
  use warnings;
  use Fcntl qw(LOCK_EX LOCK_SH LOCK_NB F_SETFD);
  use Time::HiRes qw(time);
  sub ticket_path { "$_[0]/$_[1].turn-ticket" }
  sub arrival {
    open my $fh, "<", $_[0] or return;
    my $line = <$fh>;
    return defined $line && $line =~ /^(\d+\.\d+)$/ ? $1 : undef;
  }
  # Shared, so concurrent probes of a dead ticket never read each other as its gate.
  sub live {
    open my $fh, ">>", $_[0] or return 0;
    return !flock($fh, LOCK_SH | LOCK_NB);
  }
  # Live tickets ahead of the one <slot> holds; undef when it holds none.
  sub ahead {
    my ($root, $slot) = @_;
    my $mine = arrival(ticket_path($root, $slot));
    return unless defined $mine;
    opendir my $dir, $root or die "Pool lock root $root: $!\n";
    my $ahead = 0;
    for my $name (readdir $dir) {
      next unless $name =~ /^(.+)\.turn-ticket$/ && $1 ne $slot;
      my $other = $1;
      my $theirs = arrival("$root/$name");
      next unless defined $theirs && ($theirs < $mine || ($theirs == $mine && $other lt $slot));
      $ahead++ if live("$root/$name");
    }
    return $ahead;
  }
'

# Runs <cmd...> holding the merge turn, taken in turn-ticket order behind every live earlier ticket.
# Exits LOCK_BUSY_EXIT once it has watched one holder keep the turn for MERGE_TURN_WAIT_SECONDS.
with_merge_turn() {
  local slot="$1"
  shift
  perl -e "$MERGE_TURN_LINE_PL"'
    my ($turn_path, $holder_path, $root, $slot, $cap, $poll, $busy_exit) = splice @ARGV, 0, 7;
    open my $ticket, ">>", ticket_path($root, $slot) or die "Turn ticket for $slot: $!\n";
    # Blocking: a probe holds this flock for an instant, and must not refuse the gate it probes.
    flock($ticket, LOCK_EX) or die "Turn ticket lock for $slot: $!\n";
    truncate($ticket, 0) or die "Turn ticket for $slot: $!\n";
    syswrite($ticket, sprintf("%.6f\n", time())) or die "Turn ticket for $slot: $!\n";
    open my $turn, ">>", $turn_path or die "Pool lock $turn_path: $!\n";
    my $holder = sub { open my $fh, "<", $holder_path or return ""; local $/; return <$fh> // "" };
    my ($watched, $since) = ($holder->(), time());
    until (!ahead($root, $slot) && flock($turn, LOCK_EX | LOCK_NB)) {
      my $now = $holder->();
      if ($now ne $watched) { ($watched, $since) = ($now, time()) }
      elsif (time() - $since >= $cap) { exit $busy_exit }
      select(undef, undef, undef, $poll);
    }
    # A gate holding the turn has left the line.
    close $ticket;
    fcntl($turn, F_SETFD, 0) or die "Pool lock inheritance: $!\n";
    $ENV{POOL_FLOCK_FDS} = join " ", split(" ", $ENV{POOL_FLOCK_FDS} // ""), fileno($turn);
    exec @ARGV or die "Pool lock exec: $!\n";
  ' "$MERGE_TURN_LOCK" "$MERGE_TURN_HOLDER" "$LOCK_ROOT" "$slot" "$MERGE_TURN_WAIT_SECONDS" "$MERGE_TURN_POLL_SECONDS" \
    "$LOCK_BUSY_EXIT" bash -c 'source "$1"; shift; "$@"' pool-mutation "$SCRIPT_DIR/agent_worktree_pool.sh" "$@"
}

# <slot>'s place in the line, 1 = next to take the turn; empty when it holds no live ticket.
merge_turn_place() {
  perl -e "$MERGE_TURN_LINE_PL"'
    my ($root, $slot) = @ARGV;
    my $ahead = ahead($root, $slot);
    print(($ahead + 1) . "\n") if defined $ahead && live(ticket_path($root, $slot));
  ' "$LOCK_ROOT" "$1"
}

merge_turn_release() {
  # A ( ) subshell can run the inherited EXIT trap; only the gate's own shell ends its turn.
  [[ "$BASHPID" == "$MERGE_JOURNAL_PID" ]] || return 0
  rm -f "$MERGE_TURN_HOLDER"
  if [[ "$1" -eq 0 ]]; then merge_phase_end ok; else merge_phase_end failed; fi
}

cmd_merge() {
  local slot="$1"
  shift || true

  local base_ref="origin/main" remote=0
  while [[ -n "${1:-}" && ${1:-} != "--" ]]; do
    if [[ "$1" == "--remote" ]]; then remote=1; else base_ref="$1"; fi
    shift
  done
  [[ ${1:-} != "--" ]] || shift
  local test_args=("$@")
  if [[ "$remote" -eq 1 && ${#test_args[@]} -gt 0 ]]; then
    echo "merge: --remote takes no test-runner args — the hosted headless suite has one fixed selection." >&2
    return 1
  fi
  run_merge_gate "$slot" "$base_ref" "$remote" 0 "${test_args[@]}"
}

# <authorize> 1 adds land's authorize phase. The journal closes when the calling shell exits.
run_merge_gate() {
  local slot="$1" base_ref="$2" remote="$3" authorize="$4"
  shift 4

  merge_journal_open "$slot" "$base_ref"
  trap 'merge_rc=$?; merge_journal_finish "$merge_rc"' EXIT
  # Opened before the turn is asked for, so a waiting gate shows as one.
  merge_phase_begin turn-wait

  local turn_rc=0 holder
  with_merge_turn "$slot" \
    merge_gate "$MERGE_JOURNAL" "$MERGE_RUN_START" "$MERGE_PHASE_START" "$slot" "$base_ref" "$remote" "$authorize" "$@" || turn_rc=$?
  if [[ "$turn_rc" -eq "$LOCK_BUSY_EXIT" ]]; then
    holder="$(merge_turn_holder)"
    if [[ -n "$holder" ]]; then
      echo "merge: $holder has held the merge turn for ${MERGE_TURN_WAIT_SECONDS}s ($MERGE_TURN_LOCK) — not merging." >&2
      echo "  Follow its gate with 'merge-progress $holder', then re-run 'merge $slot'." >&2
    else
      echo "merge: the merge turn has been held for ${MERGE_TURN_WAIT_SECONDS}s ($MERGE_TURN_LOCK), and not by a merge gate — not merging." >&2
      echo "  A 'lock merge-turn' caller (a docs-only landing) or a child of a killed gate holds it; re-run 'merge $slot' once it is gone." >&2
    fi
    merge_journal_note "turn held for ${MERGE_TURN_WAIT_SECONDS}s by ${holder:-a holder that is not a merge gate}"
    echo "GATE=refused:turn-held"
    return "$turn_rc"
  fi
  # merge_gate's shell closes turn-wait and every later phase, so this shell must not.
  MERGE_PHASE=""
  return "$turn_rc"
}

# A refusal site names its reason here; any other refusal is named by the phase it died in.
GATE_REASON=""

gate_trailer() {
  [[ "$BASHPID" == "$MERGE_JOURNAL_PID" ]] || return 0
  if [[ "$1" -eq 0 ]]; then echo "GATE=merged"; else echo "GATE=refused:${GATE_REASON:-${MERGE_PHASE:-preflight}}"; fi
}

# Runs holding the merge turn, in the shell with_merge_turn starts for it.
merge_gate() {
  merge_journal_adopt "$1" "$2" turn-wait "$3"
  local slot="$4" base_ref="$5" remote="$6" authorize="$7"
  shift 7
  local test_args=("$@")

  # Fires on every exit path, including a set -e abort, so no failure leaves the
  # journal with a phase open forever.
  trap 'merge_rc=$?; stop_script_suite; gate_trailer "$merge_rc"; merge_turn_release "$merge_rc"' EXIT
  merge_turn_publish "$slot"
  merge_phase_begin preflight

  require_gh || return 1

  local path task_branch base_branch
  path="$(slot_path "$slot")"
  task_branch="$(task_branch_for "$slot")"
  base_branch="${base_ref#origin/}"
  if [[ -z "$task_branch" ]]; then
    echo "merge: no task branch known for $slot (no lease/task_branch)." >&2
    return 1
  fi

  local pr
  pr="$(pr_number_for_pushed_head "$task_branch" "$base_branch")"
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
      GATE_REASON=conflict
      echo "merge: conflict merging $base_ref into $slot — resolve in the worktree," >&2
      echo "  'revise' to test+push, then re-run merge." >&2
      merge_journal_note "conflict merging $base_ref"
      return 1
    fi
  else
    merge_journal_note "already current with $base_ref"
  fi

  if [[ "$authorize" -eq 1 ]]; then
    merge_phase_begin authorize
    gate_authorize "$path" "$slot" "$pr" "$base_ref" || return 1
  fi

  # Skip the re-test only on provenance-corroborated FULL-suite proof for this exact tree: scoped runs never count, and ancestry alone is not evidence — a base-merge commit survives a failed test run, and a retry must re-test it.
  merge_phase_begin proof-check
  local current_tree proof_tree landing_sha delta="none"
  current_tree="$(git -C "$path" rev-parse "$slot^{tree}")"
  landing_sha="$(git -C "$path" rev-parse "$slot")"
  proof_tree="$(verified_proof_tree "$slot")"
  if [[ -n "$proof_tree" && "$proof_tree" == "$current_tree" ]]; then
    delta="proven"
  elif [[ -n "$proof_tree" ]]; then
    delta="$(classify_diff_since_proof "$path" "$proof_tree" "$current_tree")"
  fi

  # A PR can edit the workflow that proves it (a push runs the branch's copy), so such a landing needs the local run.
  local github_diff_rc=0 remote_reason=""
  landing_diff_touches "$path" "$base_ref" "$slot" .github || github_diff_rc=$?
  [[ "$github_diff_rc" -ne 2 ]] || return 1
  if [[ "$github_diff_rc" -eq 0 ]]; then
    remote_reason="no remote proof: the landing diff touches .github/, so this merge needs the local run"
  fi
  local scripts_diff_rc=0
  landing_diff_touches "$path" "$base_ref" "$slot" scripts || scripts_diff_rc=$?
  [[ "$scripts_diff_rc" -ne 2 ]] || return 1
  # Needs only the landing tree, so it overlaps the test run, hosted wait and ratchet.
  # Depth is bounded: the suite runs the SLOT's scripts/tests, never this script's own tree.
  if [[ "$scripts_diff_rc" -eq 0 ]]; then
    echo "Landing diff touches scripts/ — the script suite runs alongside the rest of the gate."
    merge_journal_note "script suite started alongside the gate"
    start_script_suite "$path" "$base_ref" "$slot"
  fi

  case "$delta" in
    proven)
      echo "Tree $current_tree already passed the full suite — skipping re-run."
      merge_phase_begin tests
      merge_journal_note "skipped - tree already fully proven"
      ;;
    doc)
      echo "Markdown-only delta since fully-tested tree $proof_tree — extending proof without a run."
      merge_phase_begin tests
      merge_journal_note "skipped - markdown-only delta, proof extended"
      extend_proof "$slot" "$current_tree" "inherit-doc" "$proof_tree"
      ;;
    *)
      if [[ -n "$remote_reason" ]]; then
        :
      elif [[ "$(git -C "$path" ls-remote origin "refs/heads/$task_branch" | cut -f1)" != "$landing_sha" ]]; then
        remote_reason="no remote proof: landing commit $landing_sha is not on GitHub yet"
      elif remote_reason="$(accept_remote_proof "$slot" "$landing_sha" "$current_tree")"; then
        echo "Landing commit $landing_sha carries a green $REMOTE_PROOF_CONTEXT status for tree $current_tree — remote proof, skipping the run."
        merge_phase_begin tests
        merge_journal_note "skipped - remote proof on the landing commit"
        delta="proven"
      fi
      ;;
  esac
  if [[ "$delta" != "proven" && "$delta" != "doc" ]]; then
    echo "$remote_reason."
    # A producer the user named (--remote, or runner args = the local runner) always beats the verdict.
    if [[ "$remote" -eq 0 && ${#test_args[@]} -eq 0 ]]; then
      local admission
      if ! admission="$(boot_admission_status)"; then
        echo "merge: the Unity access coordinator gave no memory admission verdict (its output is above) — not merging." >&2
        echo "  Fix the coordinator, or name the producer: 'merge $slot --remote' (hosted run) or 'merge $slot -- <runner args>' (local run)." >&2
        return 1
      fi
      case "$admission" in
        boot_admitted)
          merge_journal_note "memory admission boot_admitted - local run" ;;
        boot_not_admitted)
          if [[ "$github_diff_rc" -eq 0 ]]; then
            echo "merge: not merging — memory admission would refuse a batch Unity boot (boot_not_admitted), and the landing diff touches .github/, so remote proof is barred." >&2
            echo "  With the user's approval of that boot: 'merge $slot -- -AllowLowMemory'. It covers the TEST boot only, not the ReSharper solution-sync boot." >&2
            return 1
          fi
          echo "Memory admission would refuse a batch Unity boot (boot_not_admitted) — the test run goes to the hosted headless suite."
          merge_journal_note "memory admission boot_not_admitted - hosted run"
          remote=1 ;;
        *)
          echo "merge: memory admission status '$admission' is not one the gate knows — not merging." >&2
          echo "  Name the producer: 'merge $slot --remote' (hosted run) or 'merge $slot -- <runner args>' (local run)." >&2
          return 1 ;;
      esac
    fi
    if [[ "$remote" -eq 1 && "$github_diff_rc" -eq 0 ]]; then
      GATE_REASON=github
      echo "merge: --remote refused — the landing diff touches .github/, so this merge needs the local run." >&2
      return 1
    elif [[ "$remote" -eq 1 ]]; then
      # A comment-only delta's usual refresh is a local smoke boot, so on the hosted path it is a code delta.
      echo "Running the hosted headless suite on landing commit $landing_sha before merge."
      merge_phase_begin remote-proof
      merge_journal_note "hosted headless suite on $landing_sha"
      run_remote_for_proof "$slot" "$path" "$landing_sha" "$task_branch"
      remote_reason="$(accept_remote_proof "$slot" "$landing_sha" "$current_tree")" || {
        echo "merge: $remote_reason; not merging." >&2
        return 1
      }
    elif [[ "$delta" == "comment" ]]; then
      echo "C# comment/whitespace-only delta since fully-tested tree $proof_tree — compile-level smoke refresh."
      merge_phase_begin tests
      merge_journal_note "comment-only delta - EditMode smoke refresh"
      clear_run_summary "$path"
      cmd_run_tests_clean "$slot" -Mode EditMode -ScopeType Smoke
      extend_proof "$slot" "$current_tree" "inherit-smoke" "$proof_tree"
    elif [[ -n "$proof_tree" ]]; then
      echo "Code delta since fully-tested tree $proof_tree — running the full suite before merge."
      merge_phase_begin tests
      merge_journal_note "code delta since proof - full suite"
      run_tests_for_proof "$slot" "$path" "${test_args[@]}"
    else
      echo "No full-suite proof for tree $current_tree — running the full suite before merge."
      merge_phase_begin tests
      merge_journal_note "no proof for landing tree - full suite"
      run_tests_for_proof "$slot" "$path" "${test_args[@]}"
    fi
  fi
  if [[ "$(verified_proof_tree "$slot")" != "$current_tree" ]]; then
    echo "merge: no full-coverage proof for landing tree $current_tree (scoped gate args?); not merging." >&2
    return 1
  fi
  merge_phase_begin resharper
  local ratchet_reason="${remote_reason:-no remote proof: landing commit $landing_sha is not on GitHub yet}" hosted_ratchet=0
  if resharper_proof_matches "$slot" "$path" "$base_ref"; then
    echo "Tree already passed the ReSharper ratchet against $base_ref — skipping re-run."
  elif [[ "$github_diff_rc" -ne 0 && "$(git -C "$path" ls-remote origin "refs/heads/$task_branch" | cut -f1)" == "$landing_sha" ]] \
    && ratchet_reason="$(accept_remote_resharper_proof "$slot" "$path" "$landing_sha" "$base_ref")"; then
    echo "Landing commit $landing_sha carries a green $REMOTE_RESHARPER_CONTEXT status for this tree and base — hosted ratchet accepted, no local ratchet run."
    merge_journal_note "hosted ratchet accepted on the landing commit"
    hosted_ratchet=1
  elif [[ "$remote" -eq 1 && "$base_ref" == origin/main && "$ratchet_reason" == *" stamps baseTree "* ]]; then
    # The hosted ratchet stamps the main it fetched; another baseTree means main moved since.
    GATE_REASON=base-moved
    echo "merge: $ratchet_reason; base moved during the merge gate — re-run 'merge $slot --remote'." >&2
    return 1
  elif [[ "$remote" -eq 1 ]]; then
    echo "merge: $ratchet_reason; the hosted path runs no local ReSharper ratchet — not merging." >&2
    echo "  'gh workflow run $REMOTE_PROOF_WORKFLOW --ref $task_branch' re-posts both verdicts; then re-run 'merge $slot --remote'." >&2
    return 1
  else
    echo "$ratchet_reason."
    cmd_run_resharper "$slot" "$base_ref"
  fi

  if [[ "$scripts_diff_rc" -eq 0 ]]; then
    merge_phase_begin script-tests
    merge_journal_note "joining the script suite started alongside the gate"
    join_script_suite
  fi

  if [[ "$authorize" -eq 1 ]]; then
    gate_class "$slot" "$scripts_diff_rc" "$github_diff_rc" "$hosted_ratchet" || return 1
  fi

  merge_phase_begin push
  # Unconditional: gh merges the REMOTE branch, so any local-only commits must be on it before the squash.
  git -C "$path" push origin "$slot:refs/heads/$task_branch"

  # The gate's own minutes (more under --remote) are a window for base to move under a proven tree.
  merge_phase_begin base-recheck
  git -C "$path" fetch origin "$base_branch"
  if ! git -C "$path" merge-base --is-ancestor "$base_ref" "$slot"; then
    GATE_REASON=base-moved
    echo "merge: base moved during the merge gate — re-run 'merge $slot'." >&2
    return 1
  fi

  # GitHub recomputes mergeability asynchronously after the gate's push; a merge call inside that window fails "not mergeable" — brief retries ride it out.
  merge_phase_begin gh-merge
  local attempt merged=0
  for attempt in 1 2 3 4 5; do
    if gh pr merge "$pr" --squash --delete-branch=false --match-head-commit "$landing_sha"; then
      merged=1
      break
    fi
    echo "merge: PR #$pr not mergeable yet (attempt $attempt/5) — retrying in 3s..."
    merge_journal_note "not mergeable yet, attempt $attempt/5"
    sleep 3
  done
  if [[ "$merged" -ne 1 ]]; then
    GATE_REASON=gh
    echo "merge: gh pr merge failed for PR #$pr after 5 attempts." >&2
    return 1
  fi
  echo ""
  echo "PR #$pr squash-merged. Next: finalize the slot and sync local main:"
  echo "  ./scripts/agent_worktree_pool.sh finalize $slot $base_ref"
}

# ---- Borrow / return -------------------------------------------------------
slot_leased_to() { slot_table | awk -F'\037' -v lease="$1" '$2 == lease { print $1; exit }'; }

BORROWED_SLOT=""

# Reuse presumes the lease's last holder is dead; callers guard that (Unity owner, .merge flock).
borrow_slot() {
  local lease="$1" slot path
  BORROWED_SLOT="$(slot_leased_to "$lease")"
  if [[ -n "$BORROWED_SLOT" ]]; then
    echo "borrow: reusing $BORROWED_SLOT, which already holds lease $lease." >&2
  else
    while IFS=$'\t' read -r slot path; do
      try_lock_slot "$slot" "$lease" "$path" >/dev/null 2>&1 || continue
      if slot_is_clobber_safe "$path"; then
        BORROWED_SLOT="$slot"
        break
      fi
      echo "borrow: skipping $slot — it is free but holds unpushed work." >&2
      with_slot_mutation "$slot" release_slot "$slot" "$lease" >/dev/null || true
    done < <(slots_tsv)
    [[ -n "$BORROWED_SLOT" ]] || { echo "borrow: no free slot for $lease (a stale one is never reclaimed)." >&2; return 1; }
  fi
  mark_slot_used "$BORROWED_SLOT"
}

# Asked through the coordinator's client, as boot_admission_status asks for admission (script-contracts.md sec.3).
OWNER_READER='
. (Join-Path $env:POOL_SCRIPT_DIR "unity_access_client.ps1")
$call = Invoke-UnityAccessCoordinator -CoordinatorArgs @("-Action", "Status", "-ProjectPath", $env:POOL_PROJECT_PATH)
if ($call.stderr) { [Console]::Error.WriteLine($call.stderr) }
if ($call.exitCode -ne 0 -or $null -eq $call.result) { [Console]::Error.WriteLine("Status exit=" + $call.exitCode + " stdout=" + $call.stdout); exit 1 }
$owner = $call.result.projectOwner
if ($null -eq $owner) { Write-Output "none" } else { Write-Output ("owner " + $owner.lease) }
'

# Prints the lease of the live Unity owner on <path>'s project, or nothing; non-zero = the coordinator could not be asked.
project_owner() {
  local out
  out="$(POOL_SCRIPT_DIR="$SCRIPT_DIR" POOL_PROJECT_PATH="$1/src/Asteroids3D" \
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$OWNER_READER")" || return 1
  out="${out//$'\r'/}"
  case "$out" in
    none) ;;
    "owner "*) printf '%s\n' "${out#owner }" ;;
    *) return 1 ;;
  esac
}

# A pass between boots owns no project, so only the one-pass-at-a-time rule covers that gap.
cmd_borrow() {
  local lease="${1:-}" slot owner
  [[ $# -eq 1 && "$lease" =~ ^[A-Za-z0-9._-]+$ ]] || { echo "borrow requires <lease>" >&2; return 1; }
  slot="$(slot_leased_to "$lease")"
  if [[ -n "$slot" ]]; then
    if ! owner="$(project_owner "$(slot_path "$slot")")"; then
      echo "borrow: could not ask the access coordinator who owns $slot's project; nothing was taken." >&2
      return 1
    fi
    if [[ -n "$owner" ]]; then
      echo "borrow: $slot holds lease $lease and Unity is live on its project (owner $owner); a pass may still be running there." >&2
      return 1
    fi
  fi
  borrow_slot "$lease" || return 1
  echo "SLOT=$BORROWED_SLOT PATH=$(slot_path "$BORROWED_SLOT")"
}

# The lease check comes first: return runs unattended, so a wrong slot must stay untouched.
return_slot() {
  local slot="$1" lease="$2" held
  held="$(lease_for "$slot")"
  if [[ ! -d "$(lock_dir_for "$slot")" || "$held" != "$lease" ]]; then
    echo "return: $slot holds lease '${held:-none}', not '$lease'; nothing was reset." >&2
    return 1
  fi
  # A child process keeps prepare's errexit: a failed reset must not release the slot.
  bash "$SCRIPT_DIR/agent_worktree_pool.sh" prepare "$slot" origin/main --force \
    && with_slot_mutation "$slot" release_slot "$slot" "$lease"
}

# ---- Land ------------------------------------------------------------------
# Off until the switch: the class is reported, and every land needs a covering instruction.
AUTO_MERGE_CLASS="${WORKTREE_POOL_AUTO_MERGE_CLASS:-0}"
LAND_ATTEMPTS=3
# The refusals a fresh merge gate attempt can clear.
LAND_TRANSIENT=" base-moved no-verdict hosted-error gh "

trailer_value() { record_field <(printf '%s\n' "$2") "$1"; }

# <commit> covers <tree>: merged with <base_ref>, it gives <tree> or an inert delta from it.
commit_covers() {
  local path="$1" commit="$2" base_ref="$3" tree="$4" merged
  merged="$(git -C "$path" merge-tree --write-tree "$commit" "$base_ref" 2>/dev/null)" || return 1
  merged="${merged%%$'\n'*}"
  [[ "$merged" != "$tree" ]] || return 0
  case "$(classify_diff_since_proof "$path" "$merged" "$tree")" in
    doc|comment) return 0 ;;
    *) return 1 ;;
  esac
}

# The authorize phase. Its land-facts read also keeps what gate_class needs.
CLASS_OWED=""
CLASS_REVIEW=0
AUTHORIZED_BY=""

gate_authorize() {
  local path="$1" slot="$2" pr="$3" base_ref="$4" facts tree instruction review
  if ! facts="$(GITHUB_REPOSITORY="$(repo_slug)" bash "$SCRIPT_DIR/drain_pick.sh" land-facts "$pr")"; then
    GATE_REASON=gh
    echo "merge: could not read PR #$pr's facts ('drain_pick.sh land-facts $pr') — not merging." >&2
    return 1
  fi
  tree="$(git -C "$path" rev-parse "$slot^{tree}")"
  instruction="$(trailer_value INSTRUCTION "$facts")"
  review="$(trailer_value REVIEW "$facts")"
  CLASS_OWED="$(trailer_value OWED "$facts")"
  CLASS_REVIEW=0
  if [[ "$review" == "completed "* && "$(trailer_value UNRESOLVED "$facts")" == 0 ]] \
    && commit_covers "$path" "${review#completed }" "$base_ref" "$tree"; then
    CLASS_REVIEW=1
  fi
  if [[ "$instruction" =~ ^[0-9a-f]{40}$ ]] && commit_covers "$path" "$instruction" "$base_ref" "$tree"; then
    echo "The recorded instruction for ${instruction:0:7} covers landing tree $tree."
    merge_journal_note "instruction ${instruction:0:7} covers the landing tree"
    AUTHORIZED_BY=instruction
    return 0
  fi
  if [[ "$AUTO_MERGE_CLASS" -eq 1 ]]; then
    echo "No recorded instruction covers landing tree $tree — the auto-merge class decides once the proofs are in."
    merge_journal_note "no covering instruction - the class decides"
    AUTHORIZED_BY=class
    return 0
  fi
  GATE_REASON=unauthorized
  echo "merge: no recorded instruction covers landing tree $tree (instruction: ${instruction:-unread}) — not merging." >&2
  echo "  Show the user the PR at its head; on their word, 'drain_pick.sh instruct $pr@<head>', then re-run 'land $pr'." >&2
  return 1
}

# Conditions in a fixed order, so CLASS=out names the first that fails.
gate_class() {
  local slot="$1" scripts_rc="$2" github_rc="$3" hosted_ratchet="$4" class=in kind
  kind="$(tested_scope_field "$slot" kind || true)"
  if [[ "$CLASS_OWED" != none ]]; then
    class=out:owed
  elif [[ "$scripts_rc" -eq 0 || "$github_rc" -eq 0 ]]; then
    class=out:paths
  elif [[ "$kind" != remote-run || "$hosted_ratchet" -ne 1 ]]; then
    class=out:hosted
  elif [[ "$CLASS_REVIEW" -ne 1 ]]; then
    class=out:review
  fi
  echo "CLASS=$class"
  merge_journal_note "class $class"
  [[ "$AUTHORIZED_BY" == class && "$class" != in ]] || return 0
  GATE_REASON=unauthorized
  echo "merge: no recorded instruction covers the landing tree, and it is outside the auto-merge class ($class) — not merging." >&2
  return 1
}

LAND_REASON=""
LAND_BRANCH=""
LAND_SLOT=""

land_refuse() {
  LAND_REASON="$1"
  echo "land: refused ($1) — $2" >&2
}

# One line per slot record: slot, lease, task branch, split by US.
slot_table() {
  collect_slot_records | awk '
    /^slot=/ { slot = substr($0, 6); lease = branch = "" }
    /^lease=/ { lease = substr($0, 7) }
    /^task_branch=/ { branch = substr($0, 13) }
    /^$/ && slot != "" { print slot "\037" lease "\037" branch; slot = "" }'
}

# Checks needing no slot. Sets LAND_BRANCH.
land_preflight() {
  local pr="$1" view state base head draft cross facts owed instruction unresolved after slot lease branch
  LAND_REASON="" LAND_BRANCH="" LAND_SLOT=""
  if ! view="$(gh pr view "$pr" --json state,baseRefName,headRefName,isDraft,isCrossRepository \
    --jq '[.state, .baseRefName, .headRefName, .isDraft, .isCrossRepository] | @tsv')"; then
    land_refuse gh "could not read PR #$pr from GitHub"
    return 1
  fi
  IFS=$'\t' read -r state base head draft cross <<< "$view"
  [[ "$state" == OPEN ]] || { land_refuse not-open "PR #$pr is $state"; return 1; }
  [[ "$base" == main ]] || { land_refuse base "PR #$pr is against $base, not main"; return 1; }
  [[ "$draft" == false ]] || { land_refuse draft "PR #$pr is a draft"; return 1; }
  if [[ "$head" != task/* || "$cross" != false ]]; then
    land_refuse head-branch "PR #$pr's head $head is not a task/* branch of this repository"
    return 1
  fi
  if ! facts="$(GITHUB_REPOSITORY="$(repo_slug)" bash "$SCRIPT_DIR/drain_pick.sh" land-facts "$pr")"; then
    land_refuse gh "'drain_pick.sh land-facts $pr' failed"
    return 1
  fi
  owed="$(trailer_value OWED "$facts")"
  instruction="$(trailer_value INSTRUCTION "$facts")"
  unresolved="$(trailer_value UNRESOLVED "$facts")"
  after="$(trailer_value AFTER "$facts")"
  case "$owed" in
    absent|none|discharged) ;;
    open|malformed) land_refuse "owed-$owed" "PR #$pr's owed-local checklist is $owed"; return 1 ;;
    *) land_refuse facts "land-facts gave no owed verdict for PR #$pr"; return 1 ;;
  esac
  if [[ "$instruction" == none && "$AUTO_MERGE_CLASS" -eq 0 ]]; then
    land_refuse no-instruction "PR #$pr has no recorded instruction ('drain_pick.sh instruct $pr@<sha>' records the user's)"
    return 1
  elif [[ "$instruction" != none && ! "$instruction" =~ ^[0-9a-f]{40}$ ]]; then
    land_refuse facts "land-facts gave no instruction verdict for PR #$pr"
    return 1
  fi
  [[ "$unresolved" =~ ^[0-9]+$ ]] || { land_refuse facts "land-facts gave no thread count for PR #$pr"; return 1; }
  [[ "$unresolved" -eq 0 ]] || { land_refuse unresolved "PR #$pr has $unresolved unresolved review thread(s)"; return 1; }
  case "$after" in
    none) ;;
    malformed) land_refuse merge-order-malformed "PR #$pr's '## Merge order' section is malformed"; return 1 ;;
    *[!0-9,]*|'') land_refuse facts "land-facts gave no merge order for PR #$pr"; return 1 ;;
    *) land_refuse "after:${after%%,*}" "PR #$pr lands after #${after//,/, #}, still open"; return 1 ;;
  esac
  while IFS=$'\x1f' read -r slot lease branch; do
    if [[ "$lease" != "land-$pr" && "$branch" == "$head" ]]; then
      land_refuse "slot:$slot" "$slot holds $head (lease ${lease:-unknown}); its session merges it with 'merge $slot'"
      return 1
    fi
  done < <(slot_table)
  LAND_BRANCH="$head"
}

# A dead run's land-<pr> slot is taken over unasked: a live run holds its .merge lock, which the gate takes.
land_borrow() {
  local pr="$1"
  borrow_slot "land-$pr" || { land_refuse no-free-slot "no free slot (land never reclaims a stale one)"; return 1; }
  LAND_SLOT="$BORROWED_SLOT"
  printf '%s\n' "$LAND_BRANCH" > "$(lock_dir_for "$LAND_SLOT")/task_branch"
  echo "land: PR #$pr on $LAND_SLOT ($LAND_BRANCH)." >&2
}

# Runs under the slot's .merge lock. Proof records are dropped so every attempt proves on the hosted path.
land_gate() {
  local slot="$1" branch="$2" path ldir
  path="$(slot_path "$slot")"
  ldir="$(lock_dir_for "$slot")"
  rm -f "$ldir/tested_tree" "$ldir/tested_scope" "$ldir/resharper_proof"
  if ! { git -C "$path" fetch -q origin "refs/heads/$branch" && git -C "$path" checkout -q "$slot" \
    && git -C "$path" reset -q --hard FETCH_HEAD && git -C "$path" clean -fdq; }; then
    echo "land: could not check out $branch in $slot." >&2
    echo "GATE=refused:checkout"
    return 1
  fi
  run_merge_gate "$slot" origin/main 1 1
}

# Waits for the new run's own status: a retry before it would read the errored one.
land_redispatch() {
  local branch="$1" sha before now deadline
  sha="$(git -C "$ROOT" ls-remote origin "refs/heads/$branch" | cut -f1)"
  [[ -n "$sha" ]] && before="$(remote_status "$sha" "$REMOTE_PROOF_CONTEXT")" || return 1
  echo "land: re-dispatching the hosted headless suite on $branch." >&2
  gh workflow run "$REMOTE_PROOF_WORKFLOW" --ref "$branch" >&2 || return 1
  deadline=$(( SECONDS + REMOTE_NO_RUN_SECONDS + REMOTE_QUEUED_SECONDS ))
  while now="$(remote_status "$sha" "$REMOTE_PROOF_CONTEXT")"; do
    [[ "$now" == "$before" ]] || return 0
    if (( SECONDS >= deadline )); then
      echo "land: the re-dispatched run posted no status on $sha in $(( REMOTE_NO_RUN_SECONDS + REMOTE_QUEUED_SECONDS ))s." >&2
      return 1
    fi
    sleep "$REMOTE_POLL_SECONDS"
  done
  return 1
}

cmd_land() {
  local pr="${1:-}"
  [[ $# -eq 1 && "$pr" =~ ^[1-9][0-9]*$ ]] || { echo "land requires <pr>" >&2; return 1; }
  require_gh || return 1
  local attempt out rc reason="" class="" slot=""
  out="$(mktemp)"
  for (( attempt = 1; attempt <= LAND_ATTEMPTS; attempt++ )); do
    class=""
    if land_preflight "$pr" && land_borrow "$pr"; then
      slot="$LAND_SLOT"
      # The gate's trailers are land's to print, once, after the last attempt.
      { with_flock "$LOCK_ROOT/$slot.merge" 0 \
          "land: a merge gate is already running on $slot — follow it with 'merge-progress $slot'." \
          land_gate "$slot" "$LAND_BRANCH" && rc=0 || rc=$?
        echo "$rc" > "$out.rc"; } | tee "$out" | grep --line-buffered -v -E '^(GATE|CLASS)=' || true
      rc="$(cat "$out.rc")"
      reason="$(record_field "$out" GATE)"
      class="$(record_field "$out" CLASS)"
      if [[ "$reason" == merged ]]; then
        with_flock "$LOCK_ROOT/$slot.merge" 0 "" cmd_finalize "$slot" origin/main >&2 \
          || echo "land: PR #$pr merged, but $slot was not finalized — run 'finalize $slot origin/main'." >&2
        rm -f "$out" "$out.rc"
        [[ -z "$class" ]] || echo "CLASS=$class"
        echo "GATE=merged"
        return 0
      fi
      if [[ -n "$reason" ]]; then
        reason="${reason#refused:}"
      elif [[ "$rc" -eq "$LOCK_BUSY_EXIT" ]]; then
        reason=gate-running
      else
        reason=error
      fi
    else
      reason="$LAND_REASON"
    fi
    [[ "$LAND_TRANSIENT" == *" $reason "* && "$attempt" -lt "$LAND_ATTEMPTS" ]] || break
    echo "land: attempt $attempt/$LAND_ATTEMPTS refused ($reason) — retrying." >&2
    case "$reason" in
      hosted-error) land_redispatch "$LAND_BRANCH" || break ;;
      gh) sleep "$REMOTE_POLL_SECONDS" ;;
    esac
  done
  rm -f "$out" "$out.rc"
  # A gate-running slot is another land's; touching it would pull its tree out from under that gate.
  if [[ -n "$slot" && "$reason" != gate-running ]]; then
    with_flock "$LOCK_ROOT/$slot.merge" 0 "" return_slot "$slot" "land-$pr" >&2 \
      || echo "land: $slot was not reset and released; it keeps lease land-$pr." >&2
  fi
  [[ -z "$class" ]] || echo "CLASS=$class"
  echo "GATE=refused:$reason"
  return 1
}

# ---- Docs-only landing -----------------------------------------------------
cmd_land_docs() {
  [[ $# -eq 0 ]] || { echo "land-docs takes no arguments: run it from the worktree whose HEAD lands." >&2; return 1; }
  local path rc=0
  path="$(git rev-parse --show-toplevel)"
  with_flock "$MERGE_TURN_LOCK" "$MERGE_TURN_WAIT_SECONDS" \
    "land-docs: the merge turn is still held after ${MERGE_TURN_WAIT_SECONDS}s ($MERGE_TURN_LOCK) — not landing." \
    land_docs_push "$path" || rc=$?
  [[ "$rc" -ne "$LOCK_BUSY_EXIT" ]] || echo "LAND_DOCS=refused:turn-held"
  return "$rc"
}

# Runs under the merge turn, so the push waits for a gate in flight instead of moving base under it.
land_docs_push() {
  local path="$1" head non_docs
  # Git writes progress and conflict reports to stdout; stdout carries only the trailer.
  git -C "$path" fetch -q origin main >&2
  if ! git -C "$path" rebase -q origin/main >&2; then
    if git -C "$path" rev-parse -q --verify REBASE_HEAD >/dev/null; then git -C "$path" rebase --abort >&2; fi
    echo "LAND_DOCS=refused:rebase"
    return 1
  fi
  head="$(git -C "$path" rev-parse HEAD)"
  if [[ "$head" == "$(git -C "$path" rev-parse origin/main)" ]]; then
    echo "land-docs: HEAD changes nothing on origin/main." >&2
    echo "LAND_DOCS=refused:empty"
    return 1
  fi
  non_docs="$(git -C "$path" diff --no-renames --name-only origin/main HEAD -- ':(top,exclude)doc/' ':(top,exclude)*.md')"
  if [[ -n "$non_docs" ]]; then
    echo "land-docs: these paths are outside doc/ and *.md, so the change takes a PR:" >&2
    sed 's/^/  /' <<< "$non_docs" >&2
    echo "LAND_DOCS=refused:paths"
    return 1
  fi
  if ! git -C "$path" push -q origin HEAD:main >&2; then
    echo "LAND_DOCS=refused:push"
    return 1
  fi
  echo "LAND_DOCS=landed $head"
}

# ---- Finalize / review / revise --------------------------------------------
cmd_finalize() {
  local slot="$1"
  local base_ref="${2:-origin/main}"
  local path task_branch evidence state pr_head merge_commit remote_head
  path="$(slot_path "$slot")" || return 1
  task_branch="$(task_branch_for "$slot")"
  require_gh || return 1
  [[ -n "$task_branch" ]] || { echo "finalize: no task branch for $slot; preserve the slot and resolve its lease." >&2; return 1; }
  require_clean_slot "$slot" "$path" finalize || return 1

  evidence="$(gh pr list --head "$task_branch" --base "${base_ref#origin/}" --state all --limit 1 \
    --json state,headRefOid,mergeCommit --jq '.[0] | [.state, .headRefOid, .mergeCommit.oid] | @tsv')" || return 1
  IFS=$'\t' read -r state pr_head merge_commit <<< "$evidence"
  if [[ "$state" != MERGED || ! "$pr_head" =~ ^[0-9a-f]{40}$ || ! "$merge_commit" =~ ^[0-9a-f]{40}$ ]]; then
    echo "finalize: merged PR evidence unavailable for $task_branch; preserve the slot and verify the PR." >&2
    return 1
  fi
  if [[ "$(git -C "$path" rev-parse HEAD)" != "$pr_head" ]]; then
    echo "finalize: $slot HEAD differs from the merged PR head; preserve its additional work first." >&2
    return 1
  fi
  git -C "$path" fetch origin || return 1
  if ! git -C "$path" merge-base --is-ancestor "$merge_commit" "$base_ref"; then
    echo "finalize: $base_ref does not contain the PR merge commit; preserve the slot and verify the base." >&2
    return 1
  fi
  remote_head="$(git -C "$path" ls-remote --exit-code origin "refs/heads/$task_branch")" || {
    [[ $? == 2 ]] || return 1
    remote_head=""
  }
  remote_head="${remote_head%%$'\t'*}"
  if [[ -n "$remote_head" && "$remote_head" != "$pr_head" ]]; then
    echo "finalize: $task_branch changed after the PR merged; preserve its additional work first." >&2
    return 1
  fi
  if [[ -n "$remote_head" ]]; then
    git -C "$path" push --force-with-lease="refs/heads/$task_branch:$pr_head" origin --delete "$task_branch" || return 1
  fi
  cmd_prepare "$slot" "$base_ref" --force
  cmd_release "$slot"
  echo "Finalized $slot: reset to $base_ref and released lock."
}

cmd_review_comments() {
  local slot="$1"
  local base="${2:-main}"

  require_gh || return 1

  local head_branch
  head_branch="$(task_branch_for "$slot")"
  [[ -n "$head_branch" ]] || head_branch="$slot"

  local pr
  pr="$(pr_number_for_pushed_head "$head_branch" "$base")"
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
    run_tests_for_proof "$slot" "$path" "${test_args[@]}"
  fi
  cmd_run_resharper "$slot" origin/main

  git -C "$path" push origin "$slot:refs/heads/$task_branch"
  echo "Revised and pushed $slot -> $task_branch"
}

# ---- Dispatch --------------------------------------------------------------
require_slot_arg() {
  local usage_line="$1" argc="$2"
  [[ "$argc" -ge 1 ]] || { echo "$usage_line" >&2; exit 1; }
}

main() {
  if [[ $# -lt 1 ]]; then
    usage
    exit 1
  fi

  local cmd="$1" path
  shift || true

  # Reads (status, merge-progress) never count as use: the dashboard runs both on every slot.
  case "$cmd" in
    prepare|run-tests|run-resharper|run-script-tests|create-pr|submit|revise|review-comments|merge)
      if [[ $# -ge 1 ]]; then mark_slot_used "$1"; fi
      ;;
  esac

  case "$cmd" in
    status)
      if [[ "${1:-}" == "--porcelain" ]]; then collect_slot_records; collect_held_records; else cmd_status; fi
      ;;
    acquire) cmd_acquire "$@" ;;
    release) require_slot_arg "release requires <slot>" "$#"; cmd_release "$1" ;;
    prepare) require_slot_arg "prepare requires <slot> [base_ref]" "$#"; cmd_prepare "$@" ;;
    run-tests) require_slot_arg "run-tests requires <slot> [args...]" "$#"; cmd_run_tests "$@" ;;
    run-resharper) require_slot_arg "run-resharper requires <slot> [base_ref]" "$#"; cmd_run_resharper "$@" ;;
    run-script-tests)
      require_slot_arg "run-script-tests requires <slot>" "$#"
      path="$(slot_path "$1")" || { echo "run-script-tests: unknown slot '$1'" >&2; exit 1; }
      cmd_run_script_tests "$path"
      ;;
    create-pr) require_slot_arg "create-pr requires <slot> [base] --title \"<text>\" (--body \"<text>\" | --body-file <path>)" "$#"; cmd_create_pr "$@" ;;
    submit) require_slot_arg "submit requires <slot> [base_ref] --title \"<text>\" (--body \"<text>\" | --body-file <path>) [-- test_args...]" "$#"; cmd_submit "$@" ;;
    merge)
      require_slot_arg "merge requires <slot> [base_ref] [-- test_args...]" "$#"
      # Two gates on one slot race each other's pushes and cancel each other's hosted runs.
      with_flock "$LOCK_ROOT/$1.merge" 0 \
        "merge: a merge gate is already running on $1 — follow it with 'merge-progress $1'. If none is, a child of a killed gate still holds $LOCK_ROOT/$1.merge." \
        cmd_merge "$@"
      ;;
    land) cmd_land "$@" ;;
    borrow) cmd_borrow "$@" ;;
    return)
      [[ $# -eq 2 && "$1" =~ ^[A-Za-z0-9._-]+$ ]] || { echo "return requires <slot> <lease>" >&2; exit 1; }
      with_flock "$LOCK_ROOT/$1.merge" 0 "return: a merge gate is running on $1 — follow it with 'merge-progress $1'." \
        return_slot "$@"
      ;;
    land-docs) cmd_land_docs "$@" ;;
    merge-progress) require_slot_arg "merge-progress requires <slot> [--oneline]" "$#"; cmd_merge_progress "$@" ;;
    finalize) require_slot_arg "finalize requires <slot> [base_ref]" "$#"; cmd_finalize "$@" ;;
    review-comments) require_slot_arg "review-comments requires <slot> [base]" "$#"; cmd_review_comments "$@" ;;
    revise) require_slot_arg "revise requires <slot> [--no-test] [-- test_args...]" "$#"; cmd_revise "$@" ;;
    hold)
      require_slot_arg "hold requires <slot> [--local]" "$#"
      with_flock "$LOCK_ROOT/$1.merge" 0 \
        "hold: a merge gate is running on $1 — follow it with 'merge-progress $1'. If none is, a child of a killed gate still holds $LOCK_ROOT/$1.merge." \
        cmd_hold "$@"
      ;;
    resume) require_slot_arg "resume requires <lease> [slot]" "$#"; cmd_resume "$@" ;;
    lock) cmd_lock "$@" ;;
    -h|--help|help) usage ;;
    *)
      echo "Unknown command: $cmd" >&2
      usage
      exit 1
      ;;
  esac
}

# Executed: dispatch. Sourced (scripts/tests/): define the functions and stop, so a test can call
# one directly - the bash twin of the `$MyInvocation.InvocationName -eq '.'` guard in the PS scripts.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
