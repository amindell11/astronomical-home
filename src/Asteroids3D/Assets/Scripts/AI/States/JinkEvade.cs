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
            jinkLeft      = Random.value > 0.5f;
            nextJinkTime  = Time.time + utilityTuning.jinkInterval;
            gunner.SetTarget(Vector2.zero); // cease fire while jinking
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            if (Time.time >= nextJinkTime)
            {
                jinkLeft = !jinkLeft;
                nextJinkTime = Time.time + utilityTuning.jinkInterval;
            }

            var fleeDir = ctx.Enemy ? -ctx.VectorToEnemy.normalized : Random.insideUnitCircle.normalized;

            var sideDir = jinkLeft ? new Vector2(fleeDir.y, -fleeDir.x)  // 90° CW
                                        : new Vector2(-fleeDir.y, fleeDir.x); // 90° CCW

            var amp = ctx.IncomingMissile ? utilityTuning.jinkSideStepDistance * utilityTuning.jinkMissileAmplitudeFactor : utilityTuning.jinkSideStepDistance;

            var offset = fleeDir * utilityTuning.jinkFleeDistance + sideDir * amp;
            currentTarget  = ctx.SelfPosition + offset;

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
            if (!ctx.Enemy && !ctx.IncomingMissile) return 0f;

            var criticalState = ctx.HealthPct < utilityTuning.jinkCriticalHealthThreshold 
                && ctx.ShieldPct < utilityTuning.jinkCriticalShieldThreshold;
            
            var angleScore = ctx.SelfAngleToEnemy / 180f;
            var closingScore = Mathf.Clamp01(ctx.ClosingSpeed * 0.05f + 0.5f);
            var facingScore = ctx.Enemy ? (Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad) + 1f) / 2f : 0.5f;
            var dist = ctx.Enemy ? ctx.VectorToEnemy.magnitude : 100f;

            return new UtilityBuilder()
                .Factor("selfHealth", ctx.HealthPct, utilityTuning.evadeHealthFactor)
                .Factor("selfShield", ctx.ShieldPct, utilityTuning.evadeShieldFactor)
                .FactorBinary(ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1, "outnumbered", utilityTuning.evadeOutnumberedFactor)
                .FactorBinary(ctx.Enemy && ctx.LineOfSightToEnemy, "enemyLOS", utilityTuning.evadeEnemyLOSFactor)
                .Factor("closing", closingScore, utilityTuning.evadeClosingSpeedFactor)
                .Factor("enemyFacing", facingScore, utilityTuning.evadeEnemyFacingFactor)
                .FactorIf(ctx.IncomingMissile, "missileThreat", utilityTuning.jinkMissileThreatFactor)
                .FactorIf(criticalState, "criticalState", utilityTuning.jinkCriticalStateFactor)
                .FactorIf(ctx.Enemy && ctx.SelfAngleToEnemy > utilityTuning.jinkFacingAwayAngle, "facingAway", utilityTuning.jinkFacingAwayFactor)
                .Factor("angle", angleScore, utilityTuning.jinkAngleFactor)
                .Build();
        }
    }
} 