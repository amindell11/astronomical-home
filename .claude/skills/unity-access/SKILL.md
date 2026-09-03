---
name: unity-access
description: Coordinate access to this repository's shared Unity editors. Use before running Unity tests, opening an interactive Unity editor, driving a live editor through the unity CLI, or diagnosing why another agent cannot use Unity.
---

# Unity Access

Use `scripts/unity_access.ps1` as the authority for Unity process coordination. Ownership is **per project**: runs on different worktree projects overlap freely, and only Unity **startup** serializes through a machine-wide boot lane (concurrent boots were the deadlock hazard — postmortem D6). Prefer batch tests, wait in FIFO order when your project is busy, and leave owners, the boot lane, and the queue clean.

Every Unity boot — batch or editor — costs ~2.5–4 GB working set, and the machine sustains about two editors. Boot only when free physical RAM is ≥ ~10 GB (`(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB` → GB). Below that, use the Alastor remote-gate fallback when it is available; otherwise report the memory pressure and wait for an editor to exit.

Run commands from the repository root with PowerShell.

## Alastor remote-gate fallback

When Mordechai is below the local RAM floor and a full batch gate is needed, check Alastor before waiting: inspect its available RAM, `unity_access.ps1 -Action Status -Json`, and remote `git status`. If its lane is clear, run `scripts/remote_gate.sh <branch>` from the local branch being tested; it owns the bundle/LFS transfer, remote checkout, detached launch, and summary retrieval.

`remote_gate.sh` force-checks out the target commit on Alastor. Preserve any remote dirty state first (back up and restore the exact changed files) or get explicit authority to discard it. A passing remote summary is valid test evidence, but it does not record merge-grade proof in `agent_worktree_pool.sh`; include it in the PR and let the pool's merge protocol run its required gate when local capacity is available.

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

3. Route work into an editor your work stream already holds — one editor per work stream. A live slot editor whose manifest carries `com.unity.pipeline` (any branch containing PR #432; older branches fold main first) answers the full Unity CLI surface with no second instance:

   ```powershell
   unity command <cmd> --project-path D:\amind\git\<slot>\src\Asteroids3D
   ```

   A session collaborating on the same work stream attaches here too, instead of running its own `StartEditor`. Gate readiness with `unity command editor_status --project-path <proj>` — never `unity status` or `pipeline list` (both misreport live and dead editors). CLI contract and gotchas (eval quirks, capture paths, reload dead zones): `doc/agents/unity-cli.md`.

   Scoped test runs route into this editor too: `.\scripts\unity_test_agent.ps1 -Routed ...` (attach-only; contract in `TESTING.md` § Routed runs). Merge-gate runs stay cold — routed summaries never count as merge proof.

4. Use an interactive editor only for behavior batch mode cannot verify:

   ```powershell
   .\scripts\unity_access.ps1 -Action StartEditor -Lease <unique-lease> -Slot <slot> -Mode editor -WaitSeconds 60 -Json
   ```

   The coordinator records the editor PID. Confirm that the returned status is `attached` and that `Status` identifies the expected lease before driving the editor. A tracked editor only blocks work on its own project, but it holds the boot lane until the lane's TTL expires (~3 min), so other Unity launches queue briefly after an editor start.

   Drive the editor through the `unity` CLI (`unity-cli` skill), **always passing `--project-path <your worktree's src/Asteroids3D>`** — routing is per-project via the editor's own lockfile, so multiple editors coexist and there is nothing to pin. Gate readiness as in rung 3; entering Play Mode gives a ~2 s domain-reload window where commands transiently fail — retry once or poll `editor_status`.

   Label the window right after attach so the taskbar shows which task holds
   the editor (the [PRIMARY]/[AGENT-N] slot prefix is automatic; the label
   replaces the project-name segment and resets on every domain reload —
   re-run it after a recompile if it still matters):

   ```powershell
   unity command set_window_title --label <unique-lease> --project-path <worktree>\src\Asteroids3D
   ```

## Queue and blockers

- Use a unique, task-specific lease and the current pool slot (`agent-1` … `agent-5`).
- Use `Wait` with a bounded timeout instead of hand polling (never a `Start-Sleep` loop to wait on the lane/editor — the harness blocks chained sleeps):

  ```powershell
  .\scripts\unity_access.ps1 -Action Wait -Lease <unique-lease> -Slot <slot> -Mode batch -WaitSeconds 60 -Json
  ```

- Exit code `20` means the request is still queued (project owned, boot lane held, or a legacy owner present). Preserve the ticket if continuing later; otherwise cancel it.
- `blocked_user_editor` means an untracked editor on the main worktree belongs to the user. Report its PID and ask the user to close it. Never terminate or attach to it. Batch requests hit this only when they target the main project itself; editor-mode requests block on any untracked Unity process.
- `blocked_unmanaged_unity` means an untracked Unity process contends: for batch requests, an untracked batch process on any project (it may be mid-boot) or an untracked editor on the requested project. Do not close it; wait for it to exit or identify its owner.
- **Recovering from `blocked_unmanaged_unity` / `ownership_mismatch`:** the JSON names the blocker's `processId` and `projectPath`. Check whether it's alive (`Get-Process -Id <pid>`). If it's **dead**, the record is stale — re-run `Acquire` (dead owners self-prune on the next call); if it still blocks, report it. If it's **alive and it's an untracked editor that outlived its lease** (an orphaned RL batch process, or your own editor whose owner record aged out), seize it back with `Adopt -Lease <lease> -Slot <slot> -ProcessId <pid>`: it writes a fresh pid-backed owner (project derived from the process's own `-projectPath`). `Adopt` refuses a PID that is already tracked or is the user's hand-opened dev editor (`user_editor`); it does not refuse `-batchmode`. The `user_editor` heuristic (windowed editor on the primary tree) also catches coordinator-launched primary-tree editors whose owner aged out — there the recovery is: report the PID and get the user's explicit go-ahead to kill. Renew the owner (re-`Acquire` under the same lease) at natural breaks in a long interactive session so it never ages out under a live editor. Never hand-edit `owner.json`.
- **The coordinator launches everything — do not `Popen`/`Start-Process` Unity beside it.** RL drivers (`run_training.py`, `run_smoke.py`) boot their batch editor through `StartEditor -EditorArgs @(...)` so the owner is pid-backed from birth and the boot lane is honored; a live editor the coordinator did not launch is debt, not a supported category. If some launch genuinely lands outside the coordinator, `Adopt` is the recovery hatch (above). ⚠ `-EditorArgs` REPLACES the defaults verbatim — omit `-projectPath` and Unity opens the most-recently-used project.
- `BootAcquire`/`BootRelease` exist for launchers (`unity_test_agent.ps1` drives them); you rarely call them directly. `BootAcquire` requires already holding a project owner lease.
- Never bypass the coordinator or jump the FIFO queue.

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
