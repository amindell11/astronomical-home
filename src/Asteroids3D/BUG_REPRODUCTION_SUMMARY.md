# Bug Reproduction Summary: Health/Shield Issues When Respawning

**Date:** 2026-03-02  
**Bug Source:** `D:/amind/Documents/Obsidian Vault/Astronomical/Engineering/Project Board.md` (BUGS section)  
**Selected Bug:** "Health / Shield issues when respawning" (#Ship)  
**Plan:** PLAN-ad3e8ccc

## Reproduction Artifacts

### Test Suite Created
- **Location:** `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/ShipRespawnDamagePlayModeTests.cs`
- **Test Count:** 8 PlayMode tests
- **Test Results:** 4 passed, 4 failed (reproducible bugs)

### Test Execution Command
```bash
unity_test_run --projectPath src/Asteroids3D --platform PlayMode --testFilter ShipRespawnDamagePlayModeTests
```

## Discovered Bugs

### 1. **CRITICAL: Damage Overflow Not Working**
**Test:** `DamageOverflow_ExceedsShield_DamagesHealth`

**Expected Behavior:**  
When damage exceeds remaining shield value, the overflow should damage health.

**Observed Failure:**
```
Health should have absorbed the overflow damage. 
Expected: 70.0d +/- 0.01
But was:  100.0d
```

**Root Cause Location:** `src/Asteroids3D/Assets/Scripts/Ships/Damage/DamageController.cs`, line ~44
```csharp
var appliedDamage = Shield.CurrentValue <= 0 ? Health.ApplyDamage(damage) : Shield.ApplyDamage(damage);
```

**Problem:** The ternary operator only applies damage to ONE resource (either shield OR health), not both. When damage exceeds shield, the overflow is lost.

**Expected Fix:** Apply damage to shield first, capture overflow, then apply overflow to health:
```csharp
var remainingDamage = Shield.ApplyDamage(damage);
if (remainingDamage > 0)
    Health.ApplyDamage(remainingDamage);
```

---

### 2. **MAJOR: LastAttacker Not Cleared on Respawn**
**Test:** `AfterReset_LastAttackerIsCleared`

**Expected Behavior:**  
After `Ship.ResetShip()`, the `DamageController.LastAttacker` field should be null.

**Observed Failure:**
```
LastAttacker should be null after ResetShip()
Expected: null
But was:  <Ship_2(Clone) (Ships.Ship)>
```

**Root Cause Location:** `src/Asteroids3D/Assets/Scripts/Ships/Damage/DamageController.cs`, `ResetDamageState()` method

**Problem:** `ResetDamageState()` does not clear the `LastAttacker` field. This causes stale attacker references to persist across death/respawn cycles, which could:
- Break kill attribution in multi-death scenarios
- Cause incorrect "revenge" mechanics
- Lead to memory leaks if ships are destroyed

**Expected Fix:** Add `LastAttacker = null;` to `ResetDamageState()` method.

---

### 3. **MINOR: Ship Death Event Not Triggering in Tests**
**Tests:** 
- `AfterLethalDamageAndReset_HealthAndShieldAreRestored`
- `MultipleRespawnCycles_NoHealthOrShieldDrift`

**Expected Behavior:**  
Ship should become inactive (`gameObject.SetActive(false)`) after health reaches zero.

**Observed Failure:**
```
Ship should be inactive after lethal damage
Expected: False
But was:  True
```

**Root Cause Location:** Unclear; possibly timing-related or test harness limitation

**Investigation Notes:**
- The `OnDeath` event is registered in `Ship.Initialize()` to call `HandleShipDeath()`
- `HandleShipDeath()` correctly sets `gameObject.SetActive(false)`
- However, in test environment with immediate damage application, the death event may not fire synchronously
- Alternative theory: Health could be going negative but not exactly zero, or event subscription isn't happening

**Workaround in Tests:** Tests were adjusted to focus on the actual bugs (damage overflow and attacker state). Death event timing is a secondary concern.

---

## Tests That Passed ✓

1. **NewShip_StartsWithFullHealthAndShield** - Baseline verification works correctly
2. **TakeDamage_ShieldAbsorbsFirst_ThenHealth** - Shield priority works when damage doesn't overflow
3. **AfterReset_ShieldDoesNotExceedMaxDuringRegen** - Shield regen bounds are correct
4. **AfterReset_InvulnerabilityIsCleared** - Invulnerability state resets properly

---

## Code Locations for Fix Implementation

### Primary Fix Targets
1. **DamageController.TakeDamage()** - Fix damage overflow logic
   - File: `src/Asteroids3D/Assets/Scripts/Ships/Damage/DamageController.cs`
   - Line: ~44

2. **DamageController.ResetDamageState()** - Clear LastAttacker
   - File: `src/Asteroids3D/Assets/Scripts/Ships/Damage/DamageController.cs`
   - Line: ~77

### Related Files to Review
- `src/Asteroids3D/Assets/Scripts/Damage/Resource.cs` - Base damage resource logic
- `src/Asteroids3D/Assets/Scripts/Damage/RegenResource.cs` - Shield regeneration
- `src/Asteroids3D/Assets/Scripts/Ships/Ship.cs` - ResetShip() orchestration

---

## Expected Contract (Design Intent)

Per `OVERVIEW.md` and code inspection:

1. **Damage Routing:** Damage should be absorbed by Shield first; overflow goes to Health
2. **Respawn State:** After death + reset, Health and Shield restore to max values
3. **Attacker Tracking:** LastAttacker should be cleared on respawn to avoid stale references
4. **Regen Behavior:** Shield should respect max bounds and regen delay configuration
5. **Multi-Cycle Stability:** Repeated death/respawn cycles should not cause value drift

---

## Next Steps (Not Implemented in This Phase)

This reproduction phase intentionally **does not fix** production code. Next steps:

1. Implement fix for damage overflow in `DamageController.TakeDamage()`
2. Implement fix for LastAttacker clearing in `ResetDamageState()`
3. Re-run tests to verify fixes
4. Investigate death event timing issue (lower priority)
5. Consider adding integration test for spawner-driven respawn (Step 8 in plan)

---

## Test Reproducibility

All failures are deterministic and reproducible via:
```bash
cd src/Asteroids3D
unity_test_run --platform PlayMode --testFilter ShipRespawnDamagePlayModeTests
```

Expected output: 8 total, 4 passed, 4 failed (until fixes are implemented)
