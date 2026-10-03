# Test infrastructure grade (Opus reviewer, static-only run)

Rubric: `reports/test-infra-rubric.md`. Run date 2026-09-30, `main` at `9aa2ceeb`.
**This run is static-only.** No tests, pool commands or Unity were run. Every conclusion that the
rubric wants probed hands-on is traced through the code, marked **static — unverified**, and paired
with the command that would verify it.

Path shorthand: `Tests/` = `src/Asteroids3D/Assets/Scripts/Editor/Tests/`; `Scripts/` =
`src/Asteroids3D/Assets/Scripts/`; `runner` = `scripts/unity_test_agent.ps1`; `pool` =
`scripts/agent_worktree_pool.sh`.

Terms used below, defined once:
- **ran-zero**: a run that executes no tests at all.
- **gate** (the merge gate): `pool merge`, which requires a runner summary whose `coverage.verdict` is
  `full` for the exact landing tree, or a hosted `merge-proof/headless` status.
- **ungated test**: a test that no gate path ever runs.
- **vacuous**: an assertion that holds whether or not the behaviour it names works.

---

## 1. Scorecard

Each criterion is scored per layer, and the criterion score is the mean of the two layers. The weighted
total is the sum of criterion score × weight, out of a maximum of 76.

| # | Criterion (weight) | Unity suite | Harness | Score | Weighted | One-line evidence |
|---|---|---|---|---|---|---|
| 1 | Verdict integrity (×3) | 2 | 2 | 2.0 | 6.0 | A runtime `Assert.Ignore` on a missing asset counts toward "full" coverage (`runner:956-963`). About 65 `RequiresGraphics` tests are excluded from every gate, yet the gate still stamps the run "full" (`runner:952`). A cold ran-zero run reports `STATUS=passed`, exit 0 (`runner:1719-1730`). |
| 2 | Assertion strength (×3) | 3 | 3 | 3.0 | 9.0 | 32 mutations were argued; 20 go red. Survivors cluster in named vacuous tests, e.g. the MPC avoidance clearance at `Tests/PlayMode/MpcNavigatorPlayModeTests.cs:152` sits below the physical contact distance. |
| 3 | Determinism & load (×2) | 3 | 2 | 2.5 | 5.0 | Unity tests mostly use sim time, fixed-step counts and `Tick(dt)`. Wall-clock deadlines can only fail red (#651). The script suite still has open load flakes (#542, #762). |
| 4 | Risk coverage (×2) | 2 | 3 | 2.5 | 5.0 | The hangar input gate, ship swap and session seam are ungated. The GameHost restart loop and RL observation call sites are untested. 14 of the last 22 `fix(...)` commits carry a test. The gate's verdict reader `Get-CoverageVerdict` has no direct test. |
| 5 | Economy (×2) | 2 | 3 | 2.5 | 5.0 | About 3,950 cut-candidate lines. 1,850 of them are high confidence, dominated by ungated studies and signature pins. The `IShipStatus` stub has 11 copies. The script suite is mostly lean. |
| 6 | Feedback-loop cost (×2) | 3 | 2 | 2.5 | 5.0 | A routed run takes 8.5 s. The gate's tests phase took 111–600 s against a 480 s budget. The script-test phase took 638–1,264 s against 1,200 s; `test_pool_merge_gate.sh` alone has a median of 540 s. |
| 7 | Isolation (×2) | 2 | 3 | 2.5 | 5.0 | `PacingContract` leaks out of `RLSentencePlayModeTests`. A "study" regenerates a shipped prefab. Every Unity launch goes through the coordinator; one test has a latent bypass. |
| 8 | Diagnosability (×1) | 3 | 3 | 3.0 | 3.0 | The summary JSON distinguishes pass/fail/infra_error, `infra_error` prints compile errors inline (`runner:1771-1789`), and the merge journal records timings. Ran-zero is not labelled. |
| 9 | Test-code health (×1) | 2 | 3 | 2.5 | 2.5 | Stubs are copied (`IShipStatus` ×11, `IShooter` ×5, `IWeapons` ×5). Five fixtures have no domain tag. Private structure is pinned by reflection (`SessionContractsEditModeTests`, 18 reflection calls). |
| 10 | Doc–reality drift (×1) | 1 | 1 | 1.0 | 1.0 | `TESTING.md` describes `finalize` wrongly and documents helpers and an `[Ignore]` example that do not exist. Its "legacy" filter example runs zero tests. Four scope-map filter names are stale. |
| | **Weighted total** | | | | **46.5 / 76 (61%, mean 2.45 / 4)** | |

**Bottom line.** The harness's cold-run machinery (exit-code handling, the coverage stamp, fail-closed
proof, the merge journal) is careful and well tested. Most test cases assert real behaviour. The largest
gap is at the edge of "green": what the suite silently does not run. That covers skipped tests,
graphics-tagged tests and ran-zero selections. The largest economy win is removing the render studies
and generators that live in the test tree without ever being gated.

---

## 2. Findings (ranked by severity)

### F1. The "full" coverage stamp excludes about 65 graphics-tagged tests, including behaviour tests, and one of them is already red on main *(critical: verdict integrity, risk coverage)*

**What happens.**
- `runner:73` defaults `-ExcludeCategory RequiresGraphics`.
- `Get-CoverageVerdict` treats that exclusion as still-full: "RequiresGraphics is excluded from every gate run by design" (`runner:952-954`).
- The hosted suite excludes it too (`.github/workflows/headless-suite.yml`, `customParameters: -testCategory !RequiresGraphics`).
- `-WithGraphics` requires `-Mode PlayMode` (`runner:114-116`). So the EditMode graphics tests (BackgroundAtmosphere, Starfield{Halo,Parallax,Zoom}Render, 2 in StarfieldShader, DrawnContour, PlainCaptureView; about 1,000 lines) have no scripted runner at all. They run only from an interactive Test Runner.

**What is lost is behaviour, not pixels.** Three fixtures carry *only* `RequiresGraphics`, with no domain tag:
- `HangarInputGatePlayModeTests` (`Tests/PlayMode/HangarInputGatePlayModeTests.cs:26`). It guards "every hangar click fires a weapon on the live ship".
- `HangarShipSwapPlayModeTests:27`.
- `SessionSeamPlayModeTests:19`.

They need graphics only because the real `PlayerRig` cameras cannot create RenderTextures under `-nographics` (comment at `HangarInputGatePlayModeTests.cs:25`).

**Concrete scenario (it already happened).** `SessionSeamPlayModeTests.Session_ComposesAndTearsDownWithoutAHost` fails on main (`9aa2cee`) with an NRE from `PlayerRig.Teardown` (#764). Every gate that merged the offending change stamped `coverage.verdict = full`. It was found only because PR #755 ran the "owed" graphics tests by hand. The #764 body attributes the trigger to the Input System move (#736), unverified. HangarInputGate has had 14 refactor co-edits (`git log -- Tests/PlayMode/HangarInputGatePlayModeTests.cs`), each maintained without the test ever being gated.

**Root cause.** "Needs a GPU" and "is a behaviour test" share one tag. The render dependency lives in the rig prefab, not in the behaviour under test.

**Fix, narrow vs structural:**
- Narrow (rung 2): run a `-WithGraphics -Mode PlayMode -TestCategory RequiresGraphics` leg in the gate whenever the landing diff touches `Scripts/Game/**`, `Scripts/UI/**` or rendering paths. Stamp the summary with the result. Cost: one extra cold windowed boot on those merges, and it needs a GPU. It cannot run on the hosted lane.
- Structural (rung 1): add a headless rig seam so hangar and session tests do not build cameras. `HangarWeaponRowsPlayModeTests:192` already strips the RawImage in the same spirit. Retag the three fixtures with a domain (`UI`/`Bootstrap`) so they join the gate. That leaves `RequiresGraphics` for true pixel tests, and adds a scripted EditMode graphics path.

### F2. A test that skips itself counts as green, and a missing asset makes tests skip *(high: verdict integrity)*

**What happens.**
- `Get-CoverageVerdict` checks only `failed==0 && total>0` per platform (`runner:956-963`). Skipped tests never lower coverage.
- `TestAssets.Load*` returns `null` silently on a bad path (`Tests/PlayMode/Common/TestAssets.cs:28-34`).
- Tests turn that into a runtime skip:
  - `SectorCompositionPlayModeTests.cs:146,171,190,207,223,246,292,316,379,405` (10 of 14 tests: `if (!ship || !cmdr) { Assert.Ignore(...) }`)
  - `ShipChildComponentStatePlayModeTests.cs:206,247`
  - `MinimapShipMarkerEditModeTests.cs:20`, which skips when the layer the test exists to check is gone.

**Scenario.** Rename `Assets/Prefabs/Ships/Ship_2.prefab` or `Pilots/TestPilotMPC.prefab`. Ten sector-composition tests (adopt, teardown and respawn) turn Skipped. The runner reports passed, the coverage stamp says full, and the gate merges.

The baseline already normalizes skips. PR #755 reports "897/902 (5 skipped, as on main)", and those 5 are the env-gated tools at `MpcSolverRigTests.cs:274,302,338`, `OpponentArchetypePlayModeTests.cs:148` and `RLEpisodePlayModeTests.cs:400`. Ten more skips would not stand out.

This is the same class as #752: the test ends early and reads as green.

**Fix, narrow vs structural:**
- Narrow (rung 4): replace each runtime `Assert.Ignore` with `Assert.IsNotNull(..., "<path> missing")`. Net ~0 lines.
- Structural (rung 2): once the env-gated tools leave the suite (see C-studies), make any skip in a gate run `partial`, so that a quarantine has to be deliberate.
- Trade-off: `TESTING.md` § Retiring tests documents `[Ignore]` quarantine as legitimate, and the NUnit XML labels both `[Ignore]` and `Assert.Ignore` as `Ignored`. The structural rung therefore makes quarantine a gate-visible event. The owner should decide whether that is wanted.

### F3. A cold run that executes zero tests reports `STATUS=passed`, exit 0 *(high: verdict integrity; static — unverified)*

**What happens.**
- `overallStatus` is `failed` only if `failed > 0` or a `-WithGraphics` run is empty (`runner:1719-1730`).
- The per-run XML status is ignored at the overall level. Even `Inconclusive → "failed"` (`runner:913`) does not propagate, because the overall status reads `$failed`, not run status.
- `GateTestRunner` exits on `FailCount` alone (`Tests/EditMode/GateTestRunner.cs:141`).

**Scenarios:**
1. `.\scripts\unity_test_agent.ps1 -Mode Both -TestCategory Weapon`. The domain is `Weapons`; `TESTING.md` § Troubleshooting even lists "`-TestCategory Foo` runs nothing" as a symptom.
2. The documented legacy example `-TestFilter "Category=Smoke"` (`TESTING.md` § Legacy Examples). `-testFilter` is a name pattern, so this matches no test name.

Either run prints `STATUS=passed total=0 ...` and exits 0. An agent iterating on it sees green.

**Why the gate is safe.** Every other entry point already refuses ran-zero:
- routed (`runner:1356-1358`)
- `-WithGraphics` (`runner:1719`)
- the hosted suite's fullness guard (`headless-suite.yml`, "Fullness guards")
- the gate's `total>0` coverage check (`runner:963`)
- the script suite for a missing or empty `tests` dir (#612)

The cold path is the odd one out, and no script test covers it. `test_runner_access_refusal.ps1:49-62` fakes gate XMLs with `total="1"` only.

**Verify:** `.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter NoSuchFixtureXyz`. Expect `STATUS=passed total=0` and exit 0.

**Fix** (rung 2, earliest deterministic failure in the runner): generalize the `-WithGraphics` zero-test guard to every transport. Add one fake-Unity case (`total="0"`) to `test_runner_access_refusal.ps1`.

### F4. Specific tests are vacuous or self-satisfying, and each looks like coverage *(medium: assertion strength)*

Each entry gives the mutation that survives.

- **MPC avoidance.** `MpcNavigatorPlayModeTests.cs:152` asserts `minDistToObstacle > 1.5`. The obstacle is a real 2×2×2 BoxCollider (`:119`), so physical contact alone keeps the ship's centre beyond √2 plus the ship's collider radius. Hard-coding `enableObstacleAvoidance = false` (`Scripts/AI/Navigation/Navigator.cs:120`) likely stays green. *Static; the ship collider radius is unchecked.* End-to-end rock avoidance is the MPC's highest-blast-radius behaviour.
- **Minimap marker.** `ObjectiveChannelEditModeTests.cs:66-80`, "Marker_Bind_SubscribesToChannel", is `Assert.DoesNotThrow` only. Deleting the subscription at `Scripts/UI/MinimapObjectiveMarker.cs:34` stays green.
- **Hangar flow.** `HangarFlowPlayModeTests.cs:42-47` sets `finished = true` unconditionally, then asserts it. The bare rig short-circuits the gate condition under test, so deleting `|| !presentationEnabled` at `Scripts/Game/GameHost.cs:222` stays green.
- **Boost reset.** `AICommanderAbilityLaneEditModeTests.cs:158-166`, "ResetState_ClearsBoost", routes a null decision, which clears boost anyway (`Scripts/AI/AICommander.cs:145`). Deleting the reset line at `:115` stays green.
- **Heat penalty.** Every Heat configuration sets the overheat penalty equal to the delay (`HeatEditModeTests.cs:64-65, 187-188`). `var delay = coolDownDelay;` in place of the ternary at `Scripts/Combat/Weapons/Conditions/Heat.cs:60` stays green.
- **Forces.** The Smoke test `ForcesEditModeTests.cs:50-63` pins a null-settings → silent-zero guard: the prohibited rung-5 guard for a programmer error, made into a contract. `:65-78` asserts only magnitudes, so sign flips in strafe or bank stay green.
- **Inactive holder.** `SectorLifecyclePlayModeTests.cs:47-75` rebuilds the inactive-holder pattern by hand and never calls `Session`. It proves Unity semantics, not `Scripts/Substrate/Sessions/Session.cs:99`.
- **Ambush teardown.** `AmbushEncounterPlayModeTests.cs:231-256` has a 30 s fire delay and waits one frame after Teardown, so a leaked pending fire cannot land inside the window.
- **Anchorless colliders.** `RLEpisodePlayModeTests.cs:251` has `if (!mesh) continue;`. If the prefab stops authoring MeshColliders, every iteration skips: exactly the "ships fly through" symptom the test is pinned against.
- **Capture frames.** `CaptureScenarioPlayModeTests.cs:83-86` asserts that a frame directory exists and holds `*.png`. That is existence, not content: the #374 shape which `doc/agents/testing.md` explicitly forbids.

### F5. Test state escapes the test *(medium: isolation)*

- **`captureDeltaTime` leak.** `RLSentencePlayModeTests.cs:40-41` calls `PacingContract.Apply()` (`timeScale = 1`, `captureDeltaTime = fixedDeltaTime`; `Scripts/RL/Hosts/PacingContract.cs:9-13`) and sets `maximumDeltaTime = 1`. TearDown (`:45-52`) restores none of them.
  - Scenario: every PlayMode fixture that sorts after it (Scanner…, Sector…, Ship…, Weapon…) runs with frames locked to one fixed step. Fixed-step and wall-clock behaviour then differ between a full run and a single-fixture run: order-dependent results.
  - `RLCapturePlayModeTests.cs:58-59` has the same leak, but it is graphics-only.
- **A study rewrites shipped assets.** `LayeredExplosionStudyTests.cs:210,242,246` calls `SaveAsPrefabAsset` on `Assets/Visuals/Vfx/LayeredExplosion/Prefabs/FragmentingDrawnAsteroid.prefab` and then `AssetDatabase.SaveAssets()`. `Settings/Asteroids/SpawnSettings.asset` references that prefab (guid `cdd90320…`, verified). Running the study from an interactive "Run All" silently regenerates a shipped prefab and discards hand edits.
- **Real project files.** FlatBackground EditMode tests write `test-<guid>` folders into `Assets` (`FlatBackgroundImportEditModeTests.cs:183`, per a delegated read), so a killed run dirties the tree. `NativeGizmoRecoveryEditModeTests.cs:43` deletes `Library/NativeGizmoCapture/recovery.json` unconditionally.
- **Latent coordinator bypass.** `scripts/tests/test_scope_resolution.ps1:225-232` invokes the real runner with `-SkipUnityAccess` and *default* `-UnityPath` and `-ProjectPath`. If the Auto-argument rejection (`runner:153-165`) regresses, this test boots real Unity on the real project outside the coordinator. That breaks AGENTS.md dependency rule #6. The later call at `:238` already passes a stub `where.exe`; the earlier ones do not.
- **Machine-global state.** `GizmoViewCollidersEditModeTests.cs:44-56` writes `EditorPrefs`, which are machine-global and shared by concurrent editors. It is low severity.

### F6. The script-suite gate phase is the long pole and still flakes under load *(medium: feedback cost, determinism; already tracked)*

**Timings.** From the last 60 merge journals (`.worktree-pool/merge-runs/*.jsonl`):
- The `script-tests` phase ran 638–1,264 s against its 1,200 s budget (`pool:1491`), and went over twice.
- `test_pool_merge_gate.sh` alone: median 540 s, max 897 s (n=19).
- The next-slowest file is `test_pool_lock_serialization.sh` at a 70 s median.

**Wall-clock bounds.** `test_pool_merge_gate.sh:855` (30 s) is #762. `test_unity_access.ps1`'s Reap reconfirmation window (`:419-433`) can pass *without exercising* reconfirmation when load delays the first look.

This is tracked by #664, #639, #611, #542 and #762. It is cited here and not re-proposed.

**New observation.** The suite runs *only* at merge time (`pool:2014-2021`). `submit` and `revise` never run it, so a `scripts/` regression surfaces at the most expensive moment.

### F7. High-blast-radius seams with no test *(medium: risk coverage)*

- **GameHost restart loop** (`Scripts/Game/GameHost.cs:135-155`: death → recap → UnloadSector → hangar → LoadSector). Only first boot is tested (`GameHostRigPlayModeTests`). `CancelPendingRespawns` (`Session.cs:130`) is caught by nothing.
- **RL observation call sites.** `ShipAgent.CollectObservations` (`Scripts/RL/Episodes/Compositions/ShipAgent.cs:94-100`) and `LivePilotAgent.CollectObservations` (`Scripts/RL/Runtime/LivePilotAgent.cs:89-95`) duplicate a 13-argument positional `Fill` call. Swapping two booleans at either site stays green, and the shipped pilot would then see inputs it was not trained on.
- **Truncation vs terminal signalling** (`Scripts/RL/Episodes/EpisodeLoopDriver.cs:105-115`). A swap stays green, and it would corrupt value bootstrapping during training.
- **Combat.** Invulnerability expiry (`Scripts/Ships/Damage/DamageController.cs:39`) and lock → `ConsumeLock` → missile `SetTarget`.
- **Harness.** Nothing tests the gate's "no full-coverage proof → not merging" refusal for scoped runner args (`pool:2109-2112`). Nothing directly tests `Get-CoverageVerdict`: the gate test stub re-implements it in bash (`test_pool_merge_gate.sh:103-113`). Deleting the routed → partial rule at `runner:943` stays green.

### F8. Test metadata drifts with nothing to catch it *(low: health, doc drift)*

- **No domain tag.** Five fixtures have none: `ShipReequipPlayModeTests:18`, `PlayerCommanderReleasePlayModeTests:21`, plus the three graphics fixtures in F1. `-TestCategory`, Module and Auto runs never select them.
- **Stale scope map.** Four `testFilter` names in `scripts/unity_test_scopes.json` match no class: `CollisionDamageUtilityTests`, which sits in the **smoke** filter, plus `LockOnRegressionEditModeTests`, `LosCostEditModeTests` and `MpcBoostEditModeTests`. Nine module `paths` globs match no file.
- **`-ValidateScope` does not catch it.** It checks only "at least one test matches" (`runner:1494-1517`), so partial staleness is invisible.

---

## 3. Cut list

"Lost" names the failure mode that no other test catches after the cut. "None" means safe.
Verify-before-delete per `TESTING.md` § Best practices.

**Generators, studies and tools in the test tree.** All are `RequiresGraphics` or env-gated, so none ever runs in the gate.

| Location | ~Lines | Lost | Conf. |
|---|---|---|---|
| `Tests/PlayMode/Rendering/AsteroidField/LayeredExplosionStudyTests.cs` `BuildAssets` :153-249 + render loop. Move the recipe to an Editor menu; keep ~40 lines asserting the *saved* prefab. | 240 | none (the asserts check what the test just built) | High |
| `Tests/PlayMode/Rendering/AsteroidLightingPlayModeTests.cs` (subject material referenced by no prefab/scene) | 217 | none | High |
| `Tests/PlayMode/Rendering/DrawnInkPlayModeTests.cs` + `Scenarios/Drawn/DrawnInkStudy.cs` (shader has no production consumer; mutates the URP renderer asset) | 146 | none | High |
| `Tests/EditMode/MpcSolverRigTests.cs:298-358`: two env-gated artifact emitters with zero asserts | 55 | none | High |
| `Tests/PlayMode/Rendering/Vanguard/VanguardStudyPlayModeTests.cs` (serves closed #685; asserts on test-built materials) | 468 | contour shader on the production mesh, with test-chosen params | Med-High |
| `Tests/PlayMode/Scenarios/Drawn/*` (base + 6 stubs; #685 closed) | ~350 | none | Med-High |
| `Tests/PlayMode/Rendering/AsteroidField/DrawnFieldPlayModeTests.cs` + `DrawnFieldStudyAssets.cs`; keep mesh-fidelity :112-127 as ~20-line EditMode test | ~310 | none | Medium |
| `OpponentArchetypePlayModeTests.cs:140-183`, `RLEpisodePlayModeTests.cs:392-425` (JSONL sweeps, zero asserts) | ~80 | none as tests | Low-Med |

**Self-satisfying tests and signature pins.**

| Location | ~Lines | Lost | Conf. |
|---|---|---|---|
| `Tests/EditMode/SessionContractsEditModeTests.cs` (whole; reflection restates signatures the compiler enforces at the real call sites; repoint the `bootstrap` feature scope) | 180 | none | High |
| `Tests/PlayMode/SectorLifecyclePlayModeTests.cs` (whole; see F4; completion test duplicates `SectorCompositionPlayModeTests:349-372`) | 93 | none | High |
| `Tests/EditMode/GameContextDecouplingEditModeTests.cs` (test 1 duplicates `PlayerInputReaderEditModeTests:62-70`; test 2 is a reflection signature pin) | 37 | none | High |
| `Tests/PlayMode/GamePlanePlayModeTests.cs:58-64, 79-92` (asserts a facade equals the function it forwards to) | 21 | none | High |
| `Tests/EditMode/PilotDecisionSeamEditModeTests.cs:132-140` (tests C# default parameters) | 9 | none | High |
| `Tests/EditMode/AsteroidAttributeRollerEditModeTests.cs:83-106` (reflection shape pins; guards a retired API) | 24 | design policing only | Med-High |
| `Tests/EditMode/ShipReadoutEditModeTests.cs:12-19` (restates `ReservedLines`) | 15 | none | Medium |

**Redundant or subsumed tests.**

| Location | ~Lines | Lost | Conf. |
|---|---|---|---|
| `ObjectiveTrackerEditModeTests.cs:39-44, 59-95, 140-148, 193-205, 244-256, 339-354` (each re-asserted elsewhere in the file) | 85 | none | High |
| `MissileGuidancePlayModeTests.cs:90-137` (weaker twin of :208-245) and `:139-160` (identical to :70-88 once `Missile.cs:77` discards across-aim velocity) | 70 | none | High |
| `ShipChildComponentStatePlayModeTests.cs:76-166` (death/reset hierarchy semantics, subsumed by the functional tests) + `ENABLE_DIAGNOSTICS` scaffolding :24, 59-62, 85-95, 331-337 | 130 | none | High |
| `LockOnRegistryWiringPlayModeTests.cs:21-69` (no-op SetUp + test subsumed by :71-146) | 40 | none | High |
| `TriggerVolumeActivationPlayModeTests.cs:290-330` (triple-covered) | 40 | none | High |
| `RespawnResetEditModeTests.cs:12-40` (`RegenResourceReset`: mutation-blind, see the Combat reviewer's D2 mutation; covered by `DamageControllerEditModeTests:138-156, 252-268`) | 29 | none | High |
| `RLWorkerSeedEditModeTests` + `RLArenaSeedEditModeTests`: collapse to `[TestCase]` rows; drop the self-comparison "deterministic" tests | 55 | none | High |
| `MpcIntentSentenceEditModeTests.cs:189-218` (third-seat copies → `[TestCase(1..3)]`) | 20 | none | High |
| RunTally pairs (`RunTallyEditModeTests.cs:49-107`), WeaponCommandDispatch Primary/Secondary (:74-116), DamageController :53-61 & :125-136 | 60 | none | High |
| `AsteroidFieldCoreEditModeTests` determinism cluster (8 near-identical same-input→same-output proofs; keep one full-pipeline one) | 45 | none | Med-High |
| MPC seam overlap: `MpcAnchoredIntentEditModeTests.cs:34-59, 329-385`, `MpcSolverTests.cs:110-138` | 66 | none | Medium |
| `WeaponTriggerSemanticsPlayModeTests.cs:185-215` (a superset exists in `ParentedEntityIdentityPlayModeTests:99-113`) | 31 | the exact `45f` only | Med-High |
| `TacticalObservationEditModeTests` `Populate`/`ThreatScanner` parts (feed only the gizmo overlay; target/self paths duplicate `RLAgentEditModeTests.Fill_*`) | ~200 | gizmo-overlay regressions | Medium |
| Sectors/Ships: `ShipPresentationFootprintPlayModeTests` (70), `TriggerVolume` ExtractionZone compound (28), `Respawn_OriginNone` (22), `RespawnEditModeTests:28-40` (12) | 132 | "rig base loses all AudioSources" (Footprint only) | Medium |

**Helpers, boilerplate and dead code.**

| Location | ~Lines | Lost | Conf. |
|---|---|---|---|
| `AsyncAssert.cs:106-154` (`WaitForFloatWithinTolerance`, `WaitForVector2NearTarget`: 0 callers) | 50 | none | High |
| `TestSceneBuilder.cs` arena ceremony + `PositionForTest` (0 callers); inline `CreateObstacle` (1 caller) | 65 | none | High |
| `TestAssets.cs` single-use wrappers + `#else` branches → one generic `Load<T>` | 55 | none | High |
| Three AICommander fixtures' private `TestableCommander`/`StubStatus`/`ScriptedBrain`/weapon stubs → one shared set | 100 | none | High |
| `RLAgentEditModeTests.cs:506-673` five copies of opponent try/finally (fixture already has a tracked list + TearDown) | 30 | none | High |
| Academy auto-stepping reset blocks (`RLAgentPlayModeTests:61-63,82-83`, `RLMultiArena:62-64,93-94`, `RLSelfPlay:56-58,77-78`; nothing sets it false) | 15 | none | High |
| `MpcPerformancePlayModeTests.cs:123` `avgShipSolveMs > 0` (the rubric's own tautology example; the upper bounds stay) | 1 | none | High |
| Change-detector literals in `CombatSectorPrefabEditModeTests` (indices `Modules[4]`, coordinates), `LocaleSkyWiringEditModeTests` (absolute render queues, art names), `IllustratedShipPrefabEditModeTests.cs:43` (pins a *deferred* disabled component) | 40 | none | Med-High |
| UI smalls: `EventDrivenRefactor:49-70`, `UILifecycle:106-120`, `StarfieldParallax:110-126`, `StarfieldShader:43-46`, `HangarFlow:41-47` | 60 | none | Med-High |

**Script suite (`scripts/tests/`).**

| Location | ~Lines | Lost | Conf. |
|---|---|---|---|
| `test_pool_merge_gate.sh:484-491` + stub verdict ladder `:104-113`: the stub stamps partial and the test asserts partial. Replace with the direct `Get-CoverageVerdict` test in the add list. | 16 | none | High |
| `test_delivery_timing.sh:127-136` (duplicates `test_pool_script_tests.sh:93-111`) | 10 | none | High |
| `test_pool_locking.sh:279-296` (timing-luck race, deterministic twin in `test_pool_lock_serialization.sh:91-101`) | 18 | none | Med-High |
| Pinned human-only text: `test_merge_reconcile.sh:122,129,148,195,201,238`, `test_on_event_triage.sh:133,143,148,255`, `test_pool_merge_gate.sh:407,781,818` | 14 | none | High |
| `test_scope_resolution.ps1:168-193` (asserts real-repo tags outside its `covers` set, so it breaks late on the wrong PR) | 25 | none | Med-High |
| `test_inert_diff.ps1:183-193` ("false negative by design" pins) | 11 | none | Medium |
| Dead `nonhermetic` skip machinery (`pool:1101-1103, 1114-1117`; the list is empty by design) | 6 | none | Medium |

**Kept on purpose:**
- `GateTestRunner.cs`: live (`runner:1595`); saves ~25 s per gate.
- The RL schema pins (`wVelTrack == 50`, `ApplySchema` literals): declared trained-interface pins.
- HeatOverheatStress and Concussion at both EditMode and PlayMode: they are not duplicates.

**Totals:** about 1,850 lines high confidence, plus about 2,100 medium or lower, for about 3,950 of roughly 33,200 test lines (12%).

---

## 4. Add list

| # | Risk bought | Size | Seam |
|---|---|---|---|
| A1 | Cold ran-zero reads as red (F3) | runner ~6 + test ~10 | `runner:1719-1730`; fake-Unity case with `total="0"` in `test_runner_access_refusal.ps1` |
| A2 | Missing asset or layer fails loud (F2) | ~0 net (13 swaps) | `Assert.IsNotNull` at the call sites listed in F2; or make `TestAssets.Load*` throw |
| A3 | Hangar input gate / ship swap / session seam gated (F1) | ~3 lines of retag + production seam | Headless `PlayerRig` camera seam (`HangarWeaponRowsPlayModeTests:192` pattern) |
| A4 | Coverage verdict proven directly (routed/scoped/status/exclusions → partial) | ~40 | AST-extract `Get-CoverageVerdict`, as `test_memory_stamp.ps1` does for its function |
| A5 | Gate refuses scoped or routed runner args; missing XML, timeout and killed-after-results verdicts | ~40 | `test_pool_merge_gate.sh` (`merge <slot> -- -TestCategory X`); fake-Unity cases in `test_runner_access_refusal.ps1` |
| A6 | Test metadata cannot drift (F8): one domain tag per fixture, scope-map names and paths resolve | ~40 | EditMode test reflecting over `Tests.EditMode`/`Tests.PlayMode` fixtures; extend `test_scope_resolution.ps1` |
| A7 | GameHost restart loop (death → recap → unload → hangar → reload; no ships survive) | ~60 | PlayMode on `GameHost` with the A3 seam |
| A8 | Train/inference observation parity | ~20 after a rung-1 refactor | Hoist the duplicated 13-arg `Fill` call into one function both agents call, then test it once |
| A9 | Truncation vs terminal signalling to ML-Agents | ~25 | `EpisodeLoopDriver` with a recording agent double |
| A10 | Lock → missile handoff; invulnerability expiry | ~55 | PlayMode lock sensor → `ConsumeLock` → `Missile.SetTarget`; EditMode `DamageController` with `Tick`-style clock |
| A11 | Real avoidance proof; heat penalty; KeyPickup respawn after collection; Forces signs | ~50 | Fix the threshold at `MpcNavigatorPlayModeTests:152` to hull distance and add an avoidance-off control; penalty ≠ delay config; sign asserts |
| A12 | Capture lane content (#374 rule) | ~15 | Non-uniform-pixel assert on one filmed frame in `CaptureScenarioPlayModeTests` |
| A13 | Order independence | ~5 | Restore `captureDeltaTime`/`timeScale`/`maximumDeltaTime` in `RLSentencePlayModeTests` TearDown, or make `PacingContract.Apply` return a restorer |

About 370 lines added, plus the production seams for A3 and A8.

---

## 5. Net ledger

| | Lines |
|---|---|
| Removed, high confidence | −1,850 |
| Removed, medium or lower | −2,100 |
| Added | +370 |
| **Net, high confidence only** | **≈ −1,480** |
| **Net, all candidates** | **≈ −3,580** |

Most of the removal is the render studies and generators (about 1,860 lines). None of them runs in any gate, so cutting or moving them costs no gated coverage.

---

## 6. Issue-ready suggestions (the owner picks)

1. **Runner: a run that executes zero tests is never green.**
   - Rung 2 (runner summary).
   - Why: F3. Every other entry point already refuses ran-zero.
   - Prior art: #612 (the script-suite twin), #635 and #582 (same runner, exit and refusal verdicts), `headless-suite.yml` Fullness guards.
2. **Tests fail, not skip, when a required asset or layer is missing.**
   - Narrow rung 4: `Assert.IsNotNull`.
   - Structural rung 2: a gate run with unexpected skips is `partial`. This needs item 4 first, and it interacts with `TESTING.md`'s `[Ignore]` quarantine, so it is the owner's call.
   - Why: F2.
   - Prior art: #752 (test ends early, reads green).
3. **Graphics-tagged behaviour tests never run in any gate.**
   - Narrow rung 2: a path-triggered `-WithGraphics` leg in the gate.
   - Structural rung 1: a headless `PlayerRig` seam, then retag Hangar* and SessionSeam with a domain.
   - Why: F1.
   - Prior art: #764 (live failure), #702, and the "owed graphics tests" checklist on PR #755. No closed issue rules on this.
4. **Move studies, generators and env-gated tools out of the test tree.**
   - No rung; this is dead weight.
   - Why: C-studies, plus F5 (LayeredExplosion rewrites a shipped prefab).
   - Prior art: #685 (closed; what the studies served), and PR #730's note calling the study the prefab's "authoring recipe".
5. **Restore global Time state after RL fixtures.**
   - Narrow: TearDown.
   - Structural rung 1: a scoped `PacingContract.Apply` that returns its restorer.
   - Why: F5.
   - Prior art: none found.
6. **Lint test metadata: exactly one domain tag per fixture; scope-map names and paths resolve.**
   - Rung 2 (deterministic check on authored metadata).
   - Why: F8, and five untagged fixtures.
   - Prior art: #518 (the overlay-denylist rationale presumes every fixture is tagged).
7. **Directly test `Get-CoverageVerdict` and the gate's scoped-args refusal; add missing-XML and timeout verdict cases.**
   - Why: F7 (harness).
   - Prior art: #455 (verdict ownership put the stamp in the runner), and #635 (test style to copy).
8. **Share test stubs through `Tests.Common`.**
   - Stub copies: `IShipStatus` ×11, `IShooter` ×5, `IWeapons` ×5, `IWeaponContext` ×4, `IPilot` ×4, `DamageRecorder` ×3, `StubExtractionZone` ×3.
   - A hygiene PR, per the ratchet rule.
   - Prior art: the `TESTING.md` "third copy is a smell" rule; #456 (the scripts twin).
9. **`TESTING.md` drift sweep** (docs-only). See criterion 10 below.
   - Prior art: #661 (doc residue after changes).
10. **Close the high-blast-radius gaps in F7.**
    - Split into separate issues: GameHost restart loop; RL observation parity (rung 1: one `Fill` call site); truncation signalling; lock → missile.
    - Prior art: #739 (LivePilotAgent teardown NRE; touches the same agent).
11. **Script-suite runtime and flakes.** Already owned by #664, #639, #762 and #542; nothing new to file.
    - Add to #664: run the selected script tests at `submit`/`revise` as well as at merge.
12. **Fix the latent coordinator bypass in `test_scope_resolution.ps1:225-232`.**
    - Pass the stub `-UnityPath`/`-ProjectPath`, as `:238` already does. A one-line fix under dependency rule #6.
    - Prior art: the #297 and #293 family (coordinator hardening).

---

## Criterion notes (evidence the scorecard compresses)

**1. Verdict integrity: probes judged statically.** Each probe gives the code path, the expected verdict, and the command that would verify it.

- **Empty filter.**
  - `-TestFilter NoSuchFixtureXyz` → `total=0` → `passed`, exit 0 (`runner:1719-1730`).
  - `-Routed` → refused as `infra_error` (`:1356-1358`).
  - Verify: `.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter NoSuchFixtureXyz`.
- **Misspelled category.** Same result as an empty filter.
  - Verify: `.\scripts\unity_test_agent.ps1 -Mode Both -TestCategory Weapon`.
- **Missing tests dir.**
  - Script suite: fails loud (`pool:1107-1110`; #612, tested).
  - Unity runner: Auto/Module indexing returns an empty index → fallback to the full suite (`scripts/unity_test_scope_lib.ps1:221-224`), which is safe.
- **Unity crash before XML.** "Result XML not found" → `infra_error` (`runner:869-872`), exit 2. Correct but **untested**.
  - Verify: add a fake-Unity case that writes no XML.
- **Null or unknown exit code.** `infra_error`, tested at `test_runner_access_refusal.ps1:166-169` (#635).
- **Nonzero exit with green XML.** `infra_error`, tested at `:161-164`.
- **Timeout, and killed after results.** Handled at `runner:1614-1636`. Untested.
- **Test that ends early (the #752 shape).**
  - No `Assert.Pass` remains (grep of `Tests/`), and `AsyncAssert.WaitUntil` now returns (`AsyncAssert.cs:34-35`).
  - A runtime `Assert.Ignore` is the live variant (F2).
  - No test-helper guard exists against re-introducing `Assert.Pass`: an observation only, since there is no current failing scenario.
- **Script suite, zero selected.** Returns 0 by design and prints `unlisted` paths (`pool:1184-1187`; asserted at `test_pool_script_tests.sh:191-198`). It honestly means "no test covers this", so it is an observation, not a false pass.
- **Hosted CI.** Refuses `total=0` or an unreadable `failed` per mode. Sound.

**2. Mutation sample (32 mutations across 14 domain categories).**

Red (20):
- MPC:
  - bank narrowing (`Obstacles.cs:17`)
  - receding guard (`:62`)
  - field scale (`:22`)
- Asteroids: unload radius (`ChunkStreamer.cs:55`)
- Physics: fragment `retained` factor (`Calculator.cs:38`)
- Camera: shift sign (`CameraUtils.cs:42`)
- Weapons:
  - heat hysteresis (`Heat.cs:69`)
  - missile along-aim clamp (`Missile.cs:77`)
- Damage: death latch (`DamageController.cs:63`)
- Objectives: `IsCurrent` check (`ObjectiveService.cs:50`)
- Targeting: gunsight LOS (`Gunsight.cs:41`)
- AI/RL:
  - outcome precedence (`EpisodeRules.cs:25`)
  - roster margin (`RockSlotRoster.cs:196`)
  - reward call (`EpisodeLoopDriver.cs:89`)
- UI: ammo reload (`AmmoCounterUI.cs:67`)
- Presentation: applier ordering (`PresentationApplier.cs:244`)
- Rendering: capture framing (`CaptureFraming.cs:33`)
- Sectors: despawn on teardown (`Sector.cs:109`)
- Bootstrap: rig instantiation (`GameHost.cs:129`)
- Ships: status-bar subscription (`StatusBarUI.cs:47`)

Green (12):
- avoidance off
- Forces signs
- CameraUtils vertical branch
- heat penalty
- regen reset
- KeyPickup respawn
- minimap subscription
- AICommander `ResetState`
- truncation signalling
- hangar presentation gate
- session holder
- ambush pending-fire cancel

Harness: 3 red and 3 green:
- Red: exit-code rule (`runner:928`), lane aggregation (`pool:1196`), `drain_pick` ranking.
- Green: scoped-args refusal (`pool:2109`), routed → partial (`runner:943`), local ratchet inside merge (`pool:2131`).

The harness mutations are not counted in the 32.

**3. Timing inventory.** Wall-clock sources in the Unity suite:

- **`AsyncAssert.WaitUntil`/`WaitUntilThen` (`AsyncAssert.cs:30-32, 61-63`, `realtimeSinceStartup`).** It fails red only, never green.
  - Callers: MissileGuidance (5–8 s at 20× time), MpcNavigator (8 s), CameraFollow (3 s), Scanner (3 s, Smoke), AiCommanderDeterminism (5 s, no acceleration).
  - InferencePilot (5 s, covers Academy cold init) is **#651**, and it is the one known to fail under concurrent boots.
  - `GameHostRigPlayModeTests.cs:62-64` has a 30 s boot deadline.
- **`MpcPerformancePlayModeTests.cs:118-127`.** Stopwatch ceilings (10 ms average, 100 ms worst frame) run in every gate, although `doc/agents/testing.md` says perf "runs SOLO". The ceilings are generous, but the worst-frame sum over 120 frames is exposed to preemption under three boots: a false-red risk.
- **Deterministic.** Everything else counts fixed steps or frames, or uses `Tick(dt)`.
- **Script suite:** see F6.

**6. Feedback loop.**
- **Iteration runs.** A routed run in a warm editor took 8.5 s end to end (`results/unity-tests-agent/latest-summary.json`, 4 tests). A cold boot has an ~80 s floor (`TESTING.md` § Routed runs). `-ScopeType Auto` maps diffs to domain categories, and unmapped paths fall back to the full suite, which is safe. `Scripts/Game/**` and `Scripts/Substrate/Sessions/**` map to no module, so session work always runs full.
- **Gate phases** (last ~30 journals): tests 111–600 s against 480; resharper up to 592 s against 360; remote proof 523–728 s against 900.

**7. Isolation positives.**
- `PlayModeWorldFixture` restores `timeScale`, `maximumDeltaTime` and audio (`Tests/PlayMode/Common/PlayModeWorldFixture.cs:39-73`).
- Every Unity launch in `scripts/` goes through the coordinator: the runner via `Acquire`/`BootAcquire` (`runner:662-669`), and the solution sync via `RunBatch` (`scripts/resharper_ratchet.ps1:154-170`).
- Script tests use temp lock roots and fail-closed stubs.

**8. Diagnosability.**
- **Strengths:**
  - Every `infra_error` run carries a `note`, and a missing XML or unknown exit also carries a `logTail`.
  - `infra_error` greps `error CS` lines inline.
  - Failures carry message plus top frame.
  - The merge journal survives slot recycling (`.worktree-pool/merge-runs/`).
- **Gaps:** ran-zero carries no note. The summary itself lives in the slot's `results/` (`doc/agents/testing.md` tells agents to copy it out).

**9. Health.**
- Reflection into private structure in `Tests/EditMode/Rendering/CaptureLayersEditModeTests.cs:21-24` and `ShipPresentationPlayModeTests.cs:106`: missing seams.
- `RespawnResetEditModeTests.cs` holds two fixtures, neither named after the file.
- about 20 dead `#else Assert.Ignore("Requires Unity Editor")` branches. Low value.

**10. Doc drift.** These were checked statically in place of running 3–4 documented commands.
- **`finalize`.** `./scripts/agent_worktree_pool.sh finalize agent-1 origin/main -- -Mode Both ...` is described as "prepare + run tests + create PR + release lock" (`TESTING.md` § Warm Worktree Pool). The real `cmd_finalize` (`pool:2174-2215`) requires an already-*merged* PR, runs no tests and creates no PR.
  - Verify: `./scripts/agent_worktree_pool.sh help`.
- **`-TestFilter "Category=Smoke"`.** Runs zero tests (F3).
- **Helpers.** `AsyncAssert.WaitAndAssertRemainsFalse`, `TestAssets.LoadDefaultShipSettings` and the no-arg `ShipTestFactory.CreateDefaultShip()` do not exist. The real ones are `AssertRemainsFalseFor` and `CreateDefaultShip(IProjectileService, ...)`.
- **Quarantine example.** The `[Ignore]` example `MissileGuidancePlayModeTests.Target90Degrees_Converges` does not exist.
- **Parameter table.** `-UnityPath` defaults to empty and resolves from `ProjectVersion.txt` (`runner:55`), not to the documented `D:\Programs\...` path.
- **Test structure.** "17 total" fixtures per mode is wrong: there are about 100 EditMode files.
- **Domain table.** It lacks `Asteroids` and `Presentation`, which are used by 7+ fixtures. `RequiresGraphics` is absent from the category axes.
- **Scope map.** The smoke scope's `CollisionDamageUtilityTests` does not exist.

---

## Confidence and limits

**What I read myself, in full:**
- `runner`, `scripts/unity_test_scope_lib.ps1`, `scripts/unity_test_scopes.json`, `GateTestRunner.cs`
- `AsyncAssert.cs`, `PlayModeWorldFixture.cs`, `TestAssets.cs`
- `.github/workflows/headless-suite.yml`
- the merge-gate sections of `pool` (proof/coverage `:341-515`, run-tests `:961-1025`, script suite `:1095-1290`, `cmd_merge` `:1930-2172`, `finalize`)
- `test_runner_access_refusal.ps1`, and the runner-invoking part of `test_scope_resolution.ps1`
- `TESTING.md`, `doc/agents/testing.md`, `AGENTS.md`

**Mined for data:** 60 merge journals, git history (co-change and `fix(...)` regression tests), and issues #518, #651, #752, #764 and PR #755.

**Delegated reads.** The bulk read of the Unity suite (all ~185 files) and of `scripts/tests/` (~4.7k lines) was split across six parallel read-only reviewers, one per domain slice. Their mutation arguments, cut candidates and line numbers are reported as they gave them.

**Claims I re-verified directly:**
- MPC avoidance threshold; Forces smoke guard; Heat penalty configs
- ObjectiveChannel `DoesNotThrow`; AbilityLane `ResetState`
- RLSentence `PacingContract` leak; RLEpisode `continue`
- `LayeredExplosion` prefab write and its `SpawnSettings.asset` reference
- CaptureScenario existence-only; HangarFlow tautology
- SectorComposition `Assert.Ignore`
- the untagged fixtures
- the EditorPrefs write
- the `test_scope_resolution` runner calls; the gate test's bash re-implementation of the coverage verdict
- the stale scope-map names

Other slice-level line numbers carry the reviewers' confidence, not mine.

**Not read:**
- `scripts/unity_access_lib.ps1` in depth (only its entry and the call contract)
- `training/rl/` drivers
- `scripts/capture/*` internals
- the RLHarness editor assembly
- the other reviewer's report, and `reports/test-infra-review/codex-run.log`, which were deliberately skipped for independence

**Static — unverified.** No test, Unity or pool command was run. These conclusions were not observed:
- F3's ran-zero verdict. Command: `.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter NoSuchFixtureXyz`.
- Every "stays green" mutation. Verify each by applying the mutation in a pooled slot and running `-TestCategory <Domain>`. The MPC avoidance case also depends on the ship's collider radius, which I did not open.
- The order-dependence consequence of the `PacingContract` leak. Verify by running `-Mode PlayMode -TestFilter "RLSentencePlayModeTests|ScannerPlayModeTests"` against `ScannerPlayModeTests` alone.
- All survivability-under-three-boots judgements in criterion 3.
- The `finalize` doc divergence (verify with `./scripts/agent_worktree_pool.sh help`).

**Timing numbers** come from existing journals and summaries, not from fresh runs.
