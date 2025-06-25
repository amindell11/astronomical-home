using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using UnityEngine;
using ShipControl;

public class RLCommanderAgent : Agent, IShipCommandSource
{
    // Static tracking for debug purposes
    public static int GlobalStepCount { get; private set; } = 0;
    
    [Header("Agent Settings")]
    [Tooltip("Priority for this commander's inputs. Lower values can be overridden.")]
    [SerializeField] private int commanderPriority = 0; // Default to low priority

    [Header("Observation Settings")]
    public float sensingRange = 100f;
    public float maxSpeed = 20f;
    public float maxYawRate = 180f;

    // Non-allocating buffer for physics queries, accessed by RLObserver
    internal readonly Collider[] overlapColliders = new Collider[64];

    // --- Internal State ---
    public Ship ship { get; private set; }
    public ArenaInstance arenaInstance { get; private set; }
    private ShipCommand lastCommand;
    private RLObserver observer;
    private bool hasNewCommand;
    private IShipCommandSource fallbackCommander;
    
    // Debug toggle so we can render the observation overlay without changing build symbols
    [Header("Debug UI")]
    public bool ShowObservationUI = false;
    
    // Expose observer for external debug components
    public RLObserver Observer => observer;
    
    // --- IShipCommandSource properties ---
    public int Priority => commanderPriority;
    public bool IsPaused { get; set; } = false;

    public void InitializeCommander(Ship s)
    {
        this.ship = s;
        s.OnHealthChanged += OnHealthChanged;
        s.OnShieldChanged += OnShieldChanged;
        s.damageHandler.OnDeath += OnDeath;
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
        arenaInstance = GetComponentInParent<ArenaInstance>();
        observer = new RLObserver(this);

        if (ship == null) RLog.LogError("Agent is not attached to a Ship object.", this);
        if (arenaInstance == null) RLog.LogError("RLCommanderAgent requires a parent ArenaInstance component.", this);

        // Subscribe to global events
        Ship.OnGlobalShipDestroyed += HandleShipDestroyed;
        Ship.OnGlobalShipDamaged += HandleShipDamaged;

        // Detect if another IShipCommandSource (e.g., PlayerCommander) is attached for heuristic fallback
        foreach (var src in GetComponents<IShipCommandSource>())
        {
            if (src != this)
            {
                fallbackCommander = src;
                break;
            }
        }

        // Ensure ML-Agents team ID matches the Ship's team number so both systems agree on alliances.
        var bp = GetComponent<BehaviorParameters>();
        if (bp != null && ship != null)
        {
            bp.TeamId = ship.teamNumber;
        }
    }

    public override void OnEpisodeBegin()
    {
        lastCommand = default;
        hasNewCommand = false;
        IsPaused = false; // Ensure agent is unpaused for new episode
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        observer.CollectObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        GlobalStepCount++;
        
        if (IsPaused)
        {
            lastCommand = default;
            hasNewCommand = true;
            return;
        }

        var continuousActions = actions.ContinuousActions;
        lastCommand.Thrust = continuousActions[0];
        lastCommand.Strafe = continuousActions[1];
        lastCommand.RotateToTarget = continuousActions[2] > 0f;
        lastCommand.TargetAngle = continuousActions[2] * 180f;

        var discreteActions = actions.DiscreteActions;
        lastCommand.PrimaryFire = discreteActions[0] > 0;
        lastCommand.SecondaryFire = discreteActions[1] > 0;

        hasNewCommand = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions   = actionsOut.DiscreteActions;

        if (fallbackCommander != null && fallbackCommander.TryGetCommand(ship.CurrentState, out ShipCommand cmd))
        {
            // Map ShipCommand to action buffers
            continuousActions[0] = cmd.Thrust;
            continuousActions[1] = cmd.Strafe;
            continuousActions[2] = cmd.RotateToTarget ? cmd.TargetAngle / 180f : 0f;

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

    void OnDestroy()
    {
        Ship.OnGlobalShipDestroyed -= HandleShipDestroyed;
        Ship.OnGlobalShipDamaged -= HandleShipDamaged;
    }

    #region Reward Logic

    private float prevHealth;
    private float prevShield;

    private void OnHealthChanged(float current, float previous, float max)
    {
        float healthDelta = current - previous;
        AddReward(healthDelta / max); // Small reward/penalty for health changes
        prevHealth = current;
    }

    private void OnShieldChanged(float current, float previous, float max)
    {
        float shieldDelta = current - previous;
        // Shield damage is less critical than hull damage, so smaller reward factor
        AddReward(shieldDelta / max * 0.5f); 
        prevShield = current;
    }

    private void HandleShipDamaged(Ship victim, Ship attacker, float damage)
    {
        if (attacker == this.ship && victim != this.ship && !victim.IsFriendly(this.ship))
        {
            // Reward for damaging a non-friendly ship
            AddReward(damage / victim.settings.maxHealth * 0.2f); 
        }
    }

    private void HandleShipDestroyed(Ship victim, Ship killer)
    {
        if (victim == this.ship)
        {
            // Penalty for being destroyed
            SetReward(-1.0f);
            EndEpisode();
        }
        else if (killer == this.ship)
        {
            // Reward for destroying a non-friendly ship
            if (victim != null && !victim.IsFriendly(this.ship))
            {
                SetReward(1.0f);
                EndEpisode();
            }
            else // Penalty for friendly fire
            {
                SetReward(-1.0f);
                EndEpisode();
            }
        }
        else if (victim != null && victim.IsFriendly(this.ship) && killer != null && !killer.IsFriendly(this.ship))
        {
                // Penalty for letting a teammate be destroyed
                AddReward(-0.5f);
        }
    }

    // Called by ArenaInstance when ship exits bounds
    public void OnOutOfBounds()
    {
        SetReward(-0.75f); // Penalty for going out of bounds
        // Trigger arena-wide reset, not just agent episode end
        arenaInstance?.RequestEpisodeEnd();
    }
    
    private void OnDeath(Ship ship)
    {
        // This is a backup to the Global event, in case it's needed.
        // The global handler is preferred as it contains killer info.
    }

    #endregion
}
