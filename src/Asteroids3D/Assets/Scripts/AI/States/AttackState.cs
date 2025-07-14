using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Attack state - ship actively engages enemy targets.
    /// High utility when health is good, weapons are ready, and target is accessible.
    /// </summary>
    public class AttackState : AIState
    {
        private Transform lastTarget;
        
        // Configuration
        private const float TargetUpdateInterval = 0.5f; // How often to update navigation target
        private float lastTargetUpdate;

        public AttackState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext context)
        {
            RLog.AI($"[AttackState] Entering. Target: {context.Enemy?.name ?? "None"}");
        }

        public override void Tick(AIContext context, float deltaTime)
        {
            // Continuously update nav point to track the enemy
            if (context.Enemy != null)
            {
                gunner.TargetEnemy(context.Enemy);
                navigator.SetNavigationPoint(
                    context.EnemyPos,
                    true, // enable avoidance
                    context.EnemyVel);
            }
            else
            {
                // If enemy is lost, clear nav point
                navigator.ClearNavigationPoint();
            }
        }

        public override void Exit()
        {
            RLog.AI("[AttackState] Exiting");
            // Clear nav point but let gunner keep target for a bit
        }

        public override float ComputeUtility(AIContext ctx)
        {
            // Do not attack if no enemy
            if (ctx.Enemy == null)
                return 0f;

            // Base score: willingness to fight
            float score = 0.5f;

            // Bonus for having high health/shields
            score += AIUtilityCurves.DesireCurve(ctx.HealthPct, 0.2f);
            score += AIUtilityCurves.DesireCurve(ctx.ShieldPct, 0.2f);
                
            // Bonus for attacking a weak target. This is a "fear" curve for the enemy.
            float enemyHealthFactor = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            score += AIUtilityCurves.FearCurve(enemyHealthFactor, 0.3f);
            
            // Bonus for attacking a disarmed target
            if (ctx.EnemyLaserHeatPct > 0.9f)
                score += 0.2f;

            if (ctx.EnemyMissileAmmo == 0)
                score += 0.1f;

            // Increase if target is in good range
            float distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy > 6f && distToEnemy < 40f)
                score += 0.3f;
                
            // Major bonus if line of sight to target
            if (ctx.LineOfSightToEnemy)
                score += 0.4f;
                
            // Bonus if weapons are ready
            if (ctx.LaserHeatPct < 0.7f)
                score += 0.1f;
                
            if (ctx.MissileAmmo > 0)
                score += 0.1f;
                
            // Decrease if severely outnumbered
            int netThreat = ctx.NearbyEnemyCount - ctx.NearbyFriendCount;
            if (netThreat > 2)
                score -= 0.3f;
                
            return Mathf.Max(0f, score);
        }
    }
} 