using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "UtilitySetState", story: "Set current [State] based on Utility of [ContextProvider]", category: "Action", id: "a5fa78361b3ed4629dca4247821484c9")]
public partial class UtilitySetStateAction : Action
{
    [SerializeReference] public BlackboardVariable<AIShipBehaviorStates> State;
    [SerializeReference] public BlackboardVariable<AIContextProvider> ContextProvider;
    
    [SerializeReference, Tooltip("Log utility calculations for debugging")]
    public BlackboardVariable<bool> logUtilityScores;
    
    [SerializeReference, Tooltip("Minimum time in seconds before switching states (prevents thrashing)")]
    public BlackboardVariable<float> minTimeInState;

    // Cached references
    private AIShipBehaviorStates lastState = AIShipBehaviorStates.Idle;
    private float stateChangeTime = -1f; // -1 indicates uninitialized

    protected override Status OnStart()
    {
        if (ContextProvider == null)
        {
            RLog.AIError($"UtilitySetState: No ContextProvider variable assigned for {GameObject.name}");
            return Status.Failure;
        }
        
        // Initialize default values if not set
        if (logUtilityScores == null)
        {
            logUtilityScores = new BlackboardVariable<bool>();
            logUtilityScores.Value = false;
        }
        
        if (minTimeInState == null)
        {
            minTimeInState = new BlackboardVariable<float>();
            minTimeInState.Value = 0.5f;
        }
        
        // Only initialize stateChangeTime if this is the first time
        if (stateChangeTime < 0f)
        {
            stateChangeTime = Time.time;
        }
        
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (ContextProvider == null || ContextProvider.Value == null)
            return Status.Failure;
            
        var provider = ContextProvider.Value;
        if (!provider.IsContextValid)
        {
            // Context not ready yet, keep current state
            return Status.Running;
        }
        
        // Evaluate utilities for each state
        var bestState = EvaluateUtilities(provider);
        RLog.AI($"[{GameObject.name}] Best state: {bestState} Last state: {lastState}");
        // Apply hysteresis to prevent state thrashing
        if (bestState != lastState)
        {
            float timeSinceLastChange = Time.time - stateChangeTime;
            float minTime = minTimeInState?.Value ?? 0.5f;
            RLog.AI($"[{GameObject.name}] Time since last change: {timeSinceLastChange} Min time: {minTime}");
            if (timeSinceLastChange >= minTime)
            {
                // Switch to new state
                State.Value = bestState;
                lastState = bestState;
                stateChangeTime = Time.time;
                
                bool shouldLog = logUtilityScores?.Value ?? false;
                if (shouldLog)
                {
                    RLog.AI($"[{GameObject.name}] State changed to {bestState} (provider: {provider.name})");
                }
            }
        }
        
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
    
    /// <summary>
    /// Evaluates utility scores for all available states and returns the best one
    /// </summary>
    private AIShipBehaviorStates EvaluateUtilities(AIContextProvider provider)
    {
        float idleScore = EvaluateIdleUtility(provider);
        float patrolScore = EvaluatePatrolUtility(provider);
        float evadeScore = EvaluateEvadeUtility(provider);
        float attackScore = EvaluateAttackUtility(provider);
        
        bool shouldLog = logUtilityScores?.Value ?? false;
        if (shouldLog)
        {
            RLog.AI($"[{GameObject.name}] Utility scores - Idle:{idleScore:F2} Patrol:{patrolScore:F2} Evade:{evadeScore:F2} Attack:{attackScore:F2}");
        }
        
        // Find the state with the highest utility
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
    private float EvaluateIdleUtility(AIContextProvider provider)
    {
        float score = 0.1f; // Base minimal score
        
        // Increase if no enemies nearby
        if (provider.NearbyEnemyCount == 0)
            score += 0.3f;
            
        // Increase if very low health/shield (defensive posture)
        if (provider.HealthPct < 0.2f || provider.ShieldPct < 0.2f)
            score += 0.2f;
            
        return score;
    }
    
    /// <summary>
    /// Calculates utility for patrolling (moving around when no immediate threats)
    /// </summary>
    private float EvaluatePatrolUtility(AIContextProvider provider)
    {
        float score = 0.4f; // Default patrol score
        
        // Increase if no immediate enemies
        if (provider.NearbyEnemyCount == 0)
            score += 0.3f;
            
        // Decrease if low health/shield
        if (provider.HealthPct < 0.3f || provider.ShieldPct < 0.3f)
            score -= 0.3f;
            
        // Increase if good health/shield
        if (provider.HealthPct > 0.7f && provider.ShieldPct > 0.7f)
            score += 0.2f;
        if (provider.Enemy != null && provider.Enemy.gameObject.activeInHierarchy && provider.RelDistance < 0.5f)
            score = 0;
        RLog.AI($"[{GameObject.name}] Patrol score: {score} Enemy: {provider.Enemy?.name} Active: {provider.Enemy?.gameObject.activeInHierarchy} RelDistance: {provider.RelDistance}");
        return score;
    }
    
    /// <summary>
    /// Calculates utility for evading (retreating from threats)
    /// </summary>
    private float EvaluateEvadeUtility(AIContextProvider provider)
    {
        float score = 0f;
        
        // High priority if low health or shield
        if (provider.HealthPct < 0.3f)
            score += 0.6f;
            
        if (provider.ShieldPct < 0.2f)
            score += 0.7f;
            
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
    private float EvaluateAttackUtility(AIContextProvider provider)
    {
        if (provider.Enemy == null || !provider.Enemy.gameObject.activeInHierarchy)
            return 0f;
        float score = 0f;
        
        // Base score if enemies are present
        if (provider.NearbyEnemyCount > 0)
            score += 0.5f;
            
        // Increase with good health/shield
        if (provider.HealthPct > 0.5f)
            score += 0.3f;
            
        if (provider.ShieldPct > 0.3f)
            score += 0.2f;
            
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

