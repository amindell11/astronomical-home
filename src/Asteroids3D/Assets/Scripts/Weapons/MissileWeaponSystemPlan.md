# Missile Weapon System – Implementation Plan

> This document is formatted for large-language-model (LLM) consumption.  Each top-level heading signals a discrete work unit; sub-lists contain the concrete, order-independent steps an agent should apply to the Unity project.

---

## 1. Summary
* Add a homing **Missile** weapon: launcher must achieve line-of-sight lock-on before firing (press once to lock, again to fire). Missile then chases and explodes on impact.
* Uses existing `LauncherBase<T>` / `ProjectileBase` architecture.
* Requires new code, prefabs, minor AI & player-input changes.

---

## 2. New Scripts (create in `Assets/Scripts/Weapons/`)
1. `ITargetable.cs`
    ```csharp
    /// Marker for anything a missile can chase.
    /// Ships, Asteroids, etc. should implement this.
    public interface ITargetable { Transform TargetPoint { get; } }
    ```
2. `MissileProjectile.cs` (inherits `ProjectileBase`)
   * Fields
     * `float homingSpeed`, `float homingTurnRate` (°/s)
     * `float explosionRadius`, `float explosionDamage`
     * `Transform target`
   * Behaviour
     1. `OnEnable()` – set initial forward velocity.
     2. `FixedUpdate()` – if `target` present, steer:
        ```csharp
        Vector3 dir = (target.position - transform.position).normalized;
        Vector3 newVel = Vector3.RotateTowards(rb.linearVelocity, dir * homingSpeed,
                                 homingTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        rb.linearVelocity = newVel;
        ```
     3. `OnTriggerEnter(Collider other)` – run base logic, then `Explode()`.
     4. `Explode()` – VFX + AoE damage, then `ReturnToPool()`.
     5. `ReturnToPool()` – clear target, reset velocities.

3. `MissileLauncher.cs` (inherits `LauncherBase<MissileProjectile>`)  
   * State machine: `enum LockState { Idle, Locking, Locked }`  
   * Fields
     * `float lockOnTime = 0.6f`, `float lockExpiry = 3f`, `float maxLockDistance = 100f`
     * `ITargetable currentTarget`
     * `float lockTimer`
     * `LockState state`
   * Public API  
     * `bool TryStartLock(ITargetable candidate)` – enters **Locking**, resets timer.  
     * `override void Fire()` – behaviour depends on `state`:  
       * **Idle** → call `TryStartLock(PickTarget())`.  
       * **Locking** → spawn missile via `base.Fire()` **without assigning a target** (it will fly straight), then `CancelLock()`.  
       * **Locked** → spawn missile via `base.Fire()`, pass `currentTarget`, then `CancelLock()`.
   * `Update()` loop  
     * If **Locking**, cast a ray from ship nose; if the same `ITargetable` remains hit and distance < `maxLockDistance`, increment `lockTimer`; if `lockTimer ≥ lockOnTime` → **Locked**.  
     * If LOS breaks or distance too high, call `CancelLock()`.
     * If **Locked**, lock persists even if LOS is lost; however, if distance to `currentTarget` exceeds `maxLockDistance` **or** `Time.time - lockAcquiredTime > lockExpiry`, then `CancelLock()`.
   * Helper `CancelLock()` resets state to **Idle** and clears target.

---

## 3. Prefab & Scene Tasks (implemented by designer)
1. **Missile prefab**
   * Model (placeholder cylinder), collider (trigger), `Rigidbody` (no gravity).
   * Attach `MissileProjectile` script.
   * Tag/layer same as existing projectiles.
2. **Explosion VFX prefab** (smoke, light flash) – poolable.
3. **Launcher attachment** – add `MissileLauncher` component to player ship & enemy prefab.
4. **UI** – basic lock-on reticle (optional polish).
   * Create a `LockOnReticle` prefab (UGUI or world-space):
       * Center-anchored SVG Image (`lock_on_reticle.svg`) containing four red corner brackets—no centre dot.
       * Animator with three states:
         * **Idle** – corners at full size, default colour.
         * **Locking** – uses Animator float `lockProgress` to drive an animation clip that scales/positions the brackets inward (0 → 1).
         * **LockedFlash** – brief flash/shake clip triggered when lock completes.
       * Optional fade as `lockExpiry` approaches (blend tree or separate clip driven by remaining time).
       * Reticle colour can still swap (e.g., red ➜ green) via Animator properties if desired.

---

## 4. Code Updates
* Ships / Asteroids implement `ITargetable` returning an appropriate transform (e.g., `transform`).
* **AI** (`AIShipInput`, BT nodes):  
  1. Use sensor to pick nearest enemy implementing `ITargetable`.  
  2. Call `AcquireTarget()` then `Fire()`.
* **PlayerShipInput**: map `Fire2` to `missileLauncher.Fire()`. First press starts lock-on; a subsequent press while `launcher.IsLocked` fires the missile. Provide a helper `PickTarget()` that raycasts straight ahead to find the first `ITargetable`.
* **Damage scripts** already handle high damage; ensure asteroid blow-up method exists (`BlowUp()` or similar).

---

## 5. Implementation Sequence (recommended order)
1. Create `ITargetable.cs` ➜ implement on Ship & Asteroid.
2. Implement `MissileProjectile.cs` (compile).
3. Build missile prefab and explosion VFX.
4. Implement `MissileLauncher.cs` and add to ships.
5. Integrate with player & AI controls.
6. Tune values (speed, lock-on time, damage, explosion radius).
7. QA pass – run **Testing Checklist** below.

---

## 6. Testing Checklist
- [ ] Lock-on requires uninterrupted LOS for `lockOnTime`.
- [ ] Lock expires after `lockExpiry` seconds if not fired.
- [ ] Missile self-destructs if target destroyed or distance > `maxDistance`.
- [ ] Explosion applies damage within `explosionRadius`.
- [ ] Asteroid one-shot destruction triggers VFX.
- [ ] Ship sustains high damage; UI updates correctly.
- [ ] Pooling: no instantiation spikes in profiler.

---

## 7. Tuning Guidelines
| Parameter | Suggested Start | Notes |
|-----------|-----------------|-------|
| lockOnTime | 0.6 s | >0 feels fair to player |
| homingSpeed | 15 – 20 | match laser speed tier |
| homingTurnRate | 90 °/s | lower ⇒ wider arcs |
| explosionRadius | 3 m | tune vs asteroid size |
| explosionDamage | 60 | ~5× laser damage |

Adjust in prefab inspector during play-test.

---

## 8. File Checklist
- [ ] `Assets/Scripts/Weapons/ITargetable.cs`
- [ ] `Assets/Scripts/Weapons/MissileProjectile.cs`
- [ ] `Assets/Scripts/Weapons/MissileLauncher.cs`
- [ ] Missile prefab (`Assets/Prefabs/Missile.prefab`)
- [ ] ExplosionVFX prefab (`Assets/Prefabs/Explosion.prefab`)

---

## 9. Additional Codebase Changes & Refactors

### 9.1 Player Controls
* Update **`PlayerShipInput`**
  * Map a second fire button (e.g. `Input.GetButton("Fire2")`) to `missileLauncher.Fire()`.
  * Provide a basic target-acquisition helper:
    ```csharp
    // Cast from ship forward to pick first ITargetable under cross-hair
    ITargetable PickTarget() { /* raycast code */ }
    ```
  * Before firing, call `missileLauncher.AcquireTarget(PickTarget()?.TargetPoint)`.

### 9.2 AI Integration
* **`AIShipInput`**
  * Cache a reference to `MissileLauncher` just like `LaserGun`.
  * Expand `HandleShooting()` to decide which weapon to fire based on distance / lock status.
  * When an enemy is within `missileRange` but outside `laserRange`, call:
    ```csharp
    missileLauncher.AcquireTarget(target);
    missileLauncher.Fire();
    ```

* **Behaviour-Trees** (BT nodes)
  * Existing `FireWeaponAction` already works because it receives a `WeaponComponent` reference. Add a parallel tree branch that feeds the missile launcher.

### 9.3 Interface Extensions
* Consider extending `IWeapon` with an optional
  ```csharp
  void AcquireTarget(Transform tgt);
  ```
  so generic BT / input code can treat missiles & lasers uniformly.
  *Default implementation* in non-targeted weapons can be empty.

### 9.4 Sharing Logic with `VelocityPilot`
* **Goal:** avoid duplicating homing math.
* Create `GuidanceUtility` in **Game.Core**:
  ```csharp
  public static class GuidanceUtility
  {
      public static Vector3 ComputeHomingVelocity(
          Vector3 currentPos, Vector3 currentVel, Transform target,
          float maxSpeed, float turnRateDeg, float dt)
      {
          Vector2 pos2D = GamePlane.WorldToPlane(currentPos);
          Vector2 vel2D = GamePlane.WorldToPlane(currentVel);
          Vector2 tgt2D = GamePlane.WorldToPlane(target.position);

          // Build a fake ShipKinematics so we can call VelocityPilot
          var kin = new ShipKinematics(pos2D, vel2D, 0f);
          var input = new VelocityPilot.Input(kin, tgt2D, Vector2.zero, Vector2.zero, maxSpeed);
          var output = VelocityPilot.Compute(input);
          Vector2 desired = GamePlane.PlaneVectorToWorld(new Vector2(output.thrust, output.strafe));
          return Vector3.RotateTowards(currentVel, desired.normalized * maxSpeed,
                                       turnRateDeg * Mathf.Deg2Rad * dt, 0f);
      }
  }
  ```
* `MissileProjectile.FixedUpdate()` then simplifies to:
  ```csharp
  rb.linearVelocity = GuidanceUtility.ComputeHomingVelocity(
          transform.position, rb.linearVelocity, target,
          homingSpeed, homingTurnRate, Time.fixedDeltaTime);
  ```
* This keeps homing behaviour consistent with ship AI steering.

### 9.5 Layers / Physics
* Add **`Missile`** layer if needed; ensure collision matrix ignores shooter but hits `Asteroid` & `Ship`.
* Add **`Explosion`** layer for AoE if you want to filter overlaps.

### 9.6 Damage & Asteroid Logic
* `Asteroid.TakeDamage()` already calls fragment system. No change.
* Ensure `ShipDamageHandler.TakeDamage()` uses `explosionDamage` value.

### 9.7 Asset / Settings
* Update **`Game.InputSettings`** to include `Fire2` binding.
* Add SFX (`Missile_Launch.wav`, `Missile_Explosion.wav`) and link in prefabs.

### 9.8 UI Integration
* **New Script:** `LockOnUI.cs` (in `Assets/Scripts/UI/`)
  ```csharp
  public sealed class LockOnUI : MonoBehaviour
  {
      [SerializeField] MissileLauncher launcher;
      [SerializeField] Image progressArc;
      [SerializeField] Image crosshair;

      void Update()
      {
          switch (launcher.State)
          {
              case MissileLauncher.LockState.Idle:
                  progressArc.fillAmount = 0f;
                  crosshair.color = Color.white;
                  break;
              case MissileLauncher.LockState.Locking:
                  progressArc.fillAmount = launcher.LockProgress; // 0–1
                  crosshair.color = Color.red;
                  break;
              case MissileLauncher.LockState.Locked:
                  progressArc.fillAmount = 1f;
                  crosshair.color = Color.green;
                  break;
          }
      }
  }
  ```
* `MissileLauncher` exposes read-only `State` and `LockProgress` (0–1) to drive the UI.
* Hook the prefab into the main HUD canvas; assign the player's `MissileLauncher` reference in the inspector.

---

*Augmented plan complete – covers cross-cutting changes & VelocityPilot re-use.* 