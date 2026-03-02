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

            var fightingRetreat = ctx.HealthPct < utilityTuning.evadeFightingRetreatHealthThreshold 
                && ctx.ShieldPct > utilityTuning.evadeFightingRetreatShieldThreshold;
            
            var angleScore = ctx.SelfAngleToEnemy / 180f;
            var closingScore = Mathf.Clamp01(ctx.ClosingSpeed * 0.05f + 0.5f);
            var facingScore = ctx.Enemy ? (Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad) + 1f) / 2f : 0.5f;
            var dist = ctx.VectorToEnemy.magnitude;

            return new UtilityBuilder()
                .Factor("selfHealth", ctx.HealthPct, utilityTuning.evadeHealthFactor)
                .Factor("selfShield", ctx.ShieldPct, utilityTuning.evadeShieldFactor)
                .FactorBinary(ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1, "outnumbered", utilityTuning.evadeOutnumberedFactor)
                .FactorBinary(ctx.Enemy && ctx.LineOfSightToEnemy, "enemyLOS", utilityTuning.evadeEnemyLOSFactor)
                .Factor("closing", closingScore, utilityTuning.evadeClosingSpeedFactor)
                .Factor("enemyFacing", facingScore, utilityTuning.evadeEnemyFacingFactor)
                .FactorIf(ctx.IncomingMissile, "missile", utilityTuning.evadeMissileFactor)
                .FactorIf(dist < utilityTuning.evadeUtilityTooCloseDistance && ctx.LineOfSightToEnemy, "tooClose", utilityTuning.evadeTooCloseFactor)
                .FactorIf(fightingRetreat, "fightingRetreat", utilityTuning.evadeFightingRetreatFactor)
                .FactorIf(ctx.IncomingMissile, "missilePenalty", utilityTuning.evadeMissilePenaltyFactor)
                .Factor("angle", angleScore, utilityTuning.evadeAngleFactor)
                .Build();
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