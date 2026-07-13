---
name: unity-access
description: Coordinate access to this repository's shared Unity editors and MCP server. Use before running Unity tests, opening an interactive Unity editor, connecting through Unity MCP, or diagnosing why another agent cannot use Unity.
---

# Unity Access

Use `scripts/unity_access.ps1` as the authority for Unity process coordination. Ownership is **per project**: runs on different worktree projects overlap freely, and only Unity **startup** serializes through a machine-wide boot lane (concurrent boots were the deadlock hazard — postmortem D6). Prefer batch tests, wait in FIFO order when your project is busy, and leave owners, the boot lane, and the queue clean.

Run commands from the repository root with PowerShell.

## Choose the least disruptive path

1. Inspect the coordinator state:

   ```powershell
   .\scripts\unity_access.ps1 -Action Status -Json
   ```

   `owners` lists per-project run owners; `boot` is the machine-wide startup lane (held only while a Unity process boots); `legacyOwner` is machine-wide state from a pre-two-tier script copy and blocks everything until that session pulls main.

2. Prefer the batch test runner. It acquires the project owner, serializes its Unity startup through the boot lane, releases the lane once the log shows boot is past the global-contention window, and releases the owner when done — all automatically:

   ```powershell
   .\scripts\unity_test_agent.ps1 -Mode Both -ScopeType Smoke -OutDir results/unity-tests-agent
   ```

   Narrow the run with the appropriate `-TestCategory`, `-ScopeType`, or `-ScopeName`; consult `TESTING.md` for the suite's supported slices. Batch runs in different worktrees run in parallel; only a run on the **same** project queues.

3. Use an interactive editor only for behavior batch mode cannot verify:

   ```powershell
   .\scripts\unity_access.ps1 -Action StartEditor -Lease <unique-lease> -Slot <slot> -Mode editor -WaitSeconds 60 -Json
   ```

   The coordinator starts or reuses the shared MCP server and records the editor PID. Confirm that the returned status is `attached` and that `Status` identifies the expected lease before using MCP. A tracked editor only blocks work on its own project, but it holds the boot lane until the lane's TTL expires (~3 min), so other Unity launches queue briefly after an editor start.

   **Instance pinning is mandatory whenever more than one editor may be connected to the MCP server** — `Status` shows a second editor-mode owner, or the user's untracked main editor is open beside yours. Batch test runs never register with MCP, but every connected editor does: list `mcpforunity://instances`, then pin with `set_active_instance` (or pass `unity_instance` per call) and verify the pinned instance's project path is your worktree before issuing any MCP command. Unpinned calls in a multi-editor situation route unpredictably.

## Queue and blockers

- Use a unique, task-specific lease and the current pool slot (`agent-1` … `agent-5`).
- Use `Wait` with a bounded timeout instead of hand polling:

  ```powershell
  .\scripts\unity_access.ps1 -Action Wait -Lease <unique-lease> -Slot <slot> -Mode batch -WaitSeconds 60 -Json
  ```

- Exit code `20` means the request is still queued (project owned, boot lane held, or a legacy owner present). Preserve the ticket if continuing later; otherwise cancel it.
- `blocked_user_editor` means an untracked editor on the main worktree belongs to the user. Report its PID and ask the user to close it. Never terminate or attach to it. Batch requests hit this only when they target the main project itself; editor-mode requests block on any untracked Unity process.
- `blocked_unmanaged_unity` means an untracked Unity process contends: for batch requests, an untracked batch process on any project (it may be mid-boot) or an untracked editor on the requested project. Do not close it; wait for it to exit or identify its owner.
- `BootAcquire`/`BootRelease` exist for launchers (`unity_test_agent.ps1` drives them); you rarely call them directly. `BootAcquire` requires already holding a project owner lease.
- Never bypass the coordinator, jump the FIFO queue, or use the Unity MCP window's **Stop Server** action. The MCP server on port 8081 is shared independently of any lease.

## Cleanup is mandatory

Release an acquired batch-only lease even after a failed check (this also frees a boot lane the lease still holds):

```powershell
.\scripts\unity_access.ps1 -Action Release -Lease <unique-lease> -Json
```

Close an editor that this lease started, then release it:

```powershell
.\scripts\unity_access.ps1 -Action Release -Lease <unique-lease> -CloseEditor -Json
```

Cancel an abandoned queued request:

```powershell
.\scripts\unity_access.ps1 -Action Cancel -Lease <unique-lease> -Json
```

Finally run `Status -Json`. Verify that this lease appears in neither `owners`, `boot`, nor `queue`. Do not claim success if cleanup fails; report the remaining owner, ticket, or process PID.
