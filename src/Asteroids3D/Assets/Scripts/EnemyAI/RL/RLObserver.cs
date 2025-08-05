using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Utils;
using Weapons;
using ShipMain;

namespace EnemyAI.RL
{
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
            "self_vel_fwd_norm",        // 0
            "self_vel_right_norm",      // 1
            "self_yawrate_norm",        // 2
            "self_health_pct",          // 3
            "self_shield_pct",          // 4
            "self_laser_heat_pct",      // 5
            "self_missile_ammo_pct",    // 6

            // --- Missile system status (one-hot) ---
            "missile_idle",             // 7
            "missile_locking",          // 8
            "missile_locked",           // 9
            "missile_cooldown",         // 10

            "dist_to_center_norm",      // 11

            // --- Closest enemy ---
            "enemy_bearing",            // 12
            "enemy_dist_norm",          // 13
            "enemy_heading",            // 14
            "enemy_rel_vel_fwd_norm",   // 15
            "enemy_rel_vel_right_norm", // 16
            "enemy_health_pct",         // 17
            "enemy_shield_pct",         // 18

            // --- Closest asteroid ---
            "asteroid_bearing",         // 19
            "asteroid_dist_norm",       // 20

            // --- Closest hostile projectile ---
            "proj_bearing",             // 21
            "proj_dist_norm",           // 22
            "proj_closing_speed_norm"   // 23
        };

        /// <summary>
        /// A list that gets populated with the latest observation values each time
        /// <see cref="CollectObservations"/> is called. Used by debug tools.
        /// </summary>
        public readonly List<float> LastObservations;

        private readonly RLCommanderAgent agent;
        private readonly Ship ship;
        private readonly Transform transform;
    
        // Cached parameters from the agent
        private readonly float sensingRange;
        private readonly float maxSpeed;
        private readonly float maxYawRate;
        private Collider[] overlapColliders;

        // Cached reference to the closest enemy found in the latest observation pass.
        public Transform ClosestEnemy { get; private set; }

        // Latest enemy bearing in degrees [-180,180]
        public float EnemyBearingDeg { get; private set; }

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
            this.LastObservations = new List<float>(ObservationLabels.Length);
        }

        /// <summary>
        /// Helper method to add an observation to the sensor while also caching it
        /// in <see cref="LastObservations"/> for debugging purposes.
        /// </summary>
        private void AddObservation(VectorSensor sensor, float value)
        {
            sensor.AddObservation(value);
            LastObservations.Add(value);
        }

        public void CollectObservations(VectorSensor sensor)
        {
            // When the episode is not active, the agent's actions are ignored, but the sensor still expects a full
            // vector of observations. We don't want to collect new (potentially invalid) data,
            // so we'll just feed it the last known values and prevent the debug list from clearing.
            bool isEpisodeActive = agent.gameContext?.IsActive ?? false;
            if (!isEpisodeActive)
            {
                // If we have previous observations, send them again.
                if (LastObservations.Count == ObservationLabels.Length)
                {
                    foreach (var obs in LastObservations) { sensor.AddObservation(obs); }
                }
                else // Otherwise, send zeros as a fallback.
                {
                    for (int i = 0; i < ObservationLabels.Length; i++) { sensor.AddObservation(0f); }
                }
                return;
            }

            LastObservations.Clear();
        
            var currentState = ship.CurrentState;
            var gameContext = agent.gameContext;

            // --- Self State --- (10 floats)

            // Deconstruct velocity into local forward and strafe components
            Vector2 agentVel2D = currentState.Kinematics.Vel;
            Vector2 agentFwd2D = currentState.Kinematics.Forward;
            Vector2 agentRight2D = new Vector2(agentFwd2D.y, -agentFwd2D.x);

            float localVelFwd = Vector2.Dot(agentVel2D, agentFwd2D);
            float localVelRight = Vector2.Dot(agentVel2D, agentRight2D);

            AddObservation(sensor, Mathf.Clamp(localVelFwd / maxSpeed, -1f, 1f));
            AddObservation(sensor, Mathf.Clamp(localVelRight / maxSpeed, -1f, 1f));

            AddObservation(sensor, currentState.Kinematics.YawRate / maxYawRate);
            AddObservation(sensor, currentState.HealthPct);
            AddObservation(sensor, currentState.ShieldPct);
            AddObservation(sensor, currentState.LaserHeatPct);

            float missileAmmoPct = 0f;
            if (ship.MissileLauncher != null && ship.MissileLauncher.MaxAmmo > 0)
            {
                missileAmmoPct = (float)currentState.MissileAmmo / ship.MissileLauncher.MaxAmmo;
            }
            AddObservation(sensor, missileAmmoPct);

            // One-hot encode missile launcher state
            var mState = currentState.MissileState;
            AddObservation(sensor, mState == MissileLauncher.LockState.Idle      ? 1f : 0f);
            AddObservation(sensor, mState == MissileLauncher.LockState.Locking   ? 1f : 0f);
            AddObservation(sensor, mState == MissileLauncher.LockState.Locked    ? 1f : 0f);
            AddObservation(sensor, mState == MissileLauncher.LockState.Cooldown  ? 1f : 0f);

            float normDistToCenter = 0f;
            if (gameContext != null && gameContext.AreaSize > 0f)
            {
                normDistToCenter = Vector3.Distance(transform.position, gameContext.CenterPosition) / gameContext.AreaSize;
                normDistToCenter = Mathf.Clamp01(normDistToCenter);
            }
            AddObservation(sensor, normDistToCenter);

            // --- Environmental Awareness ---
            var closestEnemy      = FindClosestEnemy();
            ClosestEnemy = closestEnemy; // Cache for external systems (e.g., shaping rewards)
            var closestAsteroid   = FindClosestAsteroid();
            var closestProjectile = FindClosestProjectile();

            // Enemy: Bearing, Dist, Heading, RelVel (+5 floats)
            float enemyBearingDeg = AddTargetObservations2D(sensor, closestEnemy);
            EnemyBearingDeg = enemyBearingDeg; // expose raw bearing for external use
            if (closestEnemy != null)
            {
                var enemyShip = closestEnemy.GetComponent<Ship>();
                if (enemyShip != null)
                {
                    // Heading (+1 float)
                    Vector2 toAgent2D   = (transform.position - closestEnemy.position).normalized;
                    Vector2 enemyFwd2D  = enemyShip.CurrentState.Kinematics.Forward;
                    float enemyHeading  = Vector2.Dot(enemyFwd2D, toAgent2D);
                    AddObservation(sensor, enemyHeading);

                    // Relative velocity in agent's local frame (+2 floats)
                    Vector2 agentVel = this.ship.CurrentState.Kinematics.Vel;
                    Vector2 enemyVel = enemyShip.CurrentState.Kinematics.Vel;
                    Vector2 relVel2D = enemyVel - agentVel;

                    Vector2 agentFwd = this.ship.CurrentState.Kinematics.Forward;
                    Vector2 agentRight = new Vector2(agentFwd.y, -agentFwd.x);

                    float localRelVelFwd = Vector2.Dot(relVel2D, agentFwd);
                    float localRelVelRight = Vector2.Dot(relVel2D, agentRight);

                    AddObservation(sensor, Mathf.Clamp(localRelVelFwd / maxSpeed, -1f, 1f));
                    AddObservation(sensor, Mathf.Clamp(localRelVelRight / maxSpeed, -1f, 1f));

                    // Enemy Health and Shield (+2 floats)
                    AddObservation(sensor, enemyShip.CurrentState.HealthPct);
                    AddObservation(sensor, enemyShip.CurrentState.ShieldPct);
                }
                else
                {
                    // Pad heading and rel vel
                    AddObservation(sensor, 0f);
                    AddObservation(sensor, 0f);
                    AddObservation(sensor, 0f);
                    // Pad enemy health and shield
                    AddObservation(sensor, 0f);
                    AddObservation(sensor, 0f);
                }
            }
            else
            {
                // Pad heading and rel vel
                AddObservation(sensor, 0f);
                AddObservation(sensor, 0f);
                AddObservation(sensor, 0f);
                // Pad enemy health and shield
                AddObservation(sensor, 0f);
                AddObservation(sensor, 0f);
            }

            // Asteroid: Bearing, Dist (+2 floats)
            _ = AddTargetObservations2D(sensor, closestAsteroid);

            // Projectile: Bearing, Dist, Closing Speed (+3 floats)
            _ = AddTargetObservations2D(sensor, closestProjectile);
            if (closestProjectile != null)
            {
                Rigidbody projRb = closestProjectile.GetComponent<Rigidbody>();
                if (projRb != null)
                {
                    Vector3 toShip   = transform.position - closestProjectile.position;
                    Vector3 relVel   = projRb.linearVelocity;
                    float   closing  = Vector3.Dot(relVel, toShip.normalized);
                    const float MaxProjectileSpeed = 40f; // tune to your missile speed cap
                    AddObservation(sensor, Mathf.Clamp(closing / MaxProjectileSpeed, -1f, 1f));
                }
                else
                {
                    AddObservation(sensor, 0f);
                }
            }
            else
            {
                AddObservation(sensor, 0f); // no projectile
            }
        }

        /// <summary>
        /// Adds bearing and distance observations for a target and returns the raw bearing in degrees.
        /// </summary>
        /// <param name="sensor">VectorSensor to write observations to</param>
        /// <param name="target">Target transform or null</param>
        /// <returns>Bearing in degrees [-180,180]. Returns 0 if target is null.</returns>
        private float AddTargetObservations2D(VectorSensor sensor, Transform target)
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
                float bearingDeg = Vector2.SignedAngle(agentFwd, toTargetDir);

                // Normalize and add observations
                AddObservation(sensor, bearingDeg / 180f);          // Bearing: [-1, 1]
                AddObservation(sensor, distance / sensingRange);    // Distance: [0, ~1]

                return bearingDeg;
            }
            else
            {
                // Pad with zeros if no target found
                AddObservation(sensor, 0f); // bearing
                AddObservation(sensor, 0f); // distance
                return 0f;
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
            if (agent.gameContext == null) return null;

            float bestSqrDist = sensingRange * sensingRange;
            Transform closest = null;
            Vector3 origin = transform.position;
        
            foreach (var otherShip in agent.gameContext.ActiveShips)
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
            int numFound = Physics.OverlapSphereNonAlloc(origin, sensingRange, overlapColliders, LayerIds.Mask(LayerIds.Asteroid));

            for (int i = 0; i < numFound; i++)
            {
                Collider col = overlapColliders[i];
                ConsiderCandidate(col.transform, origin, ref closest, ref bestSqrDist);
            }
            return closest;
        }
    
        private Transform FindClosestProjectile()
        {
            float bestSqrDist = sensingRange * sensingRange;
            Transform closest = null;
            Vector3 origin = transform.position;

            int projectileLayer = LayerIds.Projectile;
            int layerMask = 1 << projectileLayer;

            int numFound = Physics.OverlapSphereNonAlloc(origin, sensingRange, overlapColliders, layerMask);

            for (int i = 0; i < numFound; i++)
            {
                Collider col = overlapColliders[i];
                if (!col.CompareTag(TagNames.Missile)) continue;

                var proj = col.GetComponent<ProjectileBase>();
                if (proj != null && proj.Shooter != null && ship.IsFriendly(proj.Shooter.gameObject.GetComponent<Ship>()))
                {
                    continue; 
                }

                ConsiderCandidate(col.transform, origin, ref closest, ref bestSqrDist);
            }

            return closest;
        }
    }
}   