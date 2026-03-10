using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Evade : State
    {
        public override StateType Type => StateType.Evade;

        private const float FleeProjectionDistance = 50f;

        public Evade(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);

            gunner.SetTarget((Transform)null);
            navigator.SetContinuousNavigation(true);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            if (!ctx.Combat.HasEnemy) return;

            // Project a waypoint far away from the enemy
            var awayFromEnemy = ctx.ShipInfo.Pos - ctx.Combat.EnemyPos;
            var dist = awayFromEnemy.magnitude;
            var fleeDir = dist > 0.01f ? awayFromEnemy / dist : Vector2.up;
            var fleeTarget = ctx.ShipInfo.Pos + fleeDir * FleeProjectionDistance;
            navigator.SetNavigationPoint(fleeTarget, true);
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
            navigator.SetContinuousNavigation(false);
        }

        public override float ComputeUtility(Info ctx)
        {
            if (!ctx.Combat.HasEnemy) return 0f;

            var a = ctx.Assessment;
            var t = utilityTuning.evade;
            var fightingRetreat = a.HealthPct < t.fightingRetreatHealthThreshold
                && a.ShieldPct > t.fightingRetreatShieldThreshold;

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("outnumbered", a.Outnumbered, t.outnumberedFactor)
                .FactorBinary(a.HasLineOfSight, "enemyLOS", t.enemyLOSFactor)
                .Factor("closing", a.ClosingRate, t.closingSpeedFactor)
                .Factor("enemyFacing", a.EnemyFacingThreat, t.enemyFacingFactor)
                .FactorIf(a.IncomingMissile, "missile", t.missileFactor)
                .FactorIf(a.EnemyDistance < t.tooCloseDistance && a.HasLineOfSight, "tooClose", t.tooCloseFactor)
                .FactorIf(fightingRetreat, "fightingRetreat", t.fightingRetreatFactor)
                .FactorIf(a.IncomingMissile, "missilePenalty", t.missilePenaltyFactor)
                .Factor("angle", a.SelfAngleNorm, t.angleFactor)
                .Build();
        }

    }
}
