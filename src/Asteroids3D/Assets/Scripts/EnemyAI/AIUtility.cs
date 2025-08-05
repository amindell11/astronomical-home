using UnityEngine;

namespace EnemyAI
{
    /// <summary>
    /// A collection of static methods for calculating utility scores.
    /// These replace hard-coded thresholds with more nuanced, continuous functions
    /// and centralize common utility calculations for AI states.
    /// </summary>
    public static class AIUtility
    {
        /// <summary>
        /// A smooth utility curve that returns a high score when the input value is high.
        /// This is useful for "desire" behaviors, like attacking when health is high.
        /// The curve is a standard smoothstep function.
        /// </summary>
        /// <param name="value">The input value to evaluate (e.g., HealthPct), clamped to [0, 1].</param>
        /// <param name="maxBonus">The maximum utility bonus this curve can return.</param>
        /// <returns>A utility score between 0 and maxBonus.</returns>
        public static float DesireCurve(float value, float maxBonus)
        {
            value = Mathf.Clamp01(value);
            // Standard SmoothStep: f(t) = 3t^2 - 2t^3, which is t*t*(3-2*t)
            float t = value * value * (3f - 2f * value);
            return t * maxBonus;
        }

        /// <summary>
        /// A smooth utility curve that returns a high score when the input value is low.
        /// This is useful for "fear" behaviors, like evading when health is low.
        /// The curve is an inverted smoothstep function.
        /// </summary>
        /// <param name="value">The input value to evaluate (e.g., HealthPct), clamped to [0, 1].</param>
        /// <param name="maxBonus">The maximum utility bonus this curve can return.</param>
        /// <returns>A utility score between 0 and maxBonus.</returns>
        public static float FearCurve(float value, float maxBonus)
        {
            value = Mathf.Clamp01(value);
            // Inverted SmoothStep: 1 - f(t)
            float t = value * value * (3f - 2f * value);
            return (1f - t) * maxBonus;
        }
    
        /// <summary>
        /// Computes a general-purpose utility score for aggressive, offensive actions.
        /// </summary>
        public static float ComputeAttackUtility(AIContext ctx)
        {
            if (ctx.Enemy == null)
                return 0f;

            // Base score: willingness to fight
            float score = 0.5f;

            // Bonus for having high health/shields
            score += DesireCurve(ctx.HealthPct, 0.2f);
            score += DesireCurve(ctx.ShieldPct, 0.2f);
            
            // Bonus for attacking a weak target
            float enemyHealthFactor = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            score += FearCurve(enemyHealthFactor, 0.3f);
        
            // Bonus for attacking a disarmed target
            score += DesireCurve(ctx.EnemyLaserHeatPct, 0.2f);
            score += DesireCurve(ctx.EnemyMissileAmmo, 0.1f);

            // Increase if target is in good range
            float distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy > 6f && distToEnemy < 40f)
                score += 0.3f;
            
            if (ctx.LineOfSightToEnemy)
                score += 0.1f;
            
            // Bonus for having ammo and low heat
            score += FearCurve(ctx.LaserHeatPct, 0.1f);
            score += DesireCurve(ctx.MissileAmmo, 0.1f);
            
            // Decrease if severely outnumbered
            int netThreat = ctx.NearbyEnemyCount - ctx.NearbyFriendCount;
            if (netThreat > 2)
                score -= 0.3f;
            
            return Mathf.Max(0f, score);
        }

        /// <summary>
        /// Computes a general-purpose utility score for defensive, evasive actions.
        /// </summary>
        public static float ComputeEvadeUtility(AIContext ctx)
        {
            // Base score: baseline desire to evade if threatened
            float score = 0.3f;

            // Strong incentive to evade if shields/health are low
            score += FearCurve(ctx.HealthPct, 0.4f);
            score += FearCurve(ctx.ShieldPct, 0.3f);

            // Increase score if outnumbered
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1)
                score += 0.2f;

            // Increase score if an enemy has a clear shot
            if (ctx.Enemy != null && ctx.LineOfSightToEnemy)
                score += 0.2f;

            // Adjust score based on closing speed
            if (ctx.Enemy != null)
            {
                float closingContribution = Mathf.Clamp(ctx.ClosingSpeed * 0.02f, -0.2f, 0.2f);
                score += closingContribution;
            }

            // Adjust score based on enemy facing
            if (ctx.Enemy != null)
            {
                float facingFactor = Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad); // 1 when enemy faces us
                float facingContribution = facingFactor * 0.2f;
                score += facingContribution;
            }

            // Increase score as our laser heat increases (need to cool down)
            score += DesireCurve(ctx.LaserHeatPct, 0.1f);

            // Major bonus if incoming missile detected
            if (ctx.IncomingMissile)
                score += 0.5f;
            
            // Decrease if very close to target (might need to fight through)
            if (ctx.Enemy != null)
            {
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                if (distToEnemy < 7f && ctx.LineOfSightToEnemy)
                    score -= 0.2f;
            }
            
            return Mathf.Max(0f, score);
        }
    }
} 