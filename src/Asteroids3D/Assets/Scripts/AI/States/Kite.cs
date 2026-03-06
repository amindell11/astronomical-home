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
            var combat = ctx.CombatTracker;
            if (!combat.HasEnemy) return;

            var predictedTarget = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos,
                combat.EnemyVel,
                combat.LaserSpeed);

            gunner.SetTarget(predictedTarget);
            navigator.SetFacingTarget(predictedTarget - ctx.ShipInfo.Pos);

            var t = utilityTuning.kite;
            var vectorToEnemy = combat.EnemyPos - ctx.ShipInfo.Pos;
            var distToEnemy = vectorToEnemy.magnitude;
            Vector2 waypoint;

            if (distToEnemy < t.minDistance)
            {
                waypoint = ctx.Maneuvers.ComputeKitePoint(
                    combat.EnemyPos,
                    combat.EnemyVel,
                    t.minDistance + t.pushAwayDistance);
            }
            else if (distToEnemy > t.maxDistance)
            {
                var toEnemy = vectorToEnemy.normalized;
                waypoint = combat.EnemyPos - toEnemy * (t.desiredDistance * t.returnDistanceFactor);
            }
            else
            {
                waypoint = ctx.Maneuvers.ComputeKitePoint(
                    combat.EnemyPos,
                    combat.EnemyVel,
                    t.desiredDistance);
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
            if (!ctx.CombatTracker.HasEnemy) return 0f;

            var a = ctx.Assessment;
            var t = utilityTuning.kite;
            var angleOffset = Mathf.Max(0f, a.SelfAngleToEnemy - t.angleTolerance) / 150f;

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("outnumbered", a.Outnumbered, t.outnumberedFactor)
                .FactorIf(a.EnemyDistance < t.minDistance, "tooClose", t.tooCloseFactor)
                .FactorIf(a.HealthPct < t.lowHealthThreshold && a.ShieldPct > t.highShieldThreshold,
                    "lowHealthHighShield", t.lowHealthHighShieldFactor)
                .Factor("angle", angleOffset, t.angleFactor.Inverted)
                .Build();
        }
    }
}
