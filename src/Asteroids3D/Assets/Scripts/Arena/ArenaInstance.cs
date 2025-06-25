using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_ML_AGENTS
using Unity.MLAgents;
#endif

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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Gizmos")]
    [Tooltip("Show arena boundaries and episode count in Scene view")]
    [SerializeField] private bool showGizmos = true;
    [Tooltip("Size of the arena boundary gizmo")]
    [SerializeField] private float gizmoSize = 160f;

    [Header("Arena Size")]
    [Tooltip("Radius of the arena in world units")]
    [SerializeField] private float arenaSize = 100f;
    
    [Header("Boundary Reset Settings")]
    [Tooltip("Reset the arena if a Ship exits the arena's trigger collider")]
    [SerializeField] private bool resetOnShipExit = true;

    // --------------------------- Cached references ---------------------------
    [System.NonSerialized] public Ship[] ships; // exposed for convenience (read-only)
    [System.NonSerialized] public SectorFieldManager fieldManager;
    [System.NonSerialized] private SphereCollider boundaryCollider;
#if UNITY_ML_AGENTS
    [System.NonSerialized] public Agent[] mlAgents;
#endif

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
#if UNITY_ML_AGENTS
        mlAgents     = GetComponentsInChildren<Agent>(true);
#endif
        
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
        ApplyArenaSize();
        
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

    // -----------------------------------------------------------------------
    private void OnShipDeath(Ship deadShip)
    {
        // Any ship death triggers an arena reset.  Override this logic if
        // different termination conditions are needed.
        RequestEpisodeEnd();
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
        
        if (enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Starting reset after {resetDelay}s delay. Episode: {episodeCount}");
        }

        if (resetDelay > 0f)
            yield return new WaitForSeconds(resetDelay);
        ApplyArenaSize();
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

            var movement      = ship.GetComponent<ShipMovement>();
            var damageHandler = ship.GetComponent<ShipDamageHandler>();

            movement?.ResetShip();
            damageHandler?.ResetAll();

            // Reactivate in case it was disabled on death.
            ship.gameObject.SetActive(true);

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
    #if UNITY_ML_AGENTS
        if(enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Signalling agents episode end. {mlAgents.Length} agents found.");
        }
        if (mlAgents == null || mlAgents.Length == 0) return;
        foreach (var agent in mlAgents)
        {
            agent?.EndEpisode();
        }
#endif
    }

    void SetAgentsPaused(bool paused)
    {
#if UNITY_ML_AGENTS
        if (mlAgents == null) return;
        foreach (var agent in mlAgents)
        {
            if (agent is RLCommanderAgent commander)
            {
                commander.IsPaused = paused;
            }
        }
#endif
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
    public float ArenaSize => arenaSize;
    
    /// <summary>
    /// Set the arena size and apply it to boundary collider and field manager.
    /// </summary>
    public void SetArenaSize(float newSize)
    {
        arenaSize = newSize;
    }
    
    private void ApplyArenaSize()
    {
        // Set boundary collider radius
        if (boundaryCollider != null)
        {
            boundaryCollider.radius = arenaSize;
        }
        
        // Set field manager size
        if (fieldManager != null)
        {
            fieldManager.SetFieldSize(arenaSize);
        }
        
        // Update gizmo size to match arena size
        gizmoSize = arenaSize * 2f;
        
        if (enableDebugLogs)
        {
            RLog.Log($"ArenaInstance: Applied arena size {arenaSize} (boundary radius: {arenaSize}, field size: {arenaSize})");
        }
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

        RequestEpisodeEnd();
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
        UnityEditor.Handles.Label(center + Vector3.up * 10f, $"Episode: {episodeCount}");
    }
#endif
} 