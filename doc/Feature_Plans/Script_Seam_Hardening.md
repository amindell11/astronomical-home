# Script Seam Hardening

> STATUS: living — the arc shipped (#451, phases #452–#456). Kept as the standing design record
> for `scripts/`: the target interaction pattern, the monolith-split non-goal and the do-not-break
> list are the *why* behind `doc/agents/script-contracts.md`, which carries only the review law.

Provenance: 2026-08-27 four-lane full-read audit of `scripts/` (5,141 tool
lines + 1,391 test lines). Findings are cited inline as `file:line` against
commit `cd3aff68`; this doc is self-contained — implementing agents need no
other artifact.

## Diagnosis, in one sentence

Every serious finding is the same defect: a script's **effective interface**
(everything a caller must know — exit codes, output scraping, state-file
schemas, timing constants) is far larger than its **published interface**, so
consumers compensate by re-deriving knowledge the producer owns.

Vocabulary here is the `codebase-design` skill's: module, interface, seam,
adapter, depth. "Interface" always means the full effective contract, never
just the parameter list.

## Target interaction pattern

Four standing rules. Rules 1–2 become reviewable law in
`doc/agents/script-contracts.md` (authored in Phase 2).

1. **Every script is a module with a published interface.** Comment-based
   help enumerating invocation × statuses × exit codes; one machine channel —
   exactly one JSON line on stdout (PowerShell) or stable `KEY=value`
   trailers (bash); prose to stderr. `scripts/inert_diff.ps1` (3-value exit
   contract, single-word verdict, fail-toward-doubt) is the reference
   citizen.
2. **Producers stamp verdicts; consumers trust-and-check one field.** No
   script parses another's state files, output layout, filter format, or
   process table. When a consumer needs a question answered, the producer's
   interface grows to answer it (generalize the primitive — AGENTS.md
   dependency rule 6), never a parallel re-derivation beside it.
3. **Shared primitives live in `scripts/lib/`, entry only at two real
   callers.** Seeded exclusively with already-duplicated logic (each ≥2
   divergent copies): Unity exe resolution, repo root, kill-tree, Unity-churn
   classifier — the four the lib holds. Nothing enters the lib with one caller —
   one adapter is a hypothetical seam. (The coordinator-invoke helper shipped
   instead as the sanctioned client `scripts/unity_access_client.ps1`: it is the
   coordinator's own front door, not a shared primitive.)
4. **The merge gate runs the script tests.** A diff touching `scripts/**`
   triggers `scripts/tests/`; without enforcement the other rules erode
   silently (they already did — the suite currently has zero callers).

## Non-goal: monolith file-splits

Splitting `agent_worktree_pool.sh` / `unity_test_agent.ps1` /
`unity_access.ps1` into smaller files is explicitly **out of this arc**.
Depth is a property of the interface, not the implementation: a 1,500-line
module behind a small honest interface is a deep module — the goal state.
Splits buy maintainer locality only; they re-enter the backlog only via an
observed maintenance failure, as their own hygiene arc.

## Phases - LANDED

Ordered so enforcement preceded safety preceded deepening. Each phase shipped as one PR with
regression cover from Phase 0.

| Phase | What landed | PR |
| --- | --- | --- |
| 0 - enforcement substrate | `scripts/tests/` wired into the pool merge gate (diff-triggered on `scripts/**`); `test_pool_locking.sh`; merge-gate stub honors `-OutDir` with a golden summary | #452 |
| 1 - safety (locking) | single-winner stale reclaim, atomic lock records, loud CIM-failure enumeration, `holderStartTime` PID identity | #474 |
| 2 - coordinator interface law | `doc/agents/script-contracts.md`; sanctioned client `unity_access_client.ps1`; coordinator help block, `record_unreadable` / `coordinator_error`; hermetic `test_unity_access.ps1` (skiplist empty) | #475 |
| 3 - verdict ownership | runner `coverage` stamp; pool `status --porcelain` as the read interface (human status + dashboard are adapters); coordinator answers ownership (`normalizedProjectPath`, `-Action Contract`); structured scope selection | #476 |
| 4 - shared primitives + dedup | `scripts/lib/` seeded (`repo_root`, `unity_editor`, `process_tree`, `unity_churn`); runner run-record/failure-entry constructors; pool `parse_pr_flags` / `push_and_open_pr` / `require_gh`; `pr_number_for_pushed_head`; `create-pool-prs` and `check_test_naming.ps1` deleted | #456 |

## Forks - all RULED

1. **Merge-gate trigger for script tests** (blocks Phase 0): RULED —
   diff-triggered, only when the landing diff touches `scripts/**`
   (~1 min added to those merges); not unconditional.
2. **`check_test_naming.ps1`** (Phase 4): RULED — deleted. It was never run and
   the convention is not load-bearing; TESTING.md now states the naming rules as
   review law instead of claiming script enforcement.
3. **Python coverage twin** (Phase 3): RULED — moot. The runner stamps the
   verdict, so the merge gate no longer re-derives it in any language; the one
   remaining field read is PowerShell-only.
4. **Routed leaselessness** (Phase 2 or 3): RULED — accepted dev-loop
   behavior, documented in the coordinator's help `.NOTES`; no co-lease.

## Do-not-break (load-bearing design any phase must preserve)

Fail-closed proof chain (proof = tree hash + provenance, stale summaries
cleared, unknowns → "partial"); routed parity gate (executed-set vs
`list_tests`, drift → `infra_error`); `inert_diff.ps1` doubt semantics;
mkdir-as-mutex + atomic JSON writes + `-ProcessSnapshotPath` internal seam;
durable worktree git-config lease + `task_branch_for` never bare-slot;
journal discipline (journalling never fails a merge; `merge_run` pointer);
`resharper_fingerprint` self-invalidating pass-proof; hazard comments naming
incidents (CLOBBER/REVISE HAZARD, autotick starvation, 2>&1 envelope) —
preserve verbatim through refactors.
