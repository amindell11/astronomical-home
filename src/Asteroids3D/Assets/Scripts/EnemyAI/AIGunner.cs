using Editor;
using ShipMain;
using UnityEngine;
using Utils;
using Weapons;

namespace EnemyAI
{
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

        [Header("Debug Gizmos")]
        [SerializeField] bool showGizmos = true;
        [SerializeField] bool showRanges = true;
        [SerializeField] bool showTargeting = true;
        [SerializeField] bool showLineOfSight = true;

        /* ── internals ───────────────────────────────────────────── */
        private Ship ship;
        public Vector2 Target { get; set; }       

        // LOS cache
        bool cachedLOS;
        int losFrame = -1;
        Vector3 lastRayPos, lastTgtPos;

        // ===== Context Properties - Source of Truth for Target Info =====
    
        /// <summary>
        /// Vector from ship to the gunner's current target
        /// </summary>
        public Vector2 VectorToTarget => Target != Vector2.zero ? Target - ship.CurrentState.Kinematics.Pos : Vector2.zero;
    
        /// <summary>
        /// Angle to the gunner's target in degrees
        /// </summary>
        public float AngleToTarget => GetAngleTo(VectorToTarget);

        public void SetTarget(Vector2 target)
        {
            Target = target;
        }

        public void SetTarget(Transform target)
        {
            Target = target ? GamePlane.WorldToPlane(target.position) : Vector2.zero;
        }

        public void TargetEnemy(Ship enemy)
        {
            Target = enemy ? GamePlane.WorldToPlane(enemy.transform.position) : Vector2.zero;
        }

        public void Initialize(Ship ship)
        {
            this.ship = ship;
            lineOfSightMask = LayerIds.Mask(LayerIds.Asteroid);
        }

        public void GenerateGunnerCommands(State state, ref Command cmd)
        {
            cmd.PrimaryFire = false;
            cmd.SecondaryFire = false;

            if (Target == Vector2.zero)
            {
                RLog.AI($"[AI-{name}] GenerateGunnerCommands: No target set, weapons disabled");
                return;
            }

            float dist = VectorToTarget.magnitude;
            float angle = AngleToTarget;
        
            RLog.AI($"[AI-{name}] GenerateGunnerCommands: Target at dist={dist:F1}, angle={angle:F1}°, fireDistance={fireDistance:F1}, fireAngleTolerance={fireAngleTolerance:F1}°");

            bool wantsToFireMissile = false;
            const float dummyMissileRange = 10f; // Close range for dumb-fire during locking

            if (ship.MissileLauncher)
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
            LaserGun laserGun = ship.LaserGun;
            // Only block laser when we have a locked missile ready to fire
            bool blockLaserForMissile = wantsToFireMissile && state.MissileState == MissileLauncher.LockState.Locked;

            if (laserGun && dist <= fireDistance && angle <= fireAngleTolerance && !blockLaserForMissile && laserGun.CurrentHeat < laserGun.MaxHeat - laserGun.HeatPerShot) // TODO: make this a tunable
            {
                Vector3 laserFirePos = laserGun.firePoint ? laserGun.firePoint.position : transform.position;
                Vector3 targetPos = GamePlane.PlaneToWorld(Target);
                Vector3 dir = targetPos - laserFirePos;
                bool losOK = HasLineOfSight(laserFirePos, dir, dist, angle, targetPos);
            
                RLog.AI($"[AI-{name}] Laser check: gun={ship.LaserGun != null}, inRange={dist <= fireDistance}, inAngle={angle <= fireAngleTolerance}, noMissile={!blockLaserForMissile}, LOS={losOK}");

                if (losOK)
                {
                    cmd.PrimaryFire = true;
                    RLog.AI($"[AI-{name}] FIRING LASER!");
                }
            }
            else
            {
                RLog.AI($"[AI-{name}] Laser conditions failed: gun={ship.LaserGun != null}, dist={dist:F1}<={fireDistance:F1}={dist <= fireDistance}, angle={angle:F1}<={fireAngleTolerance:F1}={angle <= fireAngleTolerance}, blockLaser={blockLaserForMissile}");
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

        /// <summary>Returns true if an unobstructed line of sight exists to the current target.</summary>
        public bool HasLineOfSight()
        {
            if (!ship.LaserGun || Target == Vector2.zero) return false;

            Vector3 firePos = ship.LaserGun.firePoint ? ship.LaserGun.firePoint.position : transform.position;
            Vector3 targetPos = GamePlane.PlaneToWorld(Target);
            Vector3 dir = targetPos - firePos;
            float dist = dir.magnitude;
            float angle = Vector3.Angle(transform.up, dir);

            return HasLineOfSight(firePos, dir, dist, angle, targetPos);
        }

        /// <summary>Returns true if an unobstructed line of sight exists to <paramref name="tgt"/>.</summary>
        public bool HasLineOfSight(Transform tgt)
        {
            if (!ship.LaserGun || !tgt) return false;

            Vector3 firePos = ship.LaserGun.firePoint ? ship.LaserGun.firePoint.position : transform.position;
            Vector3 dir = tgt.position - firePos;
            float dist = dir.magnitude;
            float angle = Vector3.Angle(transform.up, dir);

            return HasLineOfSight(firePos, dir, dist, angle, tgt.position);
        }
    
        /// <summary>
        /// Gets angle to a target vector in degrees
        /// </summary>
        private float GetAngleTo(Vector2 targetVector)
        {
            if (targetVector.sqrMagnitude < 0.01f) return 0f;
            return Vector2.Angle(ship?.CurrentState.Kinematics.Forward ?? Vector2.up, targetVector);
        }

        /// <summary>
        /// Returns true if the current target is within optimal laser firing range
        /// </summary>
        /// <param name="minRange">Minimum effective range</param>
        /// <param name="maxRange">Maximum effective range</param>
        /// <returns>True if target is in optimal range</returns>
        public bool IsInOptimalLaserRange(float minRange, float maxRange)
        {
            if (Target == Vector2.zero) return false;
        
            float distance = VectorToTarget.magnitude;
            return distance >= minRange && distance <= maxRange;
        }
    
        // ==================== Helper Utilities =============================
        public Vector2 PredictIntercept(Vector2 shooterPos, Vector2 shooterVel, Vector2 targetPos, Vector2 targetVel, float projSpeed)
        {
            // Restrict shooter velocity to its forward component so lateral drift does not skew the intercept calculation.
            Vector2 forward = ship ? ship.CurrentState.Kinematics.Forward : (shooterVel.sqrMagnitude > 0f ? shooterVel.normalized : Vector2.up);
            Vector2 forwardVel = Vector2.Dot(shooterVel, forward) * forward;

            Vector2 relPos = targetPos - shooterPos;
            Vector2 relVel = targetVel - forwardVel;

            float a = Vector2.Dot(relVel, relVel) - projSpeed * projSpeed;
            float b = 2f * Vector2.Dot(relVel, relPos);
            float c = Vector2.Dot(relPos, relPos);

            float t;
            const float eps = 0.0001f;
            if (Mathf.Abs(a) < eps)
            {
                // Linear solution
                t = (Mathf.Abs(b) < eps) ? 0f : -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                {
                    t = 0f; // No solution, fallback to current position
                }
                else
                {
                    float sqrtDisc = Mathf.Sqrt(disc);
                    float t1 = (-b + sqrtDisc) / (2f * a);
                    float t2 = (-b - sqrtDisc) / (2f * a);
                    t = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
                    if (t < 0f) t = 0f;
                }
            }
            return targetPos + targetVel * t;   // Return the predicted intercept point
        }

        /// <summary>
        /// Returns true if the current target is within optimal laser firing range using default parameters
        /// </summary>
        public bool IsInOptimalLaserRange()
        {
            return IsInOptimalLaserRange(3f, fireDistance);
        }

        #region Debug Gizmos
    
        void OnDrawGizmos()
        {
            if (!showGizmos || !ship) return;
        
            if (showRanges)
            {
                DrawRangeGizmos();
            }
        }
    
        void OnDrawGizmosSelected()
        {
            if (!showGizmos || !ship) return;
        
            if (showTargeting)
            {
                DrawTargetingGizmos();
            }
        
            if (showLineOfSight)
            {
                DrawLineOfSightGizmos();
            }
        }
    
        void DrawRangeGizmos()
        {
            Vector3 pos = transform.position;
        
            // Laser firing range
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, fireDistance);
        
            // Missile range
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, missileRange);
        
            // Optimal laser range (minimum)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(pos, 3f);
        }
    
        void DrawTargetingGizmos()
        {
            if (Target == Vector2.zero) return;
        
            Vector3 pos = transform.position;
            Vector3 targetPos = GamePlane.PlaneToWorld(Target);
            Vector3 forward = ship?.CurrentState.Kinematics.Forward ?? Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);
        
            // Line to target
            float distance = Vector3.Distance(pos, targetPos);
            bool inLaserRange = distance <= fireDistance;
            bool inMissileRange = distance <= missileRange;
        
            if (inLaserRange)
                Gizmos.color = Color.red;
            else if (inMissileRange)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.gray;
            
            Gizmos.DrawLine(pos, targetPos);
        
            // Target marker
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 2f);
        
            // Fire angle tolerance cone for laser
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            DrawAngleCone(pos, forward, fireAngleTolerance, fireDistance);
        
            // Fire angle tolerance cone for missile
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            DrawAngleCone(pos, forward, missileAngleTolerance, missileRange);
        
            // Angle to target display
            float angleToTarget = AngleToTarget;
            bool laserCanFire = angleToTarget <= fireAngleTolerance && inLaserRange;
            bool missileCanFire = angleToTarget <= missileAngleTolerance && inMissileRange;
        
            // Draw angle indicator
            Gizmos.color = laserCanFire ? Color.green : (missileCanFire ? Color.yellow : Color.red);
            Vector3 dirToTarget = (targetPos - pos).normalized;
            Gizmos.DrawRay(pos, dirToTarget * 5f);
        }
    
        void DrawLineOfSightGizmos()
        {
            if (Target == Vector2.zero || !ship.LaserGun) return;
        
            Vector3 firePos = ship.LaserGun.firePoint ? ship.LaserGun.firePoint.position : transform.position;
            Vector3 targetPos = GamePlane.PlaneToWorld(Target);
        
            // Line of sight ray
            bool hasLOS = HasLineOfSight();
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(firePos, targetPos);
        
            // Fire point marker
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePos, 0.5f);
        
            // Angle tolerance before raycast visualization
            Vector3 forward = ship?.CurrentState.Kinematics.Forward ?? Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);
        
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            DrawAngleCone(firePos, forward, angleToleranceBeforeRay, fireDistance);
        }
    
        void DrawAngleCone(Vector3 origin, Vector3 forward, float angleInDegrees, float range)
        {
            float halfAngle = angleInDegrees * 0.5f;
        
            // Convert to 3D space (assuming 2D game on XY plane)
            Vector3 forward3D = forward.normalized;
        
            // Create left and right boundaries of the cone
            Quaternion leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.forward);
            Quaternion rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.forward);
        
            Vector3 leftDirection = leftRotation * forward3D;
            Vector3 rightDirection = rightRotation * forward3D;
        
            // Draw cone edges
            Gizmos.DrawRay(origin, leftDirection * range);
            Gizmos.DrawRay(origin, rightDirection * range);
        
            // Draw arc at the end
            int segments = Mathf.Max(3, Mathf.RoundToInt(angleInDegrees / 5f));
            Vector3 prevPoint = origin + leftDirection * range;
        
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
                Vector3 direction = rotation * forward3D;
                Vector3 point = origin + direction * range;
            
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    
        #endregion
    }
} 