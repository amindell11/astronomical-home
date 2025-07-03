# General Optimizations (Easy Wins)

> Quick, low-risk improvements that give immediate performance or maintenance benefits without changing gameplay mechanics.

1. **ProjectileBase compile fix** ✅ COMPLETE  
   Missing semicolon after a `Debug.Log` call at line ~63.

2. **Editor-gate verbose logging** ✅ COMPLETE  
   Wrap every runtime `Debug.Log*/Handles.*` call in `#if UNITY_EDITOR` (or remove).  Main offenders: `MissileLauncher`, `MissileProjectile`, `AsteroidSpawner`, `AsteroidFragnetics`, `ProjectileBase`, `LaserProjectile`, several BT nodes, `CameraFollow`, etc.

3. **Cache ReferencePlane once** ✅ COMPLETE  
   `ProjectileBase.OnEnable` does `FindGameObjectWithTag` per projectile.  Cache the `Transform` statically.

4. **CameraFollow – avoid full-scene scans** ✅ COMPLETE  
   Replace `FindObjectsByType<Transform>` with a static list of ships that register in `OnEnable/OnDisable` (or tag lookup).

5. **Audio one-shot pooling** ✅ COMPLETE  
   Replace `AudioSource.PlayClipAtPoint` with a small pooled/hidden `AudioSource` to avoid per-shot GameObject churn.

6. **MissileLauncher raycasts**  
   Switch to `Physics.RaycastNonAlloc` (static 1-slot buffer) to avoid allocations each FixedUpdate.

7. **Reuse WaitForSeconds**  
   Cache `new WaitForSeconds(enemyRespawnDelay)` in `GameManager` respawn coroutine.

8. **Asteroid.UpdateMeshCollider**  
   Skip assignment if `sharedMesh` already correct.

10. **PlayerShipInput mouse work**  
    Early-out of `GetMouseWorldPosition` when `useMouseDirection` is false.

11. **SimplePool memory ceiling** ✅ COMPLETE  
    Provide a `ClearAllPools()` call on scene unload to avoid stack growth over time.

12. **Physics queries hit-triggers toggle**  
    In `AsteroidFieldManager.UpdateCachedDensity` temporarily disable `Physics.queriesHitTriggers` for the overlap test if triggers are irrelevant.

13. **Replace InvokeRepeating**  
    In `AsteroidFieldManager` use an accumulated‐time pattern instead of `InvokeRepeating` to remove reflection & GC.

14. **UI LateUpdate throttling**  
    In `LockOnIndicator`, `ShieldUI`, etc., early-return in editor and consider disabling when off-screen.

15. **MaterialPropertyBlock reuse**  
    Reuse the cached block in `ShipHealthVisuals` instead of allocating each flash.

---
Implementing the above removes the only compile error, slashes logging-related allocations, and cuts several thousand GC-allocs per minute during typical play. 