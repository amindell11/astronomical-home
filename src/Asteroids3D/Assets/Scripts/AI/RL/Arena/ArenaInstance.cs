using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;

/// <summary>
/// Stand-alone component that encapsulates all logic for a single training / gameplay arena.
///
/// Attach this to the root GameObject of an <b>Arena</b> prefab.  In normal gameplay you can
/// drop one Arena prefab into a scene and everything will work without an <see cref="ArenaManager"/>.
/// When running in batch-mode multi-arena training, <see cref="ArenaManager"/> will instantiate
/// multiple prefabs – each containing its own ArenaInstance.
///
/// Responsibilities
/// 1. Track and cache important child components (Ships, SectorFieldManager, ML Agents).
/// 2. Subscribe to <see cref="ShipDamageHandler.OnDeath"/> to trigger an episode reset.
/// 3. Provide public <see cref="ResetArena"/> API so external systems (e.g., ArenaManager) can
///    reset or iterate over arenas.
/// </summary>
public class ArenaInstance : MonoBehaviour
{
    [Header("Arena Reset Settings")]
    [Tooltip("Enable automatic arena reset functionality")]           
    [SerializeField] private bool enableArenaReset = true;
    [Tooltip("Delay (seconds) before the arena resets after a terminal event")]
    [SerializeField] private float resetDelay      = 1f;

    [Header("Ship Collection (optional)")]
    [Tooltip("If empty, ships are discovered automatically in children at runtime.")]
    [SerializeField] private Ship[] managedShips;

    [Header("Environment Settings")]
    [Tooltip("Default environment parameters. Can be overriden by ArenaManager or ML-Agents.")]
    [SerializeField] private ArenaSettings defaultEnvironmentSettings;
    
    // The settings resolved and used for the current episode.
    public ArenaSettings EffectiveSettings { get; private set; }
    
    // Manager can provide an override settings asset.
    private ArenaSettings _overrideSettings = null;

    [Header("Curriculum Bot")]
    [Tooltip("The non-RL agent ship to be enabled/disabled by the curriculum.")]
    [SerializeField] private Ship botShip;
    private AIShipInput botController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Gizmos")]
    [Tooltip("Show arena boundaries and episode count in Scene view")]
    [SerializeField] private bool showGizmos = true;
    [Tooltip("Size of the arena boundary gizmo")]
    [SerializeField] private float gizmoSize = 160f;

    [Header("Boundary Reset Settings")]
    [Tooltip("Reset the arena if a Ship exits the arena's trigger collider")]
    [SerializeField] private bool resetOnShipExit = true;

    [Header("Spawn Protection")]
    [Tooltip("Duration (seconds) of temporary invulnerability applied to ships right after spawning/reset")] 
    [SerializeField] private float spawnInvulnerabilityDuration = 2f;

    // --------------------------- Cached references ---------------------------
    [System.NonSerialized] public Ship[] ships; // exposed for convenience (read-only)
    [System.NonSerialized] public SectorFieldManager fieldManager;
    [System.NonSerialized] private SphereCollider boundaryCollider;
    [System.NonSerialized] public Agent[] mlAgents;

    // --- Private State ---
    private bool _episodeActive = true; // Gate to prevent double-ending an episode.

    // Events so external systems can respond to lifecycle changes.
    public System.Action<ArenaInstance> OnArenaReset;
    
    // Episode tracking and visual feedback
    private int episodeCount = 0;
    private float flashTimer = 0f;
    private const float flashDuration = 1f;

    // -----------------------------------------------------------------------
    void Awake()
    {
        // Cache key components in children (or supplied via inspector).
        ships        = (managedShips != null && managedShips.Length > 0) ? managedShips : GetComponentsInChildren<Ship>(true);
        fieldManager = GetComponentInChildren<SectorFieldManager>(true);
        boundaryCollider = GetComponent<SphereCollider>();
        mlAgents     = GetComponentsInChildren<Agent>();
        
        if (botShip != null)
        {
            botController = botShip.GetComponent<AIShipInput>();
        }
        
        // Create boundary collider if it doesn't exist
        if (boundaryCollider == null)
        {
            boundaryCollider = gameObject.AddComponent<SphereCollider>();
            boundaryCollider.isTrigger = true;
        }

        if (enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Awake – cached {ships.Length} ship(s) and field manager {(fieldManager ? fieldManager.gameObject.name : "<none>")}.\n");
        }
    }

    void Start()
    {
        // Apply arena size settings
        ResolveAndApplySettings();

        // Ensure field manager anchor points at this arena root so density checks use local centre.
        if (fieldManager != null)
        {
            fieldManager.SetAnchor(transform);
        }

        // Subscribe to each ship death so we can reset the arena once any ship is destroyed.
        foreach (var ship in ships)
        {
            ship.OnDeath += OnShipDeath;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe – good hygiene.
        foreach (var ship in ships)
        {
            ship.OnDeath -= OnShipDeath;
        }
    }

    void Update()
    {
        // Update flash timer for gizmo visualization
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
        }
    }

    private void OnShipDeath(Ship victim, Ship killer)
    {
        if (enableDebugLogs)
        {
            RLog.Log($"Ep.{episodeCount} ArenaInstance: Ship death. Victim: {victim?.name}, Killer: {killer?.name} applying rewards {1.0f} to killer and {-1.0f} to victim");
        }
        // Apply rewards
        if (killer != null)
        {
            var killerAgent = killer.GetComponent<RLCommanderAgent>();
            if (killerAgent != null)
            {
                if (enableDebugLogs)
                {
                    RLog.Log($"Killer agent cumulative reward before SetReward: {killerAgent.GetCumulativeReward()}");
                }
                killerAgent.SetReward(1.0f);
                if (enableDebugLogs)
                {
                    RLog.Log($"Killer agent cumulative reward after SetReward: {killerAgent.GetCumulativeReward()}");
                }
            }
        }

        if (victim != null)
        {
            var victimAgent = victim.GetComponent<RLCommanderAgent>();
            if (victimAgent != null)
            {
                if (enableDebugLogs)
                {
                    RLog.Log($"Victim agent cumulative reward before SetReward: {victimAgent.GetCumulativeReward()}");
                }
                victimAgent.SetReward(-1.0f);
                if (enableDebugLogs)
                {
                    RLog.Log($"Victim agent cumulative reward after SetReward: {victimAgent.GetCumulativeReward()}");
                }
            }
        }
        RequestEpisodeEnd();
    }

    private void ResolveAndApplySettings()
    {
        // Create a temporary, modifiable instance of settings for this episode.
        // This prevents runtime changes from saving to the ScriptableObject assets.
        EffectiveSettings = Instantiate(defaultEnvironmentSettings);

        // If manager has provided an override, copy its values.
        if (_overrideSettings != null)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_overrideSettings), EffectiveSettings);
        }

        // Allow ML-Agents to override any parameter.
        if (Academy.IsInitialized)
        {
            var envParams = Academy.Instance.EnvironmentParameters;
            EffectiveSettings.arenaSize = envParams.GetWithDefault("arena_size", EffectiveSettings.arenaSize);
            EffectiveSettings.asteroidDensity = envParams.GetWithDefault("asteroid_density", EffectiveSettings.asteroidDensity);
            EffectiveSettings.botDifficulty = envParams.GetWithDefault("bot_difficulty", EffectiveSettings.botDifficulty);
        }
        
        // --- Apply all settings ---
        if (boundaryCollider != null)
        {
            boundaryCollider.radius = EffectiveSettings.arenaSize;
        }
        if (fieldManager != null)
        {
            fieldManager.SetFieldSize(EffectiveSettings.arenaSize);
            fieldManager.TargetDensity = Mathf.Max(0f, EffectiveSettings.asteroidDensity);
        }

        gizmoSize = EffectiveSettings.arenaSize * 2f;
        
        if (botController != null)
        {
            botController.difficulty = EffectiveSettings.botDifficulty;
        }
        
        if (enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Applied Environment Settings. Arena Size: {EffectiveSettings.arenaSize}, Asteroid Density: {EffectiveSettings.asteroidDensity}, Bot Difficulty: {EffectiveSettings.botDifficulty}");
        }
    }

    /// <summary>
    /// Public entry-point to reset this arena.
    /// This is the safe, gated way to end the current episode and start a new one.
    /// </summary>
    public void ResetArena()
    {
        RequestEpisodeEnd();
    }

    /// <summary>
    /// Public entry-point for any agent or system to request the end of the current episode.
    /// Ensures the episode is only ended once per cycle.
    /// </summary>
    public void RequestEpisodeEnd()
    {
        if(enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Requesting episode end. Episode active: {_episodeActive}, enableArenaReset: {enableArenaReset}");
        }
        if (!_episodeActive || !enableArenaReset) return;
        _episodeActive = false; // Close the gate until the next episode begins.

        // Immediately pause agents to stop them from accumulating rewards on stale data.
        SetAgentsPaused(true);

        StartCoroutine(ResetArenaCoroutine());
    }

    private IEnumerator ResetArenaCoroutine()
    {
        // Increment episode count and trigger flash effect
        episodeCount++;
        flashTimer = flashDuration;
        OnArenaReset?.Invoke(this);
        
        // Apply new environment parameters for the upcoming episode.
        ResolveAndApplySettings();

        if (enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Starting reset after {resetDelay}s delay. Episode: {episodeCount}");
        }

        if (resetDelay > 0f)
            yield return new WaitForSeconds(resetDelay);

        // 1. Respawn / clear asteroids through the field manager.
        if (fieldManager != null)
        {
            fieldManager.RespawnAsteroids();
            fieldManager.SetAnchor(transform);
        }

        // 2. Reset ships (physics, health, position, rotation).
        ResetShips();

        // 3. Inform ML Agents that a new episode has begun.
        // Their OnEpisodeBegin() will un-pause them.
        SignalAgentsEpisodeEnd();

        // 4. Re-arm the gate, allowing the new episode to be terminated.
        _episodeActive = true;

        if (enableDebugLogs)
        {
            RLog.Log("ArenaInstance: Reset complete.");
        }
    }

    // ---------------------- Helper implementation ---------------------------
    void ResetShips()
    {
        foreach (var ship in ships)
        {
            if (ship == null) continue;
            // Reactivate in case it was disabled on death.
            ship.gameObject.SetActive(true);
            
            var movement      = ship.GetComponent<ShipMovement>();
            var damageHandler = ship.GetComponent<ShipDamageHandler>();

            movement?.ResetShip();
            damageHandler?.ResetAll();

            // Apply temporary spawn invulnerability so immediate asteroid collisions do not damage the ship.
            damageHandler?.SetInvulnerability(spawnInvulnerabilityDuration);

            // Place ship in a random position near the arena centre.
            Vector3 randomOffset = Random.insideUnitCircle.normalized * 20f;
            randomOffset.z = randomOffset.y;
            randomOffset.y = 0f;
            ship.transform.position = transform.position + randomOffset;
            ship.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    void SignalAgentsEpisodeEnd()
    {       
        if(enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Signalling agents episode end. {mlAgents.Length} agents found.");
        }
        if (mlAgents == null || mlAgents.Length == 0) return;
        foreach (var agent in mlAgents)
        {
            if (enableDebugLogs)
            {
                RLog.Log($"ArenaInstance: Signalling agent {agent.name} episode end.");
            }
            agent?.EndEpisode();
        }
    }

    void SetAgentsPaused(bool paused)
    {
        if (mlAgents == null) return;
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent commander)
            {
                commander.IsPaused = paused;
            }
        }
    }

    // ----------------------------- Public API --------------------------------
    /// <summary>
    /// Returns the centre point of the arena (same as transform.position).
    /// </summary>
    public Vector3 CenterPosition => transform.position;

    /// <summary>
    /// Convenience property exposing the number of ships in this arena.
    /// </summary>
    public int ShipCount => ships != null ? ships.Length : 0;
    
    /// <summary>
    /// Current episode count for this arena.
    /// </summary>
    public int EpisodeCount => episodeCount;
    
    /// <summary>
    /// Current arena size (radius).
    /// </summary>
    public float ArenaSize => EffectiveSettings != null ? EffectiveSettings.arenaSize : 0f;
    
    /// <summary>
    /// Sets the override settings for this arena, typically called by ArenaManager.
    /// </summary>
    public void SetOverrideSettings(ArenaSettings settings)
    {
        _overrideSettings = settings;
    }

    public void HandleOutOfBounds(RLCommanderAgent agent)
    {
        agent.SetReward(-1.0f);
        RequestEpisodeEnd();
    }

    // -----------------------------------------------------------------------
    // Trigger callbacks ------------------------------------------------------
    private void OnTriggerExit(Collider other)
    {
        if (!resetOnShipExit) return;

        // Find a Ship component on the collider or its parents (handles multiple collider setups)
        Ship ship = other.GetComponent<Ship>() ?? other.GetComponentInParent<Ship>();
        if (ship == null) return;

        if (enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Ship '{ship.name}' exited arena bounds – triggering reset.");
        }

        var agent = ship.GetComponent<RLCommanderAgent>();
        if (agent != null)
        {
            // The agent is responsible for applying penalty and ending the episode.
            HandleOutOfBounds(agent);
        }
        else
        {
            // No agent on the ship, so the arena ends the episode directly.
            RequestEpisodeEnd();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Vector3 center = transform.position;
        
        // Determine gizmo color based on flash state
        Color gizmoColor;
        if (flashTimer > 0f)
        {
            // Flash between red and yellow during reset
            float flashIntensity = Mathf.PingPong(flashTimer * 8f, 1f);
            gizmoColor = Color.Lerp(Color.red, Color.yellow, flashIntensity);
        }
        else
        {
            // Normal state - cyan boundary
            gizmoColor = Color.cyan;
        }

        // Draw arena boundary
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, Vector3.one * gizmoSize);

        // Draw episode count text
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(center + Vector3.up * 10f, $"Episode: {episodeCount}\nGlobal Steps: {RLCommanderAgent.GlobalStepCount}");
    }
#endif
} 