---
name: unity-access
description: Coordinate exclusive access to this repository's shared Unity editor and MCP server. Use before running Unity tests, opening an interactive Unity editor, connecting through Unity MCP, or diagnosing why another agent cannot use Unity.
---

# Unity Access

Use `scripts/unity_access.ps1` as the authority for the machine-wide Unity lane. Prefer batch tests so the interactive editor remains available, wait in FIFO order when busy, and leave both the owner and queue clean.

Run commands from the repository root with PowerShell.

## Choose the least disruptive path

1. Inspect the lane:

   ```powershell
   .\scripts\unity_access.ps1 -Action Status -Json
   ```

2. Prefer the batch test runner. It acquires and releases the lane automatically:

   ```powershell
   .\scripts\unity_test_agent.ps1 -Mode Both -ScopeType Smoke -OutDir results/unity-tests-agent
   ```

   Narrow the run with the appropriate `-TestCategory`, `-ScopeType`, or `-ScopeName`; consult `TESTING.md` for the suite's supported slices.

3. Use an interactive editor only for behavior batch mode cannot verify:

   ```powershell
   .\scripts\unity_access.ps1 -Action StartEditor -Lease <unique-lease> -Slot <slot> -Mode editor -WaitSeconds 60 -Json
   ```

   The coordinator starts or reuses the shared MCP server and records the editor PID. Confirm that the returned status is `attached` and that `Status` identifies the expected lease before using MCP.

## Queue and blockers

- Use a unique, task-specific lease and the current pool slot (`agent-1`, `agent-2`, or `agent-3`).
- Use `Wait` with a bounded timeout instead of hand polling:

  ```powershell
  .\scripts\unity_access.ps1 -Action Wait -Lease <unique-lease> -Slot <slot> -Mode batch -WaitSeconds 60 -Json
  ```

- Exit code `20` means the request is still queued. Preserve the ticket if continuing later; otherwise cancel it.
- `blocked_user_editor` means an untracked editor on the main worktree belongs to the user. Report its PID and ask the user to close it. Never terminate or attach to it.
- `blocked_unmanaged_unity` means an untracked Unity process is active. Do not close it; wait for it to exit or identify its owner.
- Never bypass the coordinator, jump the FIFO queue, or use the Unity MCP window's **Stop Server** action. The MCP server on port 8081 is shared independently of the current editor lease.

## Cleanup is mandatory

Release an acquired batch-only lease even after a failed check:

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

Finally run `Status -Json`. Verify that this lease appears in neither `owner` nor `queue`. Do not claim success if cleanup fails; report the remaining owner, ticket, or process PID.
