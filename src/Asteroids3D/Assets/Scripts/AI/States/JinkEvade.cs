using Movement;
using AI.Utility;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    /// <summary>
    /// Jink-Evade state – performs aggressive zig-zag manoeuvres and full-throttle
    /// sprints to break enemy aim and missile locks.  Triggered in the most
    /// dangerous situations (incoming missile, very low HP, or point-blank enemy).
    /// </summary>
    public partial class JinkEvade : State
    {
        public override StateType Type => StateType.JinkEvade;
        private bool  jinkLeft;
        private float nextJinkTime;
        private Vector2 currentTarget;

        public JinkEvade(Navigator navigator, Gunner gunner, UtilityTuning utilityTuning) : base(navigator, gunner, utilityTuning) { }

        public override void Enter(Info ctx)
        {
            base.Enter(ctx);
            var t = utilityTuning.jinkEvade;
            jinkLeft      = Random.value > 0.5f;
            nextJinkTime  = Time.time + t.interval;
            gunner.SetTarget(Vector2.zero); // cease fire while jinking
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            var combat = ctx.CombatTracker;
            var t = utilityTuning.jinkEvade;

            if (Time.time >= nextJinkTime)
            {
                jinkLeft = !jinkLeft;
                nextJinkTime = Time.time + t.interval;
            }

            var fleeDir = combat.HasEnemy
                ? -(combat.EnemyPos - ctx.ShipInfo.Pos).normalized
                : Random.insideUnitCircle.normalized;

            var sideDir = jinkLeft ? new Vector2(fleeDir.y, -fleeDir.x)  // 90° CW
                                        : new Vector2(-fleeDir.y, fleeDir.x); // 90° CCW

            var amp = ctx.Assessment.IncomingMissile
                ? t.sideStepDistance * t.missileAmplitudeFactor
                : t.sideStepDistance;

            var offset = fleeDir * t.fleeDistance + sideDir * amp;
            currentTarget = ctx.ShipInfo.Pos + offset;

            navigator.SetNavigationPoint(currentTarget, avoid: true);
            navigator.SetFacingTarget(fleeDir);
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(Info ctx)
        {
            if (!ctx.CombatTracker.HasEnemy && !ctx.Assessment.IncomingMissile) return 0f;

            var a = ctx.Assessment;
            var t = utilityTuning.jinkEvade;
            var criticalState = a.HealthPct < t.criticalHealthThreshold
                && a.ShieldPct < t.criticalShieldThreshold;

            return NewBuilder()
                .Factor("selfHealth", a.HealthPct, t.healthFactor)
                .Factor("selfShield", a.ShieldPct, t.shieldFactor)
                .Factor("outnumbered", a.Outnumbered, t.outnumberedFactor)
                .FactorBinary(a.HasLineOfSight, "enemyLOS", t.enemyLOSFactor)
                .Factor("closing", a.ClosingRate, t.closingSpeedFactor)
                .Factor("enemyFacing", a.EnemyFacingThreat, t.enemyFacingFactor)
                .FactorIf(a.IncomingMissile, "missileThreat", t.missileThreatFactor)
                .FactorIf(criticalState, "criticalState", t.criticalStateFactor)
                .FactorIf(ctx.CombatTracker.HasEnemy && a.SelfAngleToEnemy > t.facingAwayAngle, "facingAway", t.facingAwayFactor)
                .Factor("angle", a.SelfAngleNorm, t.angleFactor)
                .Build();
        }
    }
}
