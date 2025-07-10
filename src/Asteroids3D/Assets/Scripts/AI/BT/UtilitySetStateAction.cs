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
        
        // Evaluate utilities for each state using the new static evaluator
        var bestState = AIUtilityEvaluator.EvaluateBestState(provider, provider.utilityScores);

        bool shouldLog = logUtilityScores?.Value ?? false;
        if (shouldLog)
        {
            RLog.AI($"[{GameObject.name}] Best state determined: {bestState}. Last state: {lastState}");
        }
        
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
}

