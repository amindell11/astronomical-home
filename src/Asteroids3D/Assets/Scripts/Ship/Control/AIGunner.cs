using UnityEngine;
using ShipControl;

public class AIGunner : MonoBehaviour
{
    /* ── Combat tunables (identical to old script) ───────────── */
    [Header("Combat")]
    [SerializeField] float fireAngleTolerance = 5f;
    [SerializeField] float fireDistance = 20f;
    [SerializeField] LayerMask lineOfSightMask = ~0;
    [SerializeField] int lineOfSightCacheFrames = 5;
    [SerializeField] float angleToleranceBeforeRay = 15f;

    [Header("Missile Combat")]
    [SerializeField] float missileRange = 40f;
    [SerializeField] float missileAngleTolerance = 15f;

    /* ── internals ───────────────────────────────────────────── */
    private Ship ship;

    // LOS cache
    bool cachedLOS;
    int losFrame = -1;
    Vector3 lastRayPos, lastTgtPos;

    public void Initialize(Ship ship)
    {
        this.ship = ship;
        lineOfSightMask = LayerIds.Mask(LayerIds.Asteroid);
    }

    public void GenerateGunnerCommands(ShipState state, AICommander.Waypoint navWaypoint, ref ShipCommand cmd)
    {
        cmd.PrimaryFire = false;
        cmd.SecondaryFire = false;

        if (!navWaypoint.isValid)
        {
            RLog.AI($"[AI-{name}] GenerateGunnerCommands: No target set, weapons disabled");
            return;
        }

        Vector3 targetPos = GamePlane.PlaneToWorld(navWaypoint.position);

        Vector3 firePos = transform.position;
        Vector3 dir = targetPos - firePos;
        float dist = dir.magnitude;
        float angle = Vector3.Angle(transform.up, dir);
        
        RLog.AI($"[AI-{name}] GenerateGunnerCommands: Target at dist={dist:F1}, angle={angle:F1}°, fireDistance={fireDistance:F1}, fireAngleTolerance={fireAngleTolerance:F1}°");

        bool wantsToFireMissile = false;
        const float dummyMissileRange = 10f; // Close range for dumb-fire during locking

        if (ship.missileLauncher)
        {
            switch (state.MissileState)
            {
                case MissileLauncher.LockState.Idle:
                case MissileLauncher.LockState.Locking:
                    // Dumb-fire if target is very close, since locking is automatic or in progress.
                    if (dist <= dummyMissileRange && angle <= missileAngleTolerance)
                    {
                        wantsToFireMissile = true;
                        RLog.AI($"[AI-{name}] Missile: Idle/Locking, close enough for dumb-fire");
                    }
                    else
                    {
                        RLog.AI($"[AI-{name}] Missile: Idle/Locking, waiting for auto-lock.");
                    }
                    break;

                case MissileLauncher.LockState.Locked:
                    // Fire locked missile and don't fire laser
                    wantsToFireMissile = true;
                    RLog.AI($"[AI-{name}] Missile: Locked state, will fire");
                    break;

                case MissileLauncher.LockState.Cooldown:
                    // Do nothing during cooldown
                    RLog.AI($"[AI-{name}] Missile: Cooldown state");
                    break;
            }
        }

        cmd.SecondaryFire = wantsToFireMissile;

        // Only block laser when we have a locked missile ready to fire
        bool blockLaserForMissile = wantsToFireMissile && state.MissileState == MissileLauncher.LockState.Locked;

        if (ship.laserGun && dist <= fireDistance && angle <= fireAngleTolerance && !blockLaserForMissile)
        {
            Vector3 laserFirePos = ship.laserGun.firePoint ? ship.laserGun.firePoint.position : transform.position;
            bool losOK = HasLineOfSight(laserFirePos, dir, dist, angle, GamePlane.PlaneToWorld(navWaypoint.position));
            
            RLog.AI($"[AI-{name}] Laser check: gun={ship.laserGun != null}, inRange={dist <= fireDistance}, inAngle={angle <= fireAngleTolerance}, noMissile={!blockLaserForMissile}, LOS={losOK}");

            if (losOK)
            {
                cmd.PrimaryFire = true;
                RLog.AI($"[AI-{name}] FIRING LASER!");
            }
        }
        else
        {
            RLog.AI($"[AI-{name}] Laser conditions failed: gun={ship.laserGun != null}, dist={dist:F1}<={fireDistance:F1}={dist <= fireDistance}, angle={angle:F1}<={fireAngleTolerance:F1}={angle <= fireAngleTolerance}, blockLaser={blockLaserForMissile}");
        }
    }

    public bool HasLineOfSight(Vector3 firePos, Vector3 dir, float dist, float angle, Vector3 targetPos)
    {
        int f = Time.frameCount;
        bool need = (losFrame < 0 || f - losFrame >= lineOfSightCacheFrames)
                    || Vector3.Distance(firePos, lastRayPos) > 1f
                    || Vector3.Distance(targetPos, lastTgtPos) > 1f;

        if (angle > angleToleranceBeforeRay)
        {
            RLog.AI($"[AI-{name}] LOS: Angle {angle:F1}° > {angleToleranceBeforeRay:F1}°, skipping raycast");
            return false;
        }

        if (need)
        {
            RLog.AI($"[AI-{name}] LOS: Performing raycast (frame={f}, lastFrame={losFrame}, cache={lineOfSightCacheFrames})");
            cachedLOS = LineOfSight.IsClear(
                            firePos,
                            targetPos,
                            lineOfSightMask);
            RLog.AI($"[AI-{name}] LOS: Utility result = {cachedLOS}, mask={lineOfSightMask.value}");
            losFrame = f;
            lastRayPos = firePos;
            lastTgtPos = targetPos;
        }
        else
        {
            RLog.AI($"[AI-{name}] LOS: Using cached result = {cachedLOS} (frame={f}, lastFrame={losFrame})");
        }
        return cachedLOS;
    }

    /// <summary>Returns true if an unobstructed line of sight exists to <paramref name="tgt"/>.</summary>
    public bool HasLineOfSight(Transform tgt)
    {
        if (!ship.laserGun || !tgt) return false;

        Vector3 firePos = ship.laserGun.firePoint ? ship.laserGun.firePoint.position : transform.position;
        Vector3 dir = tgt.position - firePos;
        float dist = dir.magnitude;
        float angle = Vector3.Angle(transform.up, dir);

        return HasLineOfSight(firePos, dir, dist, angle, tgt.position);
    }
} 