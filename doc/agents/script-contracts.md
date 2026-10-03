# Script contracts

Review law for `scripts/**`. Read it before changing a script's outputs or calling
one from another script. Provenance: the 2026-08-27 `scripts/` audit arc (#451, #452-#456);
rulings and do-not-break list in memory `project_script_seam_hardening`.

## 1. Every script is a module with a published interface

A script's **interface** is everything a caller must know, not its parameter list:

- exit codes, and what each one means;
- the machine channel (below) and the exact shape on it;
- the schemas of any state files it writes for others to see;
- timing constants a caller must respect (TTLs, wait defaults, poll intervals).

All of it lives in the script's own comment-based help (PowerShell `<# .SYNOPSIS … #>`)
or header block (bash). Anything a caller had to learn by reading the body is an
interface item that was never published — publish it or stop requiring it.

Reference citizens: `scripts/inert_diff.ps1` (3-value exit contract, single-word
verdict, fail-toward-doubt) and `scripts/unity_access.ps1` (actions x statuses x
exit codes, owned state schemas, the `batch_complete` exit-0 trap named outright).

## 2. One machine channel

- **PowerShell**: exactly one compressed JSON line on stdout, so the whole stdout
  stream parses with `ConvertFrom-Json`. Prose, warnings, child-process output and
  error text go to stderr. A consumer that sniffs lines (`^\s*{`) is compensating
  for a producer that did not honor this - fix the producer.
- **bash**: stable `KEY=value` trailers on stdout, prose to stderr.
- Human-readable default modes are welcome and carry no contract at all.

## 3. Producers stamp verdicts; consumers trust-and-check one field

No script parses another's state files, output layout, filter format, or the process
table. When a consumer needs a question answered, the producer's interface grows to
answer it (generalize the primitive - AGENTS.md dependency rule 6); a parallel
re-derivation beside the owner is the defect, however small.

Corollary: invoking a coordinated tool goes through its sanctioned client when it has
one - `scripts/unity_access_client.ps1` for the Unity access coordinator.

## 4. Enforcement

`scripts/tests/` runs in the merge gate whenever the landing diff touches `scripts/**`
(`agent_worktree_pool.sh run-script-tests <slot>`), under script-suite selection: the gate hands
the runner its landing range, and only the test files that range selects run.

- **Covers line.** Every `scripts/tests/test_*` file carries `# covers: <path-or-glob> …` within
  its first 10 lines: repo-relative paths or bash globs, space-separated. List every non-lib
  script the test runs or loads, directly or through the script under test
  (`test_resharper_ratchet.ps1` lists `scripts/unity_access_client.ps1`); never the file itself.
- **Selection.** A changed path selects each file whose covers line matches it; a changed test
  file selects itself. A rename counts its old and new path. A changed script no covers line
  lists runs nothing; the suite's first line and the `script-selection` journal event name it,
  and a run that selects nothing passes.
- **Every file runs** with no landing range (`run-script-tests <slot>` by hand), when the diff
  touches a shared path (`scripts/lib/**`, or a non-`test_*` path under `scripts/tests/`), or
  when a test file has no covers line.
- **A stale covers entry refuses.** An entry matching no file fails every run, full runs
  included, before any file starts.

Tests keep their state inside a temp dir and inject every root the script would otherwise take
from this machine; the non-hermetic skiplist in `cmd_run_script_tests` is empty and should stay that way.
The gate runs the suite in the slot beside its own test run and ratchet, so a test that writes
into the worktree trips the gate's clean-tree checks.
The `.ps1` files run in a lane beside the `.sh` files, so a test file may run beside any other
and must share no state with another file. Every selected file runs and the suite fails at the end; each file's
output prints as one block in a fixed order, and its trailer and journal event stay per file.
Lanes pair bash with PowerShell only; concurrent bash copies contend on spawn cost (#611).

## 5. Shared primitives live in `scripts/lib/`

Entry requires **two real callers**: the lib was seeded exclusively with already-duplicated
logic (each with >=2 divergent copies). Nothing enters with one caller - one adapter is a
hypothetical seam. A coordinated tool's own front door is a sanctioned client (section 3),
not a shared primitive.

Splitting the monolith scripts (`agent_worktree_pool.sh`, `unity_test_agent.ps1`) into smaller
files is a standing NON-GOAL: depth is a property of the interface, not the implementation, so a
1,500-line module behind a small honest interface is already the goal state. Splits buy
maintainer locality only; they re-earn a place in the backlog via an observed maintenance
failure, as their own hygiene arc.

The one lifted case is `unity_access.ps1` (user, 2026-09-23, #664): its test spawned a process
per call, so the functions live in `unity_access_lib.ps1` and the entry point keeps the whole
published interface. The library adds no interface and is not a second front door. An in-process
caller becomes the lease holder, so only the entry point and its test load it; every other
caller uses the client (section 3).

## 6. PowerShell 5.1 trap

Piping a native command such as `git` into `Select-Object -First 1` can kill it
mid-exit: you get good output alongside `$LASTEXITCODE = -1`. Collect the output
first, then index into it.
