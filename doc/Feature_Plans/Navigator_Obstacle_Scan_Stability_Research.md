# Navigator Obstacle-Scan Stability Research

> Throwaway research context for [#371](https://github.com/amindell11/astronomical-home/issues/371). Delete after its finding is absorbed into the native-gizmo build brief and PR history.

## Question

Can `Scout.ObstacleScan` change between an MPC solve and the following gizmo draw enough for `NavigatorObstaclePainter` to show obstacles other than those the solver used?

## Resolution

Yes. The direct Scout view is structurally one scan ahead of the most recent solve at normal render-time gizmo drawing. Native Navigator obstacle gizmos should draw the retained converted obstacle buffer on `SolverBuffers`, not call `Scout.ObstacleScan`.

No raw debug copy needs to return. `SolverBuffers.Obstacles` plus `ObstacleCount` is already the exact, durable result of the solve's synchronous conversion and is a better diagnostic source than both the current painter and the pre-#349 `StoreDebugObstacles` copy.

## Evidence

### The Scout property is a live view, not a snapshot

`Scout` owns one `DetectedObstacle[128]` named `mergedObstacles`. Every `Scout.Update` calls `BuildMergedObstacles`, resets `mergedObstacleCount`, and overwrites that same array with the latest static-obstacle query followed by current nearby ships. `Scout.ObstacleScan` constructs a small struct containing the array reference and current count:

```text
ObstacleScan { DetectedObstacle[] buffer; int count; }
```

Copying the struct, including through `MpcInputs`, does not copy the array. The old contents remain stable only until the next `Scout.Update` overwrites them.

Sources: `AI/Scout.cs:63-104`, `AI/Scanning/ObstacleScanner.cs:90-102`.

### Solve and draw straddle the overwrite

The relevant player-loop sequence is:

```text
FixedUpdate phase
  AICommander.FixedUpdate
    Navigator.ComputeCommand
      scan = Scout.ObstacleScan       (view produced by the prior Update)
      Mpc.Plan(scan)
        SolverBuffers.ConvertObstacles (synchronous copy)

Update phase
  Scout.Update
    obstacleScanner.Scan
    BuildMergedObstacles              (overwrites the shared array)

render/editor gizmo phase
  NavigatorGizmos
    NavigatorObstaclePainter.Draw
      nav.scout.ObstacleScan           (new Update's contents)
```

`[DefaultExecutionOrder(-80)]` on Scout and `10` on AICommander do not move an `Update` callback ahead of the preceding `FixedUpdate` phase. They only order callbacks within the applicable player-loop phase. Game View/Scene View gizmos are evaluated for rendering after that frame's `Update` callbacks.

`CapturePacing.Locked` makes this mismatch consistent rather than removing it: one fixed step uses the previous frame's scan, the following Update refreshes the scan, then Game View records the gizmos.

The old offscreen harness can appear correct by accident. Its `recorder.Step` callback runs from a coroutine resumed by `WaitForFixedUpdate`, before the next `Update`, so the painter often reads the same still-unmodified buffer the solve just consumed. That timing does not carry into native Game View rendering.

Sources: `AI/AICommander.cs:97-114`, `AI/Navigator.cs:75-116`, `AI/Scout.cs:63-74`, `Capture/CapturePacing.cs`, `RLHarness/Episodes/EpisodeLoopDriver.cs:71-81`.

### The difference is material, not merely theoretical

The merged scan contains dynamic ships rebuilt from their current transforms and kinematics. After a fixed simulation step, the following `Scout.Update` can change their positions, count, and ordering before gizmos draw. Static contents can also change when the arena obstacle field changes through destruction, reset, or streaming.

The current painter also ignores three solver-side transformations:

- `enableObstacleAvoidance == false` converts the solve's obstacle count to zero, while the painter still draws the Scout scan;
- multi-sphere mode expands one detected elongated rock into two or three lobe circles;
- the solver admits expanded obstacles atomically into a 96-row buffer and can omit later raw obstacles that do not fit.

Consequently, even a byte-stable Scout scan is not necessarily the collision geometry the solver evaluated. The current painter's claim that it draws "the collision boundaries the MPC actually tests" is false in these cases.

Sources: `AI/Navigation/MPC/BurstSolver.cs:150-203,362-402`; `Editor/Tests/EditMode/MultiSphereObstacleEditModeTests.cs` pins expansion, kill-switch, and atomic-admission behavior.

## Correct native source

`SolverBuffers.ConvertObstacles` synchronously copies the solve input into its persistent `NativeArray<ObstacleData>` before scheduling/evaluating candidates. The array and `lastObstacleCount` remain unchanged until the next solve rewrites them or the solver is disposed. Unity invokes the solve and gizmo callbacks serially on the main thread, so a native drawer can safely inspect the last completed solve after checking that the solver/buffer exists.

Use:

- `nav.solver.Obstacles[0..nav.solver.ObstacleCount]` for exact positions and radii;
- the Navigator's retained `config`, `lastControl`, and predicted state for the existing hull/bite-range overlays;
- no Scout reference and no `DetectedObstacle` copy.

This also displays the exact lobe expansion, capacity admission, and avoidance-disabled state used by the MPC. It preserves the #349 improvement of eliminating per-solve managed copying while fixing its mistaken lifetime assumption.

The drawer should hide obstacle details when Navigator is idle, before the first completed solve, or after solver reset/disposal; otherwise it would honestly show a retained buffer, but from an obsolete solve. That is an implementation invariant, not a user-facing design fork.

## History and test coverage

- Before #349 (`5ac20a14`), `Navigator.ComputeCommand` called `StoreDebugObstacles(scan)` immediately before `Mpc.Plan`. That managed copy matched the raw input and therefore avoided the Update/render overwrite.
- #349 deleted the copy on the premise that the painter could read "the Scout scan the Navigator already feeds the solver." It is the same storage source, but not the same generation at gizmo time.
- The old copy still did not reflect multi-sphere expansion, the 96-row admission limit, or avoidance-disabled conversion. Returning it would restore temporal consistency but not exact solver parity.
- `MultiSphereObstacleEditModeTests` verifies converted counts and admission semantics. `ScannerPlayModeTests` verifies that Scout eventually receives field data. No current test pins scan generation across FixedUpdate/Update/render or asserts which obstacle data a gizmo uses.

## Decision status

No user decision remains. The deep, exact source already exists behind the solver interface: native Navigator obstacle gizmos should observe the last completed solver buffer and the build should keep `StoreDebugObstacles` deleted.
