using System.Collections;
using NUnit.Framework;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Basic integration tests to verify ML-Agents components are properly configured
/// </summary>
public class MLAgentsIntegrationTests
{
    [UnityTest]
    public IEnumerator BasicAgent_CanBeCreated()
    {
        Debug.Log("\n=== Test: BasicAgent_CanBeCreated ===");
        
        // Create a simple agent
        var go = new GameObject("TestAgent");
        var agent = go.AddComponent<TestAgent>();
        
        Assert.IsNotNull(agent, "Agent component should be created");
        Assert.IsTrue(agent.enabled, "Agent should be enabled");
        
        // Clean up
        if (Application.isPlaying)
        {
            Object.Destroy(go);
        }
        else
        {
            Object.DestroyImmediate(go);
        }
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator RLCommanderAgent_InitializesCorrectly()
    {
        Debug.Log("\n=== Test: RLCommanderAgent_InitializesCorrectly ===");
        
        // Create arena and ship
        var arenaGO = new GameObject("TestArena");
        var shipGO = new GameObject("TestShip");
        shipGO.transform.SetParent(arenaGO.transform);
        
        // Add required components
        shipGO.AddComponent<Rigidbody>();
        var ship = shipGO.AddComponent<Ship>();
        var movement = shipGO.AddComponent<ShipMovement>();
        var damageHandler = shipGO.AddComponent<ShipDamageHandler>();
        var agent = shipGO.AddComponent<RLCommanderAgent>();
        
        // Let initialization happen
        yield return null;
        
        // Verify
        Assert.IsNotNull(agent, "RLCommanderAgent should be created");
        Assert.AreEqual(0, agent.GetCumulativeReward(), "Initial reward should be 0");
        Assert.IsFalse(agent.IsPaused, "Agent should not be paused initially");
        
        // Test pause functionality - since we removed AddReward override, 
        // we need to test through the event handlers
        agent.IsPaused = true;
        
        // Try to trigger reward through health change (which should be blocked)
        var initialReward = agent.GetCumulativeReward();
        agent.OnHealthChanged(50f, 100f, 100f); // This should be blocked
        Assert.AreEqual(initialReward, agent.GetCumulativeReward(), "Reward should not change when paused via health event");
        
        agent.IsPaused = false;
        agent.OnHealthChanged(50f, 100f, 100f); // This should work
        Assert.AreNotEqual(initialReward, agent.GetCumulativeReward(), "Reward should change when not paused");
        
        // Clean up
        if (Application.isPlaying)
        {
            Object.Destroy(arenaGO);
        }
        else
        {
            Object.DestroyImmediate(arenaGO);
        }
        yield return null;
    }
}

/// <summary>
/// Minimal test agent for verifying basic ML-Agents functionality
/// </summary>
public class TestAgent : Agent
{
    public override void OnEpisodeBegin()
    {
        Debug.Log("TestAgent: OnEpisodeBegin called");
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(0f);
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Do nothing
    }
} 