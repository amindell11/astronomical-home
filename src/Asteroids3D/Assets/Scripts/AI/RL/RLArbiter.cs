using UnityEngine;
#if UNITY_ML_AGENTS
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
#endif
using Unity.Behavior;

#if UNITY_ML_AGENTS
/// <summary>
/// ML-Agents arbiter stub for controlling behavior tree states via RL.
/// TODO: Implement observations, rewards, and training logic.
/// </summary>
public class RLArbiter : Agent
#else
/// <summary>
/// Fallback RL arbiter stub when ML-Agents is not installed.
/// TODO: Implement heuristic decision making.
/// </summary>
public class RLArbiter : MonoBehaviour
#endif
{
    [Header("RL Configuration")]
    [SerializeField] private int decisionInterval = 4; // Run every k FixedUpdate ticks
    [SerializeField] private bool enableRL = true; // Toggle for debugging
    
    [Header("References")]
    [SerializeField] private BehaviorGraphAgent behaviorAgent;
    [SerializeField] private Ship ship;
    
    private new void Awake()
    {
        // Cache components
        if (!ship) ship = GetComponent<Ship>();
        if (!behaviorAgent) behaviorAgent = GetComponent<BehaviorGraphAgent>();
        
        #if !UNITY_ML_AGENTS
        RLog.RLWarning("RLArbiter: ML-Agents package not detected. Running in fallback mode.");
        #endif
    }

#if UNITY_ML_AGENTS
    public override void OnEpisodeBegin()
    {
        // TODO: Reset episode state
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // TODO: Implement observation collection
        // Placeholder: add some basic observations
        sensor.AddObservation(0f); // Placeholder observation
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (!enableRL || !behaviorAgent) return;

        // Get action and map to state
        int stateIndex = actionBuffers.DiscreteActions[0];
        stateIndex = Mathf.Clamp(stateIndex, 0, 3);
        
        AIShipBehaviorStates selectedState = (AIShipBehaviorStates)stateIndex;
        
        // Set state via bridge
        BTStateBridge.SetState(behaviorAgent, selectedState);
        
        // TODO: Calculate and assign rewards
        SetReward(0f); // Placeholder reward
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Simple placeholder heuristic
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = (int)AIShipBehaviorStates.Patrol;
    }
#else
    // Fallback behavior when ML-Agents is not installed
    private void FixedUpdate()
    {
        if (!enableRL || !behaviorAgent) return;
        
        // TODO: Implement simple heuristic decision making
        // Placeholder: just set to Patrol
        BTStateBridge.SetState(behaviorAgent, AIShipBehaviorStates.Patrol);
    }
#endif
}

/// <summary>
/// Bridge class to interface between RL decisions and Unity.Behavior blackboard system
/// </summary>
public static class BTStateBridge
{
    public static void SetState(BehaviorGraphAgent agent, AIShipBehaviorStates state)
    {
        if (!agent) return;
        
        try
        {
            // Use BehaviorGraphAgent's SetVariableValue method
            agent.SetVariableValue("State", state);
        }
        catch (System.Exception e)
        {
            RLog.AIWarning($"BTStateBridge: Failed to set 'State' variable on {agent.name}: {e.Message}");
        }
    }
    
    public static AIShipBehaviorStates GetState(BehaviorGraphAgent agent)
    {
        if (!agent) return AIShipBehaviorStates.Idle;
        
        try
        {
            // Use BehaviorGraphAgent's GetVariable method
            if (agent.GetVariable("State", out BlackboardVariable variable))
            {
                if (variable.ObjectValue is AIShipBehaviorStates state)
                {
                    return state;
                }
            }
        }
        catch (System.Exception e)
        {
            RLog.AIWarning($"BTStateBridge: Failed to get 'State' variable from {agent.name}: {e.Message}");
        }
        
        return AIShipBehaviorStates.Idle;
    }
}
