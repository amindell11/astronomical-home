using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A static utility class for evaluating the best AI state based on context.
/// This keeps decision-making logic separate from data gathering (AIContextProvider)
/// and action execution (Behavior Tree nodes).
/// </summary>
public static class AIUtilityEvaluator
{
    /// <summary>
    /// Evaluates all utility scores and determines the best state.
    /// </summary>
    /// <param name="provider">The context provider with all current AI data.</param>
    /// <param name="utilityScores">An optional dictionary to populate with the calculated scores for debugging.</param>
    /// <returns>The AI state with the highest utility score.</returns>
    public static AIShipBehaviorStates EvaluateBestState(AIContextProvider provider, Dictionary<AIShipBehaviorStates, float> utilityScores = null)
    {
        float idleScore = EvaluateIdleUtility(provider);
        float patrolScore = EvaluatePatrolUtility(provider);
        float evadeScore = EvaluateEvadeUtility(provider);
        float attackScore = EvaluateAttackUtility(provider);

        // Populate scores dictionary if provided for debugging
        if (utilityScores != null)
        {
            utilityScores.Clear();
            utilityScores[AIShipBehaviorStates.Idle] = idleScore;
            utilityScores[AIShipBehaviorStates.Patrol] = patrolScore;
            utilityScores[AIShipBehaviorStates.Evade] = evadeScore;
            utilityScores[AIShipBehaviorStates.Attack] = attackScore;
        }

        // Find and return the state with the highest utility
        float maxScore = idleScore;
        AIShipBehaviorStates bestState = AIShipBehaviorStates.Idle;

        if (patrolScore > maxScore)
        {
            maxScore = patrolScore;
            bestState = AIShipBehaviorStates.Patrol;
        }

        if (evadeScore > maxScore)
        {
            maxScore = evadeScore;
            bestState = AIShipBehaviorStates.Evade;
        }

        if (attackScore > maxScore)
        {
            maxScore = attackScore;
            bestState = AIShipBehaviorStates.Attack;
        }

        return bestState;
    }

    /// <summary>
    /// Calculates utility for remaining idle (doing nothing)
    /// </summary>
    private static float EvaluateIdleUtility(AIContextProvider provider)
    {
        float score = 0.1f; // Base minimal score
        
        // Increase if no enemies nearby
        if (provider.NearbyEnemyCount == 0)
            score += 0.3f;
            
        // Increased desire to be idle at low health/shield (defensive posture).
        // This is a "fear" response, so we use the weakest link (min health/shield).
        float healthFactor = Mathf.Min(provider.HealthPct, provider.ShieldPct);
        score += AIUtilityCurves.FearCurve(healthFactor, 0.3f); // Max bonus of 0.3 when health/shield is zero.
            
        return score;
    }
    
    /// <summary>
    /// Calculates utility for patrolling (moving around when no immediate threats)
    /// </summary>
    private static float EvaluatePatrolUtility(AIContextProvider provider)
    {
        float score = 0.4f; // Default patrol score
        
        // Increase if no immediate enemies
        if (provider.NearbyEnemyCount == 0)
            score += 0.3f;
            
        // Utility is smoothly adjusted based on health and shields.
        // Patrolling is a balanced activity, so we use average health.
        // High health increases desire to patrol, low health decreases it.
        float healthFactor = (provider.HealthPct + provider.ShieldPct) / 2.0f;
        float healthBonus = Mathf.Lerp(-0.3f, 0.2f, AIUtilityCurves.DesireCurve(healthFactor, 1f));
        score += healthBonus;

        // Don't patrol if an enemy is right on top of us.
        if (provider.Enemy != null && provider.Enemy.gameObject.activeInHierarchy && provider.RelDistance < 0.5f)
            score = 0;

        return score;
    }
    
    /// <summary>
    /// Calculates utility for evading (retreating from threats)
    /// </summary>
    private static float EvaluateEvadeUtility(AIContextProvider provider)
    {
        float score = 0f;
        
        // High priority if low health or shield, using smooth "fear" curves.
        // The bonuses are evaluated independently and summed.
        score += AIUtilityCurves.FearCurve(provider.HealthPct, 0.6f);
        score += AIUtilityCurves.FearCurve(provider.ShieldPct, 0.7f);
            
        // Increase with number of nearby enemies
        score += provider.NearbyEnemyCount * 0.2f;
        
        // Increase if outnumbered
        int netThreat = provider.NearbyEnemyCount - provider.NearbyFriendCount;
        if (netThreat > 0)
            score += netThreat * 0.15f;
            
        // Increase if incoming missile
        if (provider.IncomingMissile)
            score += 0.5f;
            
        // Decrease if very close to target (might need to fight through)
        if (provider.RelDistance < 0.5f && provider.HasLineOfSight)
            score -= 0.2f;
            
        return Mathf.Max(0f, score);
    }
    
    /// <summary>
    /// Calculates utility for attacking (engaging enemies)
    /// </summary>
    private static float EvaluateAttackUtility(AIContextProvider provider)
    {
        if (provider.Enemy == null || !provider.Enemy.gameObject.activeInHierarchy)
            return 0f;
        float score = 0f;
        
        // Base score if enemies are present
        if (provider.NearbyEnemyCount > 0)
            score += 0.5f;
            
        // Increase with good health/shield, using smooth "desire" curves.
        score += AIUtilityCurves.DesireCurve(provider.HealthPct, 0.3f);
        score += AIUtilityCurves.DesireCurve(provider.ShieldPct, 0.2f);
            
        // Bonus for attacking a weak target. This is a "fear" curve for the enemy.
        float enemyHealthFactor = (provider.EnemyHealthPct + provider.EnemyShieldPct) / 2f;
        score += AIUtilityCurves.FearCurve(enemyHealthFactor, 0.3f);
        
        // Bonus for attacking a disarmed target
        if (provider.EnemyLaserHeatPct > 0.9f)
            score += 0.2f;
        if (provider.EnemyMissileAmmo == 0)
            score += 0.1f;

        // Increase if target is in good range
        if (provider.RelDistance > 0.3f && provider.RelDistance < 2.0f)
            score += 0.3f;
            
        // Major bonus if line of sight to target
        if (provider.HasLineOfSight)
            score += 0.4f;
            
        // Bonus if weapons are ready
        if (provider.LaserHeatPct < 0.7f)
            score += 0.1f;
            
        if (provider.MissileAmmo > 0)
            score += 0.1f;
            
        // Decrease if severely outnumbered
        int netThreat = provider.NearbyEnemyCount - provider.NearbyFriendCount;
        if (netThreat > 2)
            score -= 0.3f;
            
        return Mathf.Max(0f, score);
    }
} 