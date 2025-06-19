using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ShipMovement))]
public class AIShipSteeringInput : MonoBehaviour
{
    /* ── Navigation tunables ─────────────────────────────────── */
    [Header("Navigation")]
    public Transform target;
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

    /* ── internals ───────────────────────────────────────────── */
    ShipMovement      ship;
    LaserGun  gun;
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

    // Path-planning debug information (updated every physics step)
    PathPlanner.DebugInfo dbgInfo;
    float dbgThrust, dbgStrafe;
    Vector3 dbgWaypoint;

    // Smoothed control state
    float smoothThrust, smoothStrafe;

    void Awake()
    {
        ship    = GetComponent<ShipMovement>();
        gun     = GetComponentInChildren<LaserGun>();
        mainCam = Camera.main;
    }

    /* ────────────────────────────────────────────────────────── */
    void FixedUpdate()
    {
        if (!target)
        {
            ship.Controller.SetControls(0, 0);
            ship.Controller.SetRotationTarget(false, ship.Controller.Angle);
            return;
        }

        /* 1.  Gather planner input */ 
        float currentMaxSpeed = ship.maxSpeed;
        int n = 0;
        if (enableAvoidance)
        {
            float maxDist = currentMaxSpeed * lookAheadTime + safeMargin;

            // First ray: current velocity direction
            Vector3 vel = ship.Controller.Velocity;
            Vector3 velDir = vel.sqrMagnitude > 0.1f ? vel.normalized : Vector3.zero;
            dbgRayVel = velDir * maxDist;

            if (velDir != Vector3.zero)
            {
                int cnt = Physics.RaycastNonAlloc(transform.position, velDir, rayHits, maxDist, asteroidMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < cnt && n < MaxColliders; i++)
                {
                    Collider col = rayHits[i].collider;
                    if (col && System.Array.IndexOf(hits, col, 0, n) < 0)
                    {
                        hits[n++] = col;
                    }
                }
            }

            // Second ray: ship forward/thrust direction
            Vector3 fwdDir = transform.up;
            dbgRayFwd = fwdDir * maxDist;
            int cntF = Physics.RaycastNonAlloc(transform.position, fwdDir, rayHits, maxDist, asteroidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cntF && n < MaxColliders; i++)
            {
                Collider col = rayHits[i].collider;
                if (col && System.Array.IndexOf(hits, col, 0, n) < 0)
                {
                    hits[n++] = col;
                }
            }
        }

        dbgHitCount = n; // for gizmos visualization

        if (enableAvoidance && n == MaxColliders)
        {
            Debug.LogWarning($"AIShipSteeringInput: Collider buffer full ({MaxColliders}). Some asteroids may be ignored – consider increasing MaxColliders.");
        }

        var plannerIn = new PathPlanner.Input
        {
            position         = transform.position,
            velocity         = ship.Controller.Velocity,
            radius           = avoidRadius,
            forward          = transform.up,
            goal             = target,
            maxSpeed         = currentMaxSpeed,
            arriveRadius     = arriveRadius,
            lookAheadTime    = lookAheadTime,
            safeMargin       = safeMargin,
            nearbyAsteroids  = new System.ArraySegment<Collider>(hits, 0, n)
        };

        /* 2.  Compute steering */
        Vector3 steerTarget3D = PathPlanner.SteerTarget(plannerIn, out dbgInfo);
        dbgWaypoint = steerTarget3D;

        // Convert waypoint to ship plane space (2-D) relative to plane origin
        Vector2 waypoint2D = ship.WorldToPlane(steerTarget3D - ship.GetPlaneOrigin());

        // Get current ship 2-D state
        Vector2 curPos   = ship.Controller.Position;
        Vector2 curVel   = ship.Controller.Velocity;
        float   angRad   = ship.Controller.Angle * Mathf.Deg2Rad;
        Vector2 forward2 = new Vector2(-Mathf.Sin(angRad), Mathf.Cos(angRad));

        // Use VelocityPilot to compute inputs
        VelocityPilot.ComputeInputs(waypoint2D, curPos, curVel, forward2,
                                    currentMaxSpeed,
                                    out float rawThrust, out float rawStrafe, out float rotTargetDeg);

        // Proportional smoothing (P-only PID)
        float k = proportionalGain;
        if (k > 0f)
        {
            float dt = Time.fixedDeltaTime;
            smoothThrust += (rawThrust  - smoothThrust) * k * dt;
            smoothStrafe += (rawStrafe - smoothStrafe) * k * dt;
        }
        else
        {
            smoothThrust = rawThrust;
            smoothStrafe = rawStrafe;
        }

        // cache for gizmos
        dbgThrust = smoothThrust;
        dbgStrafe = smoothStrafe;

        // Apply to ship
        ship.Controller.SetControls(smoothThrust, smoothStrafe);
        ship.Controller.SetRotationTarget(true, rotTargetDeg);

        /* 3.  Handle combat after motion planning */
        HandleShooting();
    }

    /* ── Laser logic (straight copy of old AIShipInput) ─────── */
    void HandleShooting()
    {
        if (!gun || !target || !mainCam) return;

        Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
        if (!(vp.z > 0 && vp.x is >= 0 and <= 1 && vp.y is >= 0 and <= 1)) return;

        Vector3 firePos = gun.firePoint ? gun.firePoint.position : transform.position;
        Vector3 dir     = target.position - firePos;
        float   dist    = dir.magnitude;
        if (dist > fireDistance) return;

        float angle = Vector3.Angle(transform.up, dir);
        if (angle > fireAngleTolerance) return;

        if (!LineOfSightOK(firePos, dir, dist, angle)) return;

        gun.Fire();
    }

    bool LineOfSightOK(Vector3 firePos, Vector3 dir, float dist, float angle)
    {
        int f = Time.frameCount;
        bool need = (losFrame < 0 || f - losFrame >= lineOfSightCacheFrames)
                    || Vector3.Distance(firePos, lastRayPos) > 1f
                    || Vector3.Distance(target.position, lastTgtPos) > 1f;

        if (angle > angleToleranceBeforeRay) return false;

        if (need)
        {
            cachedLOS = !Physics.Raycast(firePos, dir.normalized,
                                         dist, lineOfSightMask)
                         || Physics.Raycast(firePos, dir.normalized,
                                            out var hit, dist, lineOfSightMask)
                            && hit.transform.root == target.root;

            losFrame   = f;
            lastRayPos = firePos;
            lastTgtPos = target.position;
        }
        return cachedLOS;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (target)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }

        // Visualise planner internals when selected in the editor
        if (!Application.isPlaying) return;

        // Ship future position
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // cyan
        Gizmos.DrawLine(transform.position, dbgInfo.future);
        Gizmos.DrawSphere(dbgInfo.future, 0.3f);

        // Desired velocity vector
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + dbgInfo.desired);

        // Avoidance vector
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + dbgInfo.avoid);

        // Resulting acceleration vector (magenta)
        Gizmos.color = new Color(1f, 0f, 1f); // magenta
        Gizmos.DrawLine(transform.position, transform.position + dbgInfo.accel);

        // Waypoint marker (distinct yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, dbgWaypoint);
        Gizmos.DrawSphere(dbgWaypoint, 0.4f);

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