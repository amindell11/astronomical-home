using UnityEngine;
using Unity.MLAgents.Sensors;
using System.Runtime.CompilerServices;

/// <summary>
/// A utility class responsible for collecting all observations for an RLCommanderAgent.
/// It encapsulates the logic for sensing the environment, finding targets,
/// and encoding that information into the observation vector.
/// </summary>
public class RLObserver
{
    /// <summary>
    /// Human-readable labels for each float in the observation vector, in the
    /// exact order <see cref="CollectObservations"/> pushes them into the
    /// <see cref="VectorSensor"/>.  If you change the observation layout,
    /// please update this array accordingly so debug tools stay in sync.
    /// </summary>
    public static readonly string[] ObservationLabels =
    {
        // --- Self state ---
        "self_speed_norm",          // 0
        "self_yawrate_norm",        // 1
        "self_health_pct",          // 2
        "self_shield_pct",          // 3
        "dist_to_center_norm",      // 4

        // --- Closest enemy ---
        "enemy_bearing",            // 5
        "enemy_dist_norm",          // 6
        "enemy_heading",            // 7
        "enemy_rel_vel_fwd_norm",   // 8
        "enemy_rel_vel_right_norm", // 9

        // --- Closest asteroid ---
        "asteroid_bearing",         // 10
        "asteroid_dist_norm",       // 11

        // --- Closest hostile projectile ---
        "proj_bearing",             // 12
        "proj_dist_norm",           // 13
        "proj_closing_speed_norm"   // 14
    };

    private readonly RLCommanderAgent agent;
    private readonly Ship ship;
    private readonly Transform transform;
    
    // Cached parameters from the agent
    private readonly float sensingRange;
    private readonly float maxSpeed;
    private readonly float maxYawRate;
    private Collider[] overlapColliders;

    public RLObserver(RLCommanderAgent agent)
    {
        this.agent = agent;
        this.ship = agent.ship;
        this.transform = agent.transform;

        // Cache for performance
        this.sensingRange = agent.sensingRange;
        this.maxSpeed = agent.maxSpeed;
        this.maxYawRate = agent.maxYawRate;
        this.overlapColliders = agent.overlapColliders;
    }

    public void CollectObservations(VectorSensor sensor)
    {
        if (agent.IsPaused) return;

        var currentState = ship.CurrentState;
        var arenaInstance = agent.arenaInstance;

        // --- Self State --- (5 floats)
        sensor.AddObservation(currentState.Kinematics.Speed / maxSpeed);
        sensor.AddObservation(currentState.Kinematics.YawRate / maxYawRate);
        sensor.AddObservation(currentState.HealthPct);
        sensor.AddObservation(currentState.ShieldPct);

        float normDistToCenter = 0f;
        if (arenaInstance != null && arenaInstance.ArenaSize > 0f)
        {
            normDistToCenter = Vector3.Distance(transform.position, arenaInstance.CenterPosition) / arenaInstance.ArenaSize;
            normDistToCenter = Mathf.Clamp01(normDistToCenter);
        }
        sensor.AddObservation(normDistToCenter);

        // --- Environmental Awareness ---
        var closestEnemy      = FindClosestEnemy();
        var closestAsteroid   = FindClosestAsteroid();
        var closestProjectile = FindClosestProjectile();

        // Enemy: Bearing, Dist, Heading, RelVel (+5 floats)
        AddTargetObservations2D(sensor, closestEnemy);
        if (closestEnemy != null)
        {
            var enemyShip = closestEnemy.GetComponent<Ship>();
            if (enemyShip != null)
            {
                // Heading (+1 float)
                Vector2 toAgent2D   = GamePlane.WorldToPlane((transform.position - closestEnemy.position)).normalized;
                Vector2 enemyFwd2D  = enemyShip.CurrentState.Kinematics.Forward;
                float enemyHeading  = Vector2.Dot(enemyFwd2D, toAgent2D);
                sensor.AddObservation(enemyHeading);

                // Relative velocity in agent's local frame (+2 floats)
                Vector3 agentVel = this.ship.CurrentState.Kinematics.Vel;
                Vector3 enemyVel = enemyShip.CurrentState.Kinematics.Vel;
                Vector2 relVel2D = GamePlane.WorldToPlane(enemyVel - agentVel);

                Vector2 agentFwd = this.ship.CurrentState.Kinematics.Forward;
                Vector2 agentRight = new Vector2(agentFwd.y, -agentFwd.x);

                float localRelVelFwd = Vector2.Dot(relVel2D, agentFwd);
                float localRelVelRight = Vector2.Dot(relVel2D, agentRight);

                sensor.AddObservation(Mathf.Clamp(localRelVelFwd / maxSpeed, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(localRelVelRight / maxSpeed, -1f, 1f));
            }
            else
            {
                // Pad heading and rel vel
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }
        else
        {
            // Pad heading and rel vel
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Asteroid: Bearing, Dist (+2 floats)
        AddTargetObservations2D(sensor, closestAsteroid);

        // Projectile: Bearing, Dist, Closing Speed (+3 floats)
        AddTargetObservations2D(sensor, closestProjectile);
        if (closestProjectile != null)
        {
            Rigidbody projRb = closestProjectile.GetComponent<Rigidbody>();
            if (projRb != null)
            {
                Vector3 toShip   = transform.position - closestProjectile.position;
                Vector3 relVel   = projRb.linearVelocity;
                float   closing  = Vector3.Dot(relVel, toShip.normalized);
                const float MaxProjectileSpeed = 40f; // tune to your missile speed cap
                sensor.AddObservation(Mathf.Clamp(closing / MaxProjectileSpeed, -1f, 1f));
            }
            else
            {
                sensor.AddObservation(0f);
            }
        }
        else
        {
            sensor.AddObservation(0f); // no projectile
        }
    }

    private void AddTargetObservations2D(VectorSensor sensor, Transform target)
    {
        if (target != null)
        {
            // Project world positions to the 2D game plane
            Vector2 agentPos2D = this.ship.CurrentState.Kinematics.Pos;
            Vector2 targetPos2D = GamePlane.WorldToPlane(target.position);
            Vector2 toTarget2D = targetPos2D - agentPos2D;

            float distance = toTarget2D.magnitude;
            Vector2 toTargetDir = (distance > 0.001f) ? toTarget2D / distance : Vector2.zero;

            // Bearing: Angle between agent's forward and vector to target [-180, 180]
            Vector2 agentFwd = this.ship.CurrentState.Kinematics.Forward;
            float bearing = Vector2.SignedAngle(agentFwd, toTargetDir);

            // Normalize and add observations
            sensor.AddObservation(bearing / 180f);          // Bearing: [-1, 1]
            sensor.AddObservation(distance / sensingRange); // Distance: [0, ~1]
        }
        else
        {
            // Pad with zeros if no target found
            sensor.AddObservation(0f); // bearing
            sensor.AddObservation(0f); // distance
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConsiderCandidate(Transform candidate, Vector3 origin, ref Transform closest, ref float bestSqrDist)
    {
        if (candidate == null) return;
        float sqrDist = (candidate.position - origin).sqrMagnitude;
        if (sqrDist < bestSqrDist)
        {
            bestSqrDist = sqrDist;
            closest = candidate;
        }
    }

    private Transform FindClosestEnemy()
    {
        if (agent.arenaInstance == null) return null;

        float bestSqrDist = sensingRange * sensingRange;
        Transform closest = null;
        Vector3 origin = transform.position;
        
        foreach (var otherShip in agent.arenaInstance.ships)
        {
            if (otherShip == null || otherShip == ship || !otherShip.gameObject.activeInHierarchy || ship.IsFriendly(otherShip))
            {
                continue;
            }
            ConsiderCandidate(otherShip.transform, origin, ref closest, ref bestSqrDist);
        }
        return closest;
    }
    
    private Transform FindClosestAsteroid()
    {
        float bestSqrDist = sensingRange * sensingRange;
        Transform closest = null;
        Vector3 origin = transform.position;

        int numFound = Physics.OverlapSphereNonAlloc(origin, sensingRange, overlapColliders);

        for (int i = 0; i < numFound; i++)
        {
            Collider col = overlapColliders[i];
            if (col.CompareTag("Asteroid") || col.GetComponent<Asteroid>() != null)
            {
                ConsiderCandidate(col.transform, origin, ref closest, ref bestSqrDist);
            }
        }
        return closest;
    }
    
    private Transform FindClosestProjectile()
    {
        float bestSqrDist = sensingRange * sensingRange;
        Transform closest = null;
        Vector3 origin = transform.position;

        int projectileLayer = LayerMask.NameToLayer("Projectile");
        int layerMask = 1 << projectileLayer;

        int numFound = Physics.OverlapSphereNonAlloc(origin, sensingRange, overlapColliders, layerMask);

        for (int i = 0; i < numFound; i++)
        {
            Collider col = overlapColliders[i];
            if (!col.CompareTag("Missile")) continue;

            var proj = col.GetComponent<ProjectileBase>();
            if (proj != null && proj.Shooter != null && ship.IsFriendly(proj.Shooter.GetComponent<Ship>()))
            {
                continue; 
            }

            ConsiderCandidate(col.transform, origin, ref closest, ref bestSqrDist);
        }

        return closest;
    }
}   