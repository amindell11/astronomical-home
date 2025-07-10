using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Open-world asteroid field manager that centres spawning logic on the main
/// player camera. All heavy logic lives in <see cref="BaseFieldManager"/>.
/// </summary>
public class AsteroidFieldManager : BaseFieldManager
{
    [Header("Update Spawn Zone")]
    [Tooltip("Min spawn distance used during ongoing updates (InvokeRepeating calls)")]
    [SerializeField] protected float updateMinSpawnDistance = 30f;
    [Tooltip("Max spawn distance used during ongoing updates (InvokeRepeating calls)")]
    [SerializeField] protected float updateMaxSpawnDistance = 50f;
    
    [Header("Update Timing")]
    [SerializeField] protected float densityCheckInterval = 0.25f;
    
    public static AsteroidFieldManager Instance { get; private set; }

    private Camera mainCamera;
    private float densityCheckTimer = 0f;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        mainCamera = Camera.main;
    }
    
    protected override void Start()
    {
        // Call base.Start() to do initial spawn with base class parameters
        base.Start();
        RLog.AI($"AsteroidFieldManager: Start");
        // Initialize timer for density checks (replacing InvokeRepeating)
        densityCheckTimer = densityCheckInterval;
    }

    private void Update()
    {
        // Accumulated-time pattern to replace InvokeRepeating
        densityCheckTimer -= Time.deltaTime;
        if (densityCheckTimer <= 0f)
        {
            ManageAsteroidField(updateMinSpawnDistance, updateMaxSpawnDistance, maxSpawnsPerFrame);
            densityCheckTimer = densityCheckInterval;
        }
    }

    protected override Transform AcquireAnchor()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        return mainCamera != null ? mainCamera.transform : null;
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        // Draw the base class gizmos first (initial spawn zone and density check)
        base.OnDrawGizmosSelected();
        
        // Now draw our update spawn zone
        Transform anchor = spawnAnchor != null ? spawnAnchor : AcquireAnchor();
        if (anchor == null) return;

        Vector3 center = anchor.position;
        center.y = 0f;

        // Draw update spawn zone with different colors
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, updateMinSpawnDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, updateMaxSpawnDistance);
        
        // Add labels to distinguish the zones
        Handles.color = Color.white;
        Handles.Label(center + Vector3.forward * (minSpawnDistance + 2f), "Initial Min");
        Handles.Label(center + Vector3.forward * (maxSpawnDistance + 2f), "Initial Max");
        Handles.Label(center + Vector3.forward * (updateMinSpawnDistance + 2f), "Update Min");
        Handles.Label(center + Vector3.forward * (updateMaxSpawnDistance + 2f), "Update Max");
    }
#endif
} 