using UnityEngine;
using ShipControl;
using System.Collections.Generic;

public class AINavigator : MonoBehaviour
{
    /* ── Navigation tunables ─────────────────────────────────── */
    [Header("Navigation")]
    public float arriveRadius = 10f;

    [Header("Avoidance")]
    public LayerMask asteroidMask;
    public float lookAheadTime = 1f;
    public float safeMargin = 2f;
    public float avoidRadius = .5f;
    [Tooltip("Toggle obstacle avoidance logic on/off")] public bool enableAvoidance = false;

    [Header("Raycast Avoidance")]
    [Tooltip("Number of rays to cast on each side of the ship")]
    public int raysPerDirection = 5;
    [Tooltip("Max angle in degrees to cast rays")]
    public float maxRayDegrees = 90f;
    [Tooltip("Radius of spherecast for obstacle detection. Set to 0 for raycasts.")]
    public float sphereCastRadius = 0.5f;


    /* ── Smoothing ─────────────────────────────────────────── */
    [Header("Steering Smoothing")]
    [Tooltip("Higher values react faster; 0 disables smoothing. Units: 1/seconds (approx).")]
    [Range(0, 20)] public float proportionalGain = 5f;

    /* ── internals ───────────────────────────────────────────── */
    private Ship ship;
    private SteeringTuning steeringTuning;

    const int MaxColliders = 256;
    readonly Collider[] hits = new Collider[MaxColliders];
    readonly RaycastHit[] rayHits = new RaycastHit[MaxColliders];
    int dbgHitCount;
    readonly List<Vector3> dbgRays = new List<Vector3>();

    // Path-planning & pilot debug info (updated every physics step)
    PathPlanner.DebugInfo dbgPath;
    VelocityPilot.Output dbgPilot;
    Vector2 dbgGoal2D;

    // Smoothed control state
    float smoothThrust, smoothStrafe;

    float dbgThrust, dbgStrafe;

    public void Initialize(Ship ship)
    {
        this.ship = ship;
        float mass = ship.movement.Mass;
        var settings = ship.settings;
        steeringTuning = settings ?
            new SteeringTuning(settings.forwardAcceleration / mass,
                                settings.reverseAcceleration / mass,
                                settings.maxStrafeForce / mass,
                                SteeringTuning.Default.DeadZone)
            : SteeringTuning.Default;
    }

    public void GenerateNavCommands(ShipState state, AICommander.Waypoint navWaypoint, ref ShipCommand cmd)
    {
        if (ship == null || !navWaypoint.isValid)
        {
            cmd.TargetAngle = state.Kinematics.AngleDeg;
            return;
        }

        ShipKinematics kin = state.Kinematics;
        float currentMaxSpeed = ship.settings.maxSpeed;

        int obstacleCount = ScanObstacles(kin, currentMaxSpeed);
        VelocityPilot.Output vpOut = ComputeNavigation(kin, currentMaxSpeed, obstacleCount, navWaypoint);

        ApplyControls(vpOut, ref cmd);
    }

    int ScanObstacles(ShipKinematics kin, float currentMaxSpeed)
    {
        if (!enableAvoidance)
        {
            dbgRays.Clear();
            dbgHitCount = 0;
            return 0;
        }

        int n = 0;
        float maxDist = currentMaxSpeed * lookAheadTime + safeMargin;

        Vector2 centerDir2D = kin.Vel.sqrMagnitude > 0.1f ? kin.Vel.normalized : kin.Forward;
        Vector3 centerDirWorld = GamePlane.PlaneVectorToWorld(centerDir2D).normalized;

        dbgRays.Clear();

        // Cast forward ray
        n = CastRayAndCollect(centerDirWorld, maxDist, n);
        dbgRays.Add(centerDirWorld * maxDist);

        if (raysPerDirection > 0)
        {
            float angleStep = maxRayDegrees / raysPerDirection;
            for (int i = 1; i <= raysPerDirection; i++)
            {
                float currentAngle = i * angleStep;

                // Cast left
                var leftDir = Quaternion.Euler(0, -currentAngle, 0) * centerDirWorld;
                n = CastRayAndCollect(leftDir, maxDist, n);
                dbgRays.Add(leftDir * maxDist);

                // Cast right
                var rightDir = Quaternion.Euler(0, currentAngle, 0) * centerDirWorld;
                n = CastRayAndCollect(rightDir, maxDist, n);
                dbgRays.Add(rightDir * maxDist);
            }
        }

        dbgHitCount = n;
        return n;
    }

    int CastRayAndCollect(Vector3 dir, float maxDist, int start)
    {
        int n = start;
        int cnt;
        if (sphereCastRadius > 0f)
        {
            cnt = Physics.SphereCastNonAlloc(transform.position, sphereCastRadius, dir, rayHits, maxDist, asteroidMask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            cnt = Physics.RaycastNonAlloc(transform.position, dir, rayHits, maxDist, asteroidMask, QueryTriggerInteraction.Ignore);
        }

        for (int i = 0; i < cnt && i < MaxColliders; i++)
        {
            Collider col = rayHits[i].collider;
            if (col && System.Array.IndexOf(hits, col, 0, n) < 0) hits[n++] = col;
        }
        return n;
    }

    VelocityPilot.Output ComputeNavigation(ShipKinematics kin, float currentMaxSpeed, int obstacleCount, AICommander.Waypoint navWaypoint)
    {
        Vector2 goal2D = navWaypoint.position;
        dbgGoal2D = goal2D;

        // Velocity of the waypoint (if it's a ship or waypoint)
        Vector2 wpVel = navWaypoint.velocity;

        var ppIn = new PathPlanner.Input(kin, goal2D, wpVel, avoidRadius, arriveRadius, currentMaxSpeed,
                                           lookAheadTime, safeMargin,
                                           new System.ArraySegment<Collider>(hits, 0, obstacleCount), steeringTuning);

        var ppOut = PathPlanner.Compute(ppIn);
        dbgPath = ppOut.dbg;
        var vpIn = new VelocityPilot.Input(kin, ppOut.desiredVelocity, ppOut.desiredAccel, currentMaxSpeed, steeringTuning, false);
        var vpOut = VelocityPilot.Compute(vpIn);
        dbgPilot = vpOut;
        return vpOut;
    }

    void ApplyControls(VelocityPilot.Output vpOut, ref ShipCommand cmd)
    {
        // Proportional smoothing
        float k = proportionalGain;
        float dt = Time.fixedDeltaTime;
        if (k > 0f)
        {
            smoothThrust += (vpOut.thrust - smoothThrust) * k * dt;
            smoothStrafe += (vpOut.strafe - smoothStrafe) * k * dt;
        }
        else
        {
            smoothThrust = vpOut.thrust;
            smoothStrafe = vpOut.strafe;
        }

        cmd.Thrust = smoothThrust;
        cmd.Strafe = smoothStrafe;
        cmd.RotateToTarget = true;
        cmd.TargetAngle = vpOut.rotTargetDeg;

        dbgThrust = smoothThrust;
        dbgStrafe = smoothStrafe;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Visualise planner internals when selected in the editor
        if (!Application.isPlaying) return;

        // Ship future position
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // cyan
        Vector3 fut3 = GamePlane.PlaneToWorld(dbgPath.future);
        Gizmos.DrawLine(transform.position, fut3);
        Gizmos.DrawSphere(fut3, 0.3f);

        // Desired velocity vectorR
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
            // Draw raycast fan
            Gizmos.color = new Color(1f, 0.75f, 0f, 0.5f); // orange-ish
            if (dbgRays != null)
            {
                foreach (var ray in dbgRays)
                {
                    Gizmos.DrawLine(transform.position, transform.position + ray);
                    if (sphereCastRadius > 0)
                    {
                        Gizmos.DrawWireSphere(transform.position + ray, sphereCastRadius);
                    }
                }
            }

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