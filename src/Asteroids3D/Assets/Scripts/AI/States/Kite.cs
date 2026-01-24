using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Kite : State
    {
        public override StateType Type => StateType.Kite;
        
        public Kite(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning) { }

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            if (!ctx.Enemy) return;

            var predictedTarget = ctx.Targeting.PredictIntercept(
                ctx.EnemyPos,
                ctx.EnemyVel,
                ctx.LaserSpeed);
            
            gunner.SetTarget(predictedTarget);
            navigator.SetFacingTarget(predictedTarget - ctx.SelfPosition);

            var distToEnemy = ctx.VectorToEnemy.magnitude;
            Vector2 waypoint;

            if (distToEnemy < utilityTuning.kiteMinDistance)
            {
                // Too close - push away hard
                waypoint = ctx.Maneuvers.ComputeKitePoint(
                    ctx.EnemyPos,
                    ctx.EnemyVel,
                    utilityTuning.kiteMinDistance + utilityTuning.kitePushAwayDistance);
            }
            else if (distToEnemy > utilityTuning.kiteMaxDistance)
            {
                // Too far - close in a bit
                var toEnemy = (ctx.EnemyPos - ctx.SelfPosition).normalized;
                waypoint = ctx.EnemyPos - toEnemy * (utilityTuning.kiteDesiredDistance * utilityTuning.kiteReturnDistanceFactor);
            }
            else
            {
                // In sweet spot - maintain kite distance
                waypoint = ctx.Maneuvers.ComputeKitePoint(
                    ctx.EnemyPos,
                    ctx.EnemyVel,
                    utilityTuning.kiteDesiredDistance);
            }

            navigator.SetNavigationPoint(waypoint, avoid: true);
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(Info ctx)
        {
            if (!ctx.Enemy) return 0f;

            var dist = ctx.VectorToEnemy.magnitude;
            var enemyHealth = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            var netThreat = Mathf.Clamp01((ctx.NearbyEnemyCount - ctx.NearbyFriendCount) / 3f);
            var angleOffset = Mathf.Max(0f, ctx.SelfAngleToEnemy - utilityTuning.kiteAngleTolerance) / 150f;
            
            return new UtilityBuilder()
                .Factor("selfHealth", ctx.HealthPct, utilityTuning.evadeHealthFactor)
                .Factor("selfShield", ctx.ShieldPct, utilityTuning.evadeShieldFactor)
                .FactorBinary(ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1, "outnumbered", utilityTuning.evadeOutnumberedFactor)            
                .FactorIf(dist < utilityTuning.kiteMinDistance, "tooClose", utilityTuning.kiteTooCloseFactor)
                .FactorIf(ctx.HealthPct < utilityTuning.kiteLowHealthThreshold && ctx.ShieldPct > utilityTuning.kiteHighShieldThreshold, 
                    "lowHealthHighShield", utilityTuning.kiteLowHealthHighShieldFactor)
                .Factor("angle", angleOffset, utilityTuning.kiteAngleFactor.Inverted)
                .Build();
        }
    }
}
