using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using UnityEngine;
using ShipControl;
using System.Collections.Generic; // Required for List<T> used in arena instance
#if UNITY_EDITOR
using UnityEditor; // Required for OnDrawGizmos
#endif

/// <summary>
/// A reinforcement-learning agent for piloting a player ship.
/// Handles action processing and observation collection.
/// Training rewards and episode management are handled by ArenaInstance.
/// </summary>
public class RLCommanderAgent : Agent, IShipCommandSource
{
    // Static tracking for debug purposes
    public static int GlobalStepCount { get; private set; } = 0;
    
    [Header("Agent Settings")]
    [Tooltip("Priority for this commander's inputs. Lower values can be overridden.")]
    [SerializeField] private int commanderPriority = 100;
    [Header("Observation Settings")]
    public float sensingRange = 100f;
    public float maxSpeed = 20f;
    public float maxYawRate = 180f;

    // Non-allocating buffer for physics queries, accessed by RLObserver
    internal readonly Collider[] overlapColliders = new Collider[64];

    // --- Internal State ---
    public Ship ship { get; private set; }
    public IGameContext gameContext { get; private set; }
    private ShipCommand lastCommand;
    private RLObserver observer;
    private bool hasNewCommand;
    private IShipCommandSource fallbackCommander;

    // Debug toggle so we can render the observation overlay without changing build symbols
    [Header("Debug UI")]
    public bool ShowObservationUI = false;

    // Expose observer for external debug components
    public RLObserver Observer => observer;

    /// <summary>
    /// Helper property to check if the episode is active in the current game context.
    /// When the episode is not active, the agent will not process actions or observations.
    /// </summary>
    private bool IsEpisodeActive => gameContext == null || gameContext.IsActive;

    // --- IShipCommandSource properties ---
    public int Priority => commanderPriority;

    public int OnEpisodeBeginCount { get; private set; }

    public void InitializeCommander(Ship s)
    {
        this.ship = s;
    }

    public bool TryGetCommand(ShipState state, out ShipCommand command)
    {
        command = lastCommand;
        bool hadCmd = hasNewCommand;
        hasNewCommand = false; // Reset flag after command is read
        return hadCmd;
    }

    // --- Agent Lifecycle ---
    public override void Initialize()
    {
        base.Initialize();
        ship = GetComponent<Ship>();
        gameContext = GetComponentInParent<IGameContext>();
        observer = new RLObserver(this);

        if (ship == null) RLog.LogError("Agent is not attached to a Ship object.", this);
        //if (gameContext == null) RLog.LogError("RLCommanderAgent requires a parent IGameContext component.", this);

        // Detect if another IShipCommandSource (e.g., PlayerCommander) is attached for heuristic fallback
        foreach (var src in GetComponents<IShipCommandSource>())
        {
            if (src as Agent != this)
            {
                fallbackCommander = src;
                break;
            }
        }
        RLog.Log($"RLCommanderAgent: Initializing agent {name} with fallback commander {fallbackCommander}");

        // Ensure ML-Agents team ID matches the Ship's team number so both systems agree on alliances.
        var bp = GetComponent<BehaviorParameters>();
        if (bp != null && ship != null)
        {
            bp.TeamId = ship.teamNumber;
        }
    }

    public override void OnEpisodeBegin()
    {
        OnEpisodeBeginCount++;
        lastCommand = default;
        hasNewCommand = false;
        hasNewCommand = true;
        
        RLog.Log($"[Ep.{OnEpisodeBeginCount}] Agent {name}: Episode Begin");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!IsEpisodeActive) return;
        observer.CollectObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        GlobalStepCount++;

        if (!IsEpisodeActive)
        {
            lastCommand = default;
            hasNewCommand = true;
            return;
        }

        var continuousActions = actions.ContinuousActions;
        lastCommand.Thrust = continuousActions[0];
        lastCommand.Strafe = continuousActions[1];
        lastCommand.RotateToTarget = false;
        lastCommand.TargetAngle = 0;
        lastCommand.YawRate = continuousActions[2];

        var discreteActions = actions.DiscreteActions;
        lastCommand.PrimaryFire = discreteActions[0] > 0;
        lastCommand.SecondaryFire = discreteActions[1] > 0;

        hasNewCommand = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!IsEpisodeActive) return;

        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;
        RLog.Log($"RLCommanderAgent: Heuristic called for agent {name} with fallback commander {fallbackCommander}");
        if (fallbackCommander != null && fallbackCommander.TryGetCommand(ship.CurrentState, out ShipCommand cmd))
        {
            // Map ShipCommand to action buffers
            continuousActions[0] = cmd.Thrust;
            continuousActions[1] = cmd.Strafe;

            if (cmd.YawRate != 0f)
            {
                continuousActions[2] = cmd.YawRate;
            }
            else
            {
                continuousActions[2] = cmd.RotateToTarget ? cmd.TargetAngle / 180f : 0f;
            }

            discreteActions[0] = cmd.PrimaryFire ? 1 : 0;
            discreteActions[1] = cmd.SecondaryFire ? 1 : 0;
        }
        else
        {
            // Manual fallback using raw input for quick testing
            continuousActions[0] = Input.GetAxis("Vertical");
            continuousActions[1] = Input.GetAxis("Horizontal");
            continuousActions[2] = 0f;
            discreteActions[0] = 0;
            discreteActions[1] = 0;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !ShowObservationUI) return;

        if (!IsEpisodeActive)
        {
            var initialColor = Handles.color;
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(transform.position, transform.up, 2.5f);
            Handles.color = initialColor;
        }
        
        GUIStyle style = new GUIStyle();
        float reward = GetCumulativeReward();

        // Color goes from red -> white -> green
        if (reward >= 0)
        {
            // Assuming +5 is a good reward for full green
            style.normal.textColor = Color.Lerp(Color.white, Color.green, Mathf.Clamp01(reward / 5f));
        }
        else
        {
            // Assuming -5 is a bad reward for full red
            style.normal.textColor = Color.Lerp(Color.white, Color.red, Mathf.Clamp01(reward / -5f));
        }

        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.UpperCenter;

        Handles.Label(transform.position + Vector3.up * 2.5f, $"[Ep.{OnEpisodeBeginCount}] Reward: {reward:F2}", style);
    }
#endif
}
