using ShipControl;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A reinforcement-learning agent for piloting a player ship.
/// It learns to avoid asteroids and defeat enemy ships based on the plan in RLAgentPlan.md.
/// </summary>
public class RLCommanderAgent : Agent, IShipCommandSource
{
    [Header("Agent Configuration")]
    [SerializeField]
    private int commanderPriority = 100;

    [Header("Observations")]
    [SerializeField]
    private float maxSpeed = 15f;
    [SerializeField]
    private float maxYawRate = 180f;
    [SerializeField]
    private float sensingRange = 100f;

    private ShipCommand lastCommand;
    private ShipState currentState;
    private Ship ship;
    private ArenaInstance arenaInstance;
    private bool hasNewCommand = false;
    private float heuristicTargetAngle;
    private PlayerShipInput playerCommander;

    /// <summary>
    /// When paused, the agent will not process actions or rewards.
    /// </summary>
    public bool IsPaused { get; set; } = false;

    #region IShipCommandSource

    public int Priority => commanderPriority;

    public void InitializeCommander(Ship controlledShip)
    {
        this.ship = controlledShip;
        ship.OnHealthChanged += OnHealthChanged;
        ship.OnShieldChanged += OnShieldChanged;
        ship.damageHandler.OnDeath += OnDeath;
    }

    public bool TryGetCommand(ShipState state, out ShipCommand cmd)
    {   
        currentState = state;
        if (hasNewCommand)
        {
            cmd = this.lastCommand;
            hasNewCommand = false;
            return true;
        }

        cmd = default;
        return false;
    }

    #endregion

    #region Agent Lifecycle

    public override void Initialize()
    {
        base.Initialize();
        playerCommander = GetComponent<PlayerShipInput>();
        arenaInstance = GetComponentInParent<ArenaInstance>();
        if (arenaInstance == null)
        {
            RLog.LogError("RLCommanderAgent requires a parent ArenaInstance component.", this);
        }
        Ship.OnGlobalShipDestroyed += HandleShipDestroyed;
        Ship.OnGlobalShipDamaged += HandleShipDamaged;
    }

    private void OnDestroy()
    {
        Ship.OnGlobalShipDestroyed -= HandleShipDestroyed;
        Ship.OnGlobalShipDamaged -= HandleShipDamaged;
    }

#if UNITY_EDITOR
    public int OnEpisodeBeginCount { get; private set; }
#endif

    public override void OnEpisodeBegin()
    {
#if UNITY_EDITOR
        OnEpisodeBeginCount++;
#endif
        RLog.Log($"RLCommanderAgent {name}: OnEpisodeBegin. IsPaused: {IsPaused}");
        // The agent is un-paused at the beginning of each new episode.
        IsPaused = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (IsPaused) return;
        // Kinematics
        sensor.AddObservation(currentState.Kinematics.Speed / maxSpeed);
        sensor.AddObservation(currentState.Kinematics.YawRate / maxYawRate);

        // TODO: Get real ship health/shield state
        sensor.AddObservation(currentState.HealthPct); // HealthPct
        sensor.AddObservation(currentState.ShieldPct); // ShieldPct

        // Environmental awareness
        // NOTE: The RLAgentPlan.md has a discrepancy, listing 8 total observations
        // but the items sum to 12. I'm implementing all specified observations.
        var closestEnemy = FindClosestTarget("Enemy");
        var closestAsteroid = FindClosestTarget("Asteroid");

        AddTargetObservations(sensor, closestEnemy);
        AddTargetObservations(sensor, closestAsteroid);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Do not process actions or apply rewards if the agent is paused.
        if (IsPaused) { return; }

        // Translate actions into a ship command
        var continuousActions = actions.ContinuousActions;
        lastCommand.Thrust = continuousActions[0];
        lastCommand.Strafe = continuousActions[1];
        
        // The plan specifies a continuous yaw RATE, but ShipCommand takes a target ANGLE.
        // We are interpreting action[2] from [-1, 1] as a target angle in [0, 360].
        lastCommand.RotateToTarget = true;
        lastCommand.TargetAngle = (continuousActions[2] + 1f) * 180f;

        var discreteActions = actions.DiscreteActions;
        lastCommand.PrimaryFire = discreteActions[0] == 1;
        lastCommand.SecondaryFire = discreteActions[1] == 1;

        hasNewCommand = true;
        
        // Small penalty for existing to encourage finishing the episode.
        AddReward(-0.0001f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (IsPaused) return;
        if (playerCommander == null)
        {
            RLog.LogError("PlayerCommander (PlayerShipInput) component not found. Heuristics will not work.", this);
            return;
        }

        // Request command from the player input source
        playerCommander.TryGetCommand(currentState, out ShipCommand playerCommand);
        
        // Convert player command to agent actions
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = playerCommand.Thrust;
        continuousActions[1] = playerCommand.Strafe;

        // The agent action space for rotation is a continuous value [-1, 1] mapped to a target angle [0, 360].
        // We need to convert the player's target angle back into this action space.
        if (playerCommand.RotateToTarget)
        {
            // Map angle [0, 360] to action [-1, 1]
            continuousActions[2] = (playerCommand.TargetAngle / 180f) - 1f;
        }
        else
        {
            // If not actively rotating, hold the current angle.
            float currentAngle = currentState.Kinematics.AngleDeg;
            continuousActions[2] = (currentAngle / 180f) - 1f;
        }

        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = playerCommand.PrimaryFire ? 1 : 0;
        discreteActions[1] = playerCommand.SecondaryFire ? 1 : 0;
    }
    
    #endregion

    #region Helper Methods

    private void AddTargetObservations(VectorSensor sensor, Transform target)
    {
        if (target != null)
        {
            var vectorToTarget = target.position - transform.position;
            var localDirToTarget = transform.InverseTransformDirection(vectorToTarget.normalized);
            sensor.AddObservation(localDirToTarget);
            sensor.AddObservation(vectorToTarget.magnitude / sensingRange);
        }
        else
        {
            // Add zero observations if no target is found
            sensor.AddObservation(Vector3.zero); // local direction
            sensor.AddObservation(0f);           // distance
        }
    }

    private Transform FindClosestTarget(string tag)
    {
        // TODO: Implement actual logic to find the closest game object with the given tag.
        return null;
    }
    public void OnHealthChanged(float current, float previous, float maxHealth)
    {
        if (IsPaused) return;
        float healthDelta = current - previous;
        if (healthDelta < 0)
        {
            // Punish for taking damage
            AddReward(healthDelta / maxHealth);
        }
    }
    public void OnShieldChanged(float current, float previous, float maxShield)
    {
        if (IsPaused) return;
        float shieldDelta = current - previous;
        // Reward shield changes at a lower rate than health changes
        AddReward(shieldDelta / maxShield * 0.5f);
    }
    public void OnDeath(Ship ship)
    {
        if (IsPaused) return;
        AddReward(-1f);
        arenaInstance?.RequestEpisodeEnd();
    }

    private void HandleShipDestroyed(Ship victim, Ship killer)
    {
        if (IsPaused) return;
        if (killer == ship)
        {
            if(ship.IsFriendly(victim))
            {
                AddReward(-0.75f);
            }
            else
            {
                AddReward(5.0f);
                CheckForWinCondition();
            }
        }
    }

    private void HandleShipDamaged(Ship victim, Ship attacker, float damage)
    {
        if (IsPaused) return;
        if (attacker == ship)
        {
            if (ship.IsFriendly(victim))
            {
                // Penalize friendly fire damage
                AddReward(-damage / (victim.damageHandler.maxHealth + victim.damageHandler.maxShield) * 0.1f);
            }
            else
            {
                // Reward enemy damage - scale by max health to normalize across different ship types
                AddReward(damage / (victim.damageHandler.maxHealth + victim.damageHandler.maxShield) * 0.5f);
            }
        }
    }

    private void CheckForWinCondition()
    {
        if (IsPaused) return;
        if (arenaInstance == null) return;

        bool enemiesRemain = false;
        foreach (var otherShip in arenaInstance.ships)
        {
            if (otherShip == null || otherShip == this.ship || !otherShip.gameObject.activeInHierarchy)
            {
                continue; // Ignore self, destroyed, or inactive ships
            }

            if (!ship.IsFriendly(otherShip))
            {
                enemiesRemain = true;
                break; // Found an active enemy, no need to check further
            }
        }

        if (!enemiesRemain)
        {
            // Victory condition: all enemies are destroyed
            arenaInstance.RequestEpisodeEnd();
        }
    }
    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

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
            // Assuming -1 is a bad reward for full red
            style.normal.textColor = Color.Lerp(Color.white, Color.red, Mathf.Clamp01(-reward));
        }
        
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.UpperCenter;

        Handles.Label(transform.position + Vector3.up * 2.5f, $"Reward: {reward:F2}", style);
    }
#endif
} 