using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Orbit : State
    {
        public override StateType Type => StateType.Orbit;

        private bool orbitClockwise = true;
        private float stateEntryTime;

        public Orbit(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning) { }

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);
            stateEntryTime = Time.time;

            var combat = ctx.CombatTracker;
            if (combat.HasEnemy)
            {
                var relativeVel = ctx.TargetingUtils.RelativeVelocity(combat.EnemyVel);
                var toEnemy = combat.EnemyPos - ctx.ShipInfo.Pos;
                var cross = relativeVel.x * toEnemy.y - relativeVel.y * toEnemy.x;
                orbitClockwise = Mathf.Abs(cross) < 1f ? Random.value > 0.5f : cross > 0f;
            }
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            var combat = ctx.CombatTracker;
            if (!combat.HasEnemy) return;

            var predicted = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos,
                combat.EnemyVel,
                combat.LaserSpeed);

            gunner.SetTarget(predicted);
            navigator.SetFacingTarget(predicted - ctx.ShipInfo.Pos);

            var t = utilityTuning.orbit;
            var orbitPoint = ctx.Maneuvers.ComputeOrbitPoint(
                combat.EnemyPos,
                orbitClockwise,
                t.radius,
                t.leadTime);

            navigator.SetNavigationPoint(orbitPoint, avoid: true);

            if (Time.time - stateEntryTime > t.flipMinTime && Random.value < t.flipChancePerSecond * deltaTime)
            {
                orbitClockwise = !orbitClockwise;
            }
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
            var t = utilityTuning.orbit;
            var inRange = a.EnemyDistance >= t.minRadius && a.EnemyDistance <= t.maxRadius;
            var rangeScore = inRange ? 1f : Mathf.Clamp01(1f - Mathf.Abs(a.EnemyDistance - t.radius) / 20f);

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("enemyWeak", a.EnemyCombinedDurability, t.enemyWeakFactor)
                .Factor("range", rangeScore, t.rangeFactor)
                .FactorBinary(a.HasLineOfSight, "LOS", t.losFactor)
                .Factor("threat", a.Outnumbered, t.threatFactor)
                .FactorBinary(inRange, "orbitRange", t.inRangeFactor)
                .FactorIf(!a.HasLineOfSight, "flanking", t.flankingFactor)
                .FactorIf(a.CombinedDurability < t.lowHealthThreshold, "lowHealth", t.lowHealthFactor)
                .Build();
        }
    }
}
