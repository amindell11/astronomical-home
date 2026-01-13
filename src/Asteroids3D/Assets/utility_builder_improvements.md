# Utility Builder Refactor - Geometric Mean Implementation

This document summarizes the geometric mean-based utility scoring system that replaced the additive approach.

## Problem Statement

The previous additive scoring system had two major issues:

1. **Semantic Confusion**: Methods like `AddFear("finishOffWeak", ...)` were counterintuitive — the name described the curve shape rather than intent
2. **Score Dilution**: As more additive modifiers stacked, scores became diluted and less meaningful

## Solution: Geometric Mean Scoring

Each factor now represents: **"What percentage of my desire for this action remains?"**

### Core Concept

Instead of adding bonuses (`base + 0.2 + 0.3 + 0.1`), we multiply factors and take the geometric mean:

```
score = (factor1 × factor2 × factor3 × ... × factorN)^(1/N)
```

This prevents both:
- **Additive dilution** (too many bonuses → inflated scores)
- **Multiplicative collapse** (too many factors → vanishing scores)

### Example

| Approach | Calculation | Result |
|----------|-------------|--------|
| Additive (old) | 0.5 + 0.2 + 0.3 + 0.1 - 0.2 | 0.9 (diluted) |
| Geometric Mean (new) | (0.8 × 0.9 × 1.1 × 0.7)^(1/4) | 0.86 (balanced) |

Five "pretty good" factors (0.8 each):
- Raw product: 0.8^5 = 0.33 (collapsed)
- Geometric mean: (0.8^5)^(1/5) = 0.8 (preserved)

## Implementation

### UtilityBuilder API

**File:** `Scripts/AI/Utility/UtilityBuilder.cs`

```csharp
public class UtilityBuilder
{
    // Core method: multiply a factor
    public UtilityBuilder Factor(string name, float value)
    
    // Curve helper: maps [0,1] input to factor range
    public UtilityBuilder Factor(string name, float input, FactorRange range)
    
    // Conditional factor
    public UtilityBuilder FactorIf(bool condition, string name, float trueValue, float falseValue = 1f)
    
    // Output: geometric mean, clamped [0,1]
    public float Build()
}
```

### FactorRange Struct

```csharp
[System.Serializable]
public struct FactorRange
{
    public float AtLow;   // factor when input = 0
    public float AtHigh;  // factor when input = 1
    
    public FactorRange Inverted => new FactorRange(AtHigh, AtLow);
}
```

### Factor Interpretation

| Factor Value | Meaning | Effect |
|--------------|---------|--------|
| **1.0** | Neutral | No effect on score |
| **< 1.0** | Suppressor | Reduces desire (0.5 = halves) |
| **> 1.0** | Amplifier | Increases desire (1.3 = 30% boost) |
| **0.01** | Floor | Effectively vetoes without zeroing |

## State Examples

### Attack State

```csharp
return new UtilityBuilder()
    .Factor("selfHealth", ctx.HealthPct, tuning.attackHealthFactor)          // (0.3, 1.0)
    .Factor("selfShield", ctx.ShieldPct, tuning.attackShieldFactor)          // (0.4, 1.0)
    .Factor("enemyWeak", enemyHealth, tuning.attackEnemyWeakFactor.Inverted) // (1.3, 1.0) inverted
    .Factor("range", rangeScore, tuning.attackRangeFactor)                   // (0.6, 1.2)
    .FactorIf(ctx.LineOfSightToEnemy, "hasLOS", 1f, tuning.attackNoLOSFactor) // 1.0 or 0.4
    .Factor("threat", netThreat, tuning.attackThreatFactor)                  // (1.0, 0.5)
    .Build();
```

### Evade State

```csharp
return new UtilityBuilder()
    .Factor("selfHealth", ctx.HealthPct, tuning.evadeHealthFactor)          // (1.0, 0.4) inverted
    .Factor("selfShield", ctx.ShieldPct, tuning.evadeShieldFactor)          // (1.0, 0.5) inverted
    .FactorIf(outnumbered, "outnumbered", tuning.evadeOutnumberedFactor)    // 1.3
    .FactorIf(ctx.IncomingMissile, "missile", tuning.evadeMissileFactor)    // 1.5
    .Factor("angle", angleScore, tuning.evadeAngleFactor)                   // (0.7, 1.0)
    .Build();
```

### Kite State (Hybrid)

Kite combines attack and evade desires by averaging their raw geometric means:

```csharp
var attackScore = attackBuilder.BuildRaw();  // geometric mean, unclamped
var evadeScore = evadeBuilder.BuildRaw();
var hybridBase = (attackScore + evadeScore) / 2f;

return new UtilityBuilder()
    .Factor("hybrid", hybridBase)
    .FactorIf(tooClose, "tooClose", tuning.kiteTooCloseFactor)
    .Factor("angle", angleOffset, tuning.kiteAngleFactor.Inverted)
    .Build();
```

## UtilityTuning Changes

All additive bonus parameters were replaced with `FactorRange` structs:

| Old (Additive) | New (Geometric) |
|----------------|-----------------|
| `attackUtilityHealthBonus = 0.2f` | `attackHealthFactor = (0.3, 1.0)` |
| `attackUtilityEnemyWeakBonus = 0.3f` | `attackEnemyWeakFactor = (1.3, 1.0)` |
| `attackUtilityLOSBonus = 0.1f` | `attackNoLOSFactor = 0.4f` |
| `evadeUtilityHealthFear = 0.4f` | `evadeHealthFactor = (1.0, 0.4)` |

Factor ranges define how conditions map to multipliers across their input range.

## Benefits

1. **Clear Semantics**: Factors describe what they do, not their curve shape
2. **Balanced Scoring**: No dilution or collapse regardless of factor count
3. **Intuitive Tuning**: Think in percentages (0.5 = 50% desire, 1.3 = 130%)
4. **Transparent Composition**: Each factor's contribution is clear
5. **Debuggable**: `GetBreakdown()` shows all factors and their values (editor-only)

## Performance

- Same O(N) complexity as additive approach
- Compile-time breakdown gating (`#if UNITY_EDITOR || DEBUG`)
- Zero GC allocations in release builds
- Single `Pow()` call at `Build()` time

## Migration Notes

Existing `UtilityTuning` ScriptableObject assets need new values. The refactor provides sensible defaults based on converting old additive bonuses to equivalent factor ranges.

## Testing

All states should exhibit similar behavior to the old system while being more resilient to extreme factor combinations. Test edge cases:
- Very low health (multiple suppressing factors)
- Perfect conditions (multiple amplifying factors)
- Mixed factors (some suppress, some amplify)
- Many factors vs. few factors (geometric mean should be stable)
