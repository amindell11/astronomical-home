# Test infrastructure review — static-only

**Weighted grade: 84/148, or 56.8%.**

The suite contains substantial behavioral coverage, and the merge machinery protects several important invariants. The main weakness is fundamental: the cold runner can report success after executing zero tests, and its coverage stamp can accept platforms containing only skipped tests. Minimalism is mixed: valuable integration tests coexist with signature checks, duplicated stubs, and assertions that do not prove their advertised behavior.

Reviewed against `reports/test-infra-rubric.md`, at HEAD `a0869ce928d521f2b6f892f18ee475e812d4dee4`, including the visible working tree. No tests, Unity actions, pool commands, writes, issues, or PRs were performed.

For compact citations below, **`T/` means `src/Asteroids3D/Assets/Scripts/Editor/Tests/`**. Scores assess source and history; execution predictions are **static — unverified**.

## 1. Scorecard

| Criterion × layer | Score / 4 | Weight | Evidence |
|---|---:|---:|---|
| 1. Verdict integrity — Unity | 2 | 3 | Polling helper now returns normally, but required-asset absence still skips substantive cases: `T/PlayMode/Common/AsyncAssert.cs:35`; `T/PlayMode/SectorCompositionPlayModeTests.cs:146`. |
| 1. Verdict integrity — Harness | 0 | 3 | Zero tests fail only with `-WithGraphics`; otherwise zero failures become success. All-skipped platform records can receive full coverage: `scripts/unity_test_agent.ps1:1719`, `:1724`, `:963`. |
| 2. Assertion strength — Unity | 3 | 3 | Strong damage, reload, solver, ownership and rendered-content assertions; weaker force, marker and reset assertions. Mutation analysis below: `T/EditMode/DamageControllerEditModeTests.cs:81`; `T/EditMode/ObjectiveChannelEditModeTests.cs:66`. |
| 2. Assertion strength — Harness | 3 | 3 | Refusal tests inspect exit, summary, trailer and absence of launch; merge tests inspect actual refs/provenance. Missing verdict cases remain: `scripts/tests/test_runner_access_refusal.ps1:106`; `scripts/tests/test_pool_merge_gate.sh:574`. |
| 3. Determinism/load tolerance — Unity | 2 | 2 | Injected `Tick(dt)` and fixed-step budgets are common, but inference initialization has a five-second realtime deadline: `T/EditMode/RoundsReloadEditModeTests.cs:209`; `T/PlayMode/InferencePilotPlayModeTests.cs:28`. |
| 3. Determinism/load tolerance — Harness | 2 | 2 | Good synchronization fixtures coexist with an observed load-sensitive 30-second refusal bound: `scripts/tests/test_pool_lock_serialization.sh:65`; `scripts/tests/test_pool_merge_gate.sh:855`; [#762](https://github.com/amindell11/astronomical-home/issues/762). |
| 4. Risk coverage — Unity | 3 | 2 | Broad domain coverage and useful regression history; malformed asteroid build-input rejection lacks a regression: `T/EditMode/AsteroidRadiusEditModeTests.cs:106`; `Assets/Scripts/Editor/Asteroids/AsteroidGeometryBuildGate.cs:77` within the Unity project. |
| 4. Risk coverage — Harness | 3 | 2 | Exact-tree proof, stale declarations, renamed paths and routed result loss are tested; cold zero/all-skipped/inconclusive verdicts are not: `scripts/tests/test_scope_resolution.ps1:208`; `scripts/tests/test_routed_result_parity.ps1:47`. |
| 5. Economy — Unity | 2 | 2 | Signature-count/index checks changed with a hierarchy refactor; unused helpers and a masked reset assertion remain: `T/EditMode/SessionContractsEditModeTests.cs:166`; commit `995b6930`; `T/PlayMode/Common/TestUtilities.cs:19`. |
| 5. Economy — Harness | 3 | 2 | Previous consolidation earned measurable savings and retained broken-helper trials; a small suite-execution duplication remains: `scripts/tests/test_delivery_timing.sh:17`; `scripts/tests/test_pool_script_tests.sh:99`; [PR #675](https://github.com/amindell11/astronomical-home/pull/675). |
| 6. Feedback cost — Unity | 2 | 2 | Warm iteration exists, but cold boot dominates small slices; `ValidateScope` executes two additional probe runs: `TESTING.md:98`; `scripts/unity_test_agent.ps1:435`, `:1498`. |
| 6. Feedback cost — Harness | 2 | 2 | Parallel runtime lanes and diff selection help. Historical full-suite timing still exceeded the 300-second objective; budgets are reporting thresholds, not universal cancellation: `scripts/agent_worktree_pool.sh:1190`, `:1491`; [PR #671](https://github.com/amindell11/astronomical-home/pull/671). |
| 7. Isolation — Unity | 2 | 2 | Fixture-owned arena/time cleanup is sound, but some roots are destroyed only after assertions succeed: `T/PlayMode/Common/PlayModeWorldFixture.cs:57`; `T/PlayMode/UILifecyclePlayModeTests.cs:56`, `:88`. |
| 7. Isolation — Harness | 3 | 2 | Per-project ownership, a shared boot lane and injected test state are tested. Public `-SkipUnityAccess` still bypasses admission for a real cold launch: `scripts/tests/test_unity_access.ps1:171`; `scripts/unity_test_agent.ps1:197`, `:671`. |
| 8. Diagnosability — Unity | 3 | 1 | Many failures identify the violated behavior and observed values; invariant failures and content probes are particularly useful: `T/PlayMode/Bootstrap/GameHostRigPlayModeTests.cs:65`; `T/EditMode/Rendering/StarfieldHaloRenderTests.cs:108`. |
| 8. Diagnosability — Harness | 3 | 1 | JSON verdicts, process timing, failure details, coordinator refusals and CI uploads are useful. Some exceptional paths intentionally emit no summary: `scripts/unity_test_agent.ps1:846`, `:1732`; `scripts/tests/test_runner_access_refusal.ps1:140`. |
| 9. Test-code health — Unity | 2 | 1 | Three identical AI status/context stubs; five fixtures lack a domain tag; reflection checks duplicate compiler-enforced interfaces: `T/EditMode/AICommanderAbilityLaneEditModeTests.cs:33`; `T/PlayMode/ShipReequipPlayModeTests.cs:1`. |
| 9. Test-code health — Harness | 3 | 1 | Extensive hermetic fixtures and tested library/CLI parity. Category extraction recognizes only a subset of legal attribute syntax: `scripts/tests/test_unity_access.ps1:120`; `scripts/unity_test_scope_lib.ps1:211`. |
| 10. Doc–reality drift — Harness | 1 | 1 | Documented legacy category selection, “dry-run” validation and one-shot finalize behavior disagree with source: `TESTING.md:291`, `:215`, `:65`; command traces below. |

Criterion 10 is scored once, against the harness whose commands the documentation describes.

| Layer | Weighted points | Maximum |
|---|---:|---:|
| Unity | 42 | 72 |
| Harness | 42 | 76 |
| **Total** | **84** | **148** |

### What earns its place

The exact-tree landing proof is valuable machinery. Scoped, routed, wrong-project and unstamped results do not automatically become landing proof; proof is associated with the actual tree, and the merge tests exercise these distinctions. Evidence: `scripts/agent_worktree_pool.sh:352–399`; `scripts/tests/test_pool_merge_gate.sh:425–428`, `:485–491`.

Routed named-result parity also earns its place. It catches results lost during domain reload even when aggregate pipeline counts appear green. Removing that check was considered and rejected on evidence in [#697](https://github.com/amindell11/astronomical-home/issues/697). Likewise, string/object pipeline parsing is a live compatibility requirement, not an obsolete workaround: [PR #659](https://github.com/amindell11/astronomical-home/pull/659).

The best Unity tests assert behavior across transitions: damage crossing shield depletion, pending reload cancellation, stale objective ownership, presentation off/on reuse, and actual rendered content. These deserve preservation.

## 2. Findings

### F1 — High: cold success does not require a successful executed test

**Scenario:** an empty name filter or misspelled category produces valid zero-test XML and Unity exits zero. The runner reports `passed` and exits zero.

`Parse-UnityResultXml` reads totals but does not reject zero execution. Overall aggregation considers infrastructure errors and numerical failures; its explicit zero-test rejection applies only to `-WithGraphics`. Evidence: `scripts/unity_test_agent.ps1:918–928`, `:1708–1729`.

Two related scenarios use the same weak verdict boundary:

- An XML root marked `Inconclusive`, with `failed=0` and process exit zero, produces a per-run failure at `:913` but can still produce overall success at `:1724`.
- Both platforms contain only skipped cases: `total>0`, `passed=0`, `failed=0`, status `unknown`. Coverage accepts those records at `:962–970`. The pool trusts that stamp at `scripts/agent_worktree_pool.sh:390`.

CI’s fullness check similarly requires positive totals and zero failures, without requiring an executed passing case: `.github/workflows/headless-suite.yml:101–107`. This identifies an acceptance hole; it does **not** establish that a current hosted run actually contained only skips.

Malformed numeric attributes are another boundary weakness: `To-Int` converts absent or invalid integers to zero rather than distinguishing unreadable verdict data. Evidence: `scripts/unity_test_agent.ps1:292–298`.

**Static — unverified.** Exact cold probes, from an authorized pooled project later:

```powershell
.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter "__NoSuchTest__"
.\scripts\unity_test_agent.ps1 -Mode Both -TestCategory Weapnos
```

Inspect the exit code and `results/unity-tests-agent/latest-summary.json`.

The all-skipped coverage acceptance can be verified without launching Unity by evaluating the existing coverage function against synthetic records:

```powershell
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path .\scripts\unity_test_agent.ps1).Path, [ref]$tokens, [ref]$errors)
$fn = $ast.Find({
    param($n)
    $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
    $n.Name -eq 'Get-CoverageVerdict'
}, $true)
Invoke-Expression $fn.Extent.Text
$Mode = 'Both'; $Routed = [switch]$false
$selection = @{scopeType='Workspace'; excludeCategory='RequiresGraphics'}
$runs = @('EditMode','PlayMode') | ForEach-Object {
    [pscustomobject]@{platform=$_; status='unknown'; total=1; passed=0; failed=0; skipped=1}
}
Get-CoverageVerdict -Runs $runs -Selection $selection -OverallStatus passed |
    ConvertTo-Json
```

**Root cause:** success and coverage are reconstructed from incomplete counters rather than requiring a valid, executed outcome. **Chosen rung: 2**, deterministic classification at the result boundary.

Prior art fixed different failures: [#612](https://github.com/amindell11/astronomical-home/issues/612), [#635](https://github.com/amindell11/astronomical-home/issues/635), [#582](https://github.com/amindell11/astronomical-home/issues/582). Closed searches found no direct prior rejection of tightening this cold-result boundary.

### F2 — High: missing required assets can remove the behavior being tested

**Scenario:** `Ship_2.prefab` or `TestPilotMPC.prefab` cannot load. A selected sector-adoption test becomes ignored instead of failing its required premise.

The loaders return null without an assertion: `T/PlayMode/Common/TestAssets.cs:28–45`. Ten sector-composition cases explicitly ignore missing required assets, including adoption, teardown and respawn coverage: `T/PlayMode/SectorCompositionPlayModeTests.cs:146`, `:171`, `:190`, `:207`, `:223`, `:246`, `:292`, `:316`, `:379`, `:405`.

This is distinct from intentional opt-in characterization/emission tests, and from editor-only tests excluded outside their supported environment.

**Static — unverified.** With that missing-asset condition reproduced in a later disposable fixture, verify using:

```powershell
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter "SectorCompositionPlayModeTests.Adopt_AllShipEntries_RegisteredAtPoses"
```

Other workspace tests may independently detect the missing asset; the demonstrated gap is the selected test’s verdict, not a claim that every full suite would remain green.

**Root cause:** required test premises are treated as optional availability. **Chosen rung: 2**, assert required fixture inputs during setup.

[#719](https://github.com/amindell11/astronomical-home/issues/719) resolved import failures through production asset repair, rather than suppressing the failing tests.

### F3 — Medium: two known load failures still measure scheduling instead of the intended behavior

**Scenario A:** cold ML-Agents/Academy/Sentis initialization consumes the inference test’s five-second realtime window. The test fails before obtaining its first decision although subsequent cadence could be correct. Evidence: `T/PlayMode/InferencePilotPlayModeTests.cs:28–30`; existing [#651](https://github.com/amindell11/astronomical-home/issues/651).

**Scenario B:** the hosted-refusal test includes cold PowerShell startup in its “must refuse within 30 seconds” assertion. Under unrelated load, correct refusal takes longer and fails. Evidence: `scripts/tests/test_pool_merge_gate.sh:851–855`; observed failure recorded in [#762](https://github.com/amindell11/astronomical-home/issues/762).

**Static — unverified for the current tree/load.** Exact verification commands, under the coordinator’s admitted concurrent workload later:

```powershell
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter InferencePilotPlayModeTests
```

```bash
bash scripts/tests/test_pool_merge_gate.sh
```

**Root cause:** startup/scheduling latency shares a budget with behavioral progress. **Chosen rung: 1**, express the behavior through simulation work or synchronization state so unrelated elapsed time cannot determine its verdict.

These are existing issues to resolve, not new tickets to duplicate.

### F4 — Medium: failed assertions can bypass cleanup of locally created scene roots

**Scenario:** the lock indicator fails its alpha assertion at line 72. Execution never reaches `Object.Destroy(parent)` at line 88, leaving `RigRoot` and its components in the test scene.

Evidence: `T/PlayMode/UILifecyclePlayModeTests.cs:56–88`. Base teardown destroys its arena host and the arena tracked by `TestSceneBuilder`; it does not own this separate root: `T/PlayMode/Common/PlayModeWorldFixture.cs:59–64`.

Missile guidance also destroys some locally created shooters only after a conditional wait succeeds: `T/PlayMode/MissileGuidancePlayModeTests.cs:77–87`.

The source proves cleanup is bypassed on these failure paths. **Subsequent cross-test failures are static — unverified**, not an observed cascade from this review.

**Root cause:** resource ownership depends on reaching the happy-path tail. **Chosen rung: 1**, register owned roots at creation or enclose their lifetime in `try/finally`.

Do not replace this with a broad scene sweep. [#497](https://github.com/amindell11/astronomical-home/issues/497) records that destroying leaked pooled projectiles in setup was a failed fix. [#508](https://github.com/amindell11/astronomical-home/issues/508) supports fixture-owned teardown.

### F5 — Medium: the public cold-launch bypass defeats coordinator admission

**Scenario:** a caller supplies real Unity and `-SkipUnityAccess`. Acquire, boot admission, attach and release calls return without contacting the coordinator; the process still launches.

Evidence: `scripts/unity_test_agent.ps1:80`, `:197`, `:669–674`.

The script suite has a legitimate hermetic use for this flag with `where.exe`: `scripts/tests/test_scope_resolution.ps1:225–240`. That does not justify a public real-Unity exemption.

**Static — unverified.** A safe later verification command uses the existing non-Unity stub:

```powershell
.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter Probe -SkipUnityAccess -UnityPath "$env:WINDIR\System32\where.exe"
```

It verifies the bypass branch without booting Unity. No concurrent-editor failure is claimed here.

**Root cause:** a test escape hatch is exposed on the production launcher. **Chosen rung: 1**, remove the public bypass after moving its tests to the existing injected coordinator-reply pattern.

[#578](https://github.com/amindell11/astronomical-home/issues/578) knowingly preserved telemetry compatibility for this mode. No read prior-art record justified bypassing ownership or memory admission.

### F6 — Medium: documentation and selection metadata can give the wrong slice

Four documented commands were traced verbatim:

| Documented command | Source-predicted outcome |
|---|---|
| `.\scripts\unity_test_agent.ps1 -Mode EditMode -TestFilter "Category=Smoke"` — `TESTING.md:291` | **Static — unverified:** uses a test-name filter, not category selection; can match nothing and trigger F1. |
| `.\scripts\unity_test_agent.ps1 -ScopeType Feature -ScopeName camera -ValidateScope` — `TESTING.md:212` | **Static — unverified:** executes EditMode and PlayMode probes, then executes the actual suite. It is not the documented dry-run. |
| `./scripts/agent_worktree_pool.sh finalize agent-1 origin/main -- -Mode Both -ScopeType Workspace` — `TESTING.md:66` | **Static — unverified:** requires merged-PR evidence and performs cleanup; it does not implement the documented prepare/test/create-PR flow. |
| `.\scripts\unity_test_agent.ps1 -Routed -Mode EditMode -ScopeType Smoke` — `TESTING.md:102` | **Static — unverified:** matches the described attach-only path, subject to a tracked editor, readiness and exact result parity; coverage remains partial. |

These exact commands are the later verification commands. The finalize example must be checked in a disposable pool fixture, not against an unrelated leased slot.

Validation launches `-runTests`, checks only a positive total, and deletes probe artifacts: `scripts/unity_test_agent.ps1:431–459`. Both platforms are probed at `:1498`; there is no validation-only return before the actual run.

Other concrete selection gaps:

- `ShipReequipPlayModeTests` and `PlayerCommanderReleasePlayModeTests` have no domain tag.
- `HangarInputGatePlayModeTests`, `HangarShipSwapPlayModeTests` and `SessionSeamPlayModeTests` carry only `RequiresGraphics`.
- Selecting `Ships` therefore omits the real engine re-equipment regression in `T/PlayMode/ShipReequipPlayModeTests.cs:39`.
- `Get-CategoriesFromContent` recognizes standalone `[Category("…")]` attributes, but not combined syntax such as `[Category("Camera"), Category("RequiresGraphics")]` in `T/EditMode/Rendering/PlainCaptureViewEditModeTests.cs:10`. Evidence: `scripts/unity_test_scope_lib.ps1:211`.
- The `bootstrap` feature alias selects `SessionContractsEditModeTests`, omitting the real boot regression in `T/PlayMode/Bootstrap/GameHostRigPlayModeTests.cs:39`. Category-based Bootstrap selection covers it.

**Root causes:** stale recipes and unenforced selection metadata. **Chosen rung: 2**, validate metadata deterministically and correct the recipes. No new runtime adapter is needed.

### F7 — Low: some tests detect implementation changes or arrange away their own failure

**Scenario A:** add an optional parameter to `PlayerRig.Build`, retaining behavior and updating callers. The exact arity/index test becomes red. This is supported by actual co-change: hierarchy refactor `995b6930` changed the reflection test from six to seven parameters and shifted indices. Current evidence: `T/EditMode/SessionContractsEditModeTests.cs:166–177`.

**Scenario B:** remove `boost = false` from `AICommander.ResetState`. The reset test still sends a null decision on the next step, which independently clears boost. Evidence: production `src/Asteroids3D/Assets/Scripts/AI/AICommander.cs:115`; `T/EditMode/AICommanderAbilityLaneEditModeTests.cs:159–165`. The no-decision test already covers that later clearing path at `:152–155`.

**Scenario C:** replace `MinimapObjectiveMarker.BindObjectiveService` with a no-op. Its “subscribes to channel” test still passes because it checks only that nothing throws. Evidence: `T/EditMode/ObjectiveChannelEditModeTests.cs:66–79`.

These mutation outcomes are **static — unverified**.

**Root causes:** tests assert interface spelling, a subsequent act, or absence of exceptions instead of the promised observable result. **Chosen rung: 1** for compiler-enforced API checks; strengthen existing behavioral cases rather than adding parallel tests.

Do not indiscriminately delete negative architectural assertions. [#519](https://github.com/amindell11/astronomical-home/issues/519) documents deliberately chosen architecture boundaries.

### Assertion mutation sample

Each row assesses the named test, not whether some other test might catch the same mutation. All outcomes are **static — unverified**.

| Domain | Test evidence | Concrete production mutation | Predicted result |
|---|---|---|---|
| Camera | `T/EditMode/CameraUtilsEditModeTests.cs:13–23` | Remove horizontal-bounds aspect correction. | Fails the expected orthographic size. |
| Physics | `T/EditMode/ForcesEditModeTests.cs:66–77` | Reverse thrust direction, or multiply it by ten in `Movement/Forces.cs:20`. | **Stays green:** checks nonzero magnitude, not direction or scale. |
| Damage | `T/EditMode/DamageControllerEditModeTests.cs:81–89` | Discard damage remaining after shield exhaustion. | Fails expected hull damage. |
| Weapons | `T/EditMode/RoundsReloadEditModeTests.cs:209–223` | Preserve pending regeneration timers during reset. | Fails refill timing after reset. |
| MPC | `T/EditMode/MpcSolverTests.cs:127–138` | Ignore commanded velocity and keep default controls. | Fails directional velocity/displacement assertions. |
| Objectives | `T/EditMode/ObjectiveServiceTwoTierEditModeTests.cs:208–227` | Let an old spine handle close or mutate its successor. | Fails successor identity/target assertions. |
| Objectives | `T/EditMode/ObjectiveChannelEditModeTests.cs:66–79` | Make marker binding a no-op. | **Stays green.** |
| UI | `T/PlayMode/UILifecyclePlayModeTests.cs:127–145` | Bind both bars to shield percentage. | Fails independently seeded health/shield fills. |
| AI | `T/EditMode/AICommanderReferentEditModeTests.cs:137–147` | Cache a rock’s decision-time position instead of reading its live pose. | Fails after the rock moves. |
| AI | `T/EditMode/AICommanderAbilityLaneEditModeTests.cs:159–165` | Remove boost clearing from reset. | **Stays green:** later null decision masks it. |
| Core | `T/EditMode/PlayerInputReaderEditModeTests.cs:63–69` | Stop passing actual mouse coordinates into the projector. | Fails projected position. |
| Sectors | `T/PlayMode/SectorCompositionPlayModeTests.cs:288–308` | Omit ship removal during sector teardown. | Fails registry/spawned-state clearing. |
| Bootstrap | `T/PlayMode/Bootstrap/GameHostRigPlayModeTests.cs:39–73` | Build the player on the prefab asset instead of a scene copy. | Fails instance identity, scene validity or prefab-state assertions. |

### Risk coverage by domain

This is a risk map, not a line-coverage claim.

| Domain | Highest-blast behavior and evidence | Assessment |
|---|---|---|
| AI | Observation/action/reward contracts, live referents and decision cadence: `T/EditMode/RLAgentEditModeTests.cs:138`; `T/EditMode/RewardLayerEditModeTests.cs:164`; `T/EditMode/AICommanderReferentEditModeTests.cs:137`. | Substantial coverage; cold initialization budget remains weak. |
| MPC | Real navigation and catastrophic solver regressions: `T/EditMode/MpcSolverTests.cs:127`; `T/EditMode/MpcSolverRigTests.cs:176`. | Strong behavioral evidence, including historical cold-cache failure. |
| Sectors | Registration, teardown, adoption and respawn policy: `T/PlayMode/SectorCompositionPlayModeTests.cs:203`, `:288`. | Strong paths, weakened by required-asset skips and the position-only non-revival assertion at `:419`. |
| Weapons | Dispatch, trigger semantics and reload state: `T/PlayMode/WeaponTriggerSemanticsPlayModeTests.cs:72`, `:186`; `T/EditMode/RoundsReloadEditModeTests.cs:209`. | Good transition and exclusion coverage. |
| Targeting | Registry-enabled/disabled behavior and cached LOS: `T/PlayMode/LockOnRegistryWiringPlayModeTests.cs:72`, `:112`; `T/EditMode/RespawnResetEditModeTests.cs:72`. | Useful wiring pairs and cache behavior. |
| Objectives | Independent locals and stale ownership: `T/EditMode/ObjectiveServiceTwoTierEditModeTests.cs:101`, `:208`. | Strong ownership coverage; marker consumer remains weak. |
| Camera | Framing, orientation and follow behavior: `T/EditMode/Rendering/CaptureOrientationEditModeTests.cs:13`; `T/PlayMode/CameraFollowPlayModeTests.cs:64`. | Observable geometry and follow assertions. |
| UI | Bound resource values and resubscription: `T/PlayMode/UILifecyclePlayModeTests.cs:51`, `:127`. | Good positive cases; weak inertness assertion and failure-path cleanup. |
| Damage | Shield/hull routing and once-per-life death: `T/EditMode/DamageControllerEditModeTests.cs:81`, `:192`. | Strong, economical state-transition coverage. |
| Physics | Forces and fragment behavior: `T/EditMode/ForcesEditModeTests.cs:66`; `T/EditMode/Asteroids/Fragnetics/FragneticsShippedSettingsEditModeTests.cs:39`. | Fragment regressions are grounded; force direction/scale assertion is weak. |
| Movement | Fixed-step motion, speed ceiling and no-input behavior: `T/PlayMode/ShipSimInvariancePlayModeTests.cs:46`, `:94`. | Work-bounded characterization; forward test logs direction but asserts only speed/distance. |
| Core | Spatial conversion and input projection: `T/EditMode/PlayerInputReaderEditModeTests.cs:63`. | Useful data-flow coverage. |
| Services | Projectile lifecycle and ownership: `T/EditMode/ProjectileServiceEditModeTests.cs:1`; `T/EditMode/ProjectilePoolReturnEditModeTests.cs:34`. | Behavioral ownership checks outweigh API-only contract checks. |
| Bootstrap | Actual host boot on a scene copy: `T/PlayMode/Bootstrap/GameHostRigPlayModeTests.cs:39`. | Good recent regression; feature alias omits it. |
| Ships | Loadout propagation and objective-compatible collider geometry: `T/PlayMode/ShipReequipPlayModeTests.cs:39`; `T/EditMode/Ships/ShipObjectiveColliderWiringEditModeTests.cs:34`. | Useful coverage; category omission undermines selection. |
| Asteroids | Mesh geometry and authored bake validity: `T/EditMode/AsteroidRadiusEditModeTests.cs:62`, `:106`. | Positive shipped-data proof; negative build-validator branches lack coverage. |
| Presentation | Headless/presenting reuse and rendered content: `T/PlayMode/TransientPresentationPlayModeTests.cs:53`; `T/PlayMode/Presentation/FlatBackgroundPlayModeTests.cs:65`. | Particularly good off/on and content assertions. |

**Zero-covered rejection seam:** the only suite reference to `AsteroidGeometryBuildGate.Validate` is the positive shipped-settings test. Making validation return unconditionally would preserve that test’s green result while permitting null/cross-model collider data. Evidence: `T/EditMode/AsteroidRadiusEditModeTests.cs:110`; production validator `src/Asteroids3D/Assets/Scripts/Editor/Asteroids/AsteroidGeometryBuildGate.cs:77–88`. This mutation conclusion is **static — unverified**.

### Last 20 fix commits

History was checked using `git log -20 --grep="^fix(" --name-status`, followed by test-hunk inspection.

**Twelve changed regression test files; two explicitly relied on existing tests; six had no test-file change.** A missing new test is not automatically a coverage gap.

| Commit | Regression evidence |
|---|---|
| `66f9509d` — polling helper | Existing inference tail now executes; no direct helper-continuation regression added. |
| `4f51f036` — GameHost rig copy | New real boot regression, `T/PlayMode/Bootstrap/GameHostRigPlayModeTests.cs:39`. |
| `16c3f1b6` — inactive adopted ship | New deterministic rejection, `T/PlayMode/SectorCompositionPlayModeTests.cs:203`. |
| `77a772d8` — Blender dependency | Asset/import repair; no test-file change. |
| `8e15e140` — regeneration description | Updated shipped text assertion, `T/EditMode/ConcussionGrenadeEditModeTests.cs:41`. |
| `736e214a` — pool acquire preservation | New head/state/preservation assertions, `scripts/tests/test_pool_locking.sh:83`. |
| `666bd15f` — PR body-file transport | New large-body argument proof, `scripts/tests/test_pool_pr_seams.sh:81`. |
| `630c7235` — routed dropped results | New parity failures, `scripts/tests/test_routed_result_parity.ps1:47`. |
| `0803f79c` — unknown/nonzero exit | New parser cases, `scripts/tests/test_runner_access_refusal.ps1:148`. |
| `1eca970d` — background depth | Prefab-only change; no test-file change. |
| `c7e54a8f` — nebula material | Material-only change; no test-file change. Do not add tests for a subsequently replaced presentation feature. |
| `df9e82f0` — inherited velocity | Fragment/missile regressions, `T/EditMode/Asteroids/Fragnetics/FragneticsShippedSettingsEditModeTests.cs:39`; `T/PlayMode/Combat/Projectiles/MissileLaunchPlayModeTests.cs:54`. |
| `b3b1ea6d` — refusal classification | New refusal/lease/channel cases, `scripts/tests/test_runner_access_refusal.ps1:125`. |
| `2542768a` — absent script suite | Missing/empty suite regressions, `scripts/tests/test_pool_script_tests.sh`. |
| `db2301cb` — boot memory demand | Calibration change; no test-file change. |
| `6932bd8c` — capture pacing | Updated cadence/restoration assertions, `T/PlayMode/RLCapturePlayModeTests.cs:266`. |
| `bfd624a6` — star halo continuity | New nonblank content/continuity checks, `T/EditMode/Rendering/StarfieldHaloRenderTests.cs:108`. |
| `06f3d802` — Burst bimodality | Existing minefield test caught it; [PR #590](https://github.com/amindell11/astronomical-home/pull/590) records cold-cache main failures and subsequent repeated passes. |
| `f4bd7545` — grain/camera NaNs | Render-settings change; no test-file change. |
| `48742255` — asteroid geometry validation | Existing shipped-settings proof; [PR #586](https://github.com/amindell11/astronomical-home/pull/586) documents positive validation, not malformed-input rejection. |

### Timing inventory and three-launch assessment

The following inventory groups repeated assertions by timing mechanism.

| Mechanism and locations | Bound | Assessment |
|---|---|---|
| Injected time: heat, reload, damage and activation cases, including `T/EditMode/RoundsReloadEditModeTests.cs:209`; `T/EditMode/SectorActivationEditModeTests.cs:109` | Explicit `dt`/threshold inputs | Resistant to scheduling pressure. |
| AI acquisition: `T/PlayMode/AiCommanderDeterminismPlayModeTests.cs:69` | 5 s realtime | Startup/load sensitivity possible; no current failure established here. |
| Inference: `T/PlayMode/InferencePilotPlayModeTests.cs:28` | 5 s realtime | Known load failure, #651. |
| Camera/scanner: `T/PlayMode/CameraFollowPlayModeTests.cs:21`, `:64`; `T/PlayMode/ScannerPlayModeTests.cs:20`, `:53` | 3 s realtime | Conditional waits, but progress still depends on wall time. |
| MPC yaw: `T/PlayMode/MpcNavigatorPlayModeTests.cs:24`, `:86` | 8 s realtime | Load-sensitive scheduling budget. Other navigation loops use 20 simulated seconds at `:64`, `:137`. |
| Missile guidance: `T/PlayMode/MissileGuidancePlayModeTests.cs:81`, `:153`, `:179`, `:199` | 5/5/5/8 s realtime | Conditional waits; close-range cases instead accumulate fixed steps. |
| Host boot: `T/PlayMode/Bootstrap/GameHostRigPlayModeTests.cs:27`, `:62` | 30 s realtime | Readiness watchdog; more tolerant, still unmeasured under current load. |
| Episode/archetype completion: `T/PlayMode/RLEpisodePlayModeTests.cs:458`; `T/PlayMode/OpponentArchetypePlayModeTests.cs:201` | `120 + simulation duration`; `120 + 2×duration` | Generous watchdogs, distinct from simulation outcome budgets. |
| Negative camera proof: `T/PlayMode/CameraFollowPlayModeTests.cs:92`; `T/PlayMode/Common/AsyncAssert.cs:83` | Rendered frames/fixed steps/unscaled duration | Explicitly covers requested cadences; stronger than an arbitrary short sleep. |
| Activation integration: `T/PlayMode/TriggerVolumeActivationPlayModeTests.cs:263` | Bare scaled wait, 0.3 s | Tests a simulation-time threshold; presence of this wait alone is not proof of a flake. |
| MPC performance: `T/PlayMode/MpcPerformancePlayModeTests.cs:120–128` | Average <10 ms; worst frame <100 ms; positive measurement and movement | Real catastrophic-regression checks. Keep the test; `solveMs > 0` is only its instrumentation premise. |
| NUnit watchdogs | 180 s Vanguard; 240 s LayeredExplosion; 600 s across RL, sector, capture, encounter and trial fixtures; 3,600 s optional episode/archetype characterizations | Hang protection, not performance assertions. Examples: `T/PlayMode/Rendering/Vanguard/VanguardStudyPlayModeTests.cs:24`; `T/PlayMode/RLEpisodePlayModeTests.cs:393`. |
| Coordinator heartbeat/race/readiness: `scripts/tests/test_unity_access.ps1:220`, `:227`, `:427`, `:526`, `:535`, `:602`, `:669`, `:907` | Ages <60 s; readiness 60/30/30/15 s; early profile failure; memory refusal <15 s | Mostly bounded synchronization proofs; startup-inclusive bounds remain load-dependent. |
| Pool coordination: `test_pool_lock_serialization.sh:65`, `:126`, `:147`, `:161`; `test_pool_hold.sh:130`; `test_pool_script_tests.sh:75` | 15/10/60 s readiness windows | Genuine synchronization watchdogs. |
| Merge fixtures: `test_pool_merge_gate.sh:70`, `:161`, `:855`, `:893` | 30 s barriers/refusal; 120 s gate synchronization | Refusal bound has an observed false red, #762. |
| Journal timestamp: `scripts/tests/test_delivery_timing.sh:12–16` | Timestamp must equal either surrounding second | **Observation:** unnecessarily excludes an intermediate second if scheduling stalls across several seconds. No historical failure found. |

With three concurrent launch requests, the coordinator serializes the boot lane; it does not serialize all subsequent execution. Realtime deadlines and solve timings can still share machine pressure after admission. **Survival under that workload is static — unverified.** The exact inference and merge-fixture commands are given in F3. A later solo performance check is:

```powershell
.\scripts\unity_test_agent.ps1 -Mode PlayMode -TestFilter MpcPerformancePlayModeTests
```

### Feedback cost and selection observations

Historical measurements show worthwhile improvement:

- Merge fixture mean **547→278 seconds**, with **78→42 pool calls**; the narrower slice targets were still missed. [PR #675](https://github.com/amindell11/astronomical-home/pull/675).
- Coordinator suite **188→40/45/52 seconds**, retaining real-process proofs; that PR’s full script suite still took **892 seconds**. [PR #671](https://github.com/amindell11/astronomical-home/pull/671).
- Hosted testing historically spent about **one minute executing tests within a nine-minute run**. [#639](https://github.com/amindell11/astronomical-home/issues/639).

These are historical records, not measurements of this review’s tree.

Current phase reporting budgets include tests 480 s, remote proof 900 s, ReSharper 360 s and scripts 1,200 s: `scripts/agent_worktree_pool.sh:1488–1491`. Warm iteration addresses the documented approximately 80-second cold floor; repeated validation boots work against it.

**Observation:** script diff selection is deliberately declaration-based. An unlisted changed script can select zero files and return success, explicitly tested at `scripts/tests/test_pool_script_tests.sh:191–198`. The selection output names the omission, so this is not evidence that the whole suite ran. It does limit what “green” proves for newly unlisted scripts. Do not blindly replace the policy already shipped in [#668](https://github.com/amindell11/astronomical-home/issues/668).

## 3. Cut list

These are proposals, not deletions. Apply `TESTING.md`’s verify-before-delete rule: confirm consumers and retained failure modes, then verify retained tests with a deliberate break where appropriate.

| Candidate | Location | Lines removed, est. | Failure mode lost | Confidence |
|---|---|---:|---|---|
| Positive API/signature checks already exercised by callers | `T/EditMode/SessionContractsEditModeTests.cs:29–56`, `:67–73`, `:148–158`, `:166–177` | 65 | Runtime arity/index/property-shape policing. Preserve deliberate negative architectural bans. | High for arity/index checks; medium for the complete subset. |
| Sensing API reflection test | `T/EditMode/GameContextDecouplingEditModeTests.cs:25–34` | 11 | Runtime detection of API spelling/shape; ordinary typed callers remain. No sensing behavior is currently proved by this case. | High |
| Unused distance/audio helpers and their associated prose | `T/PlayMode/Common/TestUtilities.cs:19–35`, `:62–73` | 44 | None: repository search found definitions but no callers. Retain used angle helpers. | High |
| Masked boost-reset test | `T/EditMode/AICommanderAbilityLaneEditModeTests.cs:158–166` | 10 | None for boost-reset behavior: the later null-decision step already clears it independently. Retain the separate no-decision case. | High |
| Exact anchor lookup-count proof, replace with live-anchor effect | `T/EditMode/AICommanderAbilityLaneEditModeTests.cs:168–181` | 14 | Exact lookup frequency; preserve live-anchor freshness with an observable replacement. | Medium |
| Marker no-throw “subscription” case, replace | `T/EditMode/ObjectiveChannelEditModeTests.cs:65–80` | 16 | Binding exceptions; replacement must retain that path while asserting displayed target behavior. | High |
| Weak unbound-bar case, replace | `T/PlayMode/UILifecyclePlayModeTests.cs:110–119` | 10 | Unexpected unbound lifecycle exceptions; retain them and assert unchanged fill. | High |
| Three copies of identical AI status/context stubs | Ability `:33`, `:57`; Fire `:32`, `:60`; Referent `:35`, `:57` in their respective `AICommander*EditModeTests.cs` files | 75 | None if identical definitions become one focused test helper. Estimated replacement: 30 lines. | High |
| Duplicate run-every-file/propagate-red proof | `scripts/tests/test_delivery_timing.sh:17–27`, already stronger in `test_pool_script_tests.sh:99–111` | 10 | None **only after** relocating its unique journal-event assertion. Keep journal escaping/number preservation. | Medium |

**Estimated gross removal: 255 lines.**

Do not cut the whole performance test, rendered-content probes, named-result parity, shared pipeline string/object parsing, or architectural bans merely because they use an exact value or reflection. Each has a distinct contract or documented rationale.

## 4. Add list

Prefer strengthening existing cases over creating additional fixtures.

| Addition | Risk bought | Rough added lines | Suggested seam |
|---|---|---:|---|
| Cold verdict matrix: zero, all-skipped, inconclusive, unknown root, invalid counters and contradictory result data | Prevents unsupported success and full-coverage stamps | 80 | Existing parser, coverage function and summary classification; extend current runner fixtures. |
| Polling-helper continuation regression | Prevents reintroduction of #752’s hidden early pass | 20 | Manually drive the helper inside a boundary that detects an escaping NUnit success exception; a tail assertion alone can itself be bypassed. |
| Malformed asteroid settings rejection | Proves null/cross-model collider rejection before build | 55 | Existing `AsteroidGeometryBuildGate.Validate`, using owned temporary assets; no new production interface. |
| Observable replacements for anchor freshness, marker binding and unbound bar | Replaces call-count/no-throw/arranged-state assertions with useful behavior | 67 | Existing commander output and UI component paths; replaces cut cases. |
| Strengthen force direction/scale and non-revival assertions | Catches backwards/overscaled forces and revival at the unchanged position | 10 | Existing configured force test and sector respawn test. |
| Shared AI status/context definitions | Removes third-copy maintenance without a fixture framework | 30 | `Tests.Common`, or one focused existing fixture arrangement. |
| Retain the delivery journal-event contract in the stronger script-suite test | Allows the duplicate execution proof to be removed safely | 6 | Existing lane fixture and journal assertions. |
| Domain-tag/category-syntax checks | Prevents selective runs from silently omitting relevant fixtures | 30 | Existing scope tests plus fixture metadata inspection; keep overlay exclusion, not a closed domain allowlist. |

**Estimated additions: 298 lines.**

Required-asset assertions and failure-path cleanup should replace existing setup/cleanup code. They do not need separate suites of tests that merely mirror the new lines.

## 5. Net ledger

| Area | Removed, est. | Added, est. | Net |
|---|---:|---:|---:|
| Tests/helpers and equivalent replacements | 255 | 298 | **+43** |
| Public bypass removal and existing-stub migration | 3 | 15 | +12 |
| Verdict implementation repair | — | 10–25 | +10–25 |
| **Estimated code total** | **258** | **323–338** | **+65–80** |

Documentation, cleanup rewrites and timing corrections are excluded because their final scope is not settled. These are planning estimates, not a proposed diff or a savings quota. Most additions buy verdict integrity or replace weak assertions; the useful behavioral suite should become smaller in structure even if its total line count increases slightly.

## 6. Issue-ready suggestions

### 1. Reject cold runs without a valid executed verdict

**Root cause / why:** zero execution, inconclusive state and invalid counters can become success; all-skipped platforms can become full proof.

**Rung:** 2 — deterministic classification at the result boundary.

**Narrow:** repair cold parsing/aggregation and CI fullness checks; add the focused verdict matrix.

**Structural:** a shared verdict implementation across local and hosted paths would reduce drift, but introduces cross-runtime ownership and portability work. Take that through the cost gate separately; it is not necessary for the narrow repair.

**Prior art:** [#612](https://github.com/amindell11/astronomical-home/issues/612), [#635](https://github.com/amindell11/astronomical-home/issues/635), [#582](https://github.com/amindell11/astronomical-home/issues/582), [#518](https://github.com/amindell11/astronomical-home/issues/518). No direct rejected cold-zero/all-skipped repair found.

### 2. Fail required test-asset premises during setup

**Root cause / why:** missing authored inputs remove adoption, teardown and respawn proofs through `Assert.Ignore`.

**Rung:** 2 — earliest deterministic fixture failure.

**Scope:** replace required-asset skips with path-naming setup assertions; retain intentional opt-in and unsupported-environment skips.

**Prior art:** [#719](https://github.com/amindell11/astronomical-home/issues/719), [PR #738](https://github.com/amindell11/astronomical-home/pull/738). Do not suppress broken imports.

### 3. Make fixture-owned object cleanup survive assertion failures

**Root cause / why:** cleanup after assertions leaves owned roots alive on a red path.

**Rung:** 1 — bind resource ownership at creation.

**Scope:** fixture-owned roots or local `try/finally`; no global cleanup sweep and no production null guards.

**Prior art:** [#497](https://github.com/amindell11/astronomical-home/issues/497), [#508](https://github.com/amindell11/astronomical-home/issues/508). Keep this separate from production teardown-order bugs [#764](https://github.com/amindell11/astronomical-home/issues/764) and [#739](https://github.com/amindell11/astronomical-home/issues/739).

### 4. Remove the public cold-launch coordinator bypass

**Root cause / why:** a hermetic test facility also permits unadmitted real Unity launches.

**Rung:** 1 — make the public launch path always use the coordinator.

**Scope:** migrate the `where.exe` fixture to existing injected coordinator replies, then remove `-SkipUnityAccess`. Avoid executable-name heuristics or another launch path.

**Prior art:** [#578](https://github.com/amindell11/astronomical-home/issues/578) preserved this mode for telemetry compatibility; no ownership/admission exemption was found.

### 5. Replace signature and weak-observation tests with fewer behavioral checks

**Root cause / why:** exact API topology, call counts and no-throw assertions spend maintenance effort without proving the advertised effects.

**Rung:** 1 — let typed callers enforce ordinary signatures; use observable behavior for runtime contracts.

**Scope:** the cut list, preserving negative architecture constraints and relocating unique journal coverage.

**Prior art:** [#519](https://github.com/amindell11/astronomical-home/issues/519), [#558](https://github.com/amindell11/astronomical-home/issues/558), [#508](https://github.com/amindell11/astronomical-home/issues/508). No specific rejected signature/helper economy cut found.

### 6. Prove asteroid build validation rejects malformed authored inputs

**Root cause / why:** the positive shipped-settings test remains green if validation is disabled.

**Rung:** 2 — preserve the existing authoring/build rejection boundary.

**Scope:** null/cross-model collider cases through the current validator, with owned temporary assets.

**Prior art:** [#580](https://github.com/amindell11/astronomical-home/issues/580), [PR #586](https://github.com/amindell11/astronomical-home/pull/586). Do not reopen rejected collider extraction/prebaking or readability redesigns.

### 7. Align test recipes and domain metadata with current behavior

**Root cause / why:** stale commands and missing tags misdescribe or omit the intended test slice.

**Rung:** 2 — deterministic metadata checks, plus documentation correction.

**Narrow:** fix the category/finalize/validation recipes, missing tags and Bootstrap alias.

**Structural:** eliminate validation’s duplicate execution by deriving validation from discovery or the actual run. Keep this separate from a documentation-only correction.

**Prior art:** [#518](https://github.com/amindell11/astronomical-home/issues/518). No relevant closed `ValidateScope` ruling found.

### Existing work to continue, without duplicate issues

- **[#651](https://github.com/amindell11/astronomical-home/issues/651), [#762](https://github.com/amindell11/astronomical-home/issues/762):** separate behavioral progress from startup/scheduling latency. Rung 1.
- **[#756](https://github.com/amindell11/astronomical-home/issues/756):** hosted run-registration lag causes false refusal, not false pass. Rung 2 at the remote-verdict classification boundary.
- **[#664](https://github.com/amindell11/astronomical-home/issues/664):** finish feedback-cost measurement against the approved objective. Preserve the consolidation already shipped in [#666](https://github.com/amindell11/astronomical-home/issues/666), [#667](https://github.com/amindell11/astronomical-home/issues/667), and [#668](https://github.com/amindell11/astronomical-home/issues/668).
- Do not resurrect stochastic emergent-combat or broad mirror-determinism proofs rejected in [#489](https://github.com/amindell11/astronomical-home/issues/489).

## Confidence and limits

The inventory contains **185 Unity C# files, approximately 28.6k lines, and 22 script test files totaling 4,713 lines**. I read the rubric first, the required repo conventions, core runner/coordinator/scope/merge/CI paths, bulk Unity test bodies and assertions across the domain categories, script fixtures, timing/skip/category/reflection inventories, relevant production implementations, and the last 20 fix commits. Prior-art issue and PR bodies were available through read-only GitHub access.

I did not read every Unity line exhaustively, run any suite or mutation, measure current timings, exercise concurrent editors, or inspect another reviewer’s grade. Economy and coverage conclusions are therefore bounded by the bulk review, not an exhaustive deletion audit.

All execution outcomes, load survival, mutation predictions and documented-command results are **static — unverified**. Historical timings and regressions are cited records, not runs performed here.