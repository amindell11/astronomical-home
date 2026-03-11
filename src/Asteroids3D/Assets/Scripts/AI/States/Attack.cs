using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Attack : State
    {
        public override StateType Type => StateType.Attack;

        public Attack(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override bool IsAvailable(Info ctx) =>
            ctx.Combat.HasEnemy && ctx.Assessment.EnemyDistance <= utilityTuning.attack.optimalRangeMax;

        public override void Enter(Info context)
        {
            var t = utilityTuning.attack;
            var desiredRange = (t.optimalRangeMin + t.optimalRangeMax) * 0.5f;
            var tolerance = (t.optimalRangeMax - t.optimalRangeMin) * 0.5f;
            navigator.SetGoalMaintainRange(desiredRange, tolerance);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            navigator.SetObstacleExclusion(combat.Enemy.transform);

            var predictedTarget = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos,
                combat.EnemyVel,
                combat.LaserSpeed
            );

            gunner.SetTarget(predictedTarget);

            navigator.SetNavigationPoint(
                combat.EnemyPos,
                true,
                combat.EnemyVel);

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
        }

        public override float ComputeUtility(Info ctx)
        {
            var a = ctx.Assessment;
            var t = utilityTuning.attack;
            var inRange = a.EnemyDistance >= t.optimalRangeMin && a.EnemyDistance <= t.optimalRangeMax;
            var rangeScore = inRange ? 1f : Mathf.Clamp01(1f - Mathf.Abs(a.EnemyDistance - 20f) / 30f);

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("enemyWeak", a.EnemyCombinedDurability, t.enemyWeakFactor)
                .Factor("range", rangeScore, t.rangeFactor)
                .FactorBinary(a.HasLineOfSight, "LOS", t.losFactor)
                .Factor("threat", a.Outnumbered, t.threatFactor)
                .FactorIf(a.EnemyDistance > t.outerDistanceThreshold, "outerRange", t.outerRangeFactor)
                .Factor("desperation", a.HealthPct, t.desperationFactor)
                .Build();
        }
    }
}
