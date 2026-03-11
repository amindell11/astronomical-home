using Movement;
using Movement.MPC;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class AttackAggressive : State
    {
        public override StateType Type => StateType.AttackAggressive;

        public AttackAggressive(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning)
            : base(navigator, gunner, utilityTuning)
        {
        }

        public override bool IsAvailable(Info ctx) =>
            ctx.Combat.HasEnemy && ctx.Assessment.EnemyDistance <= utilityTuning.attackAggressive.optimalRangeMax;

        public override void Enter(Info context)
        {
            var t = utilityTuning.attackAggressive;
            var desiredRange = (t.optimalRangeMin + t.optimalRangeMax) * 0.5f;
            var tolerance = (t.optimalRangeMax - t.optimalRangeMin) * 0.5f;
            navigator.SetGoalMaintainRange(desiredRange, tolerance);

            navigator.SetMpcWeightOverrides(new WeightOverrides
            {
                wFacing = t.wFacing,
                wExposure = t.wExposure,
                facingPower = t.facingPower,
                wLos = float.NaN,
                wTangential = float.NaN,
                exposurePower = float.NaN,
            });
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            navigator.SetObstacleExclusion(combat.Enemy.transform);

            var predictedTarget = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos, combat.EnemyVel, combat.LaserSpeed);
            gunner.SetTarget(predictedTarget);

            navigator.SetNavigationPoint(combat.EnemyPos, true, combat.EnemyVel);

            var enemyFwd = combat.EnemyForward;
            var enemyYawDeg = Mathf.Atan2(-enemyFwd.x, enemyFwd.y) * Mathf.Rad2Deg;
            navigator.SetEnemyState(enemyYawDeg, combat.EnemyYawRate, combat.LaserSpeed);
        }

        public override void Exit()
        {
            navigator.ClearNavigationPoint();
            navigator.ClearGoalMode();
            navigator.ClearEnemyState();
            navigator.ClearObstacleExclusion();
            navigator.ClearMpcWeightOverrides();
        }

        public override float ComputeUtility(Info ctx)
        {
            var a = ctx.Assessment;
            var t = utilityTuning.attackAggressive;
            var inRange = a.EnemyDistance >= t.optimalRangeMin && a.EnemyDistance <= t.optimalRangeMax;
            var rangeScore = inRange ? 1f : Mathf.Clamp01(1f - Mathf.Abs(a.EnemyDistance - 15f) / 25f);

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("enemyWeak", a.EnemyCombinedDurability, t.enemyWeakFactor)
                .Factor("enemyFacing", a.EnemyFacingThreat, t.enemyFacingFactor)
                .Factor("selfAngle", a.SelfAngleNorm, t.selfAngleFactor)
                .Factor("range", rangeScore, t.rangeFactor)
                .FactorBinary(a.HasLineOfSight, "LOS", t.losFactor)
                .Build();
        }
    }
}
