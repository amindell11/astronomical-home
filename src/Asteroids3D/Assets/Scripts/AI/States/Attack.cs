using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Attack : State
    {
        public override StateType Type => StateType.Attack;

        private Transform lastTarget;
        private float lastTargetUpdate;
        
        public Attack(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override void Enter(Info context)
        {
        }

        public override void Tick(Info context, float deltaTime)
        {
            if (!context.Enemy) return;   
            
            var predictedTarget = context.Targeting.PredictIntercept(
                context.EnemyPos,
                context.EnemyVel,
                context.LaserSpeed
            );
            
            gunner.SetTarget(predictedTarget);
            
            var vectorToPredictedTarget = predictedTarget - context.SelfPosition;
            
            if(context.VectorToEnemy.magnitude < utilityTuning.attackFacingDistance || Vector3.Dot(context.EnemyRelVelocity, context.VectorToEnemy) < utilityTuning.attackFacingSpeed){
                navigator.SetFacingTarget(vectorToPredictedTarget);
            }
            else
            {
                navigator.ClearFacingOverride();
            }
            navigator.SetNavigationPoint(
                context.EnemyPos,
                true,
                context.EnemyVel);

        }

        public override void Exit()
        {
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(Info ctx)
        {
            if (!ctx.Enemy)
                return 0f;

            var dist = ctx.VectorToEnemy.magnitude;
            var enemyHealth = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            var netThreat = Mathf.Clamp01((ctx.NearbyEnemyCount - ctx.NearbyFriendCount) / 3f);
            var inRange = dist >= utilityTuning.attackUtilityOptimalRangeMin && dist <= utilityTuning.attackUtilityOptimalRangeMax;
            var rangeScore = inRange ? 1f : Mathf.Clamp01(1f - Mathf.Abs(dist - 20f) / 30f);

            return new UtilityBuilder()
                .Factor("selfHealth", ctx.HealthPct, utilityTuning.attackHealthFactor)
                .Factor("selfShield", ctx.ShieldPct, utilityTuning.attackShieldFactor)
                .Factor("enemyWeak", enemyHealth, utilityTuning.attackEnemyWeakFactor)
                .Factor("range", rangeScore, utilityTuning.attackRangeFactor)
                .FactorBinary(ctx.LineOfSightToEnemy, "LOS", utilityTuning.attackLOSFactor)
                .Factor("threat", netThreat, utilityTuning.attackThreatFactor)
                .FactorIf(dist > utilityTuning.attackOuterDistanceThreshold, "outerRange", utilityTuning.attackOuterRangeFactor)
                .Factor("desperation", ctx.HealthPct, utilityTuning.attackDesperationFactor)
                .Build();
        }
    }
} 