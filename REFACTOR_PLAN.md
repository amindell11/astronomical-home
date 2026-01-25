# Tech Debt Refactoring Plan: Audio, Visual, and UI Decoupling

## Core Principles

### Rule 1: Separation of Concerns
> **Game logic should operate independently of overlaid audio/visual/UI elements, communicating through events which UI/audio/visual systems can subscribe to.**

This means:
- Game logic classes fire events when state changes
- Audio/visual/UI classes subscribe to those events
- Game logic never depends on or references audio/visual/UI classes
- Removing audio/visual/UI should not break game functionality

### Rule 2: No Expensive Calls in Hotpath
> **GetComponent, FindGameObjectWithTag, and other expensive invocations must only appear in Awake(). Never in Start(), Update(), Initialize(), or any method called at runtime.**

This means:
- All component references cached in `Awake()`
- `Initialize()` methods receive dependencies as parameters, never discover them
- Sibling components on the same prefab use `[RequireComponent]` and cache in `Awake()`
- If a component needs a reference it can't get in `Awake()`, the architecture must change to enable DI

**Implications for scope:** This rule may require broader refactoring to ensure DI pathways exist. For example, if `Overlay.Initialize()` previously called `GetComponent<Heat>()`, the caller must instead pass the Heat reference directly.

---

## Phase 1: Reorganize Folder Structure

Move audio and visual files to live under the game aspect they serve.

### Current Structure
```
/Scripts/Audio/
    EngineAudio.cs
    ShipDamageAudio.cs
    LauncherAudio.cs
    UILaserAudio.cs
    UILockOnAudio.cs
    UIHealthAudio.cs
    OneShotSfx.cs
/Scripts/Utils/
    PooledAudioSource.cs
```

### Target Structure
```
/Scripts/Ships/Audio/
    EngineAudio.cs
    ShipDamageAudio.cs
/Scripts/Combat/Weapons/Audio/
    LauncherAudio.cs
/Scripts/Combat/Projectile/Audio/
    LaserAudio.cs      (NEW - extracted from LaserProjectile)
    MissileAudio.cs    (NEW - extracted from Missile)
/Scripts/Combat/Projectile/Visual/
    LaserVisual.cs     (NEW - extracted from LaserProjectile)
/Scripts/Asteroids/Audio/
    AsteroidAudio.cs   (NEW - extracted from Asteroid)
/Scripts/Asteroids/Visual/
    AsteroidVisual.cs  (NEW - extracted from Asteroid)
/Scripts/UI/Audio/
    UILaserAudio.cs
    UILockOnAudio.cs
    UIHealthAudio.cs
/Scripts/Audio/
    OneShotSfx.cs      (shared utility - stays)
    PooledAudioSource.cs (shared utility - move from Utils)
```

### Tasks
1. Create new folder structure
2. Move existing files, update namespaces
3. Verify no GetComponent calls exist outside of Awake in moved files

---

## Phase 2: Fix UI Dependency Injection Issues

Three UI components violate DI principles. The caller (`Overlay.Initialize`) must pass fully-resolved dependencies - no GetComponent calls allowed in Initialize methods.

### 2.1 UILaserAudio.cs
**Current:** Finds player via tag in `Start()`
**Fix:** Add `Initialize(Heat heat)` method that only stores reference and subscribes

### 2.2 LaserHeatUI.cs
**Current:** Finds player via tag in `Start()`
**Fix:** Add `Initialize(Heat heat)` method that only stores reference and subscribes

### 2.3 MissileAmmoUI.cs
**Current:** Finds player via tag in `Start()`
**Fix:** Add `Initialize(Rounds rounds, TargetingComputer targeting)` method

### Caller Responsibilities (Overlay.cs)
`Overlay.Initialize(Ship player)` must receive fully-resolved dependencies. The current implementation already does GetComponent calls - these must be moved to the caller (`GameInitiator`).

**Current in Overlay.Initialize():**
```csharp
var laserHeat = player.Weapons?.Primary?.GetComponent<Heat>();  // VIOLATION
var missileLauncher = player.Weapons?.Secondary as WeaponMissiles;
var rounds = missileLauncher.GetComponent<Rounds>();  // VIOLATION
```

**Fix:** Either:
1. Move these GetComponent calls to `GameInitiator` and pass resolved refs to `Overlay.Initialize()`
2. Or expose `Heat` and `Rounds` as properties on `WeaponLaser` and `WeaponMissiles` (cached in their Awake)

**Recommended:** Option 2 - weapons should expose their condition components as properties.

### Pattern for Initialize Methods
```csharp
// CORRECT - Initialize only stores and subscribes
public void Initialize(Heat heat)
{
    this.heat = heat;
    heat.OnOverheat += HandleOverheat;
}

// WRONG - Initialize discovers dependencies
public void Initialize(Ship player)
{
    var heat = player.GetComponent<Heat>();  // NO!
}
```

---

## Phase 3: Split LaserProjectile into Components

**Current State:** `LaserProjectile.cs` contains:
- Hit detection and damage logic (game logic)
- Hit sound playback (audio)
- Color fade effect (visual)

### Target Components

#### 3.1 LaserProjectile.cs (Game Logic Only)
- Hit detection, damage dealing, pooling lifecycle
- **Expose properties for visual:**
  - `float DistanceTraveled { get; }` - current distance from spawn
  - `float MaxDistance { get; }` - configured max range
- **New Events:**
  - `Action<Vector3> OnHit` - hit position (for audio)
  - `Action OnReturnToPool` - about to be pooled (for cleanup)

#### 3.2 LaserAudio.cs (New)
- Caches `LaserProjectile` reference in `Awake()` via `GetComponent`
- Subscribes to `OnHit` in `OnEnable()`, unsubscribes in `OnDisable()`
- Plays hit sound via `PooledAudioSource.PlayClipAtPoint()`
- Serialized fields: hit clips array, volume

#### 3.3 LaserVisual.cs (New)
- Caches `LaserProjectile` and `Renderer[]` in `Awake()`
- Reads `laser.DistanceTraveled / laser.MaxDistance` each frame to calculate alpha
- Manages renderer color fade effect
- Resets colors on `OnEnable()` (pooling-safe)

### Implementation Notes
- All three components on same prefab, use `[RequireComponent(typeof(LaserProjectile))]`
- GetComponent only in Awake
- LaserProjectile has zero knowledge of audio/visual components

---

## Phase 4: Split Missile into Components

**Current State:** `Missile.cs` is a monolith containing:
- Homing/targeting logic (game logic)
- Damage/explosion logic (game logic)
- Launch sound, engine loop, detonation sound (audio)
- Manages AudioSource component directly

### Target Components

#### 4.1 Missile.cs (Game Logic Only)
Remove all audio fields and code. Add:
- **Properties:**
  - `float NormalizedSpeed { get; }` - for engine audio pitch
- **Events:**
  - `Action OnLaunched` - when missile initializes/fires
  - `Action<Vector3> OnDetonated` - position of explosion

#### 4.2 MissileAudio.cs (New)
- Caches `Missile` and `AudioSource` in `Awake()`
- Subscribes in `OnEnable()`:
  - `OnLaunched` → Play launch clip, start engine loop coroutine
  - `OnDetonated` → Stop engine, play detonation via PooledAudioSource
- Updates engine volume/pitch in `Update()` based on `missile.NormalizedSpeed`

Serialized fields (moved from Missile):
- `launchClip`, `engineClip`, `detonationClip`
- `engineFadeInDuration`, `detonationVolume`

#### 4.3 MissileVisual.cs (Future)
Currently missile has no special visual effects beyond the mesh.
If trail/smoke effects are added later, they go here.

### Implementation Notes
- MissileAudio uses `[RequireComponent(typeof(Missile), typeof(AudioSource))]`
- AudioSource managed entirely by MissileAudio
- Pooling cleanup in `OnDisable()`

---

## Phase 5: Split Asteroid into Components

**Current State:** `Asteroid.cs` contains:
- Health, damage, collision logic (game logic)
- Fragmentation trigger (game logic)
- Explosion sound (audio)
- Explosion VFX instantiation (visual)

### Target Components

#### 5.1 Asteroid.cs (Game Logic Only)
Remove audio/visual fields and code. Add:
- **Events:**
  - `Action<Vector3> OnDestroyed` - explosion position

Keep:
- Health, damage, collision logic
- Fragmentation trigger (calls Fragger)
- Pool return logic

**Event timing:** Fire `OnDestroyed` before calling pool return, so subscribers can act.

#### 5.2 AsteroidAudio.cs (New)
- Caches `Asteroid` in `Awake()`
- Subscribes to `OnDestroyed` in `OnEnable()`
- Plays explosion sound via `PooledAudioSource.PlayClipAtPoint()`

Serialized fields (moved from Asteroid):
- `explosionSound`, `explosionVolume`

#### 5.3 AsteroidVisual.cs (New)
- Caches `Asteroid` in `Awake()`
- Subscribes to `OnDestroyed` in `OnEnable()`
- Spawns explosion VFX (pooled or instantiated)
- Respects `GameSettings.VfxEnabled`

Serialized fields (moved from Asteroid):
- `explosionPrefab`

### Implementation Notes
- Both use `[RequireComponent(typeof(Asteroid))]`
- Event fires synchronously before pool return

---

## Phase 6: Reduce Static Singleton Usage

### Current Singletons
1. `GameContext : MonoSingleton<GameContext>` - Main game state
2. `Fragger : MonoSingleton<Fragger>` - Asteroid fragmentation

### Strategy

#### 6.1 GameContext
**Accept as composition root but plan to deprecate.** Move things into DI wherever possible.

Current responsibilities to eventually extract:
- `ShipRegistry` → Pass via DI to components that need it
- `MainCamera` → Pass via DI to components that need it
- `WorldFollow` → Extract to separate injectable service
- `CurrentState` → Consider event-based state machine

**For now:** Keep GameContext but add `[Obsolete]` warnings on direct property access where DI alternatives exist.

#### 6.2 Fragger
**Refactor to injectable service:**
- Remove MonoSingleton inheritance
- `AsteroidSpawner` holds Fragger reference
- Pass Fragger to `Asteroid.Initialize()`
- Asteroid stores reference, uses it directly

```csharp
// Asteroid.cs
private Fragger fragger;

public void Initialize(..., Fragger fragger)
{
    this.fragger = fragger;
}

private void Explode()
{
    fragger.CreateFragments(this, ...);
}
```

#### 6.3 Static Utilities (Keep)
These are stateless utilities, acceptable as static:
- `GameSettings` - Simple toggles
- `TagNames`, `LayerIds` - Constants
- `PhysicsBuffers` - Reusable buffers
- `LineOfSight` - Pure functions
- `GamePlane` - Pure math

---

## Phase 7: Weapon Component Property Exposure

To support Phase 2 without GetComponent in Initialize, weapons must expose their condition components.

### WeaponLaser.cs
```csharp
public Heat Heat { get; private set; }

protected override void Awake()
{
    base.Awake();
    Heat = GetComponent<Heat>();
}
```

### WeaponMissiles.cs
```csharp
public Rounds Rounds { get; private set; }

protected override void Awake()
{
    base.Awake();
    Rounds = GetComponent<Rounds>();
}
```

This allows `Overlay.Initialize()` to access `player.Weapons.Primary.Heat` without GetComponent.

---

## Phase 8: Event Standardization

Ensure consistent event patterns across the codebase.

### Standard Pattern
```csharp
// In game logic class
public event Action<TArg1, TArg2> OnSomethingHappened;

protected void RaiseSomethingHappened(TArg1 a, TArg2 b)
{
    OnSomethingHappened?.Invoke(a, b);
}
```

### Subscriber Pattern (Pooled Objects - Same Prefab)
```csharp
[RequireComponent(typeof(TSource))]
public class MyAudio : MonoBehaviour
{
    private TSource source;

    private void Awake()
    {
        source = GetComponent<TSource>();  // OK - Awake only
    }

    private void OnEnable()
    {
        source.OnSomethingHappened += HandleSomethingHappened;
    }

    private void OnDisable()
    {
        source.OnSomethingHappened -= HandleSomethingHappened;
    }
}
```

### Subscriber Pattern (DI Initialized)
```csharp
public class MyUI : MonoBehaviour
{
    private TSource source;

    public void Initialize(TSource source)
    {
        this.source = source;
        source.OnSomethingHappened += HandleSomethingHappened;
    }

    private void OnDestroy()
    {
        if (source != null)
            source.OnSomethingHappened -= HandleSomethingHappened;
    }
}
```

---

## Implementation Order

### Batch 1: Foundation
1. **Phase 7** - Expose Heat/Rounds properties on weapons (enables Phase 2)
2. **Phase 2** - Fix UI DI issues (now possible without GetComponent in Initialize)
3. **Phase 1** - Folder reorganization

### Batch 2: Core Refactoring
4. **Phase 5** - Asteroid split (simpler, good practice run)
5. **Phase 3** - LaserProjectile split
6. **Phase 4** - Missile split (most complex)

### Batch 3: Architecture Cleanup
7. **Phase 6** - Singleton reduction (Fragger)
8. **Phase 8** - Event standardization audit

---

## Files to Modify (Summary)

### Modify Existing
- `WeaponLaser.cs` - Expose Heat property
- `WeaponMissiles.cs` - Expose Rounds property
- `Overlay.cs` - Update Initialize to use exposed properties (already partially done)
- `UILaserAudio.cs` - Add Initialize method, remove FindGameObjectWithTag
- `LaserHeatUI.cs` - Add Initialize method, remove FindGameObjectWithTag
- `MissileAmmoUI.cs` - Add Initialize method, remove FindGameObjectWithTag
- `LaserProjectile.cs` - Add events and properties, remove audio/visual code
- `Missile.cs` - Add events and properties, remove audio code
- `Asteroid.cs` - Add events, remove audio/visual code
- `Fragger.cs` - Remove singleton pattern
- `AsteroidSpawner.cs` - Pass Fragger to Asteroid.Initialize

### Create New
- `Scripts/Combat/Projectile/Audio/LaserAudio.cs`
- `Scripts/Combat/Projectile/Visual/LaserVisual.cs`
- `Scripts/Combat/Projectile/Audio/MissileAudio.cs`
- `Scripts/Asteroids/Audio/AsteroidAudio.cs`
- `Scripts/Asteroids/Visual/AsteroidVisual.cs`

### Move (with namespace updates)
- `EngineAudio.cs` → `Scripts/Ships/Audio/`
- `ShipDamageAudio.cs` → `Scripts/Ships/Audio/`
- `LauncherAudio.cs` → `Scripts/Combat/Weapons/Audio/`
- `UILaserAudio.cs` → `Scripts/UI/Audio/`
- `UILockOnAudio.cs` → `Scripts/UI/Audio/`
- `UIHealthAudio.cs` → `Scripts/UI/Audio/`
- `PooledAudioSource.cs` → `Scripts/Audio/`

---

## Success Criteria

After refactoring:
1. **No GetComponent/Find calls outside Awake()** - Audit all files
2. Deleting all audio components should not break gameplay
3. Deleting all UI components should not break gameplay
4. No `FindGameObjectWithTag` calls remain in UI/audio code
5. All audio/visual classes subscribe to events, never called directly by game logic
6. `Fragger.Singleton` pattern eliminated
7. Each game aspect folder contains its own audio/visual subfolders
8. Unit tests can instantiate game logic classes without audio/visual dependencies
9. Weapons expose their condition components (Heat, Rounds) as cached properties
