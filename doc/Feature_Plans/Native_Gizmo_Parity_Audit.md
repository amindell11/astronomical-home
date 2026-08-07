# Native Gizmo Parity Audit

> Throwaway research context for [#359](https://github.com/amindell11/astronomical-home/issues/359). Delete after the initiative absorbs its findings into the build brief and PR history.

## Scope and baselines

This audit compares `origin/main` at `3ae3af80` with three layers of history:

- `6f4b6615`, the immediate parent of #347, with `0bed27c1` used as the equivalent diagnostics baseline before the unrelated intervening RL change;
- `5888c526^`, before the harness painter/diagnostic-canvas seam (#246), for the last fully native `PolicyGizmos` and the original test-only capture overlay;
- `15e392a8`, which introduced the offscreen capture camera, `CaptureDraw`, capture scenarios, and frame/manifest contract (#154).

The result is not a revert recipe. Native gizmos should regain ownership of diagnostic drawing while retaining useful behavior added during the painter era. The capture lane, pacing, framing intent, artifact naming, and assembly script are independently useful and should survive behind the Game View Recorder path.

## Findings at a glance

There are 12 registered painters totaling about 900 lines, 11 painter-backed `[DrawGizmo]` hooks, and one pair-shaped painter with no native hook. The painter interface has only one real implementation path per frontend: `CaptureDraw` and `GizmoCanvas`. Deleting the seam makes most complexity vanish rather than reappear across unrelated callers, so it is a shallow module by the deletion test.

The recent migration did more than move drawing code:

- it deleted one important visualization (the policy action-history fan);
- it flattened several native shapes and erased movement arrowheads;
- it replaced per-instance Navigator controls with global painter atoms and constants;
- it moved editor-only solve/debug state into player builds solely so painters could compile in `Game.Core`;
- it introduced shared readout stacking and removed an unnecessary per-solve obstacle copy, both worth preserving;
- it broadened some drawers from selected-only to selected-or-global and made an empty per-worktree `EditorPrefs` gate authoritative, which explains the observed enable/disable friction.

## Complete parity inventory

### `ShipDiagnosticsPainter` (`ship-diagnostics`)

- **Origin:** No native gizmo. #154 introduced the pair-shaped `ShipDiagnosticsOverlay` for capture tests; #246 moved the same behavior into `ShipDiagnosticsPainter` and made it available to the harness.
- **Current:** For both ships, draws velocity arrows and speed text, exact Gunner intercept aim colored by primary-weapon envelope, fire-range rings, pair LOS, and live-projectile trails. `Readout` now places speed in the shared ship stack.
- **Parity ruling:** This is painter-only diagnostic content and must not disappear. It needs a native editor home, but a literal `[DrawGizmo]` translation is not obvious: LOS and intercept are pair-shaped, and projectile trails depend on `IProjectileService`, not one inspected component.
- **Useful behavior:** Preserve exact `Gunner.AimPoint`, the non-mutating `Sight.InEnvelope` check, LOS coloring, velocity colors, range rings, and projectile trails. Preserve the current avoidance of `Sight.Evaluate`, which would mutate firing-path state.
- **Fog:** Decide whether the native home is a selected-subject capture drawer owned by the capture module, a `Ship` drawer that discovers the other selected ship, or smaller per-type drawers plus one pair overlay. This is the only painter whose ownership cannot be recovered from a pre-painter gizmo file.

### `PolicyPainter` -> `PolicyGizmos` (`policy`)

- **Native baseline:** #222's `PolicyGizmos` drew commanded velocity, the newest commanded-facing ray, actual nose, range/churn/weight/error label, and an orange fading fan of recent facing commands. `AIDebugSettings.policyFanDepth` controlled fan depth.
- **Current:** The shim targets `AICommander`, then delegates to the painter. Current anchored actions are reconstructed in the enemy frame from radial/tangential speed and `facingOffsetRad`; the newest rays, nose, and status text remain. #350 moved the label into shared readout stacking.
- **Lost/changed:** #347 deliberately deleted the fan, its depth setting, and the painter parameter. Atom gating replaced the Policy channel. `AICommander` also hosts the observation drawer, so Unity type-level visibility will make policy and observation one reliable coarse toggle unless one of them finds a different target type.
- **Useful behavior:** Retain current anchored-action reconstruction and readout data, but restore the fading action-history fan over the current `PolicyAction` shape. Native code from before #246 cannot be pasted: it references obsolete `worldVelocity` and `facingRad` fields replaced by the anchored action schema in #250.

### `ScoutPainter` -> `ScoutGizmos` (`scout-scan`)

- **Native baseline:** Nearby-ship and asteroid-cover wire spheres, the scanner's fixed query wire cube, and one wire sphere at each detected collider.
- **Current:** Same information in GamePlane space: two rings, an axis-aligned rect, and rings using each detected obstacle's solver position/radius. The shim is play-mode-only.
- **Lost/changed:** 3D spheres/cube became plane-native rings/rect; gating moved from the Scanning channel to the empty-by-default atom.
- **Useful behavior:** Keep the plane-native shapes and authoritative `DetectedObstacle.position`; they match the top-down game and the data consumed by navigation better than collider-transform spheres.

### `LockOnPainter` -> `LockOnSensorGizmos` (`lock-on`)

- **Native baseline:** Cone ray fan, max-range wire sphere, forward ray, target line/ring, 16-segment lock-progress arc, and state/lock/cooldown label at the fire point. It was not under `AIDebugContext` and worked in edit mode when fields were wired.
- **Current:** Same semantic set in GamePlane space, with ring/disc and canvas arc primitives. The label is anchored to the parent ship's shared readout stack. The shim retains edit-mode support by deriving the ship from transforms before `Awake` and uses cached `selfShip` in play mode.
- **Lost/changed:** Sphere became ring; progress arc is canvas-segmented in capture and `Handles.DrawWireArc` live; label moved away from the fire point. `Cooldown` still maps to gray through the default case.
- **Useful behavior:** Keep edit-mode support, GamePlane geometry, and ship-level readout placement. The runtime `internal Ship selfShip` field exists only for this editor shim and can return to private/local ownership when the native drawer's lookup contract is chosen.

### `NavigatorTrajectoryPainter` -> `NavigatorGizmos` (`mpc-trajectories`)

- **Native baseline:** Candidate fan with cost-rank alpha and selected-candidate highlight; predicted trajectory colored optionally by obstacle/collision cost; filled node spheres; planned-yaw ticks; periodic cost labels; enemy rollout; and clickable candidate terminal handles whose breakdown appeared in `NavigatorEditor`.
- **Current:** The same broad set is drawn in plane space, but all trajectory subviews are bundled whenever the atom is active. Candidate sample count `32`, alpha falloff `2`, label step `5`, and panel offset are constants. Nodes are rings rather than filled spheres. Cost coloring is always on. Candidate terminal interaction remains, but `NavigatorEditor` is coupled to `DiagnosticGate.IsActive(mpc-trajectories)`.
- **Lost/changed:** Deleted per-Navigator `showCandidateTrajectories`, `showTrajectoryCosts`, `candidateSampleCount`, `candidateAlphaFalloff`, `labelStep`, and the interaction's local enable condition. This is the largest integration regression: opening one atom now draws a 32-candidate fan plus the chosen path and enemy rollout for every admitted Navigator.
- **Useful behavior:** Preserve the current plane-space math and cost-ranked subsampling. Restore legitimate local detail controls and candidate selection through the native Navigator drawer/editor. Under the approved type-level model, the Unity toggle controls the whole Navigator diagnostic; local controls decide its expensive/detail subviews.

### `NavigatorObstaclePainter` -> `NavigatorGizmos` (`mpc-obstacles`)

- **Native baseline:** Ship radius plus, for a copied solve-time obstacle buffer, unbanked hull, current-bank hull, and turn-away bite-range wire spheres. `showObstacleCosts` controlled the subview.
- **Current:** Equivalent GamePlane rings, but reads `nav.scout.ObstacleScan` directly; `showObstacleCosts` is gone. It shares the Navigator hook with trajectories and the still-native control panel.
- **Lost/changed:** Local control disappeared; 3D spheres became rings. The debug snapshot buffer no longer guarantees a frozen solve-time copy, but it also no longer allocates/copies on every editor solve.
- **Useful behavior:** Keep removal of `StoreDebugObstacles` and its buffer unless evidence shows the scanner can change between solve and draw. Expose the authoritative scan to the editor narrowly rather than widening all Navigator solve state for player-compiled painters.

### Native-only Navigator control panel (`mpc-controls`)

- **Origin/current:** Camera-facing THR/STR/YAW bars were intentionally left in `NavigatorGizmos`; there is no painter because billboard UI had no diagnostic-canvas form.
- **Lost/changed:** `showControlInputs` and `controlPanelOffset` became a global atom and constants. It is omitted from captured painter frames.
- **Parity ruling:** Keep it native, restore its legitimate local placement/detail control, and let Game View capture include it naturally. Under type-level visibility it should be part of the Navigator diagnostic rather than a fake painter atom.

### `GunnerTargetingPainter` -> `GunnerGizmos` (`gunner-targeting`)

- **Native baseline:** Gray gunner-to-target line, red 2x2 wire cube, red aim ray, fire-point-to-target LOS colored clear/blocked, and cyan fire-point wire sphere.
- **Current:** Same semantics in plane space; target cube is an outline rect and fire-point sphere is a ring. The shim adds an explicit play-mode gate.
- **Lost/changed:** 3D marker shapes became flat; the Targeting channel and its settings asset disappeared.
- **Useful behavior:** Retain GamePlane rect/ring and the exact `TargetingMath.IsLineClear` check in the native drawer.

### `ObservationPainter` -> `AICommanderGizmos` (`observation`)

- **Native baseline:** Reconstructed tactical-observation self forward/velocity, target line/ring/facing, threat rings/relative velocity, and obstacle spheres. A per-commander `ThreatScanner` weak cache and shared `TacticalObservation` snapshot lived in the editor drawer.
- **Current:** Semantically equivalent GamePlane lines/rings; scanner cache and snapshot moved into the runtime painter so capture can execute them.
- **Lost/changed:** 3D spheres became rings. Observation and Policy now collide at the same `AICommander` type toggle. Runtime painter execution means drawing performs a threat scan; native selected-only behavior naturally limits this cost.
- **Useful behavior:** Move scanner/snapshot ownership back into the editor drawer and keep the plane-space round-trip proof unchanged.

### `MissilesPainter` -> `MissileGizmos` + `MissilesGizmos` (`missiles`)

- **Native baseline:** Each missile had body and explosion wire spheres, velocity ray, target line, and a play-mode distance label. Each launcher had an ammo label two units above its fire point. Both hooks admitted selected and non-selected objects.
- **Current:** Missile spheres became rings; the launcher label moved into the parent ship's shared readout stack and gained a `Missiles` heading. A capture instance discovers all live missiles through `IProjectileService`. The launcher shim caches its parent `Ship` and is play-mode-only.
- **Lost/changed:** Launcher label position/selection semantics changed. One painter atom maps to two Unity target types (`Missile` and `Missiles`), so a capture profile must include both. Per-missile distance remains a positional label.
- **Useful behavior:** Keep the improved ammo wording, ship-level stacking, plane rings, and service-backed discovery only if the native capture path cannot rely on ordinary missile drawers. Game View Recorder should make the service traversal unnecessary.

### `LaserHeatPainter` -> `LasersGizmos` (`laser-heat`)

- **Native baseline:** Selected-only. A three-pixel AA heat bar sat at `parent.position + parent.right * 1.5`, with text above it.
- **Current:** Selected-or-global through the gate. The bar is a thin canvas line at a fixed plane `+X` offset from `lasers.transform`; text moved to the parent ship's readout stack. Parent ships are weak-cached.
- **Lost/changed:** Native line width, ship-local right offset, and exact label/bar grouping were lost. The fixed plane offset can put the bar on a different side as the ship turns.
- **Useful behavior:** Preserve stacked heat text, but restore a camera-readable native bar and a deliberate ship-relative or screen-relative placement.

### `MovementForcesPainter` -> `MovementControllerGizmos` (`movement-forces`)

- **Native baseline:** Normalized thrust/strafe/boost arrows with distinct sphere/cube heads via `SuperGizmos`, plus yaw ray and wire arc. `showMovementGizmos` and `movementGizmoScale` were per mover.
- **Current:** Forces are plain canvas vectors in live gizmos because `GizmoCanvas.Vector` is only a line; yaw remains a native `Handles` arc through the canvas. `showMovementGizmos` was deleted; scale remains. Debug force telemetry now updates and exists in player builds.
- **Lost/changed:** All arrowheads and thrust-vs-strafe shape distinction disappeared. The local master toggle disappeared.
- **Useful behavior:** Restore native arrowheads and yaw arc. The user-approved Unity type toggle can replace the redundant local master toggle; `movementGizmoScale` remains a legitimate local visual control. Re-guard force telemetry and `DebugForces` with `UNITY_EDITOR` once no player-compiled painter reads it.

### `DamageBarsPainter` -> `DamageControllerGizmos` (`damage-bars`)

- **Native baseline:** Selected-only, filled shallow 3D shield/health tracks and fills, plus centered numeric labels. It also worked in edit mode.
- **Current:** Flat outline rect tracks/fills and shared ship readouts; selected-or-global through the gate; explicitly play-mode-only. Parent ships are weak-cached.
- **Lost/changed:** Filled bars became wire outlines, depth disappeared, and edit-mode inspection was lost. Shared stacking prevents numeric text from colliding with other status diagnostics.
- **Useful behavior:** Retain shared readout placement and plane alignment, but restore filled, immediately legible bars and edit-mode behavior where serialized damage state supports it.

## Gate/canvas users that are not painters

- `ObserverCamGizmos` became a `GizmoCanvas.Rect` client under the `cam-bounds` atom. Its original native drawer was selected-only and explicitly checked `SecondarySubjects.Count`; #348 removed the public `SecondarySubjects` accessor after the rewritten drawer relied only on `TryGetBoundaryAroundAllSubjects`. Restore native line drawing, but keep the accessor deleted unless another real caller needs it.
- `NavigatorEditor` uses `DiagnosticGate` to admit candidate handles and the selected-candidate inspector panel. It must move back to Navigator-local detail state or a native type-visibility query before `DiagnosticGate` can be deleted.
- `DiagnosticsMenu` and `DiagnosticGate` have no role after Unity's type controls become authoritative. Do not restore `AIDebugSettings`, `AIDebugContext`, or the settings asset: that would replace one parallel gate with its predecessor.
- `GizmoCanvas` contains the only useful behavior not reducible to direct `Gizmos`/`Handles` calls: cross-drawer ship readout stacking. If stacking is retained, extract a narrow editor-only readout-placement module; do not preserve `IDiagnosticCanvas` or a general drawing adapter for it.

## Runtime and serialized residue

The following changes were made to let painters compile and run in player assemblies. They should be reviewed for rollback after native drawers own the visuals:

| Location | Painter-era change | Native disposition |
| --- | --- | --- |
| `MovementController` | `dbgThrust`, `dbgStrafe`, `dbgBoost`, `dbgYaw` and their writes moved outside `UNITY_EDITOR`; `showMovementGizmos` deleted | Re-guard telemetry/writes; keep scale. Do not automatically restore the redundant master toggle. |
| `Navigator` | Solver/config/sequence/state accessors, selected-candidate scratch, and candidate selection moved outside `UNITY_EDITOR` | Re-guard everything used only by native editor drawers. |
| `Navigator` | `scout` widened from `protected` to `protected internal` | Replace with the narrowest editor-visible observation source; keep direct scan reuse if possible. |
| `Navigator` | Seven local visualization controls were deleted | Restore the detail/tuning controls called out above, not a second global gate. |
| `Navigator` | `StoreDebugObstacles`, `dbgObstacles`, and `dbgObstacleCount` deleted | Keep deleted unless a solve-vs-draw mismatch is demonstrated. |
| `Cost` / MPC `Types` | `CostBreakdown` and `Cost.EvaluateBreakdown` moved outside `UNITY_EDITOR` | Re-guard after confirming only editor diagnostics/tests consume them. Current non-painter uses are editor-gated Navigator diagnostics and editor tests. |
| `LockOnSensor` | cached parent ship promoted from an `Awake` local to `internal Ship selfShip` | Return to private ownership or delete if the native drawer safely derives its subject. |
| `ObserverCam` | `SecondarySubjects` public read accessor deleted | Keep deleted; native bounds drawing can call `TryGetBoundaryAroundAllSubjects`. |
| `AIDebugSettings` | channels, ScriptableObject, context lookup, and tracked asset deleted | Keep deleted. Unity type controls replace them. |

The old serialized Navigator fields and `showMovementGizmos` may still exist as unknown serialized data in prefabs/scenes after code removal; Unity ignores those entries. A native restoration should inspect serialization history before renaming/retyping restored fields, but it need not resurrect the exact old fields merely to consume stale YAML.

## Exact deletion dependency graph

Deletion should proceed from callers inward, after Game View capture proves the replacement interface:

1. **Replace painter selection at the harness boundary.** Remove `SessionSpec.painters`, `ParsePainters`, `RL_HARNESS_PAINTERS`, preset expansion, and their EditMode tests/docs. Introduce only the approved gizmo-type capture profile contract; there is no compatibility alias.
2. **Replace painter invocation.** Remove `HarnessSessionHost.BuildPainterDraw`, `PainterContext`, the `IDiagnosticPainter[]`, and the `Action<CaptureDraw>` passed to `CaptureRecorder.Step`. Keep episode selection, `RecordPlan`, capture lane/client, presentation enabling, and fixed-step cadence unless the Game View recorder's interface requires a focused change.
3. **Replace the offscreen backend.** `CaptureRecorder` currently owns an offscreen `Camera`, `RenderTexture`, PNG readback, and `CaptureDraw`. Rework or replace it with the proven Game View Recorder module, retaining the output/manifest contract where useful. Delete `CaptureDraw` and its `.meta` only after `CaptureRecorder.Step` and scratch/scenario guidance no longer accept draw callbacks.
4. **Restore native domain drawers.** Rewrite the 11 shims with direct `Gizmos`/`Handles` drawing and add the native home for ship diagnostics. Do not copy the painter constructors/caches; `[DrawGizmo]` already supplies the subject. Restore Policy and Navigator behavior explicitly rather than by file checkout.
5. **Unhook the editor gate.** Move `NavigatorEditor` interaction off `DiagnosticGate`; rewrite `ObserverCamGizmos`; then delete `DiagnosticsMenu`, `DiagnosticGate`, `GizmoCanvas`, their `.meta` files, and the `Editor/Diagnostics.meta` folder marker if empty.
6. **Delete the runtime painter module.** Delete `Diagnostics/DiagnosticPainters.cs`, `Diagnostics/IDiagnosticCanvas.cs`, all 12 `Diagnostics/Painters/*.cs`, their `.meta` files, `Painters.meta`, and `Diagnostics.meta` if the folder becomes empty.
7. **Narrow runtime exposure.** Re-guard or privatize the residue listed above only after every native drawer compiles against its intended editor interface.
8. **Update capture scenarios and tests.** `TwoShipSkirmishScenario` currently calls `ShipDiagnosticsPainter.Draw`; `RLCapturePlayModeTests` constructs painter names; `CaptureScenario` and the game-capture skill teach `CaptureDraw` callbacks. Convert them to selection/profile-driven native capture and retain assertions for frames, manifest, JSONL rows, cleanup, and state restoration.
9. **Delete stale vocabulary and arc context.** Remove/update the glossary entries for `painter`, `diagnostic canvas`, and painter-defined `observation environment`; delete `Gizmo_Painter_Migration.md`; update `training/rl/README.md` and `.claude/skills/game-capture/SKILL.md` to native-gizmo terminology.

Direct references that prevent deletion today are:

```text
SessionSpec/tests/docs -> DiagnosticPainters registry and preset names
HarnessSessionHost -> PainterContext -> DiagnosticPainters -> 12 painters
HarnessSessionHost -> Action<CaptureDraw> -> CaptureRecorder.Step
CaptureRecorder -> CaptureDraw -> IDiagnosticCanvas
12 painters -> IDiagnosticPainter + IDiagnosticCanvas
11 gizmo shims -> painter static Draw + GizmoCanvas + DiagnosticGate
NavigatorEditor -> DiagnosticGate + DiagnosticPainters
ObserverCamGizmos -> GizmoCanvas + DiagnosticGate
DiagnosticsMenu -> DiagnosticPainters + DiagnosticGate + native-only atom constants
TwoShipSkirmishScenario -> ShipDiagnosticsPainter.Draw
```

`Game.RLHarness.Editor.asmdef` should continue referencing `Game.Capture.Editor` if that assembly owns the new Game View recorder. The existing `com.unity.recorder` 5.1.2 package is already in the project; this initiative does not require a new package dependency.

## Why literal reverts are unsafe

- Reverting only #347-#351 leaves #246's `IDiagnosticCanvas`, `DiagnosticPainters`, `PolicyPainter`, `ShipDiagnosticsPainter`, `GizmoCanvas`, and offscreen `CaptureDraw` backend intact. It does not achieve painter removal.
- Reverting #246 wholesale also removes the harness capture lane, `CaptureClient`, `ISessionClient` extraction, recording-axis parsing, graphics launch behavior, capture tests, and current skill/docs. Those are independent capabilities that the Game View implementation should reuse.
- Pre-#246 `PolicyGizmos` does not compile against current anchored `PolicyAction`: `worldVelocity` and `facingRad` no longer exist. Its fan and rays must be translated onto radial/tangential velocity and `facingOffsetRad`.
- Reverse-applying #349 would restore solve-time obstacle copies and reintroduce all deleted Navigator serialized toggles wholesale, while also moving runtime cost types back under editor guards before replacement drawers are ready. Preserve selected improvements deliberately.
- Reverse-applying #348 would restore `ObserverCam.SecondarySubjects` only for an old count check and would put movement telemetry back under editor guards before the capture backend changes. It would also restore filled damage bars and arrowheads, but mixed with unrelated regressions.
- Reverse-applying #350/#351 would reintroduce colliding status labels. Readout stacking needs a small native successor before canvas deletion.
- Restoring the old `AIDebugSettings` asset would contradict the approved Unity-authoritative, selected-only type control model and recreate the same reliability problem under another name.

## Resolution and remaining fog

The parity target is now concrete: direct native drawers should preserve all current semantic diagnostics, restore the policy fan, Navigator local detail/interaction controls, movement arrowheads, filled damage bars, and camera-readable heat placement, while keeping plane-native geometry, authoritative obstacle data, edit-mode LockOn behavior, and collision-free ship readouts. `ShipDiagnosticsPainter` is the only diagnostic without a preexisting native owner.

The audit surfaces four decisions for later tickets:

1. **Pair-shaped ship diagnostics:** choose its native owner and how selected capture subjects provide the pair and projectile access.
2. **Readout stacking:** choose a narrow editor-only placement module or a simpler per-drawer layout convention. Keeping `GizmoCanvas` solely for stacking would preserve the shallow seam.
3. **Same-type grouping:** approve the concrete profile mapping where Policy + Observation share `AICommander`, trajectories + obstacles + controls share `Navigator`, and missiles require `Missile` + `Missiles`.
4. **Obstacle snapshot semantics:** verify whether direct `Scout.ObstacleScan` can change between solve and gizmo draw; restore a snapshot only if that mismatch is observed.

Everything else is implementation sequencing rather than unresolved architecture.
