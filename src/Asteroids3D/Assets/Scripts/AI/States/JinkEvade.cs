using AI.Steering;
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

            Vector2 fleeDir = ctx.Enemy ? -ctx.VectorToEnemy.normalized : Random.insideUnitCircle.normalized;

            Vector2 sideDir = jinkLeft ? new Vector2(fleeDir.y, -fleeDir.x)  // 90° CW
                                        : new Vector2(-fleeDir.y, fleeDir.x); // 90° CCW

            float amp = ctx.IncomingMissile ? utilityTuning.jinkSideStepDistance * utilityTuning.jinkMissileAmplitudeFactor : utilityTuning.jinkSideStepDistance;

            Vector2 offset = fleeDir * utilityTuning.jinkFleeDistance + sideDir * amp;
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
            
            var score = Utility.Utility.ComputeEvadeUtility(ctx);

            if (ctx.IncomingMissile)
            {
                score += utilityTuning.jinkMissileThreatBonus;
            }

            if (ctx.HealthPct < utilityTuning.jinkCriticalHealthThreshold && ctx.ShieldPct < utilityTuning.jinkCriticalShieldThreshold)
            {
                score += utilityTuning.jinkCriticalStateBonus;
            }

            if (ctx.Enemy && ctx.SelfAngleToEnemy > utilityTuning.jinkFacingAwayAngle)
            {
                score += utilityTuning.jinkFacingAwayBonus;
            }

            var anglePenalty = (180f - ctx.SelfAngleToEnemy) / 180f;
            score -= anglePenalty * utilityTuning.jinkAnglePenaltyMultiplier;
            
            return Mathf.Clamp01(score);
        }
    }
} 