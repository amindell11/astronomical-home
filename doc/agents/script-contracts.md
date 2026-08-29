# Script contracts

Review law for `scripts/**`. Read it before changing a script's outputs or calling
one from another script. Provenance: `doc/Feature_Plans/Script_Seam_Hardening.md`.

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
(`agent_worktree_pool.sh run-script-tests`). Tests keep their state inside a temp dir
and inject every root the script would otherwise take from this machine; the
non-hermetic skiplist in `cmd_run_script_tests` is empty and should stay that way.
