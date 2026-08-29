# Script Seam Hardening

> STATUS: live arc — scripts/ interaction-pattern cleanup; arc #451, phases 0–4 carded #452–#456 (blocked chain), one PR each unless noted.

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
   divergent copies today): Unity exe resolution, repo root, kill-tree,
   Unity-churn classifier, coordinator-invoke helper. Nothing enters the lib
   with one caller — one adapter is a hypothetical seam.
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

## Phases

Ordered so enforcement precedes safety precedes deepening; each phase is
independently mergeable and lands with regression cover from Phase 0.

### Phase 0 — enforcement substrate

Wire `scripts/tests/` into the pool merge gate; add `test_pool_locking.sh`
covering acquire ordering, named-slot strictness, TTL reclaim contention
(the Phase 1 race), clobber-safety refusal, release, and the prepare
unpushed-work refusal (use the existing `WORKTREE_POOL_LOCK_TTL` override).
- Trigger scope and cost: OPEN FORK (below).
- Test-hygiene ride-alongs: merge-gate stub honors `-OutDir` + one golden
  summary from a real runner invocation (stub schema currently drifts free,
  `test_pool_merge_gate.sh:31-91`); convert absolute run-counters to deltas
  (the `:393` idiom).

### Phase 1 — safety (locking correctness)

Small diffs, highest stakes, now testable. Root causes, fix-ladder rung 2
(earliest deterministic failure) or structural:
- Pool reclaim double-acquire (`agent_worktree_pool.sh:544-560`): single
  winner via atomic `mv` of the stale lock dir to a tombstone, then fresh
  `mkdir`.
- Coordinator record-less-dir reap defeats the mkdir mutex
  (`unity_access.ps1:220-222, 379-395`; same shape in boot lane
  `:263-274, 475-490`): record creation atomic with the lock (temp dir +
  `Move-Item` of the dir), or age-gate the reap.
- CIM failure reads as "no Unity" and reaps live leases
  (`unity_access.ps1:109-115`): throw or sentinel that suppresses
  stale-reaping for that call — never `@()`.
- PID-recycling: bind holder liveness to `holderStartTime`
  (`unity_access.ps1:208-209`); hoist the relevant-Unity guard above both
  close branches (`:595-603`).

### Phase 2 — coordinator interface law — LANDED

Landed the contract doc `doc/agents/script-contracts.md`, the sanctioned client
`scripts/unity_access_client.ps1` (3 callers), the coordinator's help block, the
`record_unreadable` / `coordinator_error` statuses, and a hermetic
`test_unity_access.ps1` (#454) — the merge gate's non-hermetic skiplist is empty.

Shrink `unity_access.ps1`'s effective interface to its published one:
- Comment-based help: actions × statuses × exit codes × required params;
  owner/ticket/boot JSON schemas named as owned contracts.
- Machine channel guarantee: `-Json` = exactly one JSON line on stdout
  (or `-ResultPath`); kills all scrape sites (`unity_test_agent.ps1:113,
  124, 621`, `resharper_ratchet.ps1:162-165`, the sidecar's self-scrape
  `:441-443`) via one shared invoke helper.
- Ticket/reap fixes riding the same seam: no-owner Release cancels its
  ticket (`:587-592`); `Ensure-Ticket` reconciles all fields, position 0 =
  loud invariant violation (`:179-183, 342-346, 377`); one reap helper with
  one error policy replaces the four divergent blocks (`:225, 251, 274,
  618`); unreadable record ≠ missing record (`:74-79`).
- Author `doc/agents/script-contracts.md` (rules 1–2 above, stated as
  review law: what counts as interface for a script module — exit codes,
  machine channel, state-file schemas, timing constants).

### Phase 3 — verdict ownership (deepening the three producers) — LANDED

Landed: the runner's `coverage` stamp (`{verdict, reason}`) with the pool reading
that one field plus the project it owns; `status --porcelain` as the pool's read
interface, with human `status` and the dashboard as adapters over one collection
pass; the coordinator's `normalizedProjectPath`, `Status -ProjectPath`
(`projectOwner` / `projectProcesses`) and `-Action Contract` (the single home for
`bootCompletePattern`); and `ConvertTo-TestNameSelection` / `Resolve-ScopeSelection`
as the one reader of the authored filter format. Deferred out (still true): the
pool re-derives the runner's log-dir layout when tailing a live run
(`agent_worktree_pool.sh:~1139`) and `pr_number_for_slot` still looks PRs up by
bare slot name.

- **Runner stamps coverage.** `unity_test_agent.ps1` writes
  `coverage: full|partial` + reason into its summary; the pool
  trust-and-checks one field, deleting the dual-language predicate
  (`agent_worktree_pool.sh:199-323`) and the re-derived summary filenames
  (`:191, :1170`). Deletion test: no complexity reappears.
- **Pool grows `status --porcelain`** — stable `key=value` per slot (slot,
  state, lease, task_branch, path, PR). Porcelain is the pool's read
  interface; human `status` and the dashboard become adapters over it. The
  dashboard deletes its hand-parsed lock reads (`worktree_dashboard.sh:
  100-113`, wrong today: no git-config lease fallback, `--head "$slot"` PR
  lookup) and its `slots_tsv` copy (`:181-190`).
- **Coordinator answers ownership questions.** Status emits
  `normalizedProjectPath` / answers "who owns this path"; `unity_doctor.ps1`
  (`:46-66`) and routed owner-matching (`unity_test_agent.ps1:625-627`)
  become consumers; `$BootCompletePattern` gets one home.
- **Scope lib emits a structured selection** both transports consume,
  replacing routed's substring re-parse of the regex filter format
  (`unity_test_agent.ps1:690-694`).

### Phase 4 — shared primitives + dedup (mechanical, last)

- Seed `scripts/lib/`: exe resolver (from `ProjectSettings/
  ProjectVersion.txt`; the three hardcoded paths become overrides),
  repo-root (converge on the scope lib's `Get-RepoRoot`), kill-tree (the
  taskkill `/T /F` variant, replacing two root-only kills), churn classifier
  (single PS owner; `agent_worktree_pool.sh:658-676` shells out to it, and
  capture the diff before restoring).
- In-file dedup: shared failure-entry/run-record constructors
  (`unity_test_agent.ps1:526-537` ↔ `852-866`, `477-491` ↔ `884-898`);
  merge `create-pr`/`submit` parse+push (`agent_worktree_pool.sh:780-838` ↔
  `847-923`, submit rejects bare positionals); hoist submit's `gh` check to
  preflight (`:903-906`); delete dead `create-pool-prs` (`:840-845`).
- Hygiene rides only in touched hunks: bare `2>&1` under EAP=Stop,
  `$args`/`$matches` shadowing, BOM'd rerun list, `prepare --force`
  positional parse, `-CompletionFiles` on the per-platform cold path.

## Open forks (block the phase named; propose-and-default listed)

1. **Merge-gate trigger for script tests** (blocks Phase 0): RULED —
   diff-triggered, only when the landing diff touches `scripts/**`
   (~1 min added to those merges); not unconditional.
2. **`check_test_naming.ps1`** (Phase 4): delete + fix TESTING.md's
   enforcement claim (default — never run, convention not load-bearing), or
   wire into the merge gate with JSON output.
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
