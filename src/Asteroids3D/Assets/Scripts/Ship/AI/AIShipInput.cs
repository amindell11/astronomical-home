using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ShipMovement))]
public class AIShipInput : MonoBehaviour
{
    /* ── Navigation tunables ─────────────────────────────────── */
    [Header("Navigation")]
    private Transform target;
    public float arriveRadius  = 10f;

    [Header("Avoidance")]
    public LayerMask asteroidMask;
    public float lookAheadTime = 1f;
    public float safeMargin    = 2f;
    public float avoidRadius   = .5f;
    [Tooltip("Toggle obstacle avoidance logic on/off")] public bool enableAvoidance = false;

    /* ── Smoothing ─────────────────────────────────────────── */
    [Header("Steering Smoothing")]
    [Tooltip("Higher values react faster; 0 disables smoothing. Units: 1/seconds (approx).")]
    [Range(0, 20)] public float proportionalGain = 5f;

    /* ── Combat tunables (identical to old script) ───────────── */
    [Header("Combat")]
    [SerializeField] float fireAngleTolerance       = 5f;
    [SerializeField] float fireDistance             = 20f;
    [SerializeField] LayerMask lineOfSightMask      = ~0;
    [SerializeField] int lineOfSightCacheFrames     = 5;
    [SerializeField] float angleToleranceBeforeRay  = 15f;
    [SerializeField] bool updateControl = true;

    [Header("Missile Combat")]
    [SerializeField] float missileRange             = 40f;
    [SerializeField] float missileAngleTolerance    = 15f;

    /* ── internals ───────────────────────────────────────────── */
    ShipMovement      ship;
    LaserGun  gun;
    MissileLauncher missileLauncher;
    Camera    mainCam;

    // LOS cache
    bool   cachedLOS;
    int    losFrame = -1;
    Vector3 lastRayPos, lastTgtPos;

    const int MaxColliders = 256;
    readonly Collider[] hits = new Collider[MaxColliders];
    readonly RaycastHit[] rayHits = new RaycastHit[MaxColliders];
    int dbgHitCount;
    Vector3 dbgRayVel, dbgRayFwd;

    // Path-planning & pilot debug info (updated every physics step)
    PathPlanner.DebugInfo dbgPath;
    VelocityPilot.Output dbgPilot;
    Vector2 dbgGoal2D;

    // Smoothed control state
    float smoothThrust, smoothStrafe;

    // Navigation target system - completely refactored to use waypoint struct (Optimization #3)
    private enum TargetType
    {
        None,
        Transform,
        Waypoint
    }
    private TargetType targetType = TargetType.None;
    private Transform targetTransform;

    // Pure struct waypoint to replace GameObject (Optimization #3)
    private struct Waypoint
    {
        public Vector3 position;
        public Vector2 velocity;
        public bool isValid;
    }
    private Waypoint navWaypoint;

    float dbgThrust, dbgStrafe;

    // Optional reference when the navigation target is another ship (for velocity matching)

    void Awake()
    {
        ship    = GetComponent<ShipMovement>();
        gun     = GetComponentInChildren<LaserGun>();
        missileLauncher = GetComponentInChildren<MissileLauncher>();
        mainCam = Camera.main;

        // Initialize waypoint struct (Optimization #3)
        navWaypoint = new Waypoint { isValid = false };
    }

    /* ────────────────────────────────────────────────────────── */
    void FixedUpdate()
    {
        if (targetType == TargetType.None)
        {
            ship.Controller.SetControls(0, 0);
            ship.Controller.SetRotationTarget(false, ship.Controller.Angle);
            return;
        }

        ShipKinematics kin = ship.Kinematics;

        float currentMaxSpeed = ship.maxSpeed;

        int obstacleCount = ScanObstacles(kin, currentMaxSpeed);

        VelocityPilot.Output vpOut = ComputeNavigation(kin, currentMaxSpeed, obstacleCount);

        ApplyControls(vpOut);

        if (!updateControl) return;

        HandleShooting();
    }

    /* ── Enhanced weapon logic (laser + missile) ─────── */
    public void HandleShooting()
    {
        if (targetType == TargetType.None || !mainCam) return;

        Vector3 targetPos = GetTargetPosition();
        Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
        if (!(vp.z > 0 && vp.x is >= 0 and <= 1 && vp.y is >= 0 and <= 1)) return;

        Vector3 firePos = transform.position;
        Vector3 dir     = targetPos - firePos;
        float   dist    = dir.magnitude;
        float   angle   = Vector3.Angle(transform.up, dir);

        // Try missile first if we have one and target is in missile range
        if (missileLauncher && TryFireMissile(dist, angle, dir)) return;

        // Fall back to laser if available and in range
        if (gun) TryFireLaser(dist, angle, dir);
    }

    bool TryFireMissile(float dist, float angle, Vector3 dir)
    {
        if (dist > missileRange) return false;
        if (angle > missileAngleTolerance) return false;

        // For missiles, we need to check if target is ITargetable
        ITargetable targetable = GetTargetTransform()?.GetComponentInParent<ITargetable>();
        if (targetable == null) return false;

        // Try to start lock or fire if already locking/locked
        if (!missileLauncher.IsLocked)
        {
            // Begin or continue locking process (cooldown gate inside TryStartLock)
            missileLauncher.TryStartLock(targetable);
            return false; // not ready to shoot yet
        }

        // Already locked – go ahead and fire (this will also reset the lock)
        missileLauncher.Fire();
        return true;
    }

    bool TryFireLaser(float dist, float angle, Vector3 dir)
    {
        if (dist > fireDistance) return false;
        if (angle > fireAngleTolerance) return false;

        Vector3 firePos = gun.firePoint ? gun.firePoint.position : transform.position;
        if (!LineOfSightOK(firePos, dir, dist, angle)) return false;

        gun.Fire();
        return true;
    }

    public bool hasLOS(Transform tgt)
    {
        return LineOfSightOK(transform.position, tgt.position - transform.position, Vector3.Distance(transform.position, tgt.position), Vector3.Angle(transform.up, tgt.position - transform.position));
    }

    bool LineOfSightOK(Vector3 firePos, Vector3 dir, float dist, float angle)
    {
        int f = Time.frameCount;
        Vector3 targetPos = GetTargetPosition();
        bool need = (losFrame < 0 || f - losFrame >= lineOfSightCacheFrames)
                    || Vector3.Distance(firePos, lastRayPos) > 1f
                    || Vector3.Distance(targetPos, lastTgtPos) > 1f;

        if (angle > angleToleranceBeforeRay) return false;

        if (need)
        {
            cachedLOS = !Physics.Raycast(firePos, dir.normalized,
                                         dist, lineOfSightMask)
                         || Physics.Raycast(firePos, dir.normalized,
                                            out var hit, dist, lineOfSightMask)
                            && hit.transform.root == GetTargetTransform()?.root;

            losFrame   = f;
            lastRayPos = firePos;
            lastTgtPos = targetPos;
        }
        return cachedLOS;
    }

    /* ────────────────── Public helper API for BehaviourTree ────────────────── */

    /// <summary>Sets a Transform target for navigation and optionally toggles avoidance.</summary>
    public void SetNavigationTarget(Transform tgt, bool avoid)
    {
        targetTransform = tgt;
        targetType = tgt != null ? TargetType.Transform : TargetType.None;
        enableAvoidance = avoid;
    }

    /// <summary>Sets an arbitrary world-space point as the navigation goal.</summary>
    /// <remarks>Uses pure struct waypoint instead of GameObject (Optimization #3).</remarks>
    public void SetNavigationPoint(Vector3 point, bool avoid=false, Vector3? velocity=null)
    {
        // Update waypoint struct instead of creating GameObject (Optimization #3)
        navWaypoint.position = point;
        navWaypoint.velocity = velocity ?? Vector2.zero;
        navWaypoint.isValid = true;
        
        targetType = TargetType.Waypoint;
        targetTransform = null;
        enableAvoidance = avoid;
    }

    /// <summary>Convenience wrapper so BT leaf nodes can fire weapons once.</summary>
    public void TryFire()
    {
        HandleShooting();
    }

    /// <summary>Convenience wrapper for BT nodes that specifically want to fire missiles.</summary>
    public void TryFireMissile()
    {
        if (!missileLauncher || targetType == TargetType.None) return;
        
        Vector3 targetPos = GetTargetPosition();
        Vector3 dir = targetPos - transform.position;
        float dist = dir.magnitude;
        float angle = Vector3.Angle(transform.up, dir);
        
        TryFireMissile(dist, angle, dir);
    }

    /// <summary>Returns true if an unobstructed line of sight exists to <paramref name="tgt"/>.</summary>
    public bool HasLineOfSight(Transform tgt)
    {
        if (!gun || !tgt) return false;

        Vector3 firePos = gun.firePoint ? gun.firePoint.position : transform.position;
        Vector3 dir     = tgt.position - firePos;
        float   dist    = dir.magnitude;
        float   angle   = Vector3.Angle(transform.up, dir);

        return LineOfSightOK(firePos, dir, dist, angle);
    }

    /// <summary>Sets a ship as the navigation/pursuit target and optionally enables avoidance.</summary>
    public void SetShipTarget(ShipMovement tgtShip, bool avoid)
    {
        targetTransform = tgtShip ? tgtShip.transform : null;
        targetType = tgtShip != null ? TargetType.Transform : TargetType.None;
        enableAvoidance = avoid;
    }

    // Helper methods to get target information
    private Vector3 GetTargetPosition()
    {
        switch (targetType)
        {
            case TargetType.Transform:
                return targetTransform != null ? targetTransform.position : transform.position;
            case TargetType.Waypoint:
                return navWaypoint.isValid ? navWaypoint.position : transform.position;
            default:
                return transform.position;
        }
    }

    private Transform GetTargetTransform()
    {
        return targetType == TargetType.Transform ? targetTransform : null;
    }

    private Vector2 GetTargetVelocity()
    {
        switch (targetType)
        {
            case TargetType.Transform:
                // Get ShipMovement component directly from targetTransform (eliminates targetShip field)
                var targetShipMovement = targetTransform?.GetComponent<ShipMovement>();
                return targetShipMovement ? targetShipMovement.Velocity2D : Vector2.zero;
            case TargetType.Waypoint:
                return navWaypoint.isValid ? navWaypoint.velocity : Vector2.zero;
            default:
                return Vector2.zero;
        }
    }

    // --------------------------------------------------------------
    int ScanObstacles(ShipKinematics kin, float currentMaxSpeed)
    {
        int n = 0;
        if (!enableAvoidance) return 0;

        float maxDist = currentMaxSpeed * lookAheadTime + safeMargin;

        Vector2 velDir = kin.vel.sqrMagnitude > 0.1f ? kin.vel.normalized : Vector2.zero;
        Vector3 velDirWorld = GamePlane.PlaneVectorToWorld(velDir).normalized;
        dbgRayVel = velDirWorld * maxDist;

        if (velDir != Vector2.zero)
            n = CastRayAndCollect(velDirWorld, maxDist, n);

        Vector3 fwdWorld = GamePlane.PlaneVectorToWorld(kin.forward).normalized;
        dbgRayFwd = fwdWorld * maxDist;
        n = CastRayAndCollect(fwdWorld, maxDist, n);

        dbgHitCount = n;
        return n;
    }

    int CastRayAndCollect(Vector3 dir, float maxDist, int start)
    {
        int n = start;
        int cnt = Physics.RaycastNonAlloc(transform.position, dir, rayHits, maxDist, asteroidMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cnt && n < MaxColliders; i++)
        {
            Collider col = rayHits[i].collider;
            if (col && System.Array.IndexOf(hits, col, 0, n) < 0) hits[n++] = col;
        }
        return n;
    }

    VelocityPilot.Output ComputeNavigation(ShipKinematics kin, float currentMaxSpeed, int obstacleCount)
    {
        Vector2 goal2D = GamePlane.WorldToPlane(GetTargetPosition());
        dbgGoal2D = goal2D;

        var ppIn = new PathPlanner.Input(kin, goal2D, avoidRadius, arriveRadius, currentMaxSpeed,
                                           lookAheadTime, safeMargin,
                                           new System.ArraySegment<Collider>(hits, 0, obstacleCount));

        var ppOut = PathPlanner.Compute(ppIn);
        dbgPath = ppOut.dbg;

        // Velocity of the waypoint (if it's a ship or waypoint)
        Vector2 wpVel = GetTargetVelocity();

        // Provide PathPlanner's desired velocity so we still benefit from avoidance, but include waypoint velocity for braking.
        var vpIn = new VelocityPilot.Input(kin, goal2D, ppOut.desiredVelocity, wpVel, currentMaxSpeed);
        var vpOut = VelocityPilot.Compute(vpIn);
        dbgPilot = vpOut;
        return vpOut;
    }

    void ApplyControls(VelocityPilot.Output vpOut)
    {
        // Proportional smoothing
        float k = proportionalGain;
        float dt = Time.fixedDeltaTime;
        if (k > 0f)
        {
            smoothThrust += (vpOut.thrust  - smoothThrust) * k * dt;
            smoothStrafe += (vpOut.strafe - smoothStrafe) * k * dt;
        }
        else
        {
            smoothThrust = vpOut.thrust;
            smoothStrafe = vpOut.strafe;
        }

        dbgThrust = smoothThrust;
        dbgStrafe = smoothStrafe;

        ship.Controller.SetControls(smoothThrust, smoothStrafe);
        ship.Controller.SetRotationTarget(true, vpOut.rotTargetDeg);
    }
    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (targetType != TargetType.None)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, GetTargetPosition());
        }

        // Visualise planner internals when selected in the editor
        if (!Application.isPlaying) return;

        // Ship future position
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // cyan
        Vector3 fut3 = GamePlane.PlaneToWorld(dbgPath.future);
        Gizmos.DrawLine(transform.position, fut3);
        Gizmos.DrawSphere(fut3, 0.3f);

        // Desired velocity vector
        Gizmos.color = Color.green;
        Vector3 dvec = GamePlane.PlaneVectorToWorld(dbgPath.desired);
        Gizmos.DrawLine(transform.position, transform.position + dvec);

        // Avoidance vector
        Gizmos.color = Color.red;
        Vector3 av = GamePlane.PlaneVectorToWorld(dbgPath.avoid);
        Gizmos.DrawLine(transform.position, transform.position + av);

        // Resulting acceleration vector (magenta)
        Gizmos.color = new Color(1f, 0f, 1f);
        Vector3 ac = GamePlane.PlaneVectorToWorld(dbgPath.accel);
        Gizmos.DrawLine(transform.position, transform.position + ac);

        // Waypoint marker (distinct yellow)
        Gizmos.color = Color.yellow;
        Vector3 goal3 = GamePlane.PlaneToWorld(dbgGoal2D);
        Gizmos.DrawLine(transform.position, goal3);
        Gizmos.DrawSphere(goal3, 0.4f);

        // Detection/avoidance sphere radius visualization
        if (enableAvoidance)
        {
            float maxDist = ship.maxSpeed * lookAheadTime + safeMargin;

            // Draw velocity ray
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(transform.position, transform.position + dbgRayVel);

            // Draw forward ray
            Gizmos.color = new Color(1f, 0.75f, 0f); // orange-ish
            Gizmos.DrawLine(transform.position, transform.position + dbgRayFwd);

            // Draw detected asteroids prior to filtering logic
            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.6f); // greyish
            for (int i = 0; i < dbgHitCount && i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c)
                {
                    Vector3 p = c.transform.position;
                    float rad = c.bounds.extents.x;
                    Gizmos.DrawWireSphere(p, rad);
                }
            }
        }
    }
#endif
}