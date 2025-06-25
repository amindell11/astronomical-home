using UnityEngine;

/// <summary>
/// Demo script showing how to use ArenaManager.
/// Attach this to a GameObject in your scene to test arena functionality.
/// </summary>
public class ArenaManagerDemo : MonoBehaviour
{
    [Header("Demo Controls")]
    [Tooltip("Key to reset the primary (index 0) arena")]
    [SerializeField] private KeyCode resetPrimaryArenaKey = KeyCode.R;
    
    [Tooltip("Key to reset all arenas")]
    [SerializeField] private KeyCode resetAllArenasKey = KeyCode.T;
    
    [Header("Info Display")]
    [SerializeField] private bool showArenaInfo = true;
    
    private ArenaManager arenaManager;
    
    void Start()
    {
        // Get reference to the ArenaManager
        arenaManager = ArenaManager.Instance;
        
        if (arenaManager == null)
        {
            Debug.LogWarning("ArenaManagerDemo: No ArenaManager found in scene. Please add an ArenaManager component.");
            return;
        }
        
        // Subscribe to arena events
        arenaManager.OnArenaReset += OnArenaReset;
        arenaManager.OnArenaSpawned += OnArenaSpawned;
        
        if (showArenaInfo)
        {
            LogArenaInfo();
        }
    }
    
    void Update()
    {
        if (arenaManager == null) return;
        
        // Handle demo input
        if (Input.GetKeyDown(resetPrimaryArenaKey))
        {
            // Reset arena #0 for demo purposes.  If you need different behaviour
            // (e.g., closest arena to player) you can modify this selection logic.
            var primaryArena = arenaManager.GetArena(0);
            if (primaryArena != null)
            {
                Debug.Log("ArenaManagerDemo: Resetting primary arena (index 0)...");
                arenaManager.ResetArena(primaryArena);
            }
            else
            {
                Debug.LogWarning("ArenaManagerDemo: No arena found at index 0.");
            }
        }
        
        if (Input.GetKeyDown(resetAllArenasKey))
        {
            Debug.Log("ArenaManagerDemo: Resetting all arenas...");
            arenaManager.ResetAllArenas();
        }
    }
    
    private void LogArenaInfo()
    {
        Debug.Log($"ArenaManagerDemo: Arena Manager Status:");
        Debug.Log($"  - Multi-Arena Mode: {arenaManager.IsMultiArenaMode}");
        Debug.Log($"  - Arena Count: {arenaManager.ArenaCount}");
        
        var arenas = arenaManager.GetAllArenas();
        for (int i = 0; i < arenas.Count; i++)
        {
            var arena = arenas[i];
            Debug.Log($"  - Arena {i}: Position {arena.CenterPosition}, Ships: {arena.ShipCount}");
        }
    }
    
    private void OnArenaReset(ArenaInstance arena)
    {
        Debug.Log($"ArenaManagerDemo: Arena at {arena.CenterPosition} was reset");
    }
    
    private void OnArenaSpawned(ArenaInstance arena)
    {
        Debug.Log($"ArenaManagerDemo: Arena spawned at {arena.CenterPosition}");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (arenaManager != null)
        {
            arenaManager.OnArenaReset -= OnArenaReset;
            arenaManager.OnArenaSpawned -= OnArenaSpawned;
        }
    }
    
    void OnGUI()
    {
        if (arenaManager == null) return;
        
        // Simple UI for testing
        GUI.Box(new Rect(10, 10, 300, 120), "Arena Manager Demo");
        
        GUI.Label(new Rect(20, 35, 280, 20), $"Multi-Arena Mode: {arenaManager.IsMultiArenaMode}");
        GUI.Label(new Rect(20, 55, 280, 20), $"Arena Count: {arenaManager.ArenaCount}");
        GUI.Label(new Rect(20, 75, 280, 20), $"Press '{resetPrimaryArenaKey}' to reset primary arena");
        GUI.Label(new Rect(20, 95, 280, 20), $"Press '{resetAllArenasKey}' to reset all arenas");
    }
} 