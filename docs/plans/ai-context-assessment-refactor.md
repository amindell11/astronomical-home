# AI Context & Assessment Layer Refactor

## Context

The AI system's `Context/Info` package is a god-facade that re-exposes 30+ properties from subsystems, hiding raycasts, LINQ chains, and lazy-caching side effects behind simple property access. "Soft" state variables (`InCombat`, `NearbyEnemyCount`, `LineOfSightToEnemy`, etc.) have no proper definitions — each consuming state re-derives its own interpretation with inline magic constants, and they disagree with each other. This makes the utility system fragile, opaque, and hard to tune.

**Goal:** Replace ad-hoc state assessments with a single `SituationAssessment` struct computed once per tick. Clean up Info into a thin container. Restructure UtilityTuning into per-state structs. Migrate all states to use the new APIs.

**Decisions made:**
- Clean break on UtilityTuning (no [Obsolete] shim, new defaults)
- Remove Idle state + enum entry entirely
- States stay commented out in AICommander (enable later when tuned)

---

## Execution: Two Agents

**Agent 1 (Foundation):** New types + Info/Combat/Tuning rewrite
**Agent 2 (State Migration):** Migrate all state files to new APIs, delete dead code

---

## Agent 1: Foundation

### 1.1 Create `AI/Context/CombatTracker.cs` (NEW)

Replaces `Combat.cs`. Key changes:
- `Update()` method called explicitly once per tick (no side effects on property access)
- Enemy resolution: check cached enemy alive → scan for new → update contact time
- Hysteresis: `InCombat` stays true for `combatExitDelay` seconds after enemy lost
- `TimeSinceCombat` for utility curves that care about recency
- Gunner pass-throughs stay here (VectorToTarget, HasTargetLos, AngleToTarget, LaserSpeed)
- `IncomingMissile` stays `false` TODO — but now lives in a proper place

```
Fields: scout, gunner, targeting, selfId, registry, combatExitDelay, cachedEnemy, lastContactTime
Public: Enemy, InCombat, TimeSinceCombat, EnemyPos/Vel/Forward/HealthPct/ShieldPct
Public: VectorToTarget, HasTargetLos, AngleToTarget, LaserSpeed, IncomingMissile
Method: Update() — called once per tick
Constructor: (Scout, Gunner, TargetingUtils, ShipId, IShipRegistry, float combatExitDelay)
```

### 1.2 Create `AI/Context/SituationAssessment.cs` (NEW)

Readonly struct with static `Evaluate()` factory. Computed once per tick, consumed by all states.

```
// Combat status
bool   InCombat
float  TimeSinceCombat

// Self (from ShipInfo, cached)
float  HealthPct, ShieldPct, SpeedPct
float  CombinedDurability          // (health + shield) / 2

// Enemy (0/default when no enemy)
float  EnemyDistance
float  EnemyCombinedDurability     // (enemyHealth + enemyShield) / 2

// Threat (normalized 0-1, one definition)
float  Outnumbered                 // Clamp01((enemies - friends) / 3)
int    NearbyEnemyCount, NearbyFriendCount  // raw counts still available

// Spatial (cached per frame — ONE raycast, ONE trig calc)
bool   HasLineOfSight
float  ClosingRate                 // Clamp01(raw * 0.05 + 0.5)
float  EnemyFacingThreat           // (cos(angle) + 1) / 2
float  SelfAngleToEnemy            // raw degrees 0-180
float  SelfAngleNorm               // angle / 180

// Environment
bool   NearCover
bool   IncomingMissile

static Evaluate(ShipInfo, CombatTracker, Scout, TargetingUtils) -> SituationAssessment
```

### 1.3 Create per-state tuning structs (NEW files)

Directory: `AI/Utility/Tuning/`

Each is a `[Serializable] struct` with `[Header]` grouping and a `static Default` property.

| File | Contents |
|------|----------|
| `AttackTuning.cs` | healthFactor, shieldFactor, enemyWeakFactor, rangeFactor, losFactor, threatFactor, desperationFactor, optimalRangeMin/Max, outerDistanceThreshold, outerRangeFactor, facingDistance, facingSpeed |
| `EvadeTuning.cs` | healthFactor, shieldFactor, outnumberedFactor, enemyLOSFactor, closingSpeedFactor, enemyFacingFactor, missileFactor, tooCloseDistance, tooCloseFactor, fleeDistance, missilePenaltyFactor, fightingRetreatHealth/ShieldThreshold, fightingRetreatFactor, angleFactor |
| `KiteTuning.cs` | healthFactor, shieldFactor, outnumberedFactor, tooCloseFactor, lowHealthThreshold, highShieldThreshold, lowHealthHighShieldFactor, angleTolerance, angleFactor, desiredDistance, minDistance, maxDistance, pushAwayDistance, returnDistanceFactor |
| `OrbitTuning.cs` | healthFactor, shieldFactor, enemyWeakFactor, rangeFactor, losFactor, threatFactor, inRangeFactor, flankingFactor, lowHealthThreshold, lowHealthFactor, radius, minRadius, maxRadius, leadTime, flipMinTime, flipChancePerSecond |
| `JinkEvadeTuning.cs` | healthFactor, shieldFactor, outnumberedFactor, enemyLOSFactor, closingSpeedFactor, enemyFacingFactor, missileThreatFactor, criticalHealthThreshold, criticalShieldThreshold, criticalStateFactor, facingAwayAngle, facingAwayFactor, angleFactor, fleeDistance, sideStepDistance, interval, missileAmplitudeFactor |
| `PatrolTuning.cs` | radius, minDistanceFactor |

**Key principle:** States that previously borrowed another state's factors (Kite using `evadeHealthFactor`, Orbit using `attackHealthFactor`) now get their own copies with same defaults. No cross-references.

### 1.4 Rewrite `AI/Utility/UtilityTuning.cs` (MODIFY)

Replace 60+ flat fields with nested struct instances:

```csharp
[CreateAssetMenu(fileName = "UtilityTuning", menuName = "AI/Utility Tuning")]
public class UtilityTuning : ScriptableObject
{
    public UtilityWeights utilityWeights;

    [Header("Combat Assessment")]
    public float combatExitDelay = 3f;

    public AttackTuning attack = AttackTuning.Default;
    public EvadeTuning evade = EvadeTuning.Default;
    public KiteTuning kite = KiteTuning.Default;
    public OrbitTuning orbit = OrbitTuning.Default;
    public JinkEvadeTuning jinkEvade = JinkEvadeTuning.Default;
    public PatrolTuning patrol = PatrolTuning.Default;
}
```

Existing `.asset` files will lose their values (clean break). New defaults match current hardcoded values.

### 1.5 Rewrite `AI/Context/Info.cs` (MODIFY)

Remove all 30+ pass-through properties. Become a thin container:

```csharp
public partial class Info
{
    public ShipInfo ShipInfo { get; }
    public CombatTracker Combat { get; }
    public Navigation Nav { get; }
    public TargetingUtils Targeting { get; }
    public Scanning.Scout Scout { get; }
    public Maneuvers Maneuvers { get; }
    public SituationAssessment Assessment { get; private set; }

    public Info(Ship, Navigator, Gunner, Scout, TargetingUtils, Maneuvers, float combatExitDelay) { ... }

    public void UpdateAssessment()
    {
        Combat.Update();
        Assessment = SituationAssessment.Evaluate(ShipInfo, Combat, Scout, Targeting);
    }
}
```

### 1.6 Modify `AI/AICommander.cs` (MODIFY)

- Construct `CombatTracker` instead of `Combat` in `TryInitializeSystems()`, passing `utilityTuning.combatExitDelay`
- Pass `combatExitDelay` to `Info` constructor
- In `FixedUpdate()`: call `context.UpdateAssessment()` before `UtilitySelector.Tick()`

### 1.7 Simplify `AI/Context/Navigation.cs` (MODIFY)

Remove `NearAsteroidCover` (moved to SituationAssessment). Keep `VectorToWaypoint` only.

### 1.8 Update `AI/States/State.cs` (MODIFY)

- Remove `GetTuning()` method (no longer needed — sampler gets tuning differently)
- Constructor stays `(Navigator, Gunner, UtilityTuning)` for now

### 1.9 Update `AI/States/StateType.cs` or enum in `State.cs` (MODIFY)

Remove `Idle` from the `StateType` enum.

### 1.10 Update `AI/Utility/UtilityWeights.cs` (MODIFY)

`EnsureInitialized()` will auto-rebuild the array from the new enum values (minus Idle). Existing assets will re-initialize on next load.

### 1.11 Delete `AI/Context/Combat.cs`

Replaced by `CombatTracker.cs`.

### 1.12 Update `AI/Utility/Sampler.cs` (MODIFY)

`SetTuning()` currently gets tuning from `states[0].GetTuning()`. After we remove that method, pass `UtilityTuning` directly from `UtilitySelector.Initialize()`.

### 1.13 Update `AI/Utility/UtilitySelector.cs` (MODIFY)

Accept `UtilityTuning` in `Initialize()` and pass it to `Sampler.SetTuning()` instead of pulling from first state.

---

## Agent 2: State Migration

### 2.1 Migrate each state's `ComputeUtility()` (MODIFY × 6)

Pattern for each state — replace:
- `ctx.HealthPct` → `ctx.Assessment.HealthPct`
- `ctx.LineOfSightToEnemy` → `ctx.Assessment.HasLineOfSight`
- `ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1` → `ctx.Assessment.Outnumbered` (as continuous factor)
- `Mathf.Clamp01(ctx.ClosingSpeed * 0.05f + 0.5f)` → `ctx.Assessment.ClosingRate`
- `(Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad) + 1f) / 2f` → `ctx.Assessment.EnemyFacingThreat`
- `utilityTuning.attackHealthFactor` → `utilityTuning.attack.healthFactor`
- `utilityTuning.evadeHealthFactor` (when borrowed) → `utilityTuning.kite.healthFactor` (own copy)

Files:
- `AI/States/Attack.cs` — ComputeUtility + Tick (enemy null check via `ctx.Combat.Enemy`)
- `AI/States/Evade.cs` — ComputeUtility + Tick + CalculateEvadePoint
- `AI/States/Kite.cs` — ComputeUtility + Tick
- `AI/States/Orbit.cs` — ComputeUtility + Tick
- `AI/States/JinkEvade.cs` — ComputeUtility + Tick
- `AI/States/Patrol.cs` — ComputeUtility (replace `ctx.InCombat` with `ctx.Assessment.InCombat`, replace magic `2f` with builder or at minimum a tunable constant)

### 2.2 Update `State.Tick()` accesses across all states (MODIFY × 6)

`Tick()` methods use `ctx.Enemy`, `ctx.EnemyPos`, `ctx.TargetingUtils`, `ctx.Maneuvers` etc. Update to:
- `ctx.Combat.Enemy` (was `ctx.Enemy`)
- `ctx.Combat.EnemyPos` (was `ctx.EnemyPos`)
- `ctx.Combat.EnemyVel` (was `ctx.EnemyVel`)
- `ctx.Combat.LaserSpeed` (was `ctx.LaserSpeed`)
- `ctx.Targeting.PredictIntercept(...)` (was `ctx.TargetingUtils.PredictIntercept(...)`)
- `ctx.Maneuvers.ComputeOrbitPoint(...)` (unchanged)
- `ctx.ShipInfo.Pos` (was `ctx.SelfPosition`)
- `ctx.ShipInfo.Pos3D` (was `ctx.SelfPosition3D`)
- `ctx.ShipInfo.Vel` (was `ctx.SelfVelocity`)

### 2.3 Update Editor gizmo files (MODIFY × 6)

Same accessor changes as Tick() for all `States/Editor/*.Editor.cs` files. Replace `ctx.SelfPosition3D` → `ctx.ShipInfo.Pos3D`, `ctx.LineOfSightToEnemy` → `ctx.Assessment.HasLineOfSight`, etc.

### 2.4 Delete dead code

- `AI/States/Idle.cs` — delete
- `AI/States/Editor/Idle.Editor.cs` — delete
- `AI/Steering/Maneuvers.cs` — remove `ComputeEvadePoint()` and `ComputeJinkPoint()` (never called; Evade and JinkEvade compute inline)

### 2.5 Verify AICommander state list

Confirm states remain commented out but compile against new API:
```csharp
UtilitySelector.Initialize(
    new Attack(Navigator, Gunner, utilityTuning),
    // new Evade(Navigator, Gunner, utilityTuning),
    // new Kite(Navigator, Gunner, utilityTuning),
    // new Orbit(Navigator, Gunner, utilityTuning),
    // new JinkEvade(Navigator, Gunner, utilityTuning),
    new Patrol(Navigator, Gunner, utilityTuning)
);
```

---

## File Summary

| Action | File | Agent |
|--------|------|-------|
| NEW | `AI/Context/CombatTracker.cs` | 1 |
| NEW | `AI/Context/SituationAssessment.cs` | 1 |
| NEW | `AI/Utility/Tuning/AttackTuning.cs` | 1 |
| NEW | `AI/Utility/Tuning/EvadeTuning.cs` | 1 |
| NEW | `AI/Utility/Tuning/KiteTuning.cs` | 1 |
| NEW | `AI/Utility/Tuning/OrbitTuning.cs` | 1 |
| NEW | `AI/Utility/Tuning/JinkEvadeTuning.cs` | 1 |
| NEW | `AI/Utility/Tuning/PatrolTuning.cs` | 1 |
| MODIFY | `AI/Utility/UtilityTuning.cs` | 1 |
| MODIFY | `AI/Context/Info.cs` | 1 |
| MODIFY | `AI/Context/Navigation.cs` | 1 |
| MODIFY | `AI/AICommander.cs` | 1 |
| MODIFY | `AI/States/State.cs` | 1 |
| MODIFY | `AI/Utility/UtilityWeights.cs` | 1 |
| MODIFY | `AI/Utility/Sampler.cs` | 1 |
| MODIFY | `AI/Utility/UtilitySelector.cs` | 1 |
| DELETE | `AI/Context/Combat.cs` | 1 |
| MODIFY | `AI/States/Attack.cs` | 2 |
| MODIFY | `AI/States/Evade.cs` | 2 |
| MODIFY | `AI/States/Kite.cs` | 2 |
| MODIFY | `AI/States/Orbit.cs` | 2 |
| MODIFY | `AI/States/JinkEvade.cs` | 2 |
| MODIFY | `AI/States/Patrol.cs` | 2 |
| MODIFY | `AI/States/Editor/Attack.Editor.cs` | 2 |
| MODIFY | `AI/States/Editor/Evade.Editor.cs` | 2 |
| MODIFY | `AI/States/Editor/Kite.Editor.cs` | 2 |
| MODIFY | `AI/States/Editor/Orbit.Editor.cs` | 2 |
| MODIFY | `AI/States/Editor/JinkEvade.Editor.cs` | 2 |
| MODIFY | `AI/States/Editor/Patrol.Editor.cs` | 2 |
| DELETE | `AI/States/Idle.cs` | 2 |
| DELETE | `AI/States/Editor/Idle.Editor.cs` | 2 |
| MODIFY | `AI/Steering/Maneuvers.cs` (remove dead methods) | 2 |

---

## Verification

1. **Compile check:** Open Unity project, confirm zero errors in Console
2. **Runtime check:** Enter Play mode in arena scene, confirm AI ships patrol and attack as before
3. **Assessment validation:** Add temporary `Debug.Log(context.Assessment)` in AICommander.FixedUpdate to verify assessment values are populated and sane (InCombat flips correctly, distances > 0, normalized values in 0-1)
4. **Hysteresis check:** Kill an enemy ship, verify InCombat stays true for ~3 seconds then transitions to false (ship resumes patrol)
5. **Inspector check:** Select UtilityTuning asset, confirm nested struct layout is visible and editable with proper headers
6. **Existing tests:** Run EditMode and PlayMode tests — no regressions expected since tests use stub registries and don't depend on Info's pass-through properties
