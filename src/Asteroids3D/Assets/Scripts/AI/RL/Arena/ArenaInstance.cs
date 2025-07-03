using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
/// 2. Subscribe to ship events and manage all training rewards.
/// 3. Handle episode lifecycle and reset logic.
/// 4. Provide public <see cref="ResetArena"/> API so external systems (e.g., ArenaManager) can
///    reset or iterate over arenas.
/// </summary>
public class ArenaInstance : BaseGameContext
{
    [Header("Arena Reset Settings")]
    [Tooltip("Enable automatic arena reset functionality")]           
    [SerializeField] private bool enableArenaReset = true;
    [Tooltip("Delay (seconds) before the arena resets after a terminal event")]
    [SerializeField] private float resetDelay      = 1f;

    [Header("Ship Collection (optional)")]
    [Tooltip("If empty, ships are discovered automatically in children at runtime.")]
    [SerializeField] private Ship[] managedShipsOverride;

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
    private AICommander botController;

    [Header("Reward Settings")]
    [Tooltip("Small penalty applied each step to encourage finishing episodes quickly")]
    [SerializeField] private float existencePenalty = -0.0001f;
    [Tooltip("Reward/penalty multiplier for health damage (applied as: ±multiplier * damage / maxHealth)")]
    [SerializeField] private float healthRewardMultiplier = 0.2f;
    [Tooltip("Reward/penalty multiplier for shield damage (applied as: ±multiplier * damage / maxShield)")]
    [SerializeField] private float shieldRewardMultiplier = 0.1f;

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

    [Header("Hybrid Boundary System")]
    [Tooltip("Multiplier for soft boundary radius (R_soft = multiplier × arena_size)")]
    [SerializeField] private float softBoundaryMultiplier = 0.75f;
    [Tooltip("Multiplier for hard boundary radius (R_hard = multiplier × arena_size)")]
    [SerializeField] private float hardBoundaryMultiplier = 1.20f;
    [Tooltip("Coefficient for quadratic boundary penalty (tune 0.001-0.005)")]
    [SerializeField] private float boundaryPenaltyCoefficient = 0.002f;

    [Header("Spawn Protection")]
    [Tooltip("Duration (seconds) of temporary invulnerability applied to ships right after spawning/reset")] 
    [SerializeField] private float spawnInvulnerabilityDuration = 2f;

    [Header("Metrics Tracking")]
    [Tooltip("Enable TensorBoard metrics tracking")]
    [SerializeField] private bool enableMetricsTracking = true;

    // --------------------------- Cached references ---------------------------
    [System.NonSerialized] public Ship[] ships; // exposed for convenience (read-only)
    [System.NonSerialized] public SectorFieldManager fieldManager;
    [System.NonSerialized] private SphereCollider boundaryCollider;
    [System.NonSerialized] public Agent[] mlAgents;

    // --- Private State ---
    private bool _episodeActive = true; // Gate to prevent double-ending an episode.
    
    // Hybrid boundary system calculated radii
    private float _softBoundaryRadius;
    private float _hardBoundaryRadius;
    
    // --- Metrics Tracking ---
    private Dictionary<RLCommanderAgent, float> _damageDealtThisEpisode = new Dictionary<RLCommanderAgent, float>();
    private Dictionary<RLCommanderAgent, float> _damageTakenThisEpisode = new Dictionary<RLCommanderAgent, float>();
    private List<float> _distanceSamples = new List<float>();
    private int _episodesEndedInKills = 0;
    private int _totalEpisodesCompleted = 0;
    private bool _episodeEndedInKill = false;
    
    /// <summary>
    /// Indicates whether the current episode is active. When false, agents should not process actions or accumulate rewards.
    /// </summary>
    public bool IsEpisodeActive => _episodeActive;

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
        ships = (managedShipsOverride != null && managedShipsOverride.Length > 0) ? managedShipsOverride : GetComponentsInChildren<Ship>(true);
        
        // Ensure ships array is never null - initialize as empty array if needed
        if (ships == null)
        {
            ships = new Ship[0];
            if (enableDebugLogs)
                RLog.RLWarning($"ArenaInstance: No ships found in {gameObject.name}. Initialized empty ships array.");
        }
        
        // Update the base class managed ships to match our local cache
        base.managedShips = ships;
        
        fieldManager = GetComponentInChildren<SectorFieldManager>(true);
        boundaryCollider = GetComponent<SphereCollider>();
        mlAgents     = GetComponentsInChildren<Agent>();
        
        if (botShip != null)
        {
            botController = botShip.GetComponent<AICommander>();
        }
        
        // Create boundary collider if it doesn't exist
        if (boundaryCollider == null)
        {
            boundaryCollider = gameObject.AddComponent<SphereCollider>();
            boundaryCollider.isTrigger = true;
        }

        if (enableDebugLogs)
        {
            RLog.RL($"ArenaInstance: Awake – cached {ships.Length} ship(s) and field manager {(fieldManager ? fieldManager.gameObject.name : "<none>")}.\n");
        }
    }

    protected override void Start()
    {
        base.Start();
        // Apply arena size settings
        ResolveAndApplySettings();

        // Ensure field manager anchor points at this arena root so density checks use local centre.
        if (fieldManager != null)
        {
            fieldManager.SetAnchor(transform);
        }

        // Subscribe to ship events for reward administration
        foreach (var ship in ships)
        {
            ship.OnDeath += OnShipDeath;
            // Note: Health/Shield change rewards are now handled in HandleShipDamaged for symmetrical damage rewards
        }

        // Subscribe to global ship damage events for combat rewards
        Ship.OnGlobalShipDamaged += HandleShipDamaged;
    }

    void OnDestroy()
    {
        // Unsubscribe – good hygiene.
        foreach (var ship in ships)
        {
            ship.OnDeath -= OnShipDeath;
        }
        Ship.OnGlobalShipDamaged -= HandleShipDamaged;
    }

    void Update()
    {
        // Update flash timer for gizmo visualization
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
        }

        // Apply existence penalties and boundary checks to active agents each step
        if (_episodeActive)
        {
            ApplyExistencePenalties();
            CheckAgentBoundaries();
            
            // Track distance metrics every 10 frames to avoid performance impact
            if (enableMetricsTracking && Time.frameCount % 10 == 0)
            {
                TrackDistanceMetrics();
            }
        }
    }

    #region Reward Administration

    private void ApplyExistencePenalties()
    {
        if (mlAgents == null) return;
        
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent commander)
            {
                commander.AddReward(existencePenalty);
            }
        }
    }

    private void CheckAgentBoundaries()
    {
        if (mlAgents == null) return;
        
        Vector3 arenaCenter = transform.position;
        
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent commander && commander.ship != null)
            {
                float distanceFromCenter = Vector3.Distance(commander.ship.transform.position, arenaCenter);
                
                // Check if agent is beyond hard boundary - immediate episode end
                if (distanceFromCenter >= _hardBoundaryRadius)
                {
                    if (enableDebugLogs)
                    {
                        RLog.RL($"[Ep.{episodeCount}] Agent {commander.name} exceeded hard boundary at distance {distanceFromCenter:F1} (limit: {_hardBoundaryRadius:F1})");
                    }
                    
                    // Apply -1 penalty to violating agent
                    commander.AddReward(-1.0f - commander.GetCumulativeReward());
                    
                    // Apply +1 reward to opponent (zero-sum)
                    ApplyOpponentReward(commander, 1.0f - commander.GetCumulativeReward());
                    
                    // End episode
                    RequestEpisodeEnd();
                    return; // Exit early since episode is ending
                }
                
                // Check if agent is beyond soft boundary - apply quadratic penalty
                if (distanceFromCenter >= _softBoundaryRadius)
                {
                    float frac = (distanceFromCenter - _softBoundaryRadius) / (_hardBoundaryRadius - _softBoundaryRadius);
                    float penalty = -boundaryPenaltyCoefficient * frac * frac;
                    commander.AddReward(penalty);
                    
                    if (enableDebugLogs)
                    {
                        RLog.RL($"[Ep.{episodeCount}] Agent {commander.name}: Boundary penalty {penalty:F4} at distance {distanceFromCenter:F1} (soft: {_softBoundaryRadius:F1}, frac: {frac:F2})");
                    }
                }
            }
        }
    }

    private void ApplyOpponentReward(RLCommanderAgent violatingAgent, float reward)
    {
        if (mlAgents == null) return;
        
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent opponent && opponent != violatingAgent)
            {
                opponent.AddReward(reward);
                if (enableDebugLogs)
                {
                    RLog.RL($"[Ep.{episodeCount}] Opponent agent {opponent.name} received reward {reward:F1} due to boundary violation");
                }
                break; // Assuming 2-agent setup, reward the first opponent found
            }
        }
    }



    private void HandleShipDamaged(Ship victim, Ship attacker, float damage)
    {   
        if (!_episodeActive || victim == null) return;
        
        if (enableDebugLogs)
        {
            RLog.RL($"[Ep.{episodeCount}] ArenaInstance: Ship damaged. Victim: {victim.name}, Attacker: {attacker?.name}, Damage: {damage}");
        }
        
        var damageInfo = GetDamageInfo(victim, damage);
        
        // Track damage metrics for TensorBoard
        if (enableMetricsTracking)
        {
            TrackDamageMetrics(victim, attacker, damage);
        }
        
        // Apply penalty to victim (negative damage = penalty)
        ApplyDamageReward(victim, -damage, damageInfo, "Penalty");
        
        // Apply reward to attacker (positive damage = reward) if they're enemies
        if (attacker != null && !victim.IsFriendly(attacker))
        {
            ApplyDamageReward(attacker, damage, damageInfo, "Reward");
        }
    }

    private DamageInfo GetDamageInfo(Ship victim, float damage)
    {
        // Determine if damage hit shields or health based on victim's current shield level
        bool hitShields = victim.damageHandler.CurrentShield > 0f || 
                         victim.damageHandler.CurrentShield + damage > victim.damageHandler.maxShield;
        
        return new DamageInfo
        {
            HitShields = hitShields,
            MaxCapacity = hitShields ? victim.settings.maxShield : victim.settings.maxHealth,
            RewardMultiplier = hitShields ? shieldRewardMultiplier : healthRewardMultiplier,
            DamageType = hitShields ? "Shield" : "Health"
        };
    }

    private void ApplyDamageReward(Ship ship, float damageAmount, DamageInfo damageInfo, string rewardType)
    {
        var agent = GetActiveAgent(ship);
        if (agent == null) return;
        
        float reward = damageInfo.RewardMultiplier * damageAmount / damageInfo.MaxCapacity;
        agent.AddReward(reward);
        
        if (enableDebugLogs)
        {
            RLog.RL($"[Ep.{episodeCount}] Agent {agent.name}: {damageInfo.DamageType} Damage {rewardType}: {reward:F3} (damage: {Mathf.Abs(damageAmount):F1})");
        }
    }

    private RLCommanderAgent GetActiveAgent(Ship ship)
    {
        if (ship == null) return null;
        
        var agent = ship.GetComponent<RLCommanderAgent>();
        return agent; // Episode activity is now controlled at the arena level
    }

    private struct DamageInfo
    {
        public bool HitShields;
        public float MaxCapacity;
        public float RewardMultiplier;
        public string DamageType;
    }

    #endregion

    private void OnShipDeath(Ship victim, Ship killer)
    {
        if (enableDebugLogs)
        {
            RLog.RL($"Ep.{episodeCount} ArenaInstance: Ship death. Victim: {victim?.name}, Killer: {killer?.name} applying rewards {1.0f} to killer and {-1.0f} to victim");
        }
        
        // Track kill metrics for TensorBoard
        if (enableMetricsTracking && killer != null)
        {
            _episodeEndedInKill = true;
        }
        
        // Apply rewards
        if (killer != null)
        {
            var killerAgent = killer.GetComponent<RLCommanderAgent>();
            if (killerAgent != null)
            {
                if (enableDebugLogs)
                {
                    RLog.RL($"Killer agent cumulative reward before SetReward: {killerAgent.GetCumulativeReward()}");
                }
                killerAgent.AddReward(1.0f  - killerAgent.GetCumulativeReward());
                if (enableDebugLogs)
                {
                    RLog.RL($"Killer agent cumulative reward after SetReward: {killerAgent.GetCumulativeReward()}");
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
                    RLog.RL($"Victim agent cumulative reward before SetReward: {victimAgent.GetCumulativeReward()}");
                }
                victimAgent.AddReward(-1.0f - victimAgent.GetCumulativeReward());
                if (enableDebugLogs)
                {
                    RLog.RL($"Victim agent cumulative reward after SetReward: {victimAgent.GetCumulativeReward()}");
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
        
        // Calculate hybrid boundary system radii
        _softBoundaryRadius = EffectiveSettings.arenaSize * softBoundaryMultiplier;
        _hardBoundaryRadius = EffectiveSettings.arenaSize * hardBoundaryMultiplier;
        
        if (boundaryCollider != null)
        {
            // Set the trigger collider to the hard boundary radius
            boundaryCollider.radius = _hardBoundaryRadius;
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
            RLog.RL($"ArenaInstance: Applied Environment Settings. Arena Size: {EffectiveSettings.arenaSize}, Asteroid Density: {EffectiveSettings.asteroidDensity}, Bot Difficulty: {EffectiveSettings.botDifficulty}, Soft Boundary: {_softBoundaryRadius:F1}, Hard Boundary: {_hardBoundaryRadius:F1}");
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
            RLog.RL($"ArenaInstance: Requesting episode end. Episode active: {_episodeActive}, enableArenaReset: {enableArenaReset}");
        }
        if (!_episodeActive || !enableArenaReset) return;
        _episodeActive = false; // Close the gate until the next episode begins.
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent commander)
            {
                RLog.RL($"Episode end: Agent {agent.name} cumulative reward: {agent.GetCumulativeReward()}");
            }
        }
        // Agents will automatically stop processing when _episodeActive becomes false

        StartCoroutine(ResetArenaCoroutine());
    }

    private IEnumerator ResetArenaCoroutine()
    {
        // Report episode metrics to TensorBoard before reset
        if (enableMetricsTracking)
        {
            ReportEpisodeMetrics();
        }
        
        // Increment episode count and trigger flash effect
        episodeCount++;
        flashTimer = flashDuration;
        OnArenaReset?.Invoke(this);
        
        // Apply new environment parameters for the upcoming episode.
        ResolveAndApplySettings();

        if (enableDebugLogs)
        {
            RLog.RL($"ArenaInstance: Starting reset after {resetDelay}s delay. Episode: {episodeCount}");
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

        // 4. Reset metrics for the new episode
        if (enableMetricsTracking)
        {
            ResetEpisodeMetrics();
        }

        // 5. Re-arm the gate, allowing the new episode to be terminated.
        _episodeActive = true;

        if (enableDebugLogs)
        {
            RLog.RL("ArenaInstance: Reset complete.");
        }
    }

    // ---------------------- Helper implementation ---------------------------
    void ResetShips()
    {
        foreach (var ship in ships)
        {
            if (ship == null) continue;
            // Reactivate in case it was disabled on death.
            ship.ResetShip();
            ship.gameObject.SetActive(true);

            // Apply temporary spawn invulnerability so immediate asteroid collisions do not damage the ship.
            ship.damageHandler.SetInvulnerability(spawnInvulnerabilityDuration);    

            // Place ship in a random position near the arena centre.
            Vector3 randomOffset = Random.insideUnitCircle.normalized * ArenaSize *.7f; // 70% of arena size
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
            RLog.RL($"ArenaInstance: Signalling agents episode end. {mlAgents.Length} agents found.");
        }
        if (mlAgents == null || mlAgents.Length == 0) return;
        foreach (var agent in mlAgents)
        {
            if (agent != null && agent.gameObject.activeInHierarchy)
            {
                if (enableDebugLogs)
                {
                    RLog.RL($"ArenaInstance: Signalling agent {agent.name} episode end.");
                }
                agent.EndEpisode();
            }
        }
    }



    // ----------------------------- Public API --------------------------------
    
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

    // --- BaseGameContext implementation ---
    
    public override Vector3 CenterPosition => transform.position;
    
    public override float AreaSize => ArenaSize;
    
    public override bool IsActive => IsEpisodeActive;
    
    protected override Ship[] GetShipsForContext()
    {
        return ships ?? System.Array.Empty<Ship>();
    }
    
    /// <summary>
    /// Sets the override settings for this arena, typically called by ArenaManager.
    /// </summary>
    public void SetOverrideSettings(ArenaSettings settings)
    {
        _overrideSettings = settings;
    }

    // -----------------------------------------------------------------------
    // Metrics Tracking Methods -----------------------------------------------
    
    private void TrackDistanceMetrics()
    {
        if (mlAgents == null || ArenaSize <= 0f) return;
        
        Vector3 arenaCenter = transform.position;
        
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent commander && commander.ship != null)
            {
                float distance = Vector3.Distance(commander.ship.transform.position, arenaCenter);
                float normalizedDistance = distance / ArenaSize; // Normalize by arena size
                _distanceSamples.Add(normalizedDistance);
            }
        }
    }
    
    private void TrackDamageMetrics(Ship victim, Ship attacker, float damage)
    {
        // Track damage taken by victim
        var victimAgent = victim?.GetComponent<RLCommanderAgent>();
        if (victimAgent != null)
        {
            if (!_damageTakenThisEpisode.ContainsKey(victimAgent))
                _damageTakenThisEpisode[victimAgent] = 0f;
            _damageTakenThisEpisode[victimAgent] += damage;
        }
        
        // Track damage dealt by attacker (only if they're enemies)
        if (attacker != null && victim != null && !victim.IsFriendly(attacker))
        {
            var attackerAgent = attacker.GetComponent<RLCommanderAgent>();
            if (attackerAgent != null)
            {
                if (!_damageDealtThisEpisode.ContainsKey(attackerAgent))
                    _damageDealtThisEpisode[attackerAgent] = 0f;
                _damageDealtThisEpisode[attackerAgent] += damage;
            }
        }
    }
    
    private void ReportEpisodeMetrics()
    {
        if (!Academy.IsInitialized) return;
        
        var statsRecorder = Academy.Instance.StatsRecorder;
        
        // Track total episodes completed and kills
        _totalEpisodesCompleted++;
        if (_episodeEndedInKill)
        {
            _episodesEndedInKills++;
        }
        
        // Report kill rate (rolling average)
        float killRate = _totalEpisodesCompleted > 0 ? (float)_episodesEndedInKills / _totalEpisodesCompleted : 0f;
        statsRecorder.Add("Arena/KillRate", killRate);
        
        // Report average normalized distance from center
        if (_distanceSamples.Count > 0)
        {
            float averageDistance = 0f;
            foreach (float distance in _distanceSamples)
            {
                averageDistance += distance;
            }
            averageDistance /= _distanceSamples.Count;
            statsRecorder.Add("Arena/AvgNormalizedDistance", averageDistance);
        }
        
        // Report damage metrics per agent
        foreach (var kvp in _damageDealtThisEpisode)
        {
            var agent = kvp.Key;
            var damage = kvp.Value;
            if (agent != null)
            {
                statsRecorder.Add($"Arena/DamageDealt_{agent.name}", damage);
            }
        }
        
        foreach (var kvp in _damageTakenThisEpisode)
        {
            var agent = kvp.Key;
            var damage = kvp.Value;
            if (agent != null)
            {
                statsRecorder.Add($"Arena/DamageTaken_{agent.name}", damage);
            }
        }
        
        // Report aggregate damage metrics
        float totalDamageDealt = 0f;
        float totalDamageTaken = 0f;
        
        foreach (var damage in _damageDealtThisEpisode.Values)
            totalDamageDealt += damage;
        foreach (var damage in _damageTakenThisEpisode.Values)
            totalDamageTaken += damage;
            
        if (_damageDealtThisEpisode.Count > 0)
            statsRecorder.Add("Arena/AvgDamageDealt", totalDamageDealt / _damageDealtThisEpisode.Count);
        if (_damageTakenThisEpisode.Count > 0)
            statsRecorder.Add("Arena/AvgDamageTaken", totalDamageTaken / _damageTakenThisEpisode.Count);
            
        if (enableDebugLogs)
        {
            RLog.RL($"[Ep.{episodeCount}] Metrics: KillRate={killRate:F3}, AvgDist={(_distanceSamples.Count > 0 ? _distanceSamples.Average() : 0):F3}, DamageDealt={totalDamageDealt:F1}, DamageTaken={totalDamageTaken:F1}");
        }
    }
    
    private void ResetEpisodeMetrics()
    {
        _damageDealtThisEpisode.Clear();
        _damageTakenThisEpisode.Clear();
        _distanceSamples.Clear();
        _episodeEndedInKill = false;
    }

    // -----------------------------------------------------------------------
    // Trigger callbacks ------------------------------------------------------
    private void OnTriggerExit(Collider other)
    {
        // Boundary violations are now handled by the hybrid boundary system in CheckAgentBoundaries()
        // This trigger is kept as a safety net for non-agent ships or edge cases
        if (!resetOnShipExit) return;

        // Find a Ship component on the collider or its parents (handles multiple collider setups)
        Ship ship = other.GetComponent<Ship>() ?? other.GetComponentInParent<Ship>();
        if (ship == null) return;

        var agent = ship.GetComponent<RLCommanderAgent>();
        if (agent != null)
        {
            // For RL agents, boundary handling is done in CheckAgentBoundaries() to avoid double penalties
            if (enableDebugLogs)
            {
                RLog.RL($"ArenaInstance: RL Agent '{ship.name}' trigger exit detected but handled by hybrid boundary system.");
            }
            return;
        }
        else
        {
            // For non-RL ships (like AI bots), still handle trigger exit normally
            if (enableDebugLogs)
            {
                RLog.RL($"ArenaInstance: Non-agent ship '{ship.name}' exited arena bounds – triggering reset.");
            }
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

        // Draw arena boundary (original arena size)
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, Vector3.one * gizmoSize);
        
        // Draw soft boundary circle
        Gizmos.color = Color.yellow;
        //Gizmos.DrawWireSphere(center, _softBoundaryRadius);
        
        // Draw hard boundary circle
        Gizmos.color = Color.red;
        //Gizmos.DrawWireSphere(center, _hardBoundaryRadius);

        // Draw episode count text
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(center + Vector3.up * 10f, $"Episode: {episodeCount}\nGlobal Steps: {RLCommanderAgent.GlobalStepCount}\nSoft: {_softBoundaryRadius:F1} | Hard: {_hardBoundaryRadius:F1}");
    }
#endif
} 