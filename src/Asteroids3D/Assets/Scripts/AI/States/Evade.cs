using AI.Steering;
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
            if (!ctx.Enemy) return 0f;

            var score = Utility.Utility.ComputeEvadeUtility(ctx);
            
            if (ctx.HealthPct < utilityTuning.evadeFightingRetreatHealthThreshold && ctx.ShieldPct > utilityTuning.evadeFightingRetreatShieldThreshold)
            {
                score += utilityTuning.evadeFightingRetreatBonus;
            }

            if (ctx.IncomingMissile)
            {
                score -= utilityTuning.evadeMissilePenalty;
            }

            var anglePenalty = (180f - ctx.SelfAngleToEnemy) / 180f;
            score -= anglePenalty * utilityTuning.evadeAnglePenaltyMultiplier;

            return Mathf.Max(0f, score);
        }

        private Vector2 CalculateEvadePoint(Info ctx)
        {
            var selfPos = ctx.SelfPosition;
            Vector2 fleeDirection;
            
            if (ctx.Enemy && ctx.Enemy.gameObject.activeInHierarchy)
            {
                fleeDirection = -ctx.VectorToEnemy.normalized;
            }else{
                fleeDirection = Random.insideUnitCircle.normalized;
            }
            
            return selfPos + fleeDirection * utilityTuning.evadeFleeDistance;
        }
    }
} 