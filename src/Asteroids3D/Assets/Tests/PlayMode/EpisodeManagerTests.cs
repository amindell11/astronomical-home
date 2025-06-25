using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.TestTools;

public class EpisodeManagerTests
{
    private ArenaInstance arena;
    private RLCommanderAgent agent1;
    private RLCommanderAgent agent2;
    private Ship ship1;
    private Ship ship2;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Debug.Log("=== EpisodeManagerTests SetUp Starting ===");
        
        // 1. Create Arena
        var arenaGO = new GameObject("TestArena");
        var collider = arenaGO.AddComponent<SphereCollider>();
        collider.radius = 100f;
        collider.isTrigger = true;

        // 2. Create Ships with Agents and parent them
        var shipGO1 = CreateTestShip("Ship1", arenaGO.transform);
        var shipGO2 = CreateTestShip("Ship2", arenaGO.transform);
        
        Debug.Log($"Created ships: {shipGO1.name}, {shipGO2.name}");
        
        // Add ArenaInstance AFTER children are created so Awake() can find them.
        arena = arenaGO.AddComponent<ArenaInstance>();
        
        // Configure arena for test - use reflection to access private fields
        var resetDelayField = typeof(ArenaInstance).GetField("resetDelay", BindingFlags.NonPublic | BindingFlags.Instance);
        resetDelayField?.SetValue(arena, 0f);
        var enableResetField = typeof(ArenaInstance).GetField("enableArenaReset", BindingFlags.NonPublic | BindingFlags.Instance);
        enableResetField?.SetValue(arena, true);
        var debugLogsField = typeof(ArenaInstance).GetField("enableDebugLogs", BindingFlags.NonPublic | BindingFlags.Instance);
        debugLogsField?.SetValue(arena, true);

        // Get references to components
        agent1 = shipGO1.GetComponent<RLCommanderAgent>();
        agent2 = shipGO2.GetComponent<RLCommanderAgent>();
        ship1 = shipGO1.GetComponent<Ship>();
        ship2 = shipGO2.GetComponent<Ship>();
        
        // Set different team numbers
        ship1.teamNumber = 0;
        ship2.teamNumber = 1;

        // Wait for Start() methods
        yield return null;
        yield return null; // Extra frame for safety
        
        Debug.Log($"Arena setup complete. Ships found: {arena.ships?.Length ?? 0}");
        Debug.Log("=== EpisodeManagerTests SetUp Complete ===");
    }

    private GameObject CreateTestShip(string name, Transform parent)
    {
        Debug.Log($"Creating test ship: {name}");
        
        var shipGO = new GameObject(name);
        shipGO.transform.SetParent(parent);
        shipGO.transform.position = parent.position + Random.insideUnitSphere * 10f;
        
        // Add required components in correct order
        var rb = shipGO.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false; // Ensure physics works
        
        var ship = shipGO.AddComponent<Ship>();
        var movement = shipGO.AddComponent<ShipMovement>();
        var damageHandler = shipGO.AddComponent<ShipDamageHandler>();
        var agent = shipGO.AddComponent<RLCommanderAgent>();
        var decisionRequester = shipGO.AddComponent<DecisionRequester>();
        
        // Configure decision requester
        decisionRequester.DecisionPeriod = 5;
        decisionRequester.TakeActionsBetweenDecisions = true;
        
        // Add collider for physics
        var collider = shipGO.AddComponent<SphereCollider>();
        collider.radius = 1f;

        Debug.Log($"Ship {name} created with components: Ship, Movement, DamageHandler, Agent");
        return shipGO;
    }

    [TearDown]
    public void TearDown()
    {
        Debug.Log("=== EpisodeManagerTests TearDown ===");
        if (Application.isPlaying)
        {
            Object.Destroy(arena.gameObject);
        }
        else
        {
            Object.DestroyImmediate(arena.gameObject);
        }
    }
    
    [UnityTest]
    public IEnumerator WhenOneAgentDies_AllAgentsInArena_StartNewEpisode()
    {
        Debug.Log("\n=== Test: WhenOneAgentDies_AllAgentsInArena_StartNewEpisode ===");
        
        // Arrange
        var initialEpisodeCountAgent1 = agent1.OnEpisodeBeginCount;
        var initialEpisodeCountAgent2 = agent2.OnEpisodeBeginCount;
        var initialArenaEpisodes = arena.EpisodeCount;
        
        Debug.Log($"Initial state - Agent1 episodes: {initialEpisodeCountAgent1}, Agent2 episodes: {initialEpisodeCountAgent2}, Arena episodes: {initialArenaEpisodes}");

        // Act
        Debug.Log("Simulating ship1 death...");
        ship1.damageHandler.TakeDamage(1000f, 1f, Vector3.zero, Vector3.zero, null);

        // Wait for death processing and arena reset
        yield return new WaitForSeconds(0.1f);
        yield return null;
        yield return null;

        // Assert
        Debug.Log($"Final state - Agent1 episodes: {agent1.OnEpisodeBeginCount}, Agent2 episodes: {agent2.OnEpisodeBeginCount}, Arena episodes: {arena.EpisodeCount}");
        
        Assert.AreEqual(initialEpisodeCountAgent1 + 1, agent1.OnEpisodeBeginCount, 
            $"Agent 1 should have started a new episode. Initial: {initialEpisodeCountAgent1}, Current: {agent1.OnEpisodeBeginCount}");
        Assert.AreEqual(initialEpisodeCountAgent2 + 1, agent2.OnEpisodeBeginCount, 
            $"Agent 2 should have started a new episode. Initial: {initialEpisodeCountAgent2}, Current: {agent2.OnEpisodeBeginCount}");
        Assert.AreEqual(initialArenaEpisodes + 1, arena.EpisodeCount, 
            $"Arena episode count should have incremented. Initial: {initialArenaEpisodes}, Current: {arena.EpisodeCount}");
    }
    
    [UnityTest]
    public IEnumerator AgentsArePaused_DuringArenaResetDelay()
    {
        Debug.Log("\n=== Test: AgentsArePaused_DuringArenaResetDelay ===");
        
        // Arrange - set a delay for this test
        var resetDelayField = typeof(ArenaInstance).GetField("resetDelay", BindingFlags.NonPublic | BindingFlags.Instance);
        resetDelayField?.SetValue(arena, 0.1f);

        // Act
        Debug.Log("Requesting episode end...");
        arena.RequestEpisodeEnd();

        // Assert: Immediately after request, agents should be paused.
        Debug.Log($"Immediately after request - Agent1 paused: {agent1.IsPaused}, Agent2 paused: {agent2.IsPaused}");
        Assert.IsTrue(agent1.IsPaused, "Agent 1 should be paused immediately.");
        Assert.IsTrue(agent2.IsPaused, "Agent 2 should be paused immediately.");
        
        // Wait for the reset to complete
        yield return new WaitForSeconds(0.2f);
        
        // Assert: After reset, agents should be un-paused.
        Debug.Log($"After reset delay - Agent1 paused: {agent1.IsPaused}, Agent2 paused: {agent2.IsPaused}");
        Assert.IsFalse(agent1.IsPaused, "Agent 1 should be un-paused after reset.");
        Assert.IsFalse(agent2.IsPaused, "Agent 2 should be un-paused after reset.");
    }

    [UnityTest]
    public IEnumerator AgentDoesNotAccumulateReward_WhenPaused()
    {
        Debug.Log("\n=== Test: AgentDoesNotAccumulateReward_WhenPaused ===");
        
        // Arrange
        agent1.SetReward(0);
        yield return null; // Let the reward system update
        
        var initialReward = agent1.GetCumulativeReward();
        Debug.Log($"Initial reward: {initialReward}");
        Assert.AreEqual(0, initialReward, "Pre-condition failed: Reward is not zero.");

        // Act
        Debug.Log("Pausing agent1...");
        agent1.IsPaused = true;
        
        // Try to add rewards through event handlers (which should be blocked)
        Debug.Log("Attempting to add rewards while paused through event handlers...");
        agent1.OnHealthChanged(50, 100, 100); // Health damage event - should be blocked
        agent1.OnShieldChanged(0, 50, 100); // Shield damage event - should be blocked
        
        // Note: Direct AddReward calls are no longer blocked since we removed the override
        // This test now focuses on event-driven rewards being properly gated
        
        // Assert
        var finalReward = agent1.GetCumulativeReward();
        Debug.Log($"Final reward after pause attempts: {finalReward}");
        Assert.AreEqual(initialReward, finalReward, 
            $"Agent accumulated reward while paused through event handlers. Initial: {initialReward}, Final: {finalReward}");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator EpisodeGate_PreventsDoubleReset()
    {
        Debug.Log("\n=== Test: EpisodeGate_PreventsDoubleReset ===");
        
        // Arrange
        var initialEpisodes = arena.EpisodeCount;
        Debug.Log($"Initial arena episodes: {initialEpisodes}");
        
        // Act - Try to trigger multiple resets rapidly
        Debug.Log("Attempting multiple rapid reset requests...");
        arena.RequestEpisodeEnd();
        arena.RequestEpisodeEnd();
        arena.RequestEpisodeEnd();
        
        // Wait for any resets to process
        yield return new WaitForSeconds(0.2f);
        
        // Assert - Should only increment by 1
        var finalEpisodes = arena.EpisodeCount;
        Debug.Log($"Final arena episodes: {finalEpisodes}");
        Assert.AreEqual(initialEpisodes + 1, finalEpisodes, 
            $"Episode count should only increment by 1. Initial: {initialEpisodes}, Final: {finalEpisodes}");
    }
    
    [UnityTest]
    public IEnumerator AgentRewards_WorkNormallyWhenNotPaused()
    {
        Debug.Log("\n=== Test: AgentRewards_WorkNormallyWhenNotPaused ===");
        
        // Arrange
        agent1.SetReward(0);
        yield return null;
        
        var initialReward = agent1.GetCumulativeReward();
        Debug.Log($"Initial reward: {initialReward}");
        
        // Act - Ensure not paused and add rewards through event handlers
        agent1.IsPaused = false;
        Debug.Log("Adding rewards while NOT paused through event handlers...");
        
        // Use event handlers to trigger rewards (damage = negative reward)
        agent1.OnHealthChanged(90, 100, 100); // Lost 10 health = negative reward
        agent1.OnShieldChanged(40, 50, 100);  // Lost 10 shield = negative reward
        
        // Assert
        var finalReward = agent1.GetCumulativeReward();
        Debug.Log($"Final reward: {finalReward}");
        Assert.Less(finalReward, initialReward,
            $"Rewards should be negative (damage) when not paused. Initial: {initialReward}, Final: {finalReward}");
        
        yield return null;
    }
} 