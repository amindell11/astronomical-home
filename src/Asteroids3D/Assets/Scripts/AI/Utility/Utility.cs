using UnityEngine;
using Info = AI.Context.Info;

namespace AI.Utility
{
    public static class Utility
    {
        public static float DesireCurve(float value, float maxBonus)
        {
            value = Mathf.Clamp01(value);
            var t = value * value * (3f - 2f * value);
            return t * maxBonus;
        }

        public static float FearCurve(float value, float maxBonus)
        {
            value = Mathf.Clamp01(value);
            var t = value * value * (3f - 2f * value);
            return (1f - t) * maxBonus;
        }

        public static float ComputeAttackUtility(Info ctx)
        {
            if (!ctx.Enemy)
                return 0f;

            var score = 0.5f;

            score += DesireCurve(ctx.HealthPct, 0.2f);
            score += DesireCurve(ctx.ShieldPct, 0.2f);
            
            var enemyHealthFactor = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            score += FearCurve(enemyHealthFactor, 0.3f);

            var distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy > 6f && distToEnemy < 40f)
                score += 0.3f;
            
            if (ctx.LineOfSightToEnemy)
                score += 0.1f;
            
            var netThreat = ctx.NearbyEnemyCount - ctx.NearbyFriendCount;
            if (netThreat > 2)
                score -= 0.3f;
            
            return Mathf.Max(0f, score);
        }

        public static float ComputeEvadeUtility(Info ctx)
        {
            var score = 0.3f;

            score += FearCurve(ctx.HealthPct, 0.4f);
            score += FearCurve(ctx.ShieldPct, 0.3f);

            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1)
                score += 0.2f;

            if (ctx.Enemy && ctx.LineOfSightToEnemy)
                score += 0.2f;

            if (ctx.Enemy)
            {
                var closingContribution = Mathf.Clamp(ctx.ClosingSpeed * 0.02f, -0.2f, 0.2f);
                score += closingContribution;
            }

            if (ctx.Enemy)
            {
                var facingFactor = Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad);
                var facingContribution = facingFactor * 0.2f;
                score += facingContribution;
            }

            if (ctx.IncomingMissile)
                score += 0.5f;

            if (!ctx.Enemy) return Mathf.Max(0f, score);
            var distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy < 7f && ctx.LineOfSightToEnemy)
                score -= 0.2f;

            return Mathf.Max(0f, score);
        }
    }
} 