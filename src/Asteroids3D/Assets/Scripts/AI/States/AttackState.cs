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
            if (context.Enemy == null) return;   
            gunner.TargetEnemy(context.Enemy);
            if(context.VectorToEnemy.magnitude < 6f || Vector3.Dot(context.EnemyRelVelocity, context.VectorToEnemy) < -1f){ // if enemy is close or we're closing fast enough, face it
                navigator.SetFacingTarget(context.VectorToEnemy);
            }
            else
            {
                navigator.ClearFacingOverride();
            }
            navigator.SetNavigationPoint(
                context.EnemyPos,
                true, // enable avoidance
                context.EnemyVel);

        }

        public override void Exit()
        {
            RLog.AI("[AttackState] Exiting");
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
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
            score += AIUtilityCurves.DesireCurve(ctx.EnemyLaserHeatPct, 0.2f);
            score += AIUtilityCurves.DesireCurve(ctx.EnemyMissileAmmo, 0.1f);

            // Increase if target is in good range
            float distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy > 6f && distToEnemy < 40f)
                score += 0.3f;
                
            if (ctx.LineOfSightToEnemy)
                score += 0.1f;
                
            // Bonus for having missiles
            score += AIUtilityCurves.FearCurve(ctx.LaserHeatPct, 0.1f);
            score += AIUtilityCurves.DesireCurve(ctx.MissileAmmo, 0.1f);
                
            // Decrease if severely outnumbered
            int netThreat = ctx.NearbyEnemyCount - ctx.NearbyFriendCount;
            if (netThreat > 2)
                score -= 0.3f;
            score/=2f;
            return Mathf.Max(0f, score);
        }
    }
} 