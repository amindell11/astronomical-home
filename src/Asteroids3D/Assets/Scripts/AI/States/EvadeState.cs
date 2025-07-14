using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Evade state - ship retreats from threats.
    /// High utility when health/shield is low, enemies are nearby, or missiles are incoming.
    /// </summary>
    public class EvadeState : AIState
    {
        private Vector3 evadePoint;
        private bool hasEvadePoint = false;
        private float evadePointSetTime;
        
        // Configuration
        private const float DefaultFleeDistance = 30f;
        private const float EvadePointRefreshTime = 2f; // Recalculate evade point periodically

        public EvadeState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            
            gunner.SetTarget(null);

            // Calculate initial evade point
            CalculateEvadePoint(ctx);
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            // Recalculate evade point periodically or if we don't have one
            if (!hasEvadePoint || (Time.time - evadePointSetTime) > EvadePointRefreshTime)
            {
                CalculateEvadePoint(ctx);
            }
            
            // Keep navigating to the evade point
            if (hasEvadePoint)
            {
                navigator.SetNavigationPointWorld(evadePoint, true);
            }
        }

        public override void Exit()
        {
            base.Exit();
            hasEvadePoint = false;
        }

        public override float ComputeUtility(AIContext ctx)
        {
            // Base score: always a baseline desire to evade if threatened
            float score = 0.3f;

            // Strong incentive to evade if shields/health are low, using "fear" curves.
            score += AIUtilityCurves.FearCurve(ctx.HealthPct, 0.4f);
            score += AIUtilityCurves.FearCurve(ctx.ShieldPct, 0.3f);

            // Increase score if outnumbered
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1)
                score += 0.2f;

            // Increase score if an enemy has a clear shot
            if (ctx.Enemy != null && ctx.LineOfSightToEnemy)
                score += 0.2f;

            // Increase score as laser heat increases, using a curve
            score += AIUtilityCurves.DesireCurve(ctx.LaserHeatPct, 0.1f);
            if (ctx.MissileAmmo == 0 && ctx.EnemyMissileAmmo > 0)
                score += 0.1f;

            // Major bonus if incoming missile detected
            if (ctx.IncomingMissile)
                score += 0.5f;
                
            // Decrease if very close to target (might need to fight through)
            if (ctx.Enemy != null)
            {
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                if (distToEnemy < 7f && ctx.LineOfSightToEnemy)
                    score -= 0.2f;
            }
                
            return Mathf.Max(0f, score);
        }

        private void CalculateEvadePoint(AIContext ctx)
        {
            Vector3 selfPos = ctx.SelfPosition3D;
            Vector3 fleeDirection = Vector3.zero;
            int threatCount = 0;
            
            // Primary threat: the current enemy
            if (ctx.Enemy != null && ctx.Enemy.gameObject.activeInHierarchy)
            {
                Vector3 enemyPos = ctx.Enemy.transform.position;
                Vector3 dirFromEnemy = (selfPos - enemyPos).normalized;
                fleeDirection += dirFromEnemy;
                threatCount++;
            }
            
            // If we have information about nearest threat distance, use it
            if (ctx.NearestThreatDistance < float.MaxValue && ctx.NearbyEnemyCount > 0)
            {
                // We don't have exact positions of all threats, but we can add some randomness
                // to avoid always fleeing in the same direction
                Vector3 randomOffset = GamePlane.ProjectOntoPlane(Random.insideUnitSphere).normalized * 0.3f;
                fleeDirection += randomOffset;
            }
            
            // If no specific threats, pick a random direction
            if (threatCount == 0 || fleeDirection.sqrMagnitude < 0.01f)
            {
                fleeDirection = GamePlane.ProjectOntoPlane(Random.insideUnitSphere).normalized;
            }
            else
            {
                fleeDirection = fleeDirection.normalized;
            }
            
            // Calculate flee distance based on navigator's arrive radius or default
            float fleeDistance = navigator != null ? 
                Mathf.Max(navigator.arriveRadius * 3f, DefaultFleeDistance) : 
                DefaultFleeDistance;
            
            // Set the evade point
            evadePoint = selfPos + fleeDirection * fleeDistance;
            hasEvadePoint = true;
            evadePointSetTime = Time.time;
            
            // Navigate to the evade point with avoidance enabled
            navigator.SetNavigationPointWorld(evadePoint, true);
        }
    }
} 