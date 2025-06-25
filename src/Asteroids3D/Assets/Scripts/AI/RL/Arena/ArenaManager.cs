using UnityEngine;
using System.Collections.Generic;

#if UNITY_ML_AGENTS
using Unity.MLAgents;
#endif

/// <summary>
/// Spawns a grid of <see cref="ArenaInstance"/> prefabs when running in head-less batch-mode
/// for simultaneous ML-Agents training.  In normal editor/runtime usage this component has no
/// effect, allowing a scene to contain a single Arena prefab with its own <see cref="ArenaInstance"/>
/// without requiring an <see cref="ArenaManager"/>.
/// </summary>
public class ArenaManager : MonoBehaviour
{
    [Header("Multi-Arena Spawning")]
    [Tooltip("Prefab that contains an ArenaInstance component")]
    [SerializeField] private GameObject arenaPrefab;

    [Tooltip("Force multi-arena mode in editor for testing (always enabled in batch mode)")]
    [SerializeField] private bool forceMultiArenaMode = false;

    [Tooltip("Total number of arenas to spawn when in multi-arena mode")]
    [SerializeField] private int arenaCount = 4;

    [Tooltip("Spacing between arena centres")]
    [SerializeField] private float arenaSpacing = 200f;

    [Tooltip("Explicit grid dimensions.  Leave zero to auto-compute a square grid.")]
    [SerializeField] private Vector2Int gridSize = Vector2Int.zero;
    
    [Tooltip("Global arena size (radius) applied to all spawned arenas. 0 = use individual arena settings.")]
    [SerializeField] private float globalArenaSize = 0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // ---------------------------------------------------------------------
    private readonly List<GameObject>   spawnedArenas  = new();
    private readonly List<ArenaInstance> arenaInstances = new();

    public bool isMultiArenaMode{get; private set;}

    // Optional singleton for easy access
    public static ArenaManager Instance { get; private set; }

    // Events – forwarded from individual ArenaInstance components
    public System.Action<ArenaInstance> OnArenaSpawned;
    public System.Action<ArenaInstance> OnArenaReset;
    public System.Action OnArenasSpawned;

    // ---------------------------------------------------------------------
    void Awake()
    {
        // Basic singleton guard (not essential but convenient)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Spawn multiple arenas in batch mode (always) or when explicitly enabled for editor testing
        isMultiArenaMode = (Application.isBatchMode || forceMultiArenaMode) && arenaPrefab != null;

        if (enableDebugLogs)
            RLog.Log($"ArenaManager: Awake – multi-arena mode = {isMultiArenaMode}");
    }

    void Start()
    {
        if (isMultiArenaMode)
            SpawnMultipleArenas();
    }

    // ---------------------------------------------------------------------
    private void SpawnMultipleArenas()
    {
        if (arenaPrefab == null)
        {
            RLog.LogError("ArenaManager: No arena prefab assigned!");
            return;
        }

        // Derive grid size if not explicitly provided
        Vector2Int actualGrid = gridSize;
        if (actualGrid.x <= 0 || actualGrid.y <= 0)
        {
            int dim = Mathf.CeilToInt(Mathf.Sqrt(arenaCount));
            actualGrid = new Vector2Int(dim, dim);
        }

        int spawned = 0;
        for (int row = 0; row < actualGrid.y && spawned < arenaCount; row++)
        {
            for (int col = 0; col < actualGrid.x && spawned < arenaCount; col++)
            {
                Vector3 position = new Vector3(
                    col * arenaSpacing - (actualGrid.x - 1) * arenaSpacing * 0.5f,
                    0f,
                    row * arenaSpacing - (actualGrid.y - 1) * arenaSpacing * 0.5f);

                GameObject arenaGO = Instantiate(arenaPrefab, position, Quaternion.identity, transform);
                arenaGO.name = $"Arena_{spawned:D2}";

                ArenaInstance instance = arenaGO.GetComponent<ArenaInstance>();
                if (instance == null)
                {
                    RLog.LogError("ArenaManager: The arena prefab does not contain an ArenaInstance component!");
                    Destroy(arenaGO);
                    continue;
                }

                // Keep track
                spawnedArenas.Add(arenaGO);
                arenaInstances.Add(instance);

                // Forward reset events so external listeners can hook one place
                instance.OnArenaReset += OnChildArenaReset;
                instance.OnArenaReset += (a) => a.SetArenaSize(globalArenaSize);

                // Additional per-arena configuration helpful for ML training
                ConfigureArenaForTraining(instance, spawned);

                spawned++;

                if (enableDebugLogs)
                    RLog.Log($"ArenaManager: Spawned arena #{spawned} at {position}");

                OnArenaSpawned?.Invoke(instance);
            }
        }

        if (enableDebugLogs)
            RLog.Log($"ArenaManager: Finished spawning – {spawned} arena(s) in a {actualGrid.x}×{actualGrid.y} grid");
        
        OnArenasSpawned?.Invoke();
    }

    private void ConfigureArenaForTraining(ArenaInstance instance, int arenaIndex)
    {
        // Apply global arena size if specified
        if (globalArenaSize > 0f)
        {
            instance.SetArenaSize(globalArenaSize);
        }
        
        // Anchor already set in ArenaInstance.Awake(); ensure it's correct.
        if (instance.fieldManager != null)
            instance.fieldManager.SetAnchor(instance.transform);

        // Alternate team numbers for simple 1-vs-1 setups
        for (int i = 0; i < instance.ships.Length; i++)
        {
            if (instance.ships[i] != null)
                instance.ships[i].teamNumber = i % 2;
        }

#if UNITY_ML_AGENTS
        // Placeholder: assign behaviour names or models per arena if desired
        foreach (var agent in instance.mlAgents)
        {
            if (agent != null)
            {
                // agent.SetModel($"Arena_{arenaIndex}", someModel);
            }
        }
#endif
    }

    // ---------------------------------------------------------------------
    // Convenience wrappers that forward to individual arenas
    public void ResetArena(ArenaInstance arena) => arena?.ResetArena();

    public void ResetAllArenas()
    {
        foreach (var a in arenaInstances)
            a?.ResetArena();
    }
    
    /// <summary>
    /// Set the size of all managed arenas.
    /// </summary>
    public void SetAllArenaSizes(float size)
    {
        globalArenaSize = size;
        foreach (var arena in arenaInstances)
        {
            arena?.SetArenaSize(size);
        }
        
        if (enableDebugLogs)
            RLog.Log($"ArenaManager: Set all arena sizes to {size}");
    }

    private void OnChildArenaReset(ArenaInstance inst) => OnArenaReset?.Invoke(inst);

    // ---------------------------------------------------------------------
    public ArenaInstance GetArena(int index) => (index >= 0 && index < arenaInstances.Count) ? arenaInstances[index] : null;
    public List<ArenaInstance> GetAllArenas() => new(arenaInstances);

    public bool IsMultiArenaMode => isMultiArenaMode;
    public int  ArenaCount      => arenaInstances.Count;
    public float GlobalArenaSize => globalArenaSize;

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Only show preview grid when not playing and multi-arena mode would be active
        if (!Application.isPlaying && arenaPrefab != null && (Application.isBatchMode || forceMultiArenaMode))
        {
            Gizmos.color = Color.gray;
            
            Vector2Int actual = gridSize;
            if (actual.x <= 0 || actual.y <= 0)
            {
                int dim = Mathf.CeilToInt(Mathf.Sqrt(arenaCount));
                actual = new Vector2Int(dim, dim);
            }

            int preview = 0;
            for (int row = 0; row < actual.y && preview < arenaCount; row++)
            {
                for (int col = 0; col < actual.x && preview < arenaCount; col++)
                {
                    Vector3 pos = transform.position + new Vector3(
                        col * arenaSpacing - (actual.x - 1) * arenaSpacing * 0.5f,
                        0f,
                        row * arenaSpacing - (actual.y - 1) * arenaSpacing * 0.5f);

                    Gizmos.DrawWireCube(pos, Vector3.one * arenaSpacing * 0.8f);
                    preview++;
                }
            }
        }
    }
#endif
} 