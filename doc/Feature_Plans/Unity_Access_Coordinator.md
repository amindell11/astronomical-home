# Unity Access Coordinator

Board item: [Unity access coordinator](D:/amind/git/astronomical-home/doc/Feature_Plans/Unity_Access_Coordinator.md) `#Testing` `#Architecture`

## Problem

Unity is a machine-wide constrained resource in this repository. Interactive editors, MCP sessions, and batch test processes currently discover conflicts independently. That permits concurrent boots, ambiguous MCP routing, abandoned editors, and accidental interference with a user-owned main-worktree editor.

## Invariants (two-tier revision, 2026-07-12)

Originally one machine-wide lane serialized all Unity work. The D6 re-probe showed the only machine-global contention is Unity **startup** (licensing IPC, UPM package cache), so the lock was split in two:

- **Run ownership is per project.** Each project path has its own owner; runs on different worktree projects overlap. FIFO queueing applies within a project.
- **Startup serializes through one machine-wide boot lane**, held from just before launching a Unity process until its log passes the global-contention window (`Application.AssetDatabase Initial Refresh Start`) or a TTL expires. Interactive editors have no log signal and rely on the TTL.
- A durable MCP server on port 8081 is shared; owners connect or disconnect editor sessions but do not stop the server.
- The owner record names the worktree slot, project path, project key, mode, lease, and Unity process when known.
- An interactive main-worktree editor absent from coordinator state is user-owned. Agents report it and ask the user to close it; they never terminate it.
- An untracked **batch** Unity process blocks acquisition everywhere (it may be mid-boot); an untracked **editor** blocks only its own project for batch requests, and everything for editor-mode requests.
- A legacy `owner/owner.json` written by a pre-two-tier script copy is honored as a machine-wide owner until it clears.
- Batch mode is preferred whenever the requested verification supports it. Batch runs drive the whole protocol and release automatically.
- Coordinator-launched interactive editors close after verification. Release verifies exit before freeing the project.
- State is anchored beside the primary worktree pool so every worktree observes the same owners, boot lane, and queue.

## State model

`<primary>/.worktree-pool/unity-access/` contains:

- `owners/<projectKey>/owner.json` — atomically acquired per-project directory and owner metadata; the key is a sanitized path tail plus hash.
- `boot/boot.json` — the machine-wide boot lane; TTL-reclaimed (`-BootTtlSeconds`, default 180) and pid-checked once a process attaches.
- `queue/*.json` — ordered request tickets; timestamp plus random suffix provides stable FIFO ordering, position computed per project.
- `owner/owner.json` — legacy single-owner state; read-only compatibility until all live sessions run the two-tier script.

Queue tickets carry a renewable timestamp. Abandoned tickets expire after a bounded TTL. Owner recovery is conservative: live tracked Unity processes remain authoritative; stale metadata without a live process can be reclaimed.

## Command surface

`scripts/unity_access.ps1` provides:

- `request` — create or renew a FIFO ticket.
- `wait` — poll briefly, renew the ticket, and acquire only from the project's queue head;
  agents can repeat bounded waits without user-driven handoff messages.
- `status` — report per-project owners, boot lane, legacy owner, queue, and unmanaged blockers.
- `bootacquire` / `bootrelease` — take and free the machine-wide startup lane; requires holding a project owner lease. Driven by launchers, not called ad hoc.
- `run-batch` — acquire owner and boot lane, invoke the given script, and release both in `finally`.
- `start-editor` — acquire owner and boot lane, launch the selected worktree editor; the boot lane self-expires by TTL.
- `release` — close only the tracked editor when requested, verify exit, and release the owner plus any held boot lane.
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
- Per-project overlap and same-project serialization.
- Boot-lane exclusivity, owner-lease requirement, staleness reclaim, and release-frees-boot.
- Legacy single-owner honoring and clearance.
- Stale queue ticket cleanup.
- Owner release and conservative stale-owner recovery.
- Main-worktree user-editor classification.
- Untracked agent/batch blocker classification (including boot-lane blocking).
- JSON status shape.

A live smoke test acquires a free worktree, detects the currently open user main editor without closing it, and confirms clean cancellation/release without launching a second Unity process.

The `unity-access` skill is also forward-tested with a fresh Claude CLI session. The probe must acquire or queue a short-lived batch lease without starting Unity, release or cancel it, and prove from final status that it left neither an owner nor a ticket behind.

## Out of scope

- Concurrent interactive editors.
- Automatically terminating user-owned or untracked processes.
- Per-editor MCP servers or ports.
- Cross-machine coordination.
- Game/runtime code changes.
