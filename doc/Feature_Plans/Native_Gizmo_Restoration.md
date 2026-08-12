# Native Gizmo Restoration

> STATUS: live arc — frozen 2026-08-07; build proceeds through issues #374, #375, and #376.

Parent: [Chart native gizmo restoration and painter removal](https://github.com/amindell11/astronomical-home/issues/357)

## Outcome

Native Unity gizmos become the sole diagnostic implementation for live Editor
inspection and recorded gameplay. Game View Recorder captures those same
gizmos over real production play. The painter registry, diagnostic-canvas
adapters, custom diagnostic gate, and offscreen diagnostic overlay disappear.

The regression's root cause is a parallel drawing and selection system that
turned native gizmo drawers into shallow adapters. The structural fix is
fix-ladder rung 1: delete the parallel representation so a diagnostic cannot
be implemented as a painter instead of a native gizmo.

## Locked design

### Native authority

- Diagnostic drawing uses `[DrawGizmo]`, `Gizmos`, and `Handles` directly.
- Ordinary Editor use is selected-only. Unity's per-component-type Gizmos
  controls are authoritative; no custom condition guards every drawer.
- Capture temporarily selects its runtime subjects and applies a code-defined
  set of Unity component types, then restores the previous Editor state.
- Policy and Observation share the `AICommander` type control. Trajectories,
  obstacles, and control readouts share `Navigator`. Missile capture enables
  both `Missile` and `Missiles` types.
- Existing legitimate per-subject detail controls remain local to their
  drawer. They are not a second global selection grammar.

### Capture module seam

An Editor-owned episode-capture module is attached by the Editor bootstrap
only for the capture lane. The runtime harness crosses one narrow lifetime
interface: begin an episode with trusted capture configuration, the live ship
pair, and its projectile coordinator; notify each fixed step; end the episode.
UnityEditor and Recorder concepts stay behind that seam.

The module owns Game View selection, type visibility, the presentation camera
and framing, Recorder lifetime, frame cadence, artifact output, and cleanup.
During the expansion slice the old offscreen recorder may coexist behind the
same harness call site. It is deleted, not retained as a second adapter, in
the contraction slice. Player-build and `-nographics` capture remain rejected
at the parse boundary. Batch-mode capture is structurally impossible — Unity
never resumes Recorder's `WaitForEndOfFrame` under `-batchmode` — so the
capture lane runs a windowed Editor launched by the test runner
(`unity_test_agent.ps1 -WithGraphics -Windowed`), with no MCP bridge and no
human interaction. Native capture profiles run with presentation disabled:
collider silhouettes and gizmo geometry are the footage.

`RL_HARNESS_GIZMOS` selects one code-defined capture profile such as
`steering`, `combat`, or `everything`. Profiles resolve to Unity component
types. Arbitrary painter atoms and a compatibility alias for
`RL_HARNESS_PAINTERS` are prohibited.

### Unity adapter and state transaction

Public Unity interfaces own component-type visibility, subject selection,
Game View type/focus/resolution, and Recorder. One internal adapter, pinned to
Unity 6000.1.8f1, owns the Game View master Gizmos switch and exact size-preset
restoration.

Before the first mutation, capture snapshots every touched persistent and
volatile state. One outer transaction restores Recorder state, application
background behavior, type/icon visibility, selection, Game View state, and
any window it created on success, failure, cancellation, assembly reload,
Play Mode exit, and Editor quit.

Persistent Unity settings are journaled before mutation and replayed on the
next launch after a hard process kill. Volatile object selection is not
journaled because destroyed runtime objects have no valid next-process
identity. The journal is private output owned by the capture module.

### Native diagnostic ownership

Each diagnostic lives with the Unity subject whose state it explains.
Pair-shaped ship diagnostics split along that ownership:

- a selected `Ship` drawer owns velocity, speed, intercept, firing envelope,
  range, and the ship-to-ship relation derived from the other selected ship;
- projectile-owned native drawers own projectile trails;
- capture keeps the two ships and current live projectiles selected using the
  projectile coordinator already supplied at the capture seam.

No drawer receives a painter context or reconstructs world-scoped services.
Pair-level lines draw once through deterministic subject ordering.

Collision-free labels use a narrow Editor-only readout-placement module with
stable semantic rows per ship. It owns placement and styling only; it is not
a drawing canvas and does not abstract lines, rings, shapes, or vectors.

### Parity target

Preserve current semantic diagnostics and painter-era improvements that are
independent of the painter abstraction:

- plane-native geometry and authoritative obstacle data;
- current anchored-policy reconstruction;
- collision-free ship readouts;
- edit-time LockOn and damage inspection where serialized state supports it;
- direct reuse of the exact converted obstacle geometry already retained in
  Navigator solver buffers.

Restore the native behavior lost or flattened by the migration:

- the fading policy action-history fan;
- Navigator-local detail controls, cost display, candidate handles, and
  control-panel placement;
- movement arrowheads and force distinctions;
- filled damage bars;
- camera-readable, deliberate laser-heat placement.

Keep the redundant solve-time obstacle copy deleted. Retire the old
`AIDebugSettings` gate, painter atoms, 3D shapes that are less truthful than
their GamePlane forms, and runtime exposure that existed only so painters
could compile in player assemblies.

## Prototype ruling

The existing graphics-batch Game View probe is sufficient feasibility proof:
it recorded real two-ship production gameplay with native `Gizmos` and
`Handles`; the user accepted the possible Windows Editor window and the
approximately 2.2-second cold activation; repeated Game View capture was
faster than the painter backend at steady state.

The selected-only profile transaction is production acceptance for #374,
not another throwaway prototype. Its integration test must pin the observed
Game View effect of `GizmoUtility`, because Unity's scripting documentation
describes that interface primarily in Scene View terms.

Build evidence (2026-08-11) sharpened the ruling: the accepted Editor window
is mandatory, not incidental, because Recorder starves without a rendering
Game View in `-batchmode`. The windowed lane is proven — a test-runner-launched
windowed Editor ran the full native-capture integration test green (frames,
manifest, and complete state restoration) with zero human interaction. The
build-slice fixes that unlocked it live on `task/native-gizmo-capture`:
Recorder output path assigned after serialized cadence, URP compatibility
mode with global-settings dirty-state restore, Prepare/Start in one fixed
step, and explicit Game View focus inside the transaction.

## Acceptance

- A real harness clip shows selected native diagnostics over production play.
- All touched Editor state restores under the locked lifecycle and recovery
  policy.
- Steady-state Game View capture is no more than 10% slower than painters;
  the accepted cold activation is documented rather than optimized here.
- Native live inspection retains or improves every diagnostic in the parity
  target.
- Repository search finds no surviving painter, diagnostic-canvas, custom
  diagnostic-gate, or offscreen diagnostic-overlay dependency after contract.
- The complete relevant test scope passes.

## Build slices

The tracker owns the slice specifications and blocking edges:

- [Capture selected native gizmos through the real harness](https://github.com/amindell11/astronomical-home/issues/374)
- [Restore all diagnostics as native gizmos](https://github.com/amindell11/astronomical-home/issues/375)
- [Remove the painter system completely](https://github.com/amindell11/astronomical-home/issues/376)

The arc-completing contraction deletes this transient brief.

## Evidence

- [Unity 6 native gizmo control research](https://github.com/amindell11/astronomical-home/blob/research/native-gizmo-control/doc/Feature_Plans/Native_Gizmo_Control_Research.md)
- [Native-gizmo parity audit](https://github.com/amindell11/astronomical-home/blob/research/native-gizmo-parity/doc/Feature_Plans/Native_Gizmo_Parity_Audit.md)
- [Navigator obstacle-scan stability research](https://github.com/amindell11/astronomical-home/blob/research/navigator-obstacle-scan/doc/Feature_Plans/Navigator_Obstacle_Scan_Stability_Research.md)

## Out of scope

- Player-build or `-nographics` gizmo capture.
- New diagnostic content before parity and painter removal complete.
- Another generalized renderer or second capture backend.
- Shared Unity Editor startup optimization beyond the accepted activation
  cost.
