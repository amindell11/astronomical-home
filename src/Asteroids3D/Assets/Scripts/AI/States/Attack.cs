using AI.Steering;
using AI.Utility;
using Editor;
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
            RLog.AI($"[AttackState] Entering. Target: {context.Enemy?.name ?? "None"}");
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
            RLog.AI("[AttackState] Exiting");
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(Info ctx)
        {
            if (!ctx.Enemy)
                return 0f;

            var score = Utility.Utility.ComputeAttackUtility(ctx);

            var enemyHealthFactor = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            score += Utility.Utility.FearCurve(enemyHealthFactor, utilityTuning.attackEnemyHealthThreshold);

            var dist = ctx.VectorToEnemy.magnitude;
            if (dist > utilityTuning.attackOuterDistanceThreshold)
            {
                score += utilityTuning.attackOuterDistanceBonus;
            }

            score += Utility.Utility.FearCurve(ctx.HealthPct, utilityTuning.attackLowHealthFearMultiplier);

            return score;
        }
    }
} 