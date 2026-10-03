# PR-4b handoff: redundant tests (#826)

Local-only notes from the PR-4a session (2026-10-02). The brief on the issue is the authority:
https://github.com/amindell11/astronomical-home/issues/826#issuecomment-5947489349
PR-4a was #839. Everything below marked "read" is a prediction from reading code at main `b6e770bc`;
only your runs decide a cut. T = `src/Asteroids3D/Assets/Scripts/Editor/Tests`, P = `src/Asteroids3D/Assets/Scripts`.

## Rules carried over (user-confirmed)

- Locate by test name; re-check each candidate against current main.
- Verify-before-delete for every removal: break the production line the test covers, confirm a RETAINED
  test goes red. No retained catcher -> keep the test and report it. Record mutation + catcher per cut
  in the PR description.
- A test that cannot catch a break in its own claimed line, where nothing else catches it either, is
  cut and the gap is reported (user ruling). Prove "blind" with a run that has the old test present.
- Merges into `[TestCase]` rows keep every original assert.
- Leave `MpcPerformancePlayModeTests` `avgShipSolveMs > 0` and the RL schema pins alone.
- No new gate/verdict strictness. No cuts beyond the issue's list.

## Scope of 4b

### Cuts (PlayMode)

1. `T/PlayMode/MissileGuidancePlayModeTests.cs`
   - Cut `StationaryFire_CloseTarget_NoOrbit` (weaker twin of `CloseRangeLock_NoOvershootOrbit`) and
     `MovingShooter_StationaryTarget_Converges` (same launch state as `StationaryFire_DistantTarget_Converges`
     once `P/.../Missile.cs:78` clamps across-aim velocity).
   - Read mutations: `Missile.cs:79` negate aim -> `CloseRangeLock_NoOvershootOrbit` and
     `MissileLaunchPlayModeTests` red; add across-aim velocity back -> `MissileLaunchPlayModeTests`
     `Strafing_LaunchesAlongTheAim` row red; drop the `Mathf.Max(0f, …)` at `:78` ->
     `DriftingBackward_LaunchesForwardAtInitialSpeed` row red.
   - `StubShooter.Velocity` setter becomes unused (getter is required by `IShooter`).
2. `T/PlayMode/ShipChildComponentStatePlayModeTests.cs`
   - Cut `ShipDeath_DeactivatesParent_ChildComponentsAlsoDeactivate` and
     `ShipReset_ReactivatesParent_ChildComponentsShouldReactivate`; superset is
     `MultipleDeathResetCycles_ChildComponentsRemainStable`.
   - Read mutations: delete `gameObject.SetActive(false)` in `Ship.HandleShipDeath` (`P/Ships/Ship.cs:250`)
     and `SetActive(true)` in `ResetShip` (`:259`) -> `MultipleDeathResetCycles…` red.
   - One assert has no repeat: `weaponsController.Primary.gameObject.activeInHierarchy` after reset. No
     production code toggles a weapon mount's active state, so there is no line to break. Report it.
   - `ENABLE_DIAGNOSTICS` is a `private const bool = false`, not a define. Remove it, `LogDiagnostic`,
     and ALL its call sites, including five in retained tests (around lines 186, 220, 261, 290, 327) or
     the build breaks. `using System.Collections.Generic` was already unused; leave it.
3. `T/PlayMode/LockOnRegistryWiringPlayModeTests.cs`
   - Cut the no-op `SetUp` override and `Ship1_AfterFactory_TargetingIsNotNull` (strict subset of
     `Ship1_WithoutRegistryInjection_LockOnSensorIsDisabled`).
   - Read mutations: `Ship.Targeting => null` (`P/Ships/Ship.cs:68`), or delete
     `Sensor = FindMountSensor();` (`WeaponsController.cs:48`) -> both retained wiring tests red.
4. `T/PlayMode/TriggerVolumeActivationPlayModeTests.cs`
   - Cut `ParkedThenQualified_RuleFires_WhenLatchedTermArrivesWhileParkedInside` (user ruling: cut).
   - Read mutations: `TriggerVolume.cs:60` publish `false` -> `TriggerVolume_PlayerEnterExit_MirrorsBusLevel`;
     delete the latch loop `ActivationRule.cs:108-109` -> `SectorActivationEditModeTests.Chaining_…`;
     delete `Set(token, true)` at `SectorEventBus.cs:33` -> `BusChanged_RaisedOnlyOnActualValueChanges`.
   - Known give-up to state in the PR: it is the only test running a live `ActivationRule` with two terms.
   - The fixture `<summary>` mentions "the parked-then-qualified activation scenario end-to-end";
     that clause must go in the same diff.

### Merges (every assert kept)

5. `T/PlayMode/WeaponCommandDispatchPlayModeTests.cs`: merge
   `PrimaryFireCommand_DispatchesToWeapon_OnFireIsRaised` and `SecondaryFireCommand_…` into ONE
   `[UnityTest]` that counts both `Primary.OnFire` and `Secondary.OnFire` (the commander already fires
   both slots). Do not delete either half: the Secondary half is the only catcher for
   `WeaponSlot.Secondary => Secondary` (`WeaponsController.cs:146`). No repo precedent for a
   parameterized `[UnityTest]`; none needed.
6. `T/EditMode/RunTallyEditModeTests.cs`: the pairs check different lines (`RunTally.cs:88` `Kills++`,
   `:89` `Killed?.Invoke()`), so merge. Suggested rows asserting both `Kills` and the raised count:
   player kill -> 1; asteroid kill -> 0; other attacker -> 0; self-death -> 0. Merge the two
   KillAfterEnd tests into one. Keep `KillBeforeBegin_DoesNotRaiseKilled` and `FormatSeconds`.
   The `Other` field becomes unused.
7. `T/EditMode/RLWorkerSeedEditModeTests.cs` + `RLArenaSeedEditModeTests.cs`: collapse to `[TestCase]`
   rows in one fixture (delete the other .cs and its .meta). Keep the `*Zero_IsIdentity` cases as rows
   and `WorkerThenArena_OrderIsPinned` unchanged. KEEP both `Derivation_IsDeterministic` tests (brief:
   sole catcher for a non-deterministic derive) unless the user says otherwise. A public nested enum
   is needed if a layer enum appears in a public test signature (CS0051 otherwise).
8. `T/EditMode/MpcIntentSentenceEditModeTests.cs`: third-seat copies -> `[TestCase(1..3)]` on both
   `SyntheticReferent_ExtrapolatesLinearly_PerStep` and `SyntheticReferent_Invalid_DropsItsSlot`, with a
   test-side helper that sets `referent1/2/3` directly on the `CostInput`. Do not touch `BareConfig`.

### Cuts (EditMode)

9. `T/EditMode/ObjectiveTrackerEditModeTests.cs`, eight tests (file unchanged since review):
   `InitialState_IsExplore`, `KeyAcquired_TransitionsToExtractionChallenge_OnNextTick`,
   `ExtractionChallenge_TransitionsToExtracted_WhenPlayerEntersZone`,
   `AnyState_TransitionsToFailed_WhenPlayerDies_DuringExplore`,
   `Fail_TransitionsToFailed_FromAnyNonTerminalState`,
   `Restart_FromFailed_ResetsToExplore_WithNoConsequences`,
   `MissionDefinition_CreateDefault_HasExpectedTransitions`,
   `KeyPickup_SpawnKey_ResetsCollectedFlag`.
   - Read catchers (same file): `Explore_TransitionsToKeyAcquired_WhenKeyPickedUp`,
     `CurrentStep_ReflectsStringStepId`, `ExtractionChallenge_DoesNotComplete_WhileBlocked`,
     `Failed_IsTerminal_IgnoresSubsequentTicks`, `Fail_IsNoOp_WhenAlreadyFailed`,
     `OnStateChanged_FiresOnRestart`, `OnStepChanged_FiresOncePerTransition_WithStepIds`,
     `KeyPickup_SpawnKey_RepositionsAndReactivates`.
   - Mutation sites: `ObjectiveTracker.cs:31`, `:41-45`, `:49`, `:57`, `:62`; `MissionDefinition.cs:46-51`;
     `KeyAcquiredState.IsComplete`; `ExtractionChallengeState.IsComplete`; `KeyPickup.cs:26`.
   - `KeyPickup_SpawnKey_ResetsCollectedFlag` is blind (the flag is already false on a fresh
     component): prove it with a run, cut, report the gap (deleting `KeyPickup.cs:26` has no catcher).
   - `MissionDefinition_…HasExpectedTransitions` terminal-row asserts guard table rows the tracker never
     reads in terminal states. State that in the PR.
10. `T/EditMode/RespawnResetEditModeTests.cs`: cut the `RegenResourceResetEditModeTests` class.
    Read: deleting `base.Reset()` at `RegenResource.cs:29` -> `DamageControllerEditModeTests`
    `MultipleResetCycles_NoHealthOrShieldDrift` red; the `clock` and `lastDamageTime` lines are
    unobservable (equivalent mutants). The file then holds only `GunsightObservationEditModeTests`,
    so the file name no longer matches its type: flag a rename to the user, do not just do it.
11. `T/EditMode/DamageControllerEditModeTests.cs`: cut `ResetDamageState_RestoresHealthAndShield`
    (covered by `MultipleResetCycles_NoHealthOrShieldDrift`: delete `Health.Reset()` /
    `Shield.Reset()` at `DamageController.cs:82-83`). KEEP `NewController_StartsWithFullHealthAndShield`
    (brief: only test with a non-default `maxHealth`).

## Out of scope, noticed while reading

- No test sets a valid referent on MPC seat 2 today; `[TestCase(1..3)]` adds that row.
- Probable production bug (read, not verified): `Mpc.cs:157-166` builds the `probe` `CostInput` with
  `referent1` and `referent2` but not `referent3`. Not filed yet; ask the user.

## How 4a ran its mutations (works; copy it)

- `reports/test-cuts/mutate.py`: named exact-match mutations applied to the slot's production files
  (edit `ROOT` for your slot). Revert with `git -C <slot> checkout -- src/Asteroids3D/Assets`.
- `reports/test-cuts/reds.py [resultsDir]`: prints non-passing tests and messages from the newest run.
- Commit the test edits FIRST, then mutate production, so a checkout of production files is a clean revert.
- Scoped run: `./scripts/agent_worktree_pool.sh run-tests <slot> -Mode Both -TestFilter "A|B"` (~1 min).
  Full: `-Mode Both -ScopeType Workspace` (~3 min). Run pool commands from the primary tree with the
  Bash tool, in the background, output redirected to a file (never piped).
- Batch several mutations into one run when their predicted catchers are different named tests; read
  the failure messages to attribute. Rerun alone when a red is not attributable.
- Every Unity run dirties `Assets/Settings/Rendering/Build Profiles/Main.asset` in the slot; check it
  out before committing.
- Before PR: affected `-TestCategory` runs clean, `run-resharper <slot> origin/main`, one quality
  sub-agent, then `submit` (it runs the full suite).
