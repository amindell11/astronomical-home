# Unity Access Coordinator Hardening

> STATUS: live arc — issue #330; shipped defects #293/#298 are tracker
> reconciliation, ticket-cleanup race #299 is the sole build slice.

## Outcome

Close the coordinator defect cluster without reopening the two `RunBatch`
failures already shipped in PR #224. The implementation changes only queue
ticket deletion: cleanup becomes safe when another coordinator invocation has
already removed the same ticket, while malformed ticket records and real
filesystem failures remain loud.

Umbrella: [#330](https://github.com/amindell11/astronomical-home/issues/330)

## Grounded issue state

| Issue | Main today | Arc disposition |
|---|---|---|
| [#293](https://github.com/amindell11/astronomical-home/issues/293) — `RunBatch` holds the boot lane for the full child run | PR #224 added a sidecar that claims the batch child and releases the lane after the startup marker or bounded fallback. | Close as shipped by PR #224; no new code. |
| [#298](https://github.com/amindell11/astronomical-home/issues/298) — pid-less owner TTL-expires mid-run | PR #224 added `holderProcessId`, checks holder liveness before child liveness/TTL, and attaches the Unity child when it appears. | Close as shipped by PR #224; no new code. |
| [#299](https://github.com/amindell11/astronomical-home/issues/299) — ticket cleanup races under contention | `Remove-StaleTickets`, successful acquisition, and cancellation still delete discovered ticket paths with raw `Remove-Item`. A concurrent remover can turn normal cleanup into a caller failure. | Build and verify one ticket-lifecycle PR. |

PR #224 already carries live-Unity proof that the owner survives past its TTL,
the boot lane becomes free while the child remains live, and cross-project
acquisition succeeds after the child is claimed. The current coordinator test
suite pins those behaviors; this arc treats them as regression boundaries.

## Root-cause reading and decision gate

The remaining observed failure is a time-of-check/time-of-use race: a ticket
path can be valid when enumerated and absent by deletion time because another
coordinator invocation completed the same cleanup. File absence is therefore a
successful terminal state for ticket deletion, not a reason to fail the caller.

The original incident record does not retain the complete failing path or
interleaving. The implementation PR starts by producing a pre-fix contention
repro. If that repro instead shows a blank/malformed ticket record, stop and
move the fix to ticket construction/readback. Do not make deletion swallow a
ticket-record invariant violation.

## Slice 0 — brief and tracker reconciliation

1. Land this brief before implementation.
2. Attach #293, #298, and #299 as native children of #330.
3. Close #293 and #298 with thin references to PR #224.
4. Leave #299 and #330 open for the implementation slice.

## Slice 1 / PR-1 — ticket lifecycle hardening

**Lease id:** `unity-access-ticket-cleanup`

**Scope:**

1. Capture a deterministic pre-fix repro for ticket disappearance during
   cleanup.
2. Introduce one ticket-deletion primitive used by stale cleanup, successful
   acquisition, and cancellation.
3. Give the primitive exact semantics:
   - an already-absent file is success;
   - a missing or blank ticket path fails loudly;
   - permission, locking, and unexpected filesystem failures remain loud.
4. Prefer the naturally idempotent `System.IO.File.Delete` operation. Do not
   add a `Test-Path`/delete race or broad `-ErrorAction SilentlyContinue`.
5. Preserve FIFO ordering, per-project owner behavior, and every existing
   owner/boot-lane contract.

**Expected files:**

- `scripts/unity_access.ps1`
- `scripts/tests/test_unity_access.ps1`
- this brief, deleted by the PR when the arc completes

**Explicit non-goals:**

- boot-marker, boot-timeout, or boot-lane policy changes;
- owner TTL, holder, child-attachment, or blocker-rule changes;
- RL launcher/driver changes;
- a queue/storage rewrite;
- unrelated coordinator or test-harness hygiene.

## Verification and acceptance

The PR is ready only when all of these hold:

1. The regression proves the old deletion path fails under the captured
   interleaving and the new path completes without hiding malformed records.
2. Concurrent cancellation and stale-ticket cleanup leave no orphaned or
   resurrected ticket.
3. A held project plus contending waiters preserves FIFO progression: cleanup
   never produces exit 1, and the next eligible waiter acquires.
4. The complete `scripts/tests/test_unity_access.ps1` assertion suite passes
   from the claimed pooled worktree.
5. The standard merge-grade Unity run passes with explicit artifact output:
   `-Mode Both -ScopeType Workspace`, under `results/unity-tests-agent`.
6. The diff changes none of the #293/#298 mechanisms except tests that pin
   them as regression boundaries.

## Arc close-out

The completing PR closes #299 and deletes this transient brief. After merge,
replace #330's `Detail:` target with the final PR, close #330, and leave #293
and #298 pointing at PR #224.
