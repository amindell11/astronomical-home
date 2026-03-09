using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Evade : State
    {
        public override StateType Type => StateType.Evade;

        private Vector2 evadePoint;

        public Evade(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning)
        {
        }

        public override bool IsAvailable(Info ctx) => ctx.Combat.HasEnemy;

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);

            gunner.SetTarget((Transform)null);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            evadePoint = CalculateEvadePoint(ctx);
            navigator.SetNavigationPoint(evadePoint, true);
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
        }

        public override float ComputeUtility(Info ctx)
        {
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

        private Vector2 CalculateEvadePoint(Info ctx)
        {
            var combat = ctx.Combat;
            var selfPos = ctx.ShipInfo.Pos;
            Vector2 fleeDirection;

            if (combat.HasEnemy)
            {
                fleeDirection = -(combat.EnemyPos - selfPos).normalized;
            }
            else
            {
                fleeDirection = Random.insideUnitCircle.normalized;
            }

            return selfPos + fleeDirection * utilityTuning.evade.fleeDistance;
        }
    }
}
