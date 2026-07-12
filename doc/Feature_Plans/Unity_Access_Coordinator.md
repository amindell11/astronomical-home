# Unity Access Coordinator

Board item: [Unity access coordinator](D:/amind/git/astronomical-home/doc/Feature_Plans/Unity_Access_Coordinator.md) `#Testing` `#Architecture`

## Problem

Unity is a machine-wide constrained resource in this repository. Interactive editors, MCP sessions, and batch test processes currently discover conflicts independently. That permits concurrent boots, ambiguous MCP routing, abandoned editors, and accidental interference with a user-owned main-worktree editor.

## Invariants

- One managed Unity lane covers interactive editors and batch test processes on this host.
- A durable MCP server on port 8081 is shared; lane owners connect or disconnect editor sessions but do not stop the server.
- Requests are FIFO. An agent may wait, inspect its queue position, cancel, or acquire when it reaches the head.
- The owner record names the worktree slot, project path, mode, lease, and Unity process when known.
- An interactive main-worktree editor absent from coordinator state is user-owned. Agents report it and ask the user to close it; they never terminate it.
- Any other untracked Unity process blocks acquisition and requires explicit resolution.
- Batch mode is preferred whenever the requested verification supports it. Batch runs acquire the same lane, exit when finished, and release automatically.
- Coordinator-launched interactive editors close after verification. Release verifies exit before handing the lane to the next request.
- State is anchored beside the primary worktree pool so every worktree observes the same owner and queue.

## State model

`<primary>/.worktree-pool/unity-access/` contains:

- `owner/owner.json` — atomically acquired directory and current owner metadata.
- `queue/*.json` — ordered request tickets; timestamp plus random suffix provides stable FIFO ordering.

Queue tickets carry a renewable timestamp. Abandoned tickets expire after a bounded TTL. Owner recovery is conservative: live tracked Unity processes remain authoritative; stale metadata without a live process can be reclaimed.

## Command surface

`scripts/unity_access.ps1` provides:

- `request` — create or renew a FIFO ticket.
- `wait` — poll briefly, renew the ticket, and acquire only from the head;
  agents can repeat bounded waits without user-driven handoff messages.
- `status` — report owner, queue, live Unity processes, and unmanaged blockers.
- `run-batch` — acquire, invoke the existing batch runner, and release in `finally`.
- `start-editor` — acquire and launch the selected worktree editor.
- `release` — close only the tracked editor when requested, verify exit, and release.
- `cancel` — remove the caller's queued ticket.

Commands return structured JSON when `-Json` is supplied so scripts and agents do not parse prose.

## Integration

- `unity_test_agent.ps1` enters the lane before launching Unity and releases it on every exit path.
- `agent_worktree_pool.sh run-tests`, `submit`, `revise`, and `merge` inherit protection through the test runner.
- `unity_doctor.ps1` reports the tracked owner, queue, and whether a detected main editor is user-owned.
- `worktree_dashboard.sh` shows the Unity lane owner and queued slots.
- `TESTING.md`, the Unity tooling postmortem, and the worktree skill document the batch-first and close-on-completion policy.
- `.claude/skills/unity-access/SKILL.md` gives Claude a task-triggered, low-freedom workflow for status, batch-first use, FIFO waiting, tracked editor startup, blocker handling, and mandatory cleanup.

## Testing

PowerShell tests use an isolated state root and injected process snapshots to cover:

- FIFO acquisition and cancellation.
- Stale queue ticket cleanup.
- Owner release and conservative stale-owner recovery.
- Main-worktree user-editor classification.
- Untracked agent/batch blocker classification.
- JSON status shape.

A live smoke test acquires a free worktree, detects the currently open user main editor without closing it, and confirms clean cancellation/release without launching a second Unity process.

The `unity-access` skill is also forward-tested with a fresh Claude CLI session. The probe must acquire or queue a short-lived batch lease without starting Unity, release or cancel it, and prove from final status that it left neither an owner nor a ticket behind.

## Out of scope

- Concurrent interactive editors.
- Automatically terminating user-owned or untracked processes.
- Per-editor MCP servers or ports.
- Cross-machine coordination.
- Game/runtime code changes.
