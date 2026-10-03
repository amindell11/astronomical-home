# PR-4c handoff: dead helpers, TestAssets collapse, shared AICommander stubs, script suite (#826)

Local-only notes from the PR-4a session (2026-10-02). The brief on the issue is the authority:
https://github.com/amindell11/astronomical-home/issues/826#issuecomment-5947489349
PR-4a was #839. Caller counts below were verified by whole-repo search at main `b6e770bc`; re-run the
searches on current main before deleting. T = `src/Asteroids3D/Assets/Scripts/Editor/Tests`.

## Rules carried over (user-confirmed)

- Locate by symbol name; re-check each candidate against current main and drop any that no longer apply.
- Search the WHOLE repo for callers (all of `src/Asteroids3D/Assets`, `scripts/`, `doc/`, `TESTING.md`),
  not just the test tree. PRs #832 and #834 moved study code out of the test tree.
- `test_delivery_timing.sh`: move its journal-event assertion into `test_pool_script_tests.sh` BEFORE
  removing the duplicate run-every-file proof, and prove the moved assertion catches a break.
- No new gate/verdict strictness. Nothing beyond the issue's list.
- File overlap with PR-4b: `LockOnRegistryWiringPlayModeTests.cs` and
  `ShipChildComponentStatePlayModeTests.cs` (4c only changes `TestAssets` call sites there). Whichever
  PR merges second adapts.

## Scope of 4c

1. `T/PlayMode/Common/AsyncAssert.cs`: delete `WaitForFloatWithinTolerance` and
   `WaitForVector2NearTarget` (zero code callers). `TESTING.md` has sample code for
   `WaitForVector2NearTarget` (around line 450): remove that sample.
2. `T/PlayMode/Common/TestUtilities.cs`: delete both `DistanceToPlaneTarget` overloads, `PauseAudio`,
   `ResumeAudio` (zero code callers). `PlayModeWorldFixture` has an unrelated `PauseAudio` property.
   `GetPlaneFacingAngle` / `AngleDeltaToTarget` stay (used by `MpcNavigatorPlayModeTests`).
   `using Substrate;` becomes unused. Update the `TESTING.md` mentions (around lines 371, 462-471).
3. `T/PlayMode/TestSceneBuilder.cs`: delete the file and its `.meta` (user-confirmed).
   - `PositionForTest`: zero callers.
   - `CreateTestArena` / `CleanupTestArena` / `_currentArena`: called only from
     `PlayModeWorldFixture` (around lines 47 and 64); they build and destroy an empty "TestArena"
     GameObject nothing reads. Remove both calls and fix the fixture `<summary>` that mentions
     "TestSceneBuilder cleanup".
   - `CreateObstacle`: one caller, `MpcNavigatorPlayModeTests.MpcObstacleAvoidance_ShipTracksCommandWithoutColliding`;
     inline it there.
   - Update `TESTING.md` (around lines 372 and 388).
   - Behaviour check: every PlayMode test loses one root GameObject. Run the full PlayMode suite.
4. `T/PlayMode/Common/TestAssets.cs`: collapse the single-use path wrappers to one
   `Load<T>(string assetPath) where T : UnityEngine.Object` (write `UnityEngine.Object`; `using System`
   makes bare `Object` ambiguous). Keep `LoadShip2Prefab` and `LoadTestPilotMpc` (16 and 17 callers,
   they own the path constants), `NewObserverCam`, `NewNativeCapture`. 12 call sites in 7 files change:
   LockOnRegistryWiring x3, ShipPresentation x2, ShipChildComponentState, ShipSimInvariance,
   WeaponReequip, CaptureScenario, ShipReequip x3. `using Ships.Loadout;` becomes unused.
   The `#else return null` branches: `Tests.PlayMode.asmdef` also targets `WindowsStandalone64`, so the
   assembly is not Editor-only on paper, but the runner never builds a player and another file already
   has an unguarded `using UnityEditor`. Keep one guard inside `Load<T>`; do not touch the asmdef.
5. Shared AICommander stubs (user-confirmed: the four types all three fixtures copy, one type per file):
   `TestableCommander`, `StubStatus`, `StubWeaponContext`, `ScriptedBrain` move from
   `T/EditMode/AICommander{AbilityLane,FireLane,Referent}EditModeTests.cs` to `T/Common/`
   (asmdef `Tests.Common`, namespace `Tests.Common`, next to `StubShipRegistry`). The copies are
   byte-identical after indentation. `NoWeapons` and `StubPilot` appear in only two fixtures each: leave
   them. `SpyPilot` / `SpyWeapons` stay local.
   - `Tests.Common` references only `Core`; every type the stubs touch is public in `Core`.
     `AICommander.Awake` / `FixedUpdate` are `protected virtual`.
   - Seven other fixtures have their own nested `StubStatus`; a shared `Tests.Common.StubStatus` would be
     shadowed there. Consider naming the shared one `StubShipStatus`. Folding those seven in is NOT in scope.
   - New files need committed `.meta` files (two lines: `fileFormatVersion: 2`, `guid: <32 hex>`), as in
     `T/Common/SwappableField.cs.meta`. Easiest: let a Unity run generate them, then commit.
   - `T/Common` goes from 3 to 7 .cs files. Usings that become unused in the fixtures:
     `Combat.Weapons`, `AI.Context`, `Movement` (all three); `Ships.Registry` (Fire); 
     `System.Collections.Generic` (Referent).
   - PR-4a removed `AbilityLane_ResetState_ClearsBoost` from the AbilityLane fixture; line numbers shifted.
6. `scripts/tests/test_delivery_timing.sh`: lines 17-26 (run-every-file, propagate-red) duplicate
   `test_pool_script_tests.sh` ("A red file in either lane fails the suite only after every file…").
   Line 27 is the unique part: it asserts the journal carries
   `"event":"script-test","phase":"script-tests","file":…,"sec":…,"exit":7` (pool:
   `journal_event script-test script-tests "file=$base" "sec=$sec" "exit=$rc"`, around line 1296).
   - `test_pool_script_tests.sh` already journals in its selection block: `sel_run` sources the pool
     with `MERGE_JOURNAL="$SEL_JOURNAL"` and calls `cmd_run_script_tests`. Put the moved assertion there;
     keep the nonzero-exit case (the original asserts `"exit":7` for a red file).
   - Keep `test_delivery_timing.sh` lines 10-16 (journal escaping and number preservation) and fix its
     PASS message.
   - Proof: break the `journal_event script-test` line in the pool and confirm
     `test_pool_script_tests.sh` goes red; run both test files clean
     (`./scripts/agent_worktree_pool.sh run-script-tests <slot>`).
   - Changing `scripts/` means the merge gate's script-suite phase selects by `# covers:` lines; both
     files cover `scripts/agent_worktree_pool.sh`.

## Out of scope, noticed while reading

- `TESTING.md` also shows `WaitAndAssertRemainsFalse` (real name `AssertRemainsFalseFor`) and
  `TestAssets.LoadDefaultShipSettings()` (does not exist). Stale before this work; flag, do not fold in.

## Mechanics that worked in 4a

- Run pool commands from the primary tree with the Bash tool, in the background, output redirected to
  a file (never piped). Scoped run ~1 min, full suite ~3 min.
- Every Unity run dirties `Assets/Settings/Rendering/Build Profiles/Main.asset` in the slot; check it
  out before committing.
- Helper removals have no production line to break; the proof is a clean compile plus the full suite.
  The only mutation proof owed in 4c is item 6.
- `scripts/unity_test_scopes.json` names none of the files touched here, so `-ScopeType Auto` falls
  back to the full Workspace suite.
- Before PR: `run-resharper <slot> origin/main`, one quality sub-agent, then `submit`.
