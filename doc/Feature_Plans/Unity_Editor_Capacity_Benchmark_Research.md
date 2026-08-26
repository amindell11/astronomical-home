# Unity Editor Capacity Benchmark Research

Issue: [Define the Unity Editor capacity benchmark](https://github.com/amindell11/astronomical-home/issues/434)

## Decision

Use a two-level benchmark:

1. Every mechanical PR records a paired, three-run **warm-Library cold-process**
   check: a new Unity process over an already-imported worktree `Library`, with
   deterministic editor state. It reports startup time and peak private commit,
   then five minutes of idle private commit and system memory.
2. After the mechanical sequence, run three complete **4+1 trials**: four
   sequentially booted persistent editors, followed by the ordinary full
   EditMode+PlayMode test run in a fifth worktree whose `Library` was warmed on
   the base revision and has not yet imported the candidate revision.

The capacity target passes only when every valid final trial satisfies all of:

- each persistent root `Unity.exe` has five-minute idle P95 private commit at or
  below 3.25 GiB;
- physical memory available never drops below 4 GiB;
- the fifth boot begins only when physical memory available is at least 10 GiB,
  the repository's current Unity boot safety floor;
- the transient full workspace test run completes green without timeout, crash,
  operating-system allocation failure, or coordinator cleanup residue.

After the dry 4+1 result, compare two fresh single-editor processes—one in an
empty scene and one in the user-approved representative scene—with matching OS
traces and Memory Profiler snapshots. If the representative state breaches the
per-editor target or projects less than 4 GiB physical headroom for four such
editors, stop at the already-agreed choice between game-specific work and 64 GB.
If it fits, confirm once with four representative-scene editors plus the same
transient run.

This makes the capacity result an end-to-end concurrency decision while keeping
Memory Profiler evidence diagnostic. A snapshot is not the capacity metric: it
perturbs the process and, for an Editor target, necessarily includes Editor
memory.

## Why these metrics

Windows distinguishes private commit from resident memory. The root editor's
`Process.PrivateMemorySize64` is equivalent to the Windows `Private Bytes`
counter and represents memory that cannot be shared with other processes. It is
therefore the stable per-editor metric. Working set remains useful context, but
it changes under paging pressure and is not the 3.25 GiB gate.

The per-editor gate applies to the coordinator-recorded root `Unity.exe` PID.
Also report each root's descendant-process private bytes and working set, but do
not add those to the 3.25 GiB number: Unity child processes are not reliably
exclusive to one editor. The system-wide physical-headroom gate catches their
real capacity cost without questionable attribution.

Use `Get-Process -Id <pid>`, call `Refresh()`, and read
`PrivateMemorySize64`/`WorkingSet64`. Do not bind a long trace to names such as
`Unity#2`: Microsoft's legacy Process performance-counter set can reuse and
shuffle non-unique image-name instances. The coordinator already owns the
project-to-PID mapping.

At one-second intervals, collect these system counters as raw bytes or convert
them once in the report:

- `\Memory\Available MBytes` — the hard physical-headroom gate;
- `\Memory\Committed Bytes` and `\Memory\Commit Limit` — commit-reserve
  context and an OOM warning, not a substitute for physical headroom;
- `\Paging File(_Total)\% Usage` and `\Memory\Pages Input/sec` — paging
  context, not independent pass/fail thresholds.

The 4 GiB floor is both the user's target and Microsoft's published healthy
rule of thumb for `Available MBytes`. Keep page-file configuration fixed across
paired runs and record the observed commit limit; a changing commit limit makes
the pair invalid.

## Existing repository seams

The benchmark belongs at the existing Unity access coordinator seam, never in a
parallel launcher:

- [`scripts/unity_access.ps1`](../../scripts/unity_access.ps1) starts tracked
  editors, owns the boot lane, and exposes each project owner and PID through
  `Status -Json`.
- [`scripts/unity_test_agent.ps1`](../../scripts/unity_test_agent.ps1) launches
  tracked batch editors, records the PID internally, defaults to `-nographics`,
  and uses single-boot mode for an ordinary `Both` run.
- [`GateTestRunner.cs`](../../src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/GateTestRunner.cs)
  runs EditMode and PlayMode in one Editor process, avoiding a second startup in
  the transient phase.
- Current main's [Unity access skill](../../.claude/skills/unity-access/SKILL.md)
  requires at least about 10 GiB free physical RAM before any new Unity boot and
  routes work into a held editor through `unity command editor_status` when the
  branch carries `com.unity.pipeline`.

Implementation should add a read-only benchmark wrapper around those seams. It
may poll `Status -Json` for root PIDs or have the launcher emit a PID manifest;
it must not rediscover project ownership from process command lines and must not
call `Start-Process` itself. This follows the repository rule that the producer
of a process/path fact owns that output contract.

The project currently pins Unity `6000.1.8f1`, Memory Profiler `1.1.6`, and the
Unity Pipeline package `0.5.0-exp.1`. Record all three versions in each result.

## Deterministic editor states

An editor is **ready** when all of these are true:

- the coordinator reports the expected project, lease, and live root PID;
- `unity command editor_status --project-path <project>` reports ready;
- a benchmark editor hook reports the selected low-memory profile, exact scene
  path or `empty`, `EditorApplication.isCompiling == false`, and
  `EditorApplication.isUpdating == false` on two consecutive polls;
- the Editor log contains no compile/import error.

The benchmark hook is entered by a custom command-line argument passed through
`StartEditor -EditorArgs`; the exact low-memory profile selector is owned by
[Choose the low-memory editor profile interface](https://github.com/amindell11/astronomical-home/issues/437).
It should emit a machine-readable ready record under the caller-supplied output
directory. Readiness must not rely only on the existing
`Application.AssetDatabase Initial Refresh Start` marker—that marker correctly
releases the global boot lane but occurs before editor idleness.

After readiness, allow a fixed 120-second stabilization period, then collect a
300-second idle window. The fixed times make runs replayable; the full trace
still exposes late imports or upward drift. Compute P95 with the nearest-rank
method and also report median, maximum, and the difference between the first and
last 60-second medians.

The dry state must open a new single empty scene with
`EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`.
Never inherit the worktree's last-opened scene or layout state as the test
condition.

The representative state must open one explicit, user-approved scene path in
Edit Mode with the same profile. Record the path, asset GUID, and commit. Do not
infer the representative scene from build order: current build settings contain
`InitScene` plus two environment scenes, while `EditScene` is a separate
authoring scene, and those are materially different hypotheses. Choosing the
path is a later HITL input, not a research-agent guess.

## Reproducible run protocol

### 0. Freeze the environment

For every paired result, record:

- host, physical RAM, Windows build, page-file mode/commit limit, Unity version,
  package-lock hash, git commit, worktree path, graphics device/driver;
- low-memory profile identity and scene identity;
- background workload manifest selected by
  [Choose the editor and ML workload concurrency policy](https://github.com/amindell11/astronomical-home/issues/438):
  counts and aggregate private commit for Codex/Claude, Python/training, browser,
  and other material processes, without copying process command lines;
- coordinator owners, boot lane, queue, and blockers before and after;
- whether the worktree `Library` was warmed on the base or candidate revision.

A trial is invalid, not failed, if Windows updates, antivirus scans, a second
Unity boot, asset edits, a changing workload manifest, or a changed commit limit
disturbs it. Ordinary background activity inside the selected workload policy is
part of the result.

Before each boot:

1. Run `unity_access.ps1 -Action Status -Json`.
2. Confirm the target project has no owner and there are no unmanaged Unity
   blockers.
3. Confirm at least 10 GiB physical memory is available. If not, stop; do not
   waive the safety floor to force a data point.
4. Acquire/start through the coordinator only.

Release only leases created by the benchmark. Never close a user editor. After
each trial, release/close every benchmark editor and confirm its leases appear
in neither owners, boot, nor queue.

### 1. Prepare warm Libraries

The relevant capacity is a fresh Editor process over the persistent pool's warm
Library, not a first-ever project import. For each worktree:

1. prepare the exact base revision;
2. boot it once sequentially through the coordinator, wait for ready, then close
   and release it;
3. for a base measurement, leave it on base; for a candidate measurement,
   prepare the candidate revision without opening Unity.

This means the candidate's first measured boot performs exactly the normal
branch-delta imports and compilation. Never delete `Library`, `Temp`, or shader
caches for this benchmark. A fresh-Library import can be measured separately as
an operational worst case, but it does not decide 4+1 capacity.

### 2. Per-PR checkpoint

For each mechanical PR, alternate base and candidate runs on the same slot and
background-workload policy:

1. boot the dry state;
2. sample root and system memory from process creation through readiness;
3. stabilize 120 seconds;
4. sample five idle minutes;
5. close/release and verify cleanup;
6. repeat until there are three valid base and three valid candidate trials.

Report per revision:

- startup duration (process creation to benchmark-ready);
- startup peak root private commit and minimum available physical memory;
- idle root private-commit median/P95/max and drift;
- idle root working-set median, descendant-process private commit, minimum
  available physical memory, peak system committed bytes, and commit reserve.

The checkpoint is evidence, not permission to broaden a PR. Compile/tests still
gate the PR. A dry-memory PR that is above 3.25 GiB or whose candidate is higher
in all three paired trials needs diagnosis before landing; do not mask the result
with a hand-picked run.

### 3. Final dry 4+1 capacity trial

Use five warmed pool worktrees at the same candidate commit. Prepare worktrees
1–4 for the dry low-memory state. Warm worktree 5 on the exact base commit, then
prepare it to the candidate without launching it.

1. Start persistent editor 1 and wait through ready + 120-second stabilization.
2. Repeat sequentially for editors 2, 3, and 4. The boot lane remains the
   serialization authority.
3. Sample all four editors together for 300 seconds.
4. If any idle editor's P95 exceeds 3.25 GiB, or available physical memory is
   below 10 GiB before the transient boot, stop and record a failed trial.
5. With the four editors still open, run the ordinary merge-grade command on
   worktree 5: `unity_test_agent.ps1 -Mode Both -ScopeType Workspace`. Do not
   pass `-SkipUnityAccess`, `-WithGraphics`, or `-Windowed`.
6. Sample all five root PIDs and system counters from transient process creation
   until it exits. The fifth PID comes from the same coordinator owner record.
7. Require a green test summary, at least 4 GiB available physical memory at
   every sample, and clean transient lease release.
8. Re-sample the four persistent editors for 300 seconds to detect lasting
   pressure, then close/release only the benchmark's leases.

Run the whole sequence three times. Every valid trial must pass. If a trial is
invalidated by an external disturbance, retain its artifacts, mark the reason,
and replace it; never silently discard a valid failure.

### 4. Dry versus representative scene

Use two isolated fresh processes, not a scene load/unload cycle in one process:
Unity can retain caches and assets after scene changes, making order part of the
result.

For both `dry` and `representative`:

1. start from the same warmed Library, commit, profile, and background policy;
2. open the deterministic state, wait for ready, stabilize 120 seconds, and
   collect the five-minute OS trace;
3. stop the capacity trace;
4. only then capture one Editor-target Memory Profiler snapshot with identical
   flags: managed objects, native objects, and native allocations, without native
   allocation call stacks;
5. wait for the snapshot callback before closing, then release and verify
   cleanup.

Store `.snap` files outside version control. Compare the pair in Memory
Profiler and export/report at least:

- total committed memory and the snapshot's accounted/unaccounted split;
- native, managed, graphics, and executable/mapped categories;
- top positive deltas by Unity object type and asset, especially textures,
  meshes, render textures, audio clips, and native allocations;
- the scene-minus-dry OS private-commit delta from the pre-snapshot windows.

Snapshot capture is deliberately after the OS gate because Unity documents that
capture locks execution and can take time. Editor snapshots are appropriate here
because editor capacity is the question; they must not be presented as shipped
Player memory. Unity explicitly notes that Play Mode cannot be separated from
the Editor in an Editor snapshot and that Player memory must be checked in a
Player build when that becomes the question.

## Result artifacts

Write ignored artifacts to
`results/editor-capacity/<candidate-sha>/<trial-id>/`:

- `manifest.json` — revisions, versions, state/profile, workload, counter and
  snapshot settings;
- `samples.jsonl` — UTC timestamp, phase, project, lease, PID, root private and
  working-set bytes, descendant totals, available bytes, committed bytes, commit
  limit, paging-file use, and pages input/sec;
- `summary.json` — all derived values plus explicit `pass`, `fail`, or `invalid`
  and reasons;
- Unity/test logs and test-run summary;
- a pointer to the ignored `.snap` file, never the snapshot itself in git.

The summary should carry byte values, not only rounded GiB, so later tooling can
recompute percentiles. Preserve the raw failed/invalid trials.

## Primary sources

- Microsoft: [`Process.PrivateMemorySize64`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.privatememorysize64)
  is equivalent to Process `Private Bytes` and requires `Refresh()` for a current
  value.
- Microsoft: [Memory Performance Information](https://learn.microsoft.com/en-us/windows/win32/memory/memory-performance-information)
  maps Private Bytes to `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage` and distinguishes
  it from working set.
- Microsoft: [About Performance Counters](https://learn.microsoft.com/en-us/windows/win32/perfctrs/about-performance-counters)
  recommends roughly one-second sampling and warns that legacy Process instances
  are non-unique.
- Microsoft: [Troubleshoot performance problems in Windows](https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/troubleshoot-performance-problems-in-windows)
  describes `Available MBytes` and gives at least 4 GB or 10% free as healthy.
- Unity 6.1: [Editor command-line arguments](https://docs.unity3d.com/6000.1/Documentation/Manual/EditorCommandLineArguments.html)
  defines `-projectPath`, batch mode, `-nographics`, and log routing used by the
  existing launchers.
- Unity Memory Profiler 1.1: [Collect memory data](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/snapshots.html),
  [capture snapshots](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/snapshot-capture.html),
  and [compare snapshots](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/snapshots-comparison.html).
- Unity 6: [`MemoryProfiler.TakeSnapshot`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot.html)
  documents capture flags, callback completion, capture cost, and the Editor vs
  Player boundary.
- Unity 6: [`EditorApplication`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorApplication.html)
  exposes `isCompiling` and `isUpdating` for the benchmark-ready signal.
