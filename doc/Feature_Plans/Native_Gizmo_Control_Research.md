# Unity 6 native gizmo control research

> THROWAWAY RESEARCH CONTEXT for [#358, Establish the Unity 6 native gizmo control contract](https://github.com/amindell11/astronomical-home/issues/358). This note belongs only on `research/native-gizmo-control`; it is evidence for the wayfinder resolution, not a production plan or living reference.

Research target: Unity `6000.1.8f1` and Recorder `5.1.2`, as pinned by `ProjectVersion.txt` and `Packages/manifest.json` on 2026-08-07.

## Resolution

Native-gizmo capture is viable with supported Unity interfaces for every operation except the Game View's master **Gizmos** switch and exact Game View size-preset restoration. Those two facts live on the internal `UnityEditor.GameView` type. A capture implementation therefore needs one small, Unity-version-pinned internal adapter; it does not need a parallel painter/canvas/rendering system.

The supported core is:

- `GizmoUtility` / `GizmoInfo` enumerate, snapshot, enable, disable, and restore gizmos and icons by component target type.
- `Selection.objects` and `Selection.activeObject` select the runtime subject GameObjects that selected-only drawers target, and restore the previous selection.
- `PlayModeWindow` switches the selected play-mode view to Game View and reads/sets its focus and pixel resolution.
- Recorder's public `GameViewInputSettings`, `RecorderControllerSettings`, and `RecorderController` record the rendered Game View during Play Mode.
- `-batchmode` with a graphics device can drive this path. `-nographics` cannot: it explicitly prevents graphics-device initialization. A Windows Editor window may still appear, and the full Editor startup remains part of cold capture.

Two independent switches must be on for a drawer to appear:

1. the target component type's `gizmoEnabled` state; and
2. the selected Game View's master `drawGizmos` state.

For `[DrawGizmo(GizmoType.Selected, typeof(T))]`, the subject GameObject must also be in the Editor selection. The capture profile therefore controls component **types**, while subject selection controls component **instances**.

## Supported public contract

| Need | Supported interface | Exact behavior and constraint |
|---|---|---|
| Enumerate registered types | `GizmoUtility.GetGizmoInfo()` | Returns `GizmoInfo[]` for every component type with a gizmo or icon. This is a complete pre-mutation snapshot object, including hidden annotation identity used by `ApplyGizmoInfo`. |
| Query one target type | `GizmoUtility.TryGetGizmoInfo(Type, out GizmoInfo)` | Accepts a `Component` type. A capture profile should fail at startup if a declared target has no registered gizmo, rather than silently producing incomplete footage. |
| Toggle one target type | `GizmoUtility.SetGizmoEnabled(Type, bool, addToRecentlyChanged: false)` | One bit per component target type. Every `[DrawGizmo]` method targeting the same type shares it; Unity offers no drawer/method-level bit. Passing `false` avoids polluting the user's **Recently Changed** list. |
| Snapshot/restore all type and icon bits | retain the `GizmoInfo[]`; restore each with `GizmoUtility.ApplyGizmoInfo(info, false)` | `ApplyGizmoInfo` applies both the saved gizmo and icon bit using the saved internal annotation identity. This also works for built-in component entries whose `script` is null. |
| Select capture subjects | `Selection.objects`, plus `Selection.activeObject` | Assign the runtime subject GameObjects after they spawn. Multi-selection activates selected-only drawers on every selected subject. Snapshot both values before replacement and restore them afterward. A destroyed prior selection object cannot be restored without a stronger identity/reload scheme. |
| Choose Game rather than Simulator | `PlayModeWindow.GetViewType()` / `SetViewType(GameView)` | Public and documented in Unity 6. Recorder 5.1.2 calls these methods itself before Game View input begins. |
| Read/set render resolution | `PlayModeWindow.GetRenderingResolution` / `SetCustomRenderingResolution` | Restores the same pixels, but setting a custom resolution does not restore the exact named aspect/preset selection. Recorder explicitly documents that it does not restore the old Game View resolution. |
| Read/set play-mode focus behavior | `PlayModeWindow.GetPlayModeFocused()` / `SetPlayModeFocused(bool)` | Recorder calls `SetPlayModeFocused(true)` from its window workflow. A scripted transaction can snapshot and restore this public state. |
| Record Game View | `GameViewInputSettings`; `RecorderController.PrepareRecording()`, `StartRecording()`, `StopRecording()` | Works only in Play Mode. `StartRecording()` reports failure with `false`; callers must treat that as failure and still call `StopRecording()` in cleanup. |
| Ask whether the currently rendering view wants gizmos | `Handles.ShouldRenderGizmos()` | Public, but read-only and render-context-dependent. Outside a Game/Scene render it can return false because there is no current rendering view, so it is not a dependable snapshot getter for the selected Game View. |

The `PlayModeWindow` methods have one important side effect not expressed by their public signatures: every getter and setter first calls Unity's internal `GetOrCreateWindow()`. Even `GetViewType()` and `GetRenderingResolution()` create a Game View when no play-mode view exists. The internal adapter must therefore check `PlayModeView.GetMainPlayModeView()` before the first public `PlayModeWindow` call and record whether the transaction owns the newly created window.

Official documentation:

- [GizmoUtility](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GizmoUtility.html), [GetGizmoInfo](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GizmoUtility.GetGizmoInfo.html), [SetGizmoEnabled](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GizmoUtility.SetGizmoEnabled.html), and [ApplyGizmoInfo](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GizmoUtility.ApplyGizmoInfo.html)
- [GizmoInfo](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GizmoInfo.html)
- [DrawGizmo](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/DrawGizmo.html) and [GizmoType](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/GizmoType.html)
- [Selection.objects](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Selection-objects.html) and [Selection.activeObject](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Selection-activeObject.html)
- [PlayModeWindow](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/PlayModeWindow.html)
- [Handles.ShouldRenderGizmos](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Handles.ShouldRenderGizmos.html)
- [Game View](https://docs.unity3d.com/6000.1/Documentation/Manual/GameView.html) and the shared [Gizmos menu](https://docs.unity3d.com/6000.1/Documentation/Manual/GizmosMenu.html)
- Recorder 5.1 [GameViewInputSettings](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/api/UnityEditor.Recorder.Input.GameViewInputSettings.html), [RecorderController](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/api/UnityEditor.Recorder.RecorderController.html), [command-line recording](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/CommandLineRecorder.html), and [video recording](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecordingVideo.html)
- [Unity Editor command-line arguments](https://docs.unity3d.com/6000.1/Documentation/Manual/EditorCommandLineArguments.html)

### Type-level granularity

The public API is intentionally type-level. The local Editor assembly's `GizmoUtility.SetGizmoEnabled` converts a `Type` to Unity's annotation identity and changes that one entry. It has no drawer identity. Consequently:

- `PolicyGizmos` and `AICommanderGizmos`, both targeting `AICommander`, become one `AICommander` switch.
- `Navigator` diagnostics become one native type switch; any narrower controls must be legitimate state owned and read by the `Navigator` drawer, not a universal global atom registry.
- A capture profile can be a small set of component types such as steering or combat. It should not recreate painter atom names.

The Unity API documentation describes these methods as controlling Scene View gizmos, while the Unity manual says the component-type Gizmos menu is shared by Scene View and Game View. Unity's local implementation stores the bit in the shared annotation table, and the completed Game View probe confirms that the bit gates Game View output too. Because the scripting page does not explicitly promise the Game View effect, pin this behavior with an Editor integration test.

### Selected-only behavior

`GizmoType.Selected` means Unity invokes the drawer when the target is selected. `NonSelected`, `InSelectionHierarchy`, and `NotInSelectionHierarchy` are separate flags. For the approved selected-only model:

1. drawers declare `GizmoType.Selected` and omit `NonSelected`;
2. capture waits for the intended runtime subjects to exist;
3. capture assigns their GameObjects to `Selection.objects`; and
4. capture restores the prior selection after Recorder has stopped.

This activates every enabled selected-only drawer on those GameObjects. To keep built-in or unrelated selected gizmos out of footage, a deterministic capture profile should snapshot all `GizmoInfo` entries, disable all gizmos/icons, then enable only its target component types. Restoring the original `GizmoInfo[]` reverses the operation without a bespoke registry of prior values.

Passive `Handles` drawing used inside a `[DrawGizmo]` callback is part of the Game View gizmo pass; the existing probe captured both `Gizmos` and `Handles` output. `OnSceneGUI` interaction remains a Scene View facility and should not be treated as capturable Game View UI.

## The unsupported Game View switch

Unity 6000.1.8f1 has no documented public getter or setter for the Game View's master **Gizmos** toggle.

Local `UnityEditor.dll` metadata, read without executing the Editor via Mono.Cecil, shows:

- `UnityEditor.GameView` is an internal type.
- Its `drawGizmos` property is internal and reads/writes the serialized `m_Gizmos` toolbar field.
- `GameView.OnGUI` copies `m_Gizmos` into `PlayModeView.showGizmos` immediately before rendering.
- `UnityEditor.PlayModeView` is internal. Its public-looking `IsShowingGizmos()` and `SetShowGizmos(bool)` methods cannot be referenced through a public type.
- `UnityEditorInternal.InternalEditorUtility.SetShowGizmos(bool)` is CLR-public but undocumented. It changes `PlayModeView.showGizmos`, not `GameView.m_Gizmos`; the next `GameView.OnGUI` overwrites that value. It also has no paired getter. It is not a reliable stateful toolbar contract.

The narrow dependency should therefore resolve the selected `GameView` and read/write its internal `drawGizmos` property. It should be isolated behind one adapter that:

- is pinned to Unity `6000.1.8f1`;
- resolves `UnityEditor.PlayModeView.GetMainPlayModeView` and verifies the concrete view is `GameView`;
- resolves the non-public instance property `drawGizmos`;
- snapshots the old value before setting it;
- repaints the view after mutation;
- fails immediately with the expected Unity version and missing member named if any part of the contract changes.

Calling the undocumented `InternalEditorUtility.SetShowGizmos` is not a safer alternative. Reflection is unavoidable for a reliable read/write pair, so the semantic `drawGizmos` property is the smaller and more testable dependency.

### Exact size-preset restoration

Recorder 5.1.2 changes the Game View to its recording resolution and explicitly does not restore the previous resolution. The public `PlayModeWindow` API can restore the old pixel dimensions but not the original named aspect/preset selection. Exact UI restoration requires reading and restoring `GameView.selectedSizeIndex`, another member on the internal `GameView` type. Recorder may also leave its `Recording Resolution` custom entry present.

This can live in the same version-pinned adapter. Restoring the prior index returns the user's visible selection; removing the package-created custom entry would require substantially deeper Game View internals and is not justified by the capture contract.

## Batch mode with graphics

Unity's supported command-line contract is:

- `-batchmode` suppresses interaction and blocking popups; it does not mean no graphics and does not promise that no native Editor window appears.
- `-nographics` prevents graphics-device initialization. Game View rendering and `ScreenCapture.CaptureScreenshotIntoRenderTexture` therefore require that flag to be absent.
- Recorder's official command-line tutorial launches the Editor, enters Play Mode, and runs `RecorderController`; it does not claim a player-only or truly headless Game View path.
- Unity permits only one Editor process per project path, so automated capture must continue using the repository's Unity access coordinator and an isolated worktree project.

Recorder 5.1.2's local Unity-6 code confirms the mechanism:

- `GameViewInput.BeginRecording` calls `PlayModeWindow.SetViewType(GameView)`, applies a custom rendering resolution, allocates render textures, and captures each frame with `ScreenCapture.CaptureScreenshotIntoRenderTexture`.
- The Unity-6 branch of `GameViewSize.cs` uses the documented public `PlayModeWindow` methods. Its legacy pre-2022 branch contains reflection but is not compiled for this project.
- `RecorderController.PrepareRecording` creates sessions; `StartRecording` begins them and can return false; `StopRecording` disposes every session.

The prior probe is sufficient empirical evidence, so this research did not launch Unity:

- `results/capture-probes/native-gizmo/recorder-gameview-combat.png` is a real two-ship Game View screenshot with native gizmos from a graphics `-batchmode` Editor run.
- `results/capture-probes/native-gizmo/capture-backend-bench-20260806/benchmark.json` records three 30-frame Game View repetitions. It also establishes the accepted cold Game View activation cost and shows steady-state Game View capture was not slower than the painter backend.
- The run displayed a Unity window on Windows. That is allowed by the approved initiative destination; the contract must not call this path truly headless.

At capture startup, assert `SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null` and fail with a message telling the caller to remove `-nographics`. This turns an otherwise ambiguous empty/failed capture into the earliest deterministic failure.

## Transaction and restoration contract

The capture operation must own a single transaction. Before the first public `PlayModeWindow` call, use the internal adapter to discover whether a main play-mode view already exists. Then snapshot the applicable state before mutation:

1. every `GizmoInfo` returned by `GizmoUtility.GetGizmoInfo()`;
2. `Selection.objects` and `Selection.activeObject`;
3. `PlayModeWindow.GetViewType()`, `GetPlayModeFocused()`, and rendering width/height after recording whether those getters will create the view;
4. `drawGizmos` and `selectedSizeIndex` when the pre-existing main view is a Game View, or an ownership marker when capture creates or swaps to one;
5. `Application.runInBackground`, because Recorder sets it to true and does not restore it; and
6. the Recorder controller/settings objects owned by the operation.

Then, inside one outer `try`:

1. ensure/create the selected Game View and enable its master switch;
2. apply the deterministic type profile;
3. select the runtime subject GameObjects;
4. call `PrepareRecording()`;
5. require `StartRecording()` to return true; and
6. run until the requested frames complete.

Cleanup needs nested `finally` blocks so a Recorder cleanup failure cannot skip editor-state restoration:

1. call `StopRecording()` whenever a controller was constructed, including when prepare/start failed;
2. restore `Application.runInBackground`;
3. restore the saved `GizmoInfo[]`;
4. restore selection and its active object;
5. restore Game View master switch and exact size index through the internal adapter;
6. restore public play-mode view type/focus/resolution where still applicable; and
7. close only a Game View window created solely for this transaction.

The owner should also attempt the same idempotent restore on Play Mode exit, assembly reload, and Editor quit. These callbacks cover ordinary exceptions, cancellation, domain reload, and controlled shutdown. No `finally` or Editor callback can run after an OS kill or process crash. If the initiative requires restoring persistent gizmo/layout state after a hard kill in an interactive user's Editor, it needs a small recovery journal written before mutation and replayed on next launch. A dedicated batch process that exits after capture lowers the user-state impact but does not logically eliminate that failure mode.

## Initiative constraints and newly surfaced fog

1. **One internal adapter is necessary.** The initiative should accept and isolate the Game View `drawGizmos`/`selectedSizeIndex` dependency, or abandon automatic master-switch/state restoration. Keeping painters does not improve this contract.
2. **Profiles are type sets, not atoms.** Multiple drawers for one target type cannot be independently toggled through Unity's native controls. This confirms the approved type-level visibility decision.
3. **Capture must own selection.** Selected-only drawers are reliable only after the runtime subjects have spawned and been multi-selected. Selection restoration cannot resurrect objects destroyed during capture.
4. **Game View input is mandatory.** Targeted-camera and render-texture Recorder inputs do not carry the Editor's Game View gizmo overlay. They are not substitutes for this path.
5. **Graphics batch mode is automated, not truly headless.** Omit `-nographics`; expect full Editor startup and the possibility of a window. Amortize startup in a warm coordinated Editor when useful.
6. **Recorder does not restore all state.** Resolution and `Application.runInBackground` restoration belong to the capture transaction, not the package.
7. **Hard-kill recovery remains a policy decision.** Normal success/failure restoration is fully specifiable now. Crash-safe restoration across process death requires deciding whether an on-disk recovery journal is worth the extra mechanism.
8. **The public API's Game View effect deserves a pinning test.** Unity documents `GizmoUtility` in Scene View terms even though the shared menu, local implementation, and probe show it controls Game View output. A Unity upgrade gate should capture a selected-only test drawer with the type disabled and enabled.

## Local evidence inspected

- `src/Asteroids3D/ProjectSettings/ProjectVersion.txt`
- `src/Asteroids3D/Packages/manifest.json`
- `src/Asteroids3D/Library/PackageCache/com.unity.recorder@979a3db2a781/package.json`
- Recorder `Editor/Sources/RecorderController.cs`, `RecordingSession.cs`, `Recorders/_Inputs/GameView/GameViewInput.cs`, and `Recorders/_Inputs/GameViewSize.cs`
- Recorder `Editor/Unity.Recorder.Editor.api`, `Documentation~/CommandLineRecorder.md`, and `Documentation~/InclCaptureOptionsGameView.md`
- `D:/Programs/Unity/Editor/6000.1.8f1/Editor/Data/Managed/UnityEditor.dll`, inspected as metadata with the project's cached `Mono.Cecil.dll`; the Editor was not loaded or executed
- the existing native-gizmo screenshots and benchmark JSON under the primary worktree's ignored `results/capture-probes/native-gizmo/`
