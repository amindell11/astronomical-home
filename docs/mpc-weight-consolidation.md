# MPC Weight Consolidation (cleanup #4)

Status: **executing**. Part of the RL-agnostic AI/MPC streamlining on branch
`feat/asteroid-nav-planner`. Chosen approach: **sparse overrides** (full refactor).
Behavior-preserving — verify against the MPC + AIIntegration play-mode suite.

## Objective
Remove the parallel-mirror duplication and the zero-default footgun in the per-state
MPC weight system, without changing AI behavior.

## Current chain
`MpcSettings.asset` (~40 base weights) → `Settings.ToConfig()` copies each into `Config`
→ `StateProfile.weightMultipliers` (`WeightMultipliers`, ~18 fields) →
`WeightMultipliers.Apply(ref config)` in `MpcNavigator.RefreshConfig` multiplies them in.
The struct also rides through `NavigationIntent.weightMultipliers` →
`Navigator.SetWeightMultipliers` / `ClearWeightMultipliers` / `ApplyIntent`.

Two defects:
1. **Parallel mirror** — `WeightMultipliers` restates ~18 base weight names; each new
   weight needs ~6 synchronized edits.
2. **Zero-default footgun** — a serialized `WeightMultipliers` is all-zero by default,
   which *zeroes* those weights (absence should mean "×1", not "×0").

## Design — sparse overrides
```csharp
public enum MpcWeight { Pos, Vel, Yaw, YawRate, Effort, SmoothnessThrust,
    SmoothnessStrafe, SmoothnessYaw, Momentum, Facing, FacingWidth, Los,
    Exposure, ExposureWidth, Tangential, MissDistance, Obstacle, BoostEffort }

[Serializable] public struct WeightOverride { public MpcWeight weight; public float multiplier; }
```
- `StateProfile` stores `WeightOverride[] weightOverrides` (empty = base as-is).
- A single managed-side `ApplyOverrides(IReadOnlyList<WeightOverride>, ref Config)` switch
  maps each id → `config.wX *= multiplier` (FacingWidth/ExposureWidth target
  `cfg.facingWidth`/`cfg.exposureWidth`, not a `w*`). Runs in `RefreshConfig` (managed,
  pre-Burst) so a switch is fine.
- **Absence = ×1**, killing the zero-trap. One switch replaces the struct + `Apply` + `Default`.

## Blast radius
- `Types.cs` — remove `WeightMultipliers`; add `MpcWeight` + `WeightOverride` (+ apply helper).
- `StateProfile.cs` — `weightMultipliers` → `weightOverrides` (use `[FormerlySerializedAs]`
  or migrate before the rename so data isn't dropped).
- `NavigationIntent.cs`, `AIState.cs`, `Navigator.cs`, `MpcNavigator.cs` — carry/apply
  overrides instead of the struct (`SetWeightMultipliers`/`ClearWeightMultipliers`/`ApplyIntent`,
  `RefreshConfig`).
- Editor consumers: `StateProfile.Editor.cs` (`DrawWeightMultipliersWithCurves`, keyed on
  propertyPath `"weightMultipliers"`) → new override-list drawer; `MpcNavigator.Editor.cs:118`
  (`profile.weightMultipliers.Apply`) → `weightOverrides.Apply`; `States/Editor/MigrateStateProfiles.cs`
  (lines ~159-162 read/write `p.weightMultipliers`) → update or drop.
- Tests: `MpcNavigatorPlayModeTests` (`ClearWeightMultipliers()`, `WeightMultipliers.Default`),
  `AIIntegrationPlayModeTests` (profiles built with `WeightMultipliers.Default`).
- **7 `StateProfile` assets**: `Attack, AttackAggressive, AttackEvasive, AttackFast, Evade,
  Patrol, Pursuit` — migrate `weightMultipliers:` YAML → `weightOverrides:` (only fields ≠ 1).
  NOTE `Attack.asset` was NOT retuned in-flight but still carries the old struct. Meaningful
  zeros (e.g. AttackFast `vel:0, yaw:0`) MUST become explicit override entries (absence = ×1).

## Migration (risky part)
Profiles were just retuned, so preserve exact values:
1. Read each asset's `weightMultipliers` block; record every field ≠ 1.
2. One-shot `[MenuItem]` editor migration (mirror `States/Editor/MigrateStateProfiles.cs`)
   converting the struct → `weightOverrides` for non-1 fields; run once; commit assets separately.
3. Caveat: `facingWidth`/`exposureWidth` scale widths, not `w*` weights — map them to the
   right `Config` fields.

## Risks & mitigations
- **Behavior drift** — apply switch must reproduce `WeightMultipliers.Apply` exactly; verify
  with a `Cost.EvaluateBreakdown` parity check on a sample state before/after + the play-mode suite.
- **Asset corruption** — migrate via deterministic editor script, not hand-edited YAML; commit
  assets in their own commit for easy revert.
- **Dropped serialized data** — `[FormerlySerializedAs]` or migrate-before-rename.

## Phasing (each step compiles + tests green)
1. Add `MpcWeight` + `WeightOverride` + apply helper alongside the existing struct (no behavior change).
2. Editor migration script; run it; commit converted assets.
3. Switch `StateProfile`/intent/navigator to `weightOverrides`; delete `WeightMultipliers`;
   update tests + editor drawer.
4. Full suite + breakdown-parity check.

## Out of scope
Utility-*selection* weights (`UtilitySelectorSettings` + `UtilityWeights` + per-factor weight)
— separate, low-slop; follow-up.

## Progress
- [x] Phase 1: `MpcWeight` enum + `WeightOverride` struct + `WeightOverrideExtensions.Apply`
      added in `Types.cs` alongside the still-present `WeightMultipliers` (additive, compiles).
- [ ] Phase 2: migrate the 7 assets' `weightMultipliers:` → `weightOverrides:` (direct YAML,
      deterministic; commit assets separately). Convert every field ≠ 1, including explicit 0s.
- [ ] Phase 3: swap `StateProfile`/`NavigationIntent`/`AIState`/`Navigator`/`MpcNavigator` to
      `weightOverrides`; update `StateProfile.Editor`, `MpcNavigator.Editor:118`,
      `MigrateStateProfiles.cs`, and the 2 test files (`ClearWeightMultipliers`,
      `WeightMultipliers.Default` → `Array.Empty<WeightOverride>()` / no-op); delete `WeightMultipliers`.
- [ ] Phase 4: full suite green + `Cost.EvaluateBreakdown` parity check on a sample state.

### Resume hint for a fresh context
Phase 1 is committed. Start Phase 2: for each of the 7 assets, replace the `weightMultipliers:`
block with `weightOverrides:` (a YAML sequence of `{weight: <enum int>, multiplier: <v>}` for each
field ≠ 1). `MpcWeight` enum order (0-based int values): Pos,Vel,Yaw,YawRate,Effort,SmoothnessThrust,
SmoothnessStrafe,SmoothnessYaw,Momentum,Facing,FacingWidth,Los,Exposure,ExposureWidth,Tangential,
MissDistance,Obstacle,BoostEffort. Then do Phase 3 (the `weightMultipliers.Apply` call sites become
`weightOverrides.Apply`). Verify with the MPC + AIIntegration play-mode suite.

## Context: prior cleanup state (this branch)
- #1 dead `Maneuvers` removed; #2 `Navigator.ApplyIntent` single entry point; #3 cost
  reconciliation centralized in `EvalContext` + `PositionalGoalCost`. All committed.
- #5 (retire Standard nav stack) ABORTED — `TestPilotMPC`/non-MPC test suite depends on it.
- Test suite green except known-flaky `FullLoop_NoEnemy_PatrolStateSelected` and 3 `[Ignore]`d
  `MpcEnemyProjection` facing tests (base facing/exposure weights intentionally collapsed; to
  redesign after this refactor).
