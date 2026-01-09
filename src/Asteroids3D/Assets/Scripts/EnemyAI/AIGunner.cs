using System;
using Editor;
using Game;
using Ships;
using UnityEngine;
using Utils;
using Weapons;

namespace EnemyAI
{
    public partial class AIGunner : MonoBehaviour
    {
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

        private Ships.Ship ship;
        public Vector2 Target { get; set; }       

        private IWeaponAIStrategy primaryAI;
        private IWeaponAIStrategy secondaryAI;

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
            Target = target ? GamePlane.WorldPointToPlane(target.position) : Vector2.zero;
        }

        public void TargetEnemy(Ships.Ship enemy)
        {
            Target = enemy ? GamePlane.WorldPointToPlane(enemy.transform.position) : Vector2.zero;
        }

        public void Initialize(Ships.Ship ship)
        {
            this.ship = ship;
            lineOfSightMask = LayerIds.Mask(LayerIds.Asteroid);

            if (ship.Weapons.Primary)
                primaryAI = ship.Weapons.Primary.GetComponent<IWeaponAIStrategy>();
            if (ship.Weapons.Secondary)
                secondaryAI = ship.Weapons.Secondary.GetComponent<IWeaponAIStrategy>();
        }

        public void GenerateGunnerCommands(State state, ref Command cmd)
        {

            if (Target == Vector2.zero) return;
            

            // 1. Create the shared context for all weapons
            var context = new IWeaponAIStrategy.TargetingContext
            {
                TargetPosition = Target,
                DistanceToTarget = VectorToTarget.magnitude,
                AngleToTarget = AngleToTarget,
                HasLineOfSight = HasLineOfSight()
            };

            // 2. Poll the strategies
            bool primaryWantsFire = primaryAI?.ShouldFire(context) ?? false;
            bool secondaryWantsFire = secondaryAI?.ShouldFire(context) ?? false;

            cmd.PrimaryFire = primaryWantsFire;
            cmd.SecondaryFire = secondaryWantsFire;
        }

        public bool HasLineOfSight(Vector3 firePos, Vector3 dir, float dist, float angle, Vector3 targetPos)
        {
            int f = Time.frameCount;
            bool need = (losFrame < 0 || f - losFrame >= lineOfSightCacheFrames)
                        || Vector3.Distance(firePos, lastRayPos) > 1f
                        || Vector3.Distance(targetPos, lastTgtPos) > 1f;

            if (angle > angleToleranceBeforeRay)
            {
                return false;
            }

            if (need)
            {
                cachedLOS = LineOfSight.IsClear(
                    firePos,
                    targetPos,
                    lineOfSightMask);
                losFrame = f;
                lastRayPos = firePos;
                lastTgtPos = targetPos;
            }
            return cachedLOS;
        }

        /// <summary>Returns true if an unobstructed line of sight exists to the current target.</summary>
        public bool HasLineOfSight()
        {
            if (!ship.Weapons.Primary || Target == Vector2.zero) return false;

            var firePos = ship.Weapons.Primary.firePoint ? ship.Weapons.Primary.firePoint.position : transform.position;
            var targetPos = GamePlane.PlanePointToWorld(Target);
            var dir = targetPos - firePos;
            float dist = dir.magnitude;
            float angle = Vector3.Angle(transform.up, dir);

            return HasLineOfSight(firePos, dir, dist, angle, targetPos);
        }

        /// <summary>Returns true if an unobstructed line of sight exists to <paramref name="tgt"/>.</summary>
        public bool HasLineOfSight(Transform tgt)
        {
            if (!ship.Weapons.Primary || !tgt) return false;

            var firePos = ship.Weapons.Primary.firePoint ? ship.Weapons.Primary.firePoint.position : transform.position;
            var dir = tgt.position - firePos;
            float dist = dir.magnitude;
            float angle = Vector3.Angle(transform.up, dir);

            return HasLineOfSight(firePos, dir, dist, angle, tgt.position);
        }
    
        /// <summary>
        /// Gets angle to a target vector in degrees
        /// </summary>
        private float GetAngleTo(Vector2 targetVector)
        {
            return targetVector.sqrMagnitude < 0.01f ? 0f : Vector2.Angle(ship?.CurrentState.Kinematics.Forward ?? Vector2.up, targetVector);
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
    }
} 
