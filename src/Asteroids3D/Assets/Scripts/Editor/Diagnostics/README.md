# Diagnostics Components

This folder contains diagnostic components to help identify unexpected GameObject/Component state changes during gameplay.

## Problem Statement

User reported that LockOnIndicator, Shield UI, and primary weapon GameObjects can disable unexpectedly. PlayMode characterization tests (see `ShipChildComponentStatePlayModeTests`) did not reproduce any bugs, indicating the issue may be intermittent or related to specific runtime conditions.

## Diagnostic Components

### 1. ComponentStateDiagnostics

**Purpose:** Track GameObject enable/disable state changes with optional stack traces.

**Usage:**
1. Select a GameObject in the Scene hierarchy (e.g., LockOnIndicator, StatusBarUI, or weapon mount)
2. Click "Add Component" → Search for "Component State Diagnostics"
3. In the Inspector:
   - ✅ Log Enable - logs OnEnable calls
   - ✅ Log Disable - logs OnDisable calls
   - ✅ Log Stack Trace - includes call stack (helps identify what triggered the state change)
   - ❌ Log Update - verbose frame-by-frame logging (use sparingly)
   - Custom Label - optional label for log messages

**What to watch for:**
- OnDisable calls when you don't expect them
- Stack traces showing unexpected code paths
- Mismatched enable/disable counts

### 2. WeaponStateDiagnostics

**Purpose:** Monitor WeaponsController and weapon mount state, including null references and disabled GameObjects.

**Usage:**
1. Select the Ship GameObject in the Scene hierarchy
2. Click "Add Component" → Search for "Weapon State Diagnostics"
3. In the Inspector:
   - ✅ Monitor Weapons - enables all weapon monitoring
   - ✅ Log Fire Events - logs when weapons fire
   - ❌ Log Every Frame - very verbose (use only when debugging specific issues)

**What to watch for:**
- "Primary weapon became NULL" or "Secondary weapon became NULL" warnings
- "Weapon GameObject is INACTIVE while ship is ACTIVE" warnings
- Weapon fire events stopping unexpectedly

**Inspector Context Menu:**
- Right-click component → "Log Current Weapon State" - snapshot of current state
- Right-click component → "Reset Counters" - reset diagnostic counters

### 3. UIDiagnostics

**Purpose:** Monitor UI components (LockOnIndicator, StatusBarUI) for event subscription issues and state changes.

**Usage:**
1. Select a GameObject with LockOnIndicator or StatusBarUI component
2. Click "Add Component" → Search for "UI Diagnostics"
3. In the Inspector:
   - ✅ Monitor UI Components - enables all UI monitoring
   - ✅ Log Shield Changes - logs shield value changes
   - ✅ Log Lock Progress - logs lock progress updates
   - ✅ Log Alpha Changes - logs CanvasGroup alpha changes

**What to watch for:**
- Shield change events not firing after ship respawn
- CanvasGroup alpha stuck at 0 (invisible)
- Components receiving events while disabled (indicates subscription leak)

**Inspector Context Menu:**
- Right-click component → "Log Current UI State" - snapshot of current state
- Right-click component → "Reset Counters" - reset diagnostic counters

## How to Use in a Scene

### Quick Setup for Ship Debugging:

1. **Open your game scene** (e.g., `Scenes/MainGame.scene`)

2. **Find a Ship GameObject** in the hierarchy (player ship or AI ship)

3. **Add WeaponStateDiagnostics:**
   - Select the Ship GameObject
   - Add Component → Weapon State Diagnostics
   - Enable all logging options

4. **Add ComponentStateDiagnostics to child objects:**
   - Find the primary weapon GameObject (child of Ship)
   - Add Component → Component State Diagnostics
   - Enable "Log Disable" and "Log Stack Trace"
   - Repeat for StatusBarUI and LockOnIndicator if present

5. **Add UIDiagnostics to UI elements:**
   - Find StatusBarUI GameObject (if present)
   - Add Component → UI Diagnostics
   - Enable all logging options
   - Repeat for LockOnIndicator if present

6. **Play the scene** and perform actions that might trigger the bug:
   - Damage the ship until shields drop
   - Kill the ship (trigger death/respawn)
   - Try to fire weapons after respawn
   - Watch the Console for diagnostic logs

7. **Look for anomalies:**
   - OnDisable called when ship is still active
   - Weapon references becoming null
   - UI components not responding to events
   - CanvasGroup alpha stuck at 0

## Test Results

**PlayMode Tests:** All tests in `ShipChildComponentStatePlayModeTests` passed (8/8), indicating:
- Child components properly deactivate when parent ship dies
- Child components properly reactivate when ship resets
- Weapons can fire after ship death/reset
- UI components respond to events after ship death/reset
- Multiple death/reset cycles work correctly

**Conclusion:** No bugs found in normal operation. If the issue is intermittent:
- It may be triggered by specific timing or event ordering
- It may be scene-specific (different prefab configuration)
- It may involve interactions with other systems not covered by tests

Use the diagnostic components above to capture the exact conditions when the bug occurs.

## Removing Diagnostics

When you're done debugging, you can:
1. Disable the diagnostic components (uncheck the component in Inspector)
2. Remove the components entirely (right-click → Remove Component)
3. Delete this Diagnostics folder if no longer needed

## Related Files

- Tests: `Assets/Scripts/Editor/Tests/PlayMode/ShipChildComponentStatePlayModeTests.cs`
- Components under test:
  - `Assets/Scripts/UI/LockOnIndicator.cs`
  - `Assets/Scripts/UI/PlayerState/StatusBarUI.cs`
  - `Assets/Scripts/Ships/Weapons/WeaponsController.cs`
  - `Assets/Scripts/Ships/Ship.cs`
