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

            if (ctx?.Enemy)
            {
                var relativeVel = ctx.EnemyRelVelocity;
                var toEnemy = ctx.VectorToEnemy;
                var cross = relativeVel.x * toEnemy.y - relativeVel.y * toEnemy.x;
                orbitClockwise = Mathf.Abs(cross) < 1f ? Random.value > 0.5f : cross > 0f;
            }
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            if (!ctx?.Enemy) return;

            var predicted = ctx.Targeting.PredictIntercept(
                ctx.EnemyPos,
                ctx.EnemyVel,
                ctx.LaserSpeed);

            gunner.SetTarget(predicted);
            navigator.SetFacingTarget(predicted - ctx.SelfPosition);

            var orbitPoint = ctx.Maneuvers.ComputeOrbitPoint(
                ctx.EnemyPos,
                orbitClockwise,
                utilityTuning.orbitRadius,
                utilityTuning.orbitLeadTime);

            navigator.SetNavigationPoint(orbitPoint, avoid: true);

            if (Time.time - stateEntryTime > utilityTuning.orbitFlipMinTime && Random.value < utilityTuning.orbitFlipChancePerSecond * deltaTime)
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
            if (!ctx?.Enemy) return 0f;

            var dist = ctx.VectorToEnemy.magnitude;
            var enemyHealth = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            var healthFactor = (ctx.HealthPct + ctx.ShieldPct) / 2f;
            var netThreat = Mathf.Clamp01((ctx.NearbyEnemyCount - ctx.NearbyFriendCount) / 3f);
            var inRange = dist >= utilityTuning.orbitMinRadius && dist <= utilityTuning.orbitMaxRadius;
            var rangeScore = inRange ? 1f : Mathf.Clamp01(1f - Mathf.Abs(dist - utilityTuning.orbitRadius) / 20f);

            return new UtilityBuilder()
                .Factor("selfHealth", ctx.HealthPct, utilityTuning.attackHealthFactor)
                .Factor("selfShield", ctx.ShieldPct, utilityTuning.attackShieldFactor)
                .Factor("enemyWeak", enemyHealth, utilityTuning.attackEnemyWeakFactor)
                .Factor("range", rangeScore, utilityTuning.attackRangeFactor)
                .FactorBinary(ctx.LineOfSightToEnemy, "LOS", utilityTuning.attackLOSFactor)
                .Factor("threat", netThreat, utilityTuning.attackThreatFactor)
                .FactorBinary(inRange, "orbitRange", utilityTuning.orbitInRangeFactor)
                .FactorIf(!ctx.LineOfSightToEnemy, "flanking", utilityTuning.orbitFlankingFactor)
                .FactorIf(healthFactor < utilityTuning.orbitLowHealthThreshold, "lowHealth", utilityTuning.orbitLowHealthFactor)
                .Build();
        }
    }
} 