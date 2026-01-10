using AI.Steering;
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

            var score = Utility.Utility.ComputeAttackUtility(ctx);
            
            var dist = ctx.VectorToEnemy.magnitude;
            if (dist >= utilityTuning.orbitMinRadius && dist <= utilityTuning.orbitMaxRadius)
            {
                score += utilityTuning.orbitRangeBonus;
            }
            
            if (!ctx.LineOfSightToEnemy)
            {
                score += utilityTuning.orbitNoLosBonus;
            }

            var healthFactor = (ctx.HealthPct + ctx.ShieldPct) / 2f;
            if (healthFactor < utilityTuning.orbitLowHealthThreshold)
            {
                score -= utilityTuning.orbitLowHealthPenalty;
            }
            
            return Mathf.Max(0f, score);
        }
    }
} 